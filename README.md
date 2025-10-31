# Member Property Market Alert Service

## Overview
Member Property Market Alert is a cloud-native notification platform that monitors rental listings for addresses registered by partner institutions. The backend is implemented with .NET 8 and Azure Functions (isolated worker) and persists data into Azure Cosmos DB while publishing downstream events through Azure Service Bus and SignalR. The solution is organized with a domain-centric core library, an HTTP/API function app, a mock webhook client, and a React-based UI (placeholder).

## Solution Layout
| Project | Description |
| --- | --- |
| `MemberPropertyAlert.Core` | Domain entities, value objects, services, and result abstractions used across the solution. |
| `MemberPropertyAlert.Functions` | Azure Functions worker hosting HTTP APIs, background orchestration, and integrations for Cosmos DB, Service Bus, and SignalR. |
| `MemberPropertyAlert.MockWebhookClient` | Console client that mimics downstream webhook consumers for local development. |
| `MemberPropertyAlert.UI` | React single-page application shell for surfacing real-time alerts (scaffolded). |
| `tests/*` | Unit, integration, and end-to-end tests targeting the projects above. |

## Prerequisites
- [.NET SDK 8.0](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) for running the worker locally
- Access to required Azure resources (Cosmos DB SQL API, Service Bus namespace, SignalR service, Application Insights) or corresponding emulator settings

## Running Locally
1. Restore and build the solution:
   ```powershell
   dotnet build
   ```
2. Update `src/MemberPropertyAlert.Functions/local.settings.json` (create if missing) with your Cosmos DB, Service Bus, SignalR, and Application Insights connection information. API key authentication is handled by middleware and expects a hashed secret in configuration.
3. Start the functions host from the project directory:
   ```powershell
   cd src/MemberPropertyAlert.Functions
   func start
   ```
4. (Optional) Launch the mock webhook client to observe outgoing notifications:
   ```powershell
   cd src/MemberPropertyAlert.MockWebhookClient
   dotnet run
   ```

## HTTP API Surface
The `MemberPropertyAlert.Functions` project currently exposes the institution management endpoints below (all secured via API key middleware):

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/institutions?pageNumber=&pageSize=` | Returns paginated institution summaries. |
| `GET` | `/institutions/{id}` | Retrieves full details for a single institution, including addresses. |
| `POST` | `/institutions` | Creates a new institution with an API key hash and optional contact email. |
| `PUT` | `/institutions/{id}` | Updates institution metadata and status. |
| `DELETE` | `/institutions/{id}` | Removes an institution and its linked resources. |
| `GET` | `/institutions/{institutionId}/addresses?pageNumber=&pageSize=` | Lists registered member addresses for an institution. |
| `POST` | `/institutions/{institutionId}/addresses` | Adds an address to the institution roster. |
| `DELETE` | `/institutions/{institutionId}/addresses/{addressId}` | Deletes a registered address. |

Shared helpers such as `FunctionHttpHelpers` handle query parsing, pagination defaults, and consistent problem responses. DTOs live under `src/MemberPropertyAlert.Functions/Models` and map from domain entities to serialized responses.

## Running Tests
Execute the full test suite from the repository root:
```powershell
dotnet test
```
The command runs unit tests for the core library plus integration and end-to-end tests for the function app. Ensure any required environment variables or local settings are present for integration scenarios.

## Additional Documentation
- [Local Quickstart](docs/quickstart-guide.md) – streamlined setup flow for restoring, building, and testing the solution.
- [Infrastructure Overview](docs/infrastructure-overview.md) – summary of cloud resources and deployment topology.
- [Enterprise Recommendations](docs/enterprise-recommendations.md) – roadmap of architecture, security, and operational enhancements aligned with enterprise requirements.

## Next Steps
- Build out remaining scan orchestration, listing match, and analytics endpoints.
- Complete SignalR and Service Bus bindings for real-time alerting.
- Flesh out the React UI and automated end-to-end tests covering the onboarding workflow.
