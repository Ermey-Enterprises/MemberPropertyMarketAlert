# Member Property Market Alert - Local Development Guide

## Prerequisites

- .NET 8 SDK
- Azure Functions Core Tools v4
- Azure Storage Emulator or Azurite
- Azure Cosmos DB Emulator
- Visual Studio 2022 or VS Code with Azure Functions extension

## Quick Start

### 1. Install Required Tools

```bash
# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Install Azurite (Azure Storage Emulator)
npm install -g azurite
```

### 2. Start Local Services

**Start Azurite (in a separate terminal):**
```bash
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

**Start Cosmos DB Emulator:**
- Download and install from: https://aka.ms/cosmosdb-emulator
- Start the emulator (it will run on https://localhost:8081)

### 3. Configure Local Settings

Update `src/MemberPropertyMarketAlert.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDb:DatabaseName": "MemberPropertyMarketAlert"
  },
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
  }
}
```

### 4. Initialize Cosmos DB

Create a PowerShell script `scripts/setup-local-cosmosdb.ps1`:

```powershell
# Install the Cosmos DB module if not already installed
if (!(Get-Module -ListAvailable -Name CosmosDB)) {
    Install-Module -Name CosmosDB -Force -AllowClobber
}

# Cosmos DB Emulator connection details
$cosmosDbUri = "https://localhost:8081"
$cosmosDbKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
$databaseName = "MemberPropertyMarketAlert"

try {
    # Create database
    Write-Host "Creating database: $databaseName"
    $database = New-CosmosDbDatabase -Context $cosmosDbContext -Id $databaseName -OfferThroughput 400
    
    # Create containers
    $containers = @(
        @{ Name = "MemberAddresses"; PartitionKey = "/partitionKey" },
        @{ Name = "PropertyListings"; PartitionKey = "/partitionKey" },
        @{ Name = "PropertyMatches"; PartitionKey = "/partitionKey" }
    )
    
    foreach ($container in $containers) {
        Write-Host "Creating container: $($container.Name)"
        New-CosmosDbCollection -Context $cosmosDbContext -Database $databaseName -Id $container.Name -PartitionKey $container.PartitionKey -OfferThroughput 400
    }
    
    Write-Host "Local Cosmos DB setup completed successfully!"
}
catch {
    Write-Error "Error setting up Cosmos DB: $($_.Exception.Message)"
}
```

### 5. Run the Functions App

```bash
# Navigate to the Functions project
cd src/MemberPropertyMarketAlert.Functions

# Start the Functions runtime
func start
```

The Functions app will start on `http://localhost:7071`

## Testing the API

### Using curl

**1. Create a member address:**
```bash
curl -X POST "http://localhost:7071/api/members/addresses" \
  -H "Content-Type: application/json" \
  -d '{
    "institutionId": "test-bank-001",
    "anonymousReferenceId": "member-12345",
    "address": "123 Main Street",
    "city": "Anytown",
    "state": "CA",
    "zipCode": "12345"
  }'
```

**2. Bulk create member addresses:**
```bash
curl -X POST "http://localhost:7071/api/members/addresses/bulk" \
  -H "Content-Type: application/json" \
  -d '{
    "institutionId": "test-bank-001",
    "addresses": [
      {
        "anonymousReferenceId": "member-001",
        "address": "123 Main St",
        "city": "Anytown",
        "state": "CA",
        "zipCode": "12345"
      },
      {
        "anonymousReferenceId": "member-002",
        "address": "456 Oak Ave",
        "city": "Somewhere",
        "state": "CA",
        "zipCode": "67890"
      }
    ]
  }'
```

**3. Get member addresses:**
```bash
curl "http://localhost:7071/api/members/addresses/test-bank-001"
```

### Using PowerShell

Create `scripts/test-api.ps1`:

