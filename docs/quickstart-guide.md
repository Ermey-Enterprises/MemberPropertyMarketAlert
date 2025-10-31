# Local Quickstart Guide

The `scripts/Quickstart.ps1` PowerShell script streamlines local onboarding for the Member Property Market Alert solution. It restores dependencies, builds the solution, runs the test suite, and can optionally launch the Azure Functions host and mock webhook client—making it easy to verify the project in one step.

## Prerequisites

Install the following tools before running the script:

- **.NET SDK 8.0** – required for building the solution (`dotnet --info` should show version 8.x).
- **Azure Functions Core Tools v4** – needed only when launching the Functions host (`func --version`).
- **Azure CLI** *(optional)* – useful for authenticating against Azure resources when calling remote services.
- **Powershell 5.1 or later** – the script targets Windows PowerShell; it also runs under PowerShell 7.

Ensure `src/MemberPropertyAlert.Functions/local.settings.json` contains the required secrets for Cosmos DB, Service Bus, SignalR, and the API key hash.

## Running the Quickstart

From the repository root, execute:

```powershell
.\scripts\Quickstart.ps1
```

The script performs these steps:

1. Validates that the .NET SDK (and optionally Azure Functions Core Tools) are installed.
2. Restores NuGet packages for the entire solution.
3. Builds the solution in **Debug** configuration.
4. Runs the full test suite (unless disabled).
5. Optionally opens new terminal windows for the Functions host and mock webhook client.

> The script stops immediately if any step fails, surfacing the exit code so you can address the issue.

## Parameters

| Parameter | Description | Default |
| --- | --- | --- |
| `-SkipTests` | Skips the `dotnet test` step. | `false` |
| `-StartFunctions` | Launches `func start` for `MemberPropertyAlert.Functions` in a new PowerShell window. | `false` |
| `-StartWebhookClient` | Launches `dotnet run` for the mock webhook client in a new PowerShell window. | `false` |
| `-Configuration <Debug|Release>` | Builds/tests using the specified configuration. | `Debug` |

### Examples

Run the script, skip tests, and immediately start the Functions host:

```powershell
.\scripts\Quickstart.ps1 -SkipTests -StartFunctions
```

Build and test in Release mode while launching both the Functions host and mock webhook client:

```powershell
.\scripts\Quickstart.ps1 -Configuration Release -StartFunctions -StartWebhookClient
```

## Troubleshooting

- **Missing tools** – The script halts if it cannot find `dotnet` or `func`. Install the prerequisites and re-run.
- **Configuration errors** – If the Functions host fails to start, confirm that all connection strings in `local.settings.json` are valid.
- **Port conflicts** – Stop any existing Functions host sessions before relaunching to avoid port bindings on 7071.

## Next Steps

- After the Functions host is running, call the HTTP endpoints listed in the root `README.md` using the API key header required by the middleware.
- Pair the quickstart with the infrastructure guide (`docs/infrastructure-overview.md`) to provision the backing Azure resources required for end-to-end workflows.
