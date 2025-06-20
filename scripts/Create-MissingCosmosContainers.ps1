<#
.SYNOPSIS
    Creates missing CosmosDB containers for the Member Property Alert application.

.DESCRIPTION
    This script creates the missing Alerts and ScanLogs containers in an existing CosmosDB database.
    It also handles renaming the existing MemberAddresses container to Addresses if needed.

.PARAMETER ResourceGroupName
    The name of the Azure resource group containing the CosmosDB account.

.PARAMETER CosmosAccountName
    The name of the CosmosDB account.

.PARAMETER DatabaseName
    The name of the database (default: MemberPropertyAlert).

.PARAMETER DryRun
    If specified, shows what would be created without actually creating anything.

.EXAMPLE
    .\Create-MissingCosmosContainers.ps1 -ResourceGroupName "rg-member-property-alert-dev" -CosmosAccountName "cosmos-member-property-alert-dev"

.EXAMPLE
    .\Create-MissingCosmosContainers.ps1 -ResourceGroupName "rg-member-property-alert-dev" -CosmosAccountName "cosmos-member-property-alert-dev" -DryRun
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $true)]
    [string]$CosmosAccountName,
    
    [Parameter(Mandatory = $false)]
    [string]$DatabaseName = "MemberPropertyAlert",
    
    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

# Ensure Azure PowerShell module is available
if (-not (Get-Module -ListAvailable -Name Az.CosmosDB)) {
    Write-Error "Azure PowerShell module 'Az.CosmosDB' is not installed. Please install it using: Install-Module -Name Az -Force"
    exit 1
}

# Ensure user is logged in to Azure
try {
    $context = Get-AzContext
    if (-not $context) {
        Write-Host "Please log in to Azure..." -ForegroundColor Yellow
        Connect-AzAccount
    }
    Write-Host "Connected to Azure subscription: $($context.Subscription.Name)" -ForegroundColor Green
}
catch {
    Write-Error "Failed to connect to Azure: $($_.Exception.Message)"
    exit 1
}

# Function to check if container exists
function Test-CosmosContainer {
    param(
        [string]$ResourceGroupName,
        [string]$AccountName,
        [string]$DatabaseName,
        [string]$ContainerName
    )
    
    try {
        $container = Get-AzCosmosDBSqlContainer -ResourceGroupName $ResourceGroupName -AccountName $AccountName -DatabaseName $DatabaseName -Name $ContainerName -ErrorAction SilentlyContinue
        return $null -ne $container
    }
    catch {
        return $false
    }
}

# Function to create container
function New-CosmosContainer {
    param(
        [string]$ResourceGroupName,
        [string]$AccountName,
        [string]$DatabaseName,
        [string]$ContainerName,
        [string]$PartitionKeyPath,
        [bool]$DryRun
    )
    
    if ($DryRun) {
        Write-Host "DRY RUN: Would create container '$ContainerName' with partition key '$PartitionKeyPath'" -ForegroundColor Cyan
        return
    }
    
    try {
        Write-Host "Creating container '$ContainerName' with partition key '$PartitionKeyPath'..." -ForegroundColor Yellow
        
        $containerProperties = @{
            ResourceGroupName = $ResourceGroupName
            AccountName = $AccountName
            DatabaseName = $DatabaseName
            Name = $ContainerName
            PartitionKeyPath = $PartitionKeyPath
            PartitionKeyKind = "Hash"
        }
        
        $result = New-AzCosmosDBSqlContainer @containerProperties
        Write-Host "✓ Successfully created container '$ContainerName'" -ForegroundColor Green
        return $result
    }
    catch {
        Write-Error "Failed to create container '$ContainerName': $($_.Exception.Message)"
        return $null
    }
}

# Function to list all containers
function Get-AllContainers {
    param(
        [string]$ResourceGroupName,
        [string]$AccountName,
        [string]$DatabaseName
    )
    
    try {
        $containers = Get-AzCosmosDBSqlContainer -ResourceGroupName $ResourceGroupName -AccountName $AccountName -DatabaseName $DatabaseName
        return $containers
    }
    catch {
        Write-Error "Failed to list containers: $($_.Exception.Message)"
        return @()
    }
}

Write-Host "=== CosmosDB Container Creation Script ===" -ForegroundColor Magenta
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "Cosmos Account: $CosmosAccountName" -ForegroundColor White
Write-Host "Database: $DatabaseName" -ForegroundColor White
Write-Host "Dry Run: $DryRun" -ForegroundColor White
Write-Host ""

