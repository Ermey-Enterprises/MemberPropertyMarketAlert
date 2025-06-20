<#
.SYNOPSIS
    Deploys the updated infrastructure with corrected CosmosDB containers.

.DESCRIPTION
    This script deploys the updated Bicep infrastructure template that includes
    the corrected CosmosDB container definitions (Alerts and ScanLogs containers
    with proper naming and partition keys).

.PARAMETER ResourceGroupName
    The name of the Azure resource group to deploy to.

.PARAMETER Environment
    The environment to deploy (dev, test, prod). Default is 'dev'.

.PARAMETER Location
    The Azure region to deploy to. Default is 'East US'.

.PARAMETER RentCastApiKey
    The RentCast API key (optional, can be set later).

.PARAMETER AdminApiKey
    The admin API key for secure endpoints (optional, can be set later).

.PARAMETER WhatIf
    Shows what would be deployed without actually deploying.

.EXAMPLE
    .\Deploy-Infrastructure.ps1 -ResourceGroupName "rg-member-property-alert-dev"

.EXAMPLE
    .\Deploy-Infrastructure.ps1 -ResourceGroupName "rg-member-property-alert-dev" -Environment "dev" -WhatIf
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment = "dev",
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory = $false)]
    [string]$RentCastApiKey = "",
    
    [Parameter(Mandatory = $false)]
    [string]$AdminApiKey = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

# Colors for output
$Red = "Red"
$Green = "Green"
$Yellow = "Yellow"
$Blue = "Blue"
$Cyan = "Cyan"
$Magenta = "Magenta"

Write-Host "=== Infrastructure Deployment Script ===" -ForegroundColor $Magenta
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "Environment: $Environment" -ForegroundColor White
Write-Host "Location: $Location" -ForegroundColor White
Write-Host "What-If Mode: $WhatIf" -ForegroundColor White
Write-Host ""

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "Error: Azure CLI is not installed. Please install it first." -ForegroundColor $Red
    Write-Host "Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor $Yellow
    exit 1
}

# Check if user is logged in
try {
    $account = az account show --query name -o tsv 2>$null
    if (-not $account) {
        Write-Host "Please log in to Azure..." -ForegroundColor $Yellow
        az login
        $account = az account show --query name -o tsv
    }
    Write-Host "✓ Connected to Azure subscription: $account" -ForegroundColor $Green
}
catch {
    Write-Host "Error: Failed to connect to Azure" -ForegroundColor $Red
    exit 1
}

# Check if resource group exists
Write-Host "`nChecking resource group..." -ForegroundColor $Yellow
$rgExists = az group exists --name $ResourceGroupName
if ($rgExists -eq "false") {
    Write-Host "Resource group '$ResourceGroupName' does not exist. Creating it..." -ForegroundColor $Yellow
    az group create --name $ResourceGroupName --location $Location
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to create resource group" -ForegroundColor $Red
        exit 1
    }
    Write-Host "✓ Resource group created successfully" -ForegroundColor $Green
}
else {
    Write-Host "✓ Resource group exists" -ForegroundColor $Green
}

# Prepare parameters
$parametersFile = "infra/main.$Environment.parameters.json"
if (-not (Test-Path $parametersFile)) {
    Write-Host "Warning: Parameters file '$parametersFile' not found. Using default parameters." -ForegroundColor $Yellow
    $parametersFile = $null
}

# Build deployment command
$deploymentName = "infrastructure-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$bicepFile = "infra/main.bicep"

if (-not (Test-Path $bicepFile)) {
    Write-Host "Error: Bicep template '$bicepFile' not found" -ForegroundColor $Red
    exit 1
}

Write-Host "`nPreparing deployment..." -ForegroundColor $Yellow
Write-Host "Deployment Name: $deploymentName" -ForegroundColor White
Write-Host "Bicep Template: $bicepFile" -ForegroundColor White
Write-Host "Parameters File: $(if ($parametersFile) { $parametersFile } else { 'None (using defaults)' })" -ForegroundColor White

