# Infrastructure Overview

The Member Property Market Alert platform runs on Azure services. This guide documents the infrastructure components, configuration, and deployment flow used by the solution so new environments can be provisioned consistently.

## Platform Topology

| Resource | Purpose | Notes |
| --- | --- | --- |
| **Azure Function App** (Consumption or Premium) | Hosts the .NET 8 isolated worker that exposes the HTTP APIs, orchestrates scans, and publishes downstream notifications. | Requires .NET 8 support and application settings described below. Backed by a storage account for triggers and hosting metadata. |
| **Azure Storage Account** | Provides the function app runtime storage (AzureWebJobsStorage). | StandardV2, locally redundant storage (LRS) is sufficient for non-production. |
| **Azure Cosmos DB** (SQL API) | Persists institutions, member addresses, scan jobs, scan schedules, and listing matches. | Uses the database name configured in `Cosmos:DatabaseName` (defaults to `MemberPropertyMarketAlert`) and the following containers: `institutions`, `addresses`, `scans`, `alerts`. Partition keys mirror the IDs for each aggregate. |
| **Azure Service Bus** | Publishes listing match notifications to the configured queue. | Queue name defaults to `member-alerts` (`Notifications:AlertQueueName`). The service is optional for local development; if omitted, webhooks still fire. |
| **Azure SignalR Service** | Broadcasts live alert streams to the UI. | Currently scaffolding only; provision Standard tier to support hybrid connections when real-time streaming is enabled. |
| **Application Insights** | Captures structured telemetry and traces from the isolated worker. | Instrumentation key is consumed through the standard `APPLICATIONINSIGHTS_CONNECTION_STRING` setting. |
| **Azure Key Vault** (recommended) | Stores API keys, connection strings, and webhook secrets. | Integrate with Function App using managed identity and Key Vault references. |

## Configuration Reference

Populate these settings in each environment through the Function App configuration (or `local.settings.json` when running locally).

### Connection Strings

| Setting | Description |
| --- | --- |
| `AzureWebJobsStorage` | Storage account connection string required by the Functions runtime. |
| `Cosmos:ConnectionString` | Cosmos DB SQL API connection string. |
| `ConnectionStrings:ServiceBus` **or** `ServiceBus:ConnectionString` | Service Bus namespace connection string. Optional in development. |
| `SignalR:HubEndpoint` | Azure SignalR endpoint URL. |

### Application Settings

| Setting | Description |
| --- | --- |
| `Cosmos:DatabaseName` | Database that hosts all containers. Defaults to `MemberPropertyMarketAlert`. |
| `Cosmos:InstitutionsContainerName` | Container for institutions. Partition key: `/id`. |
| `Cosmos:AddressesContainerName` | Container for member addresses. Partition key: `/institutionId`. |
| `Cosmos:ScansContainerName` | Container for scan jobs and schedules. |
| `Cosmos:AlertsContainerName` | Container for delivered listing matches. |
| `Notifications:EnableWebhook` | Enables webhook delivery path (default `true`). |
| `Notifications:EnableEmail` / `Notifications:EnableSms` | Toggles additional delivery channels. |
| `Notifications:DefaultWebhookUrl` | Optional default webhook target. |
| `Notifications:AlertQueueName` | Service Bus queue name for matches (`member-alerts`). |
| `RentCast:BaseUrl` | Base URI for the RentCast API (defaults to `https://api.rentcast.io`). |
| `RentCast:ApiKey` | API key issued by RentCast. |
| `ApiKey:Hash` | Hash of the API key used by the middleware to authorize HTTP requests. |
| `SignalR:HubName` | SignalR hub name (defaults to `alerts`). |
| `SignalR:AccessKey` | Optional SignalR access key for server-to-service negotiation. |

> **Note:** Secrets should be sourced from Azure Key Vault via managed identity, not stored directly in configuration for production workloads.

## Environment Layout

| Environment | Resource Group | Naming Recommendations |
| --- | --- | --- |
| `dev` | `rg-mpma-dev` | Suffix resources with `-dev` (e.g., `func-mpma-dev`, `cosmos-mpma-dev`). Use cost-friendly SKUs (Consumption Functions, Cosmos Serverless). |
| `test` | `rg-mpma-test` | Mirror production topology with lower throughput (Cosmos autoscale 400–1,000 RU/s, Basic SignalR). |
| `prod` | `rg-mpma-prod` | Hardened SKUs: Functions Premium plan or Elastic Premium, Cosmos autoscale with >= 1,000 RU/s, Standard SignalR, redundant storage. Enable zone redundancy where available. |