# Check if CosmosDB account exists
try {
    $cosmosAccount = Get-AzCosmosDBAccount -ResourceGroupName $ResourceGroupName -Name $CosmosAccountName -ErrorAction Stop
    Write-Host "✓ Found CosmosDB account: $CosmosAccountName" -ForegroundColor Green
}
catch {
    Write-Error "CosmosDB account '$CosmosAccountName' not found in resource group '$ResourceGroupName'"
    exit 1
}

# Check if database exists
try {
    $database = Get-AzCosmosDBSqlDatabase -ResourceGroupName $ResourceGroupName -AccountName $CosmosAccountName -Name $DatabaseName -ErrorAction Stop
    Write-Host "✓ Found database: $DatabaseName" -ForegroundColor Green
}
catch {
    Write-Error "Database '$DatabaseName' not found in CosmosDB account '$CosmosAccountName'"
    exit 1
}

# List existing containers
Write-Host "`nChecking existing containers..." -ForegroundColor Yellow
$existingContainers = Get-AllContainers -ResourceGroupName $ResourceGroupName -AccountName $CosmosAccountName -DatabaseName $DatabaseName

if ($existingContainers.Count -gt 0) {
    Write-Host "Existing containers:" -ForegroundColor White
    foreach ($container in $existingContainers) {
        $partitionKey = $container.Resource.PartitionKey.Paths[0]
        Write-Host "  - $($container.Name) (partition: $partitionKey)" -ForegroundColor Gray
    }
}
else {
    Write-Host "No existing containers found." -ForegroundColor Gray
}

Write-Host ""

# Define required containers
$requiredContainers = @(
    @{ Name = "Institutions"; PartitionKey = "/id" },
    @{ Name = "Addresses"; PartitionKey = "/institutionId" },
    @{ Name = "Alerts"; PartitionKey = "/institutionId" },
    @{ Name = "ScanLogs"; PartitionKey = "/institutionId" }
)

# Check which containers need to be created
$containersToCreate = @()
$warnings = @()

foreach ($container in $requiredContainers) {
    $exists = Test-CosmosContainer -ResourceGroupName $ResourceGroupName -AccountName $CosmosAccountName -DatabaseName $DatabaseName -ContainerName $container.Name
    
    if (-not $exists) {
        $containersToCreate += $container
        Write-Host "⚠ Missing container: $($container.Name)" -ForegroundColor Red
    }
    else {
        Write-Host "✓ Container exists: $($container.Name)" -ForegroundColor Green
    }
}

# Check for legacy container names
$legacyMemberAddresses = Test-CosmosContainer -ResourceGroupName $ResourceGroupName -AccountName $CosmosAccountName -DatabaseName $DatabaseName -ContainerName "MemberAddresses"
if ($legacyMemberAddresses) {
    $warnings += "Found legacy container 'MemberAddresses'. You may need to rename it to 'Addresses' or update your application configuration."
}

$legacyPropertyAlerts = Test-CosmosContainer -ResourceGroupName $ResourceGroupName -AccountName $CosmosAccountName -DatabaseName $DatabaseName -ContainerName "PropertyAlerts"
if ($legacyPropertyAlerts) {
    $warnings += "Found legacy container 'PropertyAlerts'. You may need to rename it to 'Alerts' or update your application configuration."
}

# Display warnings
if ($warnings.Count -gt 0) {
    Write-Host "`nWarnings:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "⚠ $warning" -ForegroundColor Yellow
    }
}

# Create missing containers
if ($containersToCreate.Count -eq 0) {
    Write-Host "`n✓ All required containers already exist!" -ForegroundColor Green
}
else {
    Write-Host "`nCreating missing containers..." -ForegroundColor Yellow
    
    foreach ($container in $containersToCreate) {
        $result = New-CosmosContainer -ResourceGroupName $ResourceGroupName -AccountName $CosmosAccountName -DatabaseName $DatabaseName -ContainerName $container.Name -PartitionKeyPath $container.PartitionKey -DryRun $DryRun
        
        if ($result -and -not $DryRun) {
            Write-Host "✓ Container '$($container.Name)' created successfully" -ForegroundColor Green
        }
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Magenta
if ($DryRun) {
    Write-Host "Dry run completed. $($containersToCreate.Count) containers would be created." -ForegroundColor Cyan
}
else {
    Write-Host "Script completed. $($containersToCreate.Count) containers were processed." -ForegroundColor Green
}

if ($warnings.Count -gt 0) {
    Write-Host "`nPlease review the warnings above and consider updating your infrastructure or application configuration." -ForegroundColor Yellow
}

Write-Host "`nNext steps:" -ForegroundColor White
Write-Host "1. Verify containers were created correctly in the Azure portal" -ForegroundColor Gray
Write-Host "2. Test your application to ensure it can connect to all containers" -ForegroundColor Gray
Write-Host "3. Consider updating your Bicep infrastructure to match the current state" -ForegroundColor Gray