```powershell
$baseUrl = "http://localhost:7071/api"

# Test data
$testInstitution = "test-bank-001"
$testAddresses = @(
    @{
        anonymousReferenceId = "member-001"
        address = "123 Main Street"
        city = "Anytown"
        state = "CA"
        zipCode = "12345"
    },
    @{
        anonymousReferenceId = "member-002"
        address = "456 Oak Avenue"
        city = "Somewhere"
        state = "CA"
        zipCode = "67890"
    }
)

# Create bulk addresses
$bulkRequest = @{
    institutionId = $testInstitution
    addresses = $testAddresses
} | ConvertTo-Json -Depth 3

Write-Host "Creating bulk member addresses..."
$response = Invoke-RestMethod -Uri "$baseUrl/members/addresses/bulk" -Method POST -Body $bulkRequest -ContentType "application/json"
Write-Host "Response: $($response | ConvertTo-Json)"

# Get addresses
Write-Host "`nRetrieving member addresses..."
$addresses = Invoke-RestMethod -Uri "$baseUrl/members/addresses/$testInstitution" -Method GET
Write-Host "Found $($addresses.Count) addresses:"
$addresses | ForEach-Object { Write-Host "  - $($_.address), $($_.city), $($_.state)" }
```

## Debugging

### Visual Studio 2022
1. Set `MemberPropertyMarketAlert.Functions` as startup project
2. Press F5 to start debugging
3. Set breakpoints in your functions
4. Use the test scripts above to trigger the functions

### VS Code
1. Open the project in VS Code
2. Install the Azure Functions extension
3. Press F5 to start debugging
4. Select "Attach to .NET Functions" when prompted

## Monitoring and Logs

### Function Logs
When running locally, logs appear in the console where you ran `func start`. You can also view them in:
- Visual Studio Output window
- VS Code Debug Console

### Cosmos DB Data Explorer
Access the Cosmos DB Emulator at: https://localhost:8081/_explorer/index.html

## Common Issues

### 1. Cosmos DB Connection Issues
- Ensure the Cosmos DB Emulator is running
- Check that the connection string in `local.settings.json` is correct
- Verify the database and containers exist

### 2. Storage Connection Issues
- Ensure Azurite is running
- Check that `AzureWebJobsStorage` is set to `UseDevelopmentStorage=true`

### 3. Function Not Starting
- Ensure .NET 8 SDK is installed
- Check that all NuGet packages are restored: `dotnet restore`
- Verify Azure Functions Core Tools v4 is installed

### 4. Port Conflicts
- Default Functions port is 7071
- Change port with: `func start --port 7072`
- Cosmos DB Emulator uses port 8081

## Sample Test Data

Create `scripts/seed-test-data.ps1` for testing:

```powershell
$baseUrl = "http://localhost:7071/api"

# Sample institutions and addresses
$institutions = @(
    @{
        id = "credit-union-001"
        addresses = @(
            @{ ref = "CU001-M001"; address = "123 Main St"; city = "Springfield"; state = "IL"; zip = "62701" },
            @{ ref = "CU001-M002"; address = "456 Oak Ave"; city = "Springfield"; state = "IL"; zip = "62702" },
            @{ ref = "CU001-M003"; address = "789 Pine Rd"; city = "Decatur"; state = "IL"; zip = "62521" }
        )
    },
    @{
        id = "community-bank-002"
        addresses = @(
            @{ ref = "CB002-M001"; address = "321 Elm St"; city = "Peoria"; state = "IL"; zip = "61601" },
            @{ ref = "CB002-M002"; address = "654 Maple Dr"; city = "Peoria"; state = "IL"; zip = "61602" }
        )
    }
)

foreach ($institution in $institutions) {
    Write-Host "Seeding data for institution: $($institution.id)"
    
    $bulkRequest = @{
        institutionId = $institution.id
        addresses = $institution.addresses | ForEach-Object {
            @{
                anonymousReferenceId = $_.ref
                address = $_.address
                city = $_.city
                state = $_.state
                zipCode = $_.zip
            }
        }
    } | ConvertTo-Json -Depth 3
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/members/addresses/bulk" -Method POST -Body $bulkRequest -ContentType "application/json"
        Write-Host "  Created $($response.successCount) addresses"
    }
    catch {
        Write-Error "  Failed to create addresses: $($_.Exception.Message)"
    }
}

Write-Host "`nTest data seeding completed!"
```

Run the script: `.\scripts\seed-test-data.ps1`

## Next Steps

After local testing, you can:
1. Deploy to Azure using the Deployment Guide
2. Set up CI/CD pipelines
3. Configure production monitoring
4. Add real estate data source integrations
