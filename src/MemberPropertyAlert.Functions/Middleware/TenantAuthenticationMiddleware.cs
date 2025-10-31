using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using MemberPropertyAlert.Functions.Configuration;
using MemberPropertyAlert.Functions.Extensions;
using MemberPropertyAlert.Functions.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using WorkerHttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;

namespace MemberPropertyAlert.Functions.Middleware;

public sealed class TenantAuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";
    private const string CorrelationHeader = "x-correlation-id";

    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    private readonly TenantAuthenticationOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly ITenantRequestContextAccessor _tenantContextAccessor;
    private readonly ILogger<TenantAuthenticationMiddleware> _logger;

    static TenantAuthenticationMiddleware()
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
    }

    public TenantAuthenticationMiddleware(
        IOptions<TenantAuthenticationOptions> options,
        ITenantRequestContextAccessor tenantContextAccessor,
        ILogger<TenantAuthenticationMiddleware> logger)
    {
        _options = options.Value;
        _tenantContextAccessor = tenantContextAccessor;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Authority))
        {
            throw new InvalidOperationException("Authentication:Authority configuration is required.");
        }

        var metadataAddress = $"{_options.Authority.TrimEnd('/')}/.well-known/openid-configuration";
        var documentRetriever = new HttpDocumentRetriever
        {
            RequireHttps = _options.RequireHttpsMetadata
        };

        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            documentRetriever);
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpRequest = context.GetHttpRequestData();
        if (httpRequest is null)
        {
            await next(context);
            return;
        }

        var targetMethod = context.GetTargetFunctionMethod();
        if (targetMethod is not null && Attribute.IsDefined(targetMethod, typeof(AllowAnonymousAttribute)))
        {
            await next(context);
            return;
        }

        if (!httpRequest.Headers.TryGetValues(AuthorizationHeader, out var headerValues))
        {
            await RejectAsync(context, httpRequest, HttpStatusCode.Unauthorized, "Authorization header is missing");
            return;
        }

        var authorizationHeader = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await RejectAsync(context, httpRequest, HttpStatusCode.Unauthorized, "Authorization header must be a Bearer token");
            return;
        }

        var token = authorizationHeader[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await RejectAsync(context, httpRequest, HttpStatusCode.Unauthorized, "Bearer token is empty");
            return;
        }

        try
        {
            var openIdConfig = await _configurationManager.GetConfigurationAsync(context.CancellationToken);
            var validationParameters = CreateTokenValidationParameters(openIdConfig);
            var principal = TokenHandler.ValidateToken(token, validationParameters, out _);

            var tenantContext = BuildTenantContext(principal, httpRequest);
            _tenantContextAccessor.SetCurrent(tenantContext);
            context.Items[nameof(TenantRequestContext)] = tenantContext;

            try
            {
                await next(context);
            }
            finally
            {
                _tenantContextAccessor.Clear();
            }
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(ex, "Token expired");
            await RejectAsync(context, httpRequest, HttpStatusCode.Unauthorized, "Token has expired");
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            await RejectAsync(context, httpRequest, HttpStatusCode.Forbidden, "Token validation failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected authentication error");
            await RejectAsync(context, httpRequest, HttpStatusCode.InternalServerError, "Authentication failed");
        }
    }

    private TokenValidationParameters CreateTokenValidationParameters(OpenIdConnectConfiguration configuration)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateIssuer = _options.AllowedTenants is { Length: > 0 },
            ValidIssuers = _options.AllowedTenants,
            ValidateAudience = _options.Audiences is { Length: > 0 },
            ValidAudiences = _options.Audiences,
            ValidateLifetime = true,
            ClockSkew = _options.ClockSkew
        };
    }

    private TenantRequestContext BuildTenantContext(ClaimsPrincipal principal, WorkerHttpRequestData request)
    {
        var tenantIdClaim = _options.TenantIdClaim;
        var tenantId = principal.FindFirstValue(tenantIdClaim);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new SecurityTokenException($"Token missing required claim '{tenantIdClaim}'.");
        }

        if (_options.AllowedTenants is { Length: > 0 } tenants && !tenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Tenant is not authorized to access this application.");
        }

        var institutionId = _options.InstitutionIdClaim is { Length: > 0 } claim
            ? principal.FindFirstValue(claim)
            : null;

        if (string.IsNullOrWhiteSpace(institutionId))
        {
            institutionId = tenantId;
        }

        if (_options.EnforceInstitutionClaim && string.IsNullOrWhiteSpace(institutionId))
        {
            throw new SecurityTokenException("Institution identifier claim is required.");
        }

        var roleClaimType = string.IsNullOrWhiteSpace(_options.RoleClaimType) ? ClaimTypes.Role : _options.RoleClaimType;
        var roles = principal.FindAll(roleClaimType)
            .Select(claimValue => claimValue.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var isPlatformAdmin = _options.PlatformAdminRoles.Any() && roles.Any(role =>
            _options.PlatformAdminRoles.Contains(role, StringComparer.OrdinalIgnoreCase));

        var objectId = principal.FindFirstValue(_options.ObjectIdClaim);
        var preferredUsername = _options.PreferredUsernameClaim is { Length: > 0 } preferred
            ? principal.FindFirstValue(preferred)
            : null;

        var correlationId = ResolveCorrelationId(request);

        return new TenantRequestContext(
            principal,
            tenantId,
            institutionId,
            isPlatformAdmin,
            objectId,
            preferredUsername,
            correlationId,
            roles);
    }

    private static string ResolveCorrelationId(WorkerHttpRequestData request)
    {
        if (request.Headers.TryGetValues(CorrelationHeader, out var headerValues))
        {
            var headerValue = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static async Task RejectAsync(FunctionContext context, WorkerHttpRequestData request, HttpStatusCode statusCode, string message)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteJsonAsync(new
        {
            error = statusCode == HttpStatusCode.Unauthorized ? "unauthorized" : "forbidden",
            message
        });

        response.Headers.Add("WWW-Authenticate", "Bearer");
        context.SetHttpResponseData(response);
    }
}