# Build command arguments
$deployArgs = @(
    "deployment", "group", "create",
    "--resource-group", $ResourceGroupName,
    "--name", $deploymentName,
    "--template-file", $bicepFile,
    "--parameters", "environment=$Environment"
)

if ($parametersFile) {
    $deployArgs += "--parameters"
    $deployArgs += "@$parametersFile"
}

if ($RentCastApiKey) {
    $deployArgs += "--parameters"
    $deployArgs += "rentCastApiKey=$RentCastApiKey"
}

if ($AdminApiKey) {
    $deployArgs += "--parameters"
    $deployArgs += "adminApiKey=$AdminApiKey"
}

if ($WhatIf) {
    $deployArgs += "--what-if"
    Write-Host "`n=== WHAT-IF DEPLOYMENT ===" -ForegroundColor $Cyan
    Write-Host "This will show what would be deployed without making actual changes." -ForegroundColor $Cyan
}
else {
    Write-Host "`n=== STARTING DEPLOYMENT ===" -ForegroundColor $Yellow
    Write-Host "This will deploy the updated infrastructure with corrected CosmosDB containers." -ForegroundColor $Yellow
}

Write-Host "`nExecuting: az $($deployArgs -join ' ')" -ForegroundColor $Blue
Write-Host ""

# Execute deployment
$startTime = Get-Date
try {
    & az @deployArgs
    $exitCode = $LASTEXITCODE
}
catch {
    Write-Host "Error during deployment: $($_.Exception.Message)" -ForegroundColor $Red
    exit 1
}

$endTime = Get-Date
$duration = $endTime - $startTime

if ($exitCode -eq 0) {
    if ($WhatIf) {
        Write-Host "`n✓ What-if analysis completed successfully" -ForegroundColor $Green
        Write-Host "Review the changes above and run without --WhatIf to deploy." -ForegroundColor $Yellow
    }
    else {
        Write-Host "`n✓ Deployment completed successfully!" -ForegroundColor $Green
        Write-Host "Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor $White
        
        # Get deployment outputs
        Write-Host "`nRetrieving deployment outputs..." -ForegroundColor $Yellow
        $outputs = az deployment group show --resource-group $ResourceGroupName --name $deploymentName --query properties.outputs --output json | ConvertFrom-Json
        
        if ($outputs) {
            Write-Host "`n=== Deployment Outputs ===" -ForegroundColor $Magenta
            if ($outputs.functionAppUrl) {
                Write-Host "Function App URL: $($outputs.functionAppUrl.value)" -ForegroundColor $White
            }
            if ($outputs.webAppUrl) {
                Write-Host "Web App URL: $($outputs.webAppUrl.value)" -ForegroundColor $White
            }
            if ($outputs.cosmosAccountName) {
                Write-Host "CosmosDB Account: $($outputs.cosmosAccountName.value)" -ForegroundColor $White
            }
        }
        
        Write-Host "`n=== Next Steps ===" -ForegroundColor $Magenta
        Write-Host "1. Verify the new containers exist in CosmosDB:" -ForegroundColor $White
        Write-Host "   - Institutions (partition: /id)" -ForegroundColor $White
        Write-Host "   - Addresses (partition: /institutionId)" -ForegroundColor $White
        Write-Host "   - Alerts (partition: /institutionId)" -ForegroundColor $White
        Write-Host "   - ScanLogs (partition: /institutionId)" -ForegroundColor $White
        Write-Host "2. Deploy your application code to the updated infrastructure" -ForegroundColor $White
        Write-Host "3. Test the application to ensure all containers are accessible" -ForegroundColor $White
    }
}
else {
    Write-Host "`n✗ Deployment failed with exit code: $exitCode" -ForegroundColor $Red
    Write-Host "Check the error messages above for details." -ForegroundColor $Yellow
    exit $exitCode
}

Write-Host "`n=== Summary ===" -ForegroundColor $Magenta
if ($WhatIf) {
    Write-Host "What-if analysis completed. No changes were made." -ForegroundColor $Cyan
}
else {
    Write-Host "Infrastructure deployment completed with corrected CosmosDB containers." -ForegroundColor $Green
}
