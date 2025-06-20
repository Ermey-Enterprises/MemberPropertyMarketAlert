# Get Azure Authentication Values for GitHub Actions CI/CD
# This script retrieves the values you need to configure GitHub repository variables

Write-Host "🔍 Getting Azure Authentication Values for GitHub Actions..." -ForegroundColor Cyan
Write-Host ""

# Check if Azure CLI is installed and logged in
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    if (-not $account) {
        Write-Host "❌ Please login to Azure CLI first:" -ForegroundColor Red
        Write-Host "   az login" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "❌ Azure CLI not found. Please install Azure CLI first:" -ForegroundColor Red
    Write-Host "   https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Azure CLI is logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host ""

# Get Subscription ID
$subscriptionId = $account.id
Write-Host "📋 AZURE_SUBSCRIPTION_ID:" -ForegroundColor Yellow
Write-Host "   $subscriptionId" -ForegroundColor White
Write-Host ""

# Get Tenant ID
$tenantId = $account.tenantId
Write-Host "📋 AZURE_TENANT_ID:" -ForegroundColor Yellow
Write-Host "   $tenantId" -ForegroundColor White
Write-Host ""

# Check if App Registration exists
$appName = "MemberPropertyAlert-GitHub-Actions"
Write-Host "🔍 Checking for existing App Registration: $appName..." -ForegroundColor Cyan

try {
    $app = az ad app list --display-name $appName --output json | ConvertFrom-Json
    if ($app -and $app.Count -gt 0) {
        $clientId = $app[0].appId
        Write-Host "✅ Found existing App Registration" -ForegroundColor Green
        Write-Host "📋 AZURE_CLIENT_ID:" -ForegroundColor Yellow
        Write-Host "   $clientId" -ForegroundColor White
    } else {
        Write-Host "❌ App Registration '$appName' not found" -ForegroundColor Red
        Write-Host "🔧 Creating App Registration..." -ForegroundColor Cyan
        
        # Create App Registration
        $newApp = az ad app create --display-name $appName --output json | ConvertFrom-Json
        $clientId = $newApp.appId
        
        Write-Host "✅ Created App Registration" -ForegroundColor Green
        Write-Host "📋 AZURE_CLIENT_ID:" -ForegroundColor Yellow
        Write-Host "   $clientId" -ForegroundColor White
        
        # Create Service Principal
        Write-Host "🔧 Creating Service Principal..." -ForegroundColor Cyan
        az ad sp create --id $clientId --output none
        Write-Host "✅ Created Service Principal" -ForegroundColor Green
        
        # Add Federated Credential
        Write-Host "🔧 Adding Federated Credential for GitHub Actions..." -ForegroundColor Cyan
        $federatedCredential = @{
            name = "MemberPropertyAlert-Main-Branch"
            issuer = "https://token.actions.githubusercontent.com"
            subject = "repo:Ermey-Enterprises/MemberPropertyMarketAlert:ref:refs/heads/main"
            audiences = @("api://AzureADTokenExchange")
        } | ConvertTo-Json -Depth 3
        
        $federatedCredential | az ad app federated-credential create --id $clientId --parameters '@-' --output none
        Write-Host "✅ Added Federated Credential" -ForegroundColor Green
        
        # Assign Contributor role
        Write-Host "🔧 Assigning Contributor role..." -ForegroundColor Cyan
        az role assignment create --assignee $clientId --role Contributor --scope "/subscriptions/$subscriptionId" --output none
        Write-Host "✅ Assigned Contributor role" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ Error managing App Registration: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎯 GitHub Repository Variables Configuration:" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Go to: https://github.com/Ermey-Enterprises/MemberPropertyMarketAlert/settings/secrets/actions" -ForegroundColor Blue
Write-Host ""
Write-Host "Click 'Variables' tab and add these Repository Variables:" -ForegroundColor Yellow
Write-Host ""
Write-Host "Variable Name: AZURE_CLIENT_ID" -ForegroundColor Green
Write-Host "Value: $clientId" -ForegroundColor White
Write-Host ""
Write-Host "Variable Name: AZURE_TENANT_ID" -ForegroundColor Green
Write-Host "Value: $tenantId" -ForegroundColor White
Write-Host ""
Write-Host "Variable Name: AZURE_SUBSCRIPTION_ID" -ForegroundColor Green
Write-Host "Value: $subscriptionId" -ForegroundColor White
Write-Host ""

# Check for Static Web App to get deployment token
Write-Host "🔍 Checking for Static Web App deployment token..." -ForegroundColor Cyan
$resourceGroup = "memberpropertyalert-eastus2-rg"

try {
    $staticWebApps = az staticwebapp list --resource-group $resourceGroup --output json 2>$null | ConvertFrom-Json
    if ($staticWebApps -and $staticWebApps.Count -gt 0) {
        $staticWebAppName = $staticWebApps[0].name
        Write-Host "✅ Found Static Web App: $staticWebAppName" -ForegroundColor Green
        
        $deploymentToken = az staticwebapp secrets list --name $staticWebAppName --resource-group $resourceGroup --query "properties.apiKey" -o tsv 2>$null
        if ($deploymentToken) {
            Write-Host ""
            Write-Host "Click 'Secrets' tab and add this Repository Secret:" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Secret Name: AZURE_STATIC_WEB_APPS_API_TOKEN" -ForegroundColor Green
            Write-Host "Value: $deploymentToken" -ForegroundColor White
        } else {
            Write-Host "⚠️  Could not retrieve Static Web App deployment token" -ForegroundColor Yellow
            Write-Host "   You'll need to deploy infrastructure first, then get the token manually" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️  Static Web App not found in resource group: $resourceGroup" -ForegroundColor Yellow
        Write-Host "   You'll need to deploy infrastructure first to get the deployment token" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "After infrastructure deployment, run this command to get the token:" -ForegroundColor Yellow
        Write-Host "az staticwebapp secrets list --name [STATIC_WEB_APP_NAME] --resource-group $resourceGroup --query \"properties.apiKey\" -o tsv" -ForegroundColor Cyan
    }
} catch {
    Write-Host "⚠️  Could not check for Static Web App (resource group may not exist yet)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🚀 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Copy the values above into GitHub repository variables/secrets" -ForegroundColor White
Write-Host "2. Push a commit or manually trigger the CI/CD workflow" -ForegroundColor White
Write-Host "3. Watch the 4-stage pipeline complete successfully!" -ForegroundColor White
Write-Host ""
Write-Host "✅ Script completed successfully!" -ForegroundColor Green