## Deployment Workflow

1. **Authenticate**: Sign in with the Azure CLI and select the subscription.
2. **Create the resource group** for the desired environment.
3. **Provision shared services** (Storage, Cosmos, Service Bus, SignalR, Application Insights).
4. **Deploy the Function App** and connect the managed identity to other services.
5. **Seed Cosmos containers** with database and container definitions.
6. **Configure application settings** (connection strings, option values, API key hash).
7. **Deploy the function binaries** via `func azure functionapp publish` or the CI/CD pipeline.

Example command sequence (adjust names, SKUs, and locations to match your environment):

```powershell
# 1. Login and pick subscription
az login
az account set --subscription "<subscription-id>"

# 2. Resource group
az group create --name rg-mpma-dev --location eastus

# 3. Storage + Function App plan
az storage account create --name stmpmadev --resource-group rg-mpma-dev --sku Standard_LRS --kind StorageV2
az functionapp create --name func-mpma-dev --resource-group rg-mpma-dev --storage-account stmpmadev --consumption-plan-location eastus --runtime dotnet-isolated --functions-version 4

# 4. Cosmos DB (serverless) with database and containers
az cosmosdb create --name cosmos-mpma-dev --resource-group rg-mpma-dev --kind GlobalDocumentDB --capabilities EnableServerless
az cosmosdb sql database create --account-name cosmos-mpma-dev --resource-group rg-mpma-dev --name MemberPropertyMarketAlert
az cosmosdb sql container create --account-name cosmos-mpma-dev --resource-group rg-mpma-dev --database-name MemberPropertyMarketAlert --name institutions --partition-key-path "/id"
az cosmosdb sql container create --account-name cosmos-mpma-dev --resource-group rg-mpma-dev --database-name MemberPropertyMarketAlert --name addresses --partition-key-path "/institutionId"
az cosmosdb sql container create --account-name cosmos-mpma-dev --resource-group rg-mpma-dev --database-name MemberPropertyMarketAlert --name scans --partition-key-path "/id"
az cosmosdb sql container create --account-name cosmos-mpma-dev --resource-group rg-mpma-dev --database-name MemberPropertyMarketAlert --name alerts --partition-key-path "/id"

# 5. Service Bus namespace + queue
az servicebus namespace create --resource-group rg-mpma-dev --name sb-mpma-dev --sku Standard --location eastus
az servicebus queue create --resource-group rg-mpma-dev --namespace-name sb-mpma-dev --name member-alerts

# 6. SignalR + App Insights
az signalr create --resource-group rg-mpma-dev --name sig-mpma-dev --location eastus --sku Standard_S1
az monitor app-insights component create --app mpma-dev-ai --location eastus --resource-group rg-mpma-dev
```

After provisioning, assign managed identity permissions and populate the app settings (preferably via Key Vault and deployment automation). 

## Observability and Operations

- **Telemetry**: Application Insights auto-collects function traces and dependency calls. Configure sampling rules as needed.
- **Logging**: The project uses structured logging through `ILogger`. Retain logs for at least 30 days in production.
- **Resiliency**: HTTP integrations are wrapped with Polly retry policies and Service Bus publishes are retried per message. Monitor dead-letter queues and Cosmos throttling metrics.
- **Backup & DR**: Enable point-in-time restore for Cosmos DB, configure geo-redundancy for critical environments, and back up Service Bus queues where compliance requires it.

## Security Considerations

- Enable **managed identities** on the Function App and grant least-privilege access to Cosmos DB, Service Bus, and SignalR.
- Store API keys and secrets in **Azure Key Vault** and reference them via app settings (e.g., `@Microsoft.KeyVault(SecretUri=...)`).
- Rotate API keys regularly and enforce TLS 1.2 for all integrations.
- Use **Azure Front Door or API Management** in front of the Function App if exposing the API publicly.

## Automation Recommendations

- Keep infrastructure-as-code (Bicep/Terraform) definitions under source control as the authoritative deployment mechanism.
- Integrate deployment scripts into the CI/CD pipeline to validate infrastructure changes in test before production.
- Combine the infrastructure steps above with the quickstart script (documented separately) to deliver a one-command developer setup experience.
