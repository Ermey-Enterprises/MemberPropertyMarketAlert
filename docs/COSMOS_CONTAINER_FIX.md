# CosmosDB Container Configuration Fix

## Problem Description

The CosmosDB database exists with `Institutions` and `Addresses` containers, but is missing the `Alerts` and `ScanLogs` containers that the application expects. Additionally, there were naming and partition key mismatches between the infrastructure definition and application code.

## Issues Identified

### 1. Missing Containers
- **Alerts container**: Required for storing property alerts
- **ScanLogs container**: Required for storing scan operation logs

### 2. Naming Mismatches
- Infrastructure defined `MemberAddresses` but application expects `Addresses`
- Infrastructure defined `PropertyAlerts` but application expects `Alerts`

### 3. Partition Key Mismatches
- ScanLogs container was defined with `/date` partition key but application uses `/institutionId`

## Solutions Provided

### Option 1: Infrastructure Fix (Recommended)

Updated the Bicep infrastructure template (`infra/main.bicep`) to match application expectations:

```bicep
// Fixed container definitions
resource addressesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'Addresses'  // Changed from 'MemberAddresses'
  properties: {
    resource: {
      id: 'Addresses'
      partitionKey: {
        paths: ['/institutionId']
        kind: 'Hash'
      }
    }
  }
}

resource alertsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'Alerts'  // Changed from 'PropertyAlerts'
  properties: {
    resource: {
      id: 'Alerts'
      partitionKey: {
        paths: ['/institutionId']
        kind: 'Hash'
      }
    }
  }
}

resource scanLogsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'ScanLogs'
  properties: {
    resource: {
      id: 'ScanLogs'
      partitionKey: {
        paths: ['/institutionId']  // Changed from '/date'
        kind: 'Hash'
      }
    }
  }
}
```

### Option 2: Manual Container Creation Scripts

Created scripts for immediate deployment in both PowerShell and Bash:

#### PowerShell Script (Windows)
```powershell
# Example usage
.\scripts\Create-MissingCosmosContainers.ps1 -ResourceGroupName "rg-member-property-alert-dev" -CosmosAccountName "cosmos-member-property-alert-dev"

# Dry run to see what would be created
.\scripts\Create-MissingCosmosContainers.ps1 -ResourceGroupName "rg-member-property-alert-dev" -CosmosAccountName "cosmos-member-property-alert-dev" -DryRun
```

#### Bash Script (Linux/macOS/WSL)
```bash
# Make script executable (Linux/macOS/WSL)
chmod +x scripts/create-missing-cosmos-containers.sh

# Example usage
./scripts/create-missing-cosmos-containers.sh -g "rg-member-property-alert-dev" -a "cosmos-member-property-alert-dev"

# Dry run to see what would be created
./scripts/create-missing-cosmos-containers.sh -g "rg-member-property-alert-dev" -a "cosmos-member-property-alert-dev" --dry-run
```

### Option 3: Application Code Updates

Updated the CosmosService (`src/MemberPropertyAlert.Functions/Services/CosmosService.cs`) to handle ScanLog partitioning correctly:

- Ensures `InstitutionId` is always set for ScanLogs (defaults to "system" for global scans)
- Consistent partition key handling across all ScanLog operations

## Container Schema

The application now expects these containers with the following partition keys:

| Container Name | Partition Key | Purpose |
|----------------|---------------|---------|
| `Institutions` | `/id` | Financial institution data |
| `Addresses` | `/institutionId` | Member addresses to monitor |
| `Alerts` | `/institutionId` | Property alerts generated |
| `ScanLogs` | `/institutionId` | Scan operation logs |

## Deployment Steps

### For New Deployments
1. Use the updated Bicep template to deploy infrastructure
2. All containers will be created with correct names and partition keys

### For Existing Deployments
Choose one of these approaches:

#### Approach A: Redeploy Infrastructure (Recommended)
1. Update your infrastructure using the corrected Bicep template
2. This will create the missing containers with correct configuration
3. May require data migration if existing containers have different names

#### Approach B: Manual Container Creation
1. Run the PowerShell script to create missing containers:
   ```powershell
   .\scripts\Create-MissingCosmosContainers.ps1 -ResourceGroupName "your-rg" -CosmosAccountName "your-cosmos-account"
   ```
2. The script will:
   - Check existing containers
   - Create missing `Alerts` and `ScanLogs` containers
   - Warn about any naming mismatches
   - Provide guidance for next steps

#### Approach C: Azure Portal Manual Creation
1. Navigate to your CosmosDB account in Azure Portal
2. Create missing containers manually:
   - **Alerts**: Partition key `/institutionId`
   - **ScanLogs**: Partition key `/institutionId`

## Verification Steps

After implementing the fix:

1. **Check Container Existence**:
   ```powershell
   # List all containers
   Get-AzCosmosDBSqlContainer -ResourceGroupName "your-rg" -AccountName "your-cosmos-account" -DatabaseName "MemberPropertyAlert"
   ```

2. **Test Application Connectivity**:
   - Run the application locally or in Azure
   - Check that all CRUD operations work for each container
   - Verify the health endpoint returns success

3. **Monitor Application Logs**:
   - Look for any CosmosDB-related errors
   - Ensure container initialization succeeds on startup

## Configuration Reference

The application uses these configuration values (from `CosmosConfiguration`):

```json
{
  "CosmosDB": {
    "DatabaseName": "MemberPropertyAlert",
    "InstitutionsContainer": "Institutions",
    "AddressesContainer": "Addresses",
    "AlertsContainer": "Alerts",
    "ScanLogsContainer": "ScanLogs"
  }
}
```

## Troubleshooting

### Common Issues

1. **Container Not Found Errors**:
   - Verify container names match exactly (case-sensitive)
   - Check that containers exist in the correct database

2. **Partition Key Errors**:
   - Ensure all documents have the required partition key field
   - For ScanLogs, `InstitutionId` will default to "system" if not provided

3. **Permission Errors**:
   - Verify the application has read/write permissions to CosmosDB
   - Check connection string and authentication

### Useful Commands

```powershell
# Check if specific container exists
Get-AzCosmosDBSqlContainer -ResourceGroupName "rg-name" -AccountName "cosmos-name" -DatabaseName "MemberPropertyAlert" -Name "Alerts"

# List all containers with partition keys
Get-AzCosmosDBSqlContainer -ResourceGroupName "rg-name" -AccountName "cosmos-name" -DatabaseName "MemberPropertyAlert" | ForEach-Object { 
    Write-Host "$($_.Name): $($_.Resource.PartitionKey.Paths[0])" 
}
```

## Next Steps

1. **Immediate**: Run the container creation script or redeploy infrastructure
2. **Short-term**: Test application functionality thoroughly
3. **Long-term**: Consider implementing automated container validation in CI/CD pipeline

## Related Files

- `infra/main.bicep` - Infrastructure template with corrected container definitions
- `scripts/Create-MissingCosmosContainers.ps1` - Manual container creation script (PowerShell)
- `scripts/create-missing-cosmos-containers.sh` - Manual container creation script (Bash)
- `src/MemberPropertyAlert.Functions/Services/CosmosService.cs` - Updated service implementation
- `src/MemberPropertyAlert.Core/Services/ICosmosService.cs` - Container configuration class
