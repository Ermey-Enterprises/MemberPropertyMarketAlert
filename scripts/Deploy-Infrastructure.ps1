<#
.SYNOPSIS
    Deploy Member Property Alert infrastructure to Azure
    
.DESCRIPTION
    This script deploys the Member Property Alert infrastructure using Azure Bicep templates.
    It supports multiple environments and includes validation and error handling.
    
.PARAMETER Environment
    The target environment (dev, test, prod)
    
.PARAMETER Location
    Azure region for deployment (default: eastus2)
    
.PARAMETER SubscriptionId
    Azure subscription ID (optional, uses current subscription if not specified)
    
.PARAMETER ResourceGroupName
    Custom resource group name (optional, uses naming convention if not specified)
    
.PARAMETER ValidateOnly
    Only validate the deployment without executing it
    
.PARAMETER WhatIf
    Show what resources would be created/modified without deploying
    
.EXAMPLE
    .\Deploy-Infrastructure.ps1 -Environment dev
    
.EXAMPLE
    .\Deploy-Infrastructure.ps1 -Environment prod -Location eastus2 -ValidateOnly
    
.EXAMPLE
    .\Deploy-Infrastructure.ps1 -Environment test -WhatIf
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment,
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "eastus2",
    
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $false)]
    [switch]$ValidateOnly,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Script variables
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectRoot = Split-Path -Parent $ScriptPath
$BicepTemplate = Join-Path $ProjectRoot "infra\main.bicep"
$ParameterFile = Join-Path $ProjectRoot "infra\main.$Environment.parameters.json"

# Functions
function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "=" * 80 -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "🔹 $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️ $Message" -ForegroundColor Yellow
}

function Test-Prerequisites {
    Write-Step "Checking prerequisites..."
    
    # Check if Azure CLI is installed
    try {
        $azVersion = az version --output json | ConvertFrom-Json
        Write-Success "Azure CLI version: $($azVersion.'azure-cli')"
    }
    catch {
        Write-Error "Azure CLI is not installed or not in PATH"
        throw "Please install Azure CLI: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    }
    
    # Check if logged in to Azure
    try {
        $account = az account show --output json | ConvertFrom-Json
        Write-Success "Logged in as: $($account.user.name)"
        Write-Success "Current subscription: $($account.name) ($($account.id))"
    }
    catch {
        Write-Error "Not logged in to Azure"
        throw "Please run 'az login' to authenticate"
    }
    
    # Check if Bicep template exists
    if (-not (Test-Path $BicepTemplate)) {
        Write-Error "Bicep template not found: $BicepTemplate"
        throw "Bicep template is missing"
    }
    Write-Success "Bicep template found: $BicepTemplate"
    
    # Check if parameter file exists
    if (-not (Test-Path $ParameterFile)) {
        Write-Warning "Parameter file not found: $ParameterFile"
        Write-Host "Will use inline parameters instead"
    }
    else {
        Write-Success "Parameter file found: $ParameterFile"
    }
}

function Set-AzureContext {
    if ($SubscriptionId) {
        Write-Step "Setting Azure subscription context..."
        try {
            az account set --subscription $SubscriptionId
            Write-Success "Switched to subscription: $SubscriptionId"
        }
        catch {
            Write-Error "Failed to set subscription context"
            throw
        }
    }
}

function Get-ResourceGroupName {
    if ($ResourceGroupName) {
        return $ResourceGroupName
    }
    else {
        return "rg-member-property-alert-$Environment-$Location"
    }
}

function New-ResourceGroup {
    param([string]$RgName)
    
    Write-Step "Creating resource group: $RgName"
    
    try {
        $rg = az group show --name $RgName --output json 2>$null | ConvertFrom-Json
        if ($rg) {
            Write-Success "Resource group already exists: $RgName"
            return
        }
    }
    catch {
        # Resource group doesn't exist, create it
    }
    
    try {
        az group create --name $RgName --location $Location --output none
        Write-Success "Created resource group: $RgName"
    }
    catch {
        Write-Error "Failed to create resource group: $RgName"
        throw
    }
}

function Clear-AzureCache {
    Write-Step "Clearing Azure CLI and Bicep caches..."
    
    try {
        # Clear Azure CLI cache
        az cache purge --verbose 2>$null
        Write-Success "Azure CLI cache cleared"
        
        # Clear Bicep cache and ensure latest version
        az bicep upgrade --target-version latest --verbose 2>$null
        az config set bicep.use_binary_from_path=false --verbose 2>$null
        Write-Success "Bicep cache cleared and updated"
    }
    catch {
        Write-Warning "Cache clearing encountered issues (non-critical): $_"
    }
}

function Test-BicepTemplate {
    Write-Step "Validating Bicep template..."
    
    try {
        # Clear caches before compilation
        Clear-AzureCache
        
        # Compile with verbose and debug output
        Write-Host "Executing: az bicep build --file `"$BicepTemplate`" --verbose --debug" -ForegroundColor Gray
        az bicep build --file $BicepTemplate --verbose --debug
        Write-Success "Bicep template compiled successfully"
    }
    catch {
        Write-Error "Bicep template compilation failed"
        Write-Host "Debug: Check Bicep syntax and dependencies" -ForegroundColor Red
        throw
    }
}

function Get-DeploymentParameters {
    param([string]$RgName)
    
    $parameters = @()
    
    if (Test-Path $ParameterFile) {
        $parameters += "--parameters"
        $parameters += "@$ParameterFile"
        Write-Step "Using parameter file: $ParameterFile"
    }
    else {
        $parameters += "--parameters"
        $parameters += "environment=$Environment"
        $parameters += "location=$Location"
        Write-Step "Using inline parameters for environment: $Environment"
    }
    
    # Add secrets from environment variables if available
    if ($env:RENTCAST_API_KEY) {
        $parameters += "rentCastApiKey=$env:RENTCAST_API_KEY"
        Write-Success "Added RentCast API key from environment variable"
    }
    else {
        Write-Warning "RENTCAST_API_KEY environment variable not set"
    }
    
    if ($env:ADMIN_API_KEY) {
        $parameters += "adminApiKey=$env:ADMIN_API_KEY"
        Write-Success "Added Admin API key from environment variable"
    }
    else {
        Write-Warning "ADMIN_API_KEY environment variable not set"
    }
    
    return $parameters
}

function Invoke-DeploymentValidation {
    param(
        [string]$RgName,
        [string[]]$Parameters
    )
    
    Write-Step "Validating deployment parameters with enhanced debugging..."
    
    try {
        # Clear cache before validation
        Write-Host "Clearing cache before validation..." -ForegroundColor Gray
        az cache purge --verbose 2>$null
        
        $cmd = "az deployment group validate --resource-group `"$RgName`" --template-file `"$BicepTemplate`" --verbose --debug --only-show-errors"
        foreach ($param in $Parameters) {
            $cmd += " `"$param`""
        }
        
        Write-Host "Executing: $cmd" -ForegroundColor Gray
        
        # Capture both stdout and stderr for debug analysis
        $validationOutput = [System.IO.Path]::GetTempFileName()
        $validationError = [System.IO.Path]::GetTempFileName()
        
        $result = Invoke-Expression "$cmd > `"$validationOutput`" 2>`"$validationError`""
        $exitCode = $LASTEXITCODE
        
        if ($exitCode -eq 0) {
            Write-Success "Deployment validation succeeded"
            
            # Display validation output if available
            if (Test-Path $validationOutput) {
                $output = Get-Content $validationOutput -Raw
                if ($output -and $output.Trim()) {
                    Write-Host "Validation output:" -ForegroundColor Gray
                    Write-Host $output -ForegroundColor Gray
                }
            }
        }
        else {
            Write-Error "Deployment validation failed with exit code: $exitCode"
            
            # Display detailed error information
            if (Test-Path $validationError) {
                $errorContent = Get-Content $validationError -Raw
                if ($errorContent) {
                    Write-Host "Validation error details:" -ForegroundColor Red
                    Write-Host $errorContent -ForegroundColor Red
                }
            }
            
            throw "Validation failed"
        }
        
        # Clean up temporary files
        Remove-Item $validationOutput -ErrorAction SilentlyContinue
        Remove-Item $validationError -ErrorAction SilentlyContinue
    }
    catch {
        Write-Error "Deployment validation failed: $_"
        Write-Host "Debug: Check template syntax, parameters, and Azure permissions" -ForegroundColor Red
        throw
    }
}

function Invoke-WhatIfAnalysis {
    param(
        [string]$RgName,
        [string[]]$Parameters
    )
    
    Write-Step "Running What-If analysis with enhanced debugging..."
    
    try {
        # Clear cache before what-if analysis
        Write-Host "Clearing cache before what-if analysis..." -ForegroundColor Gray
        az cache purge --verbose 2>$null
        
        $cmd = "az deployment group what-if --resource-group `"$RgName`" --template-file `"$BicepTemplate`" --verbose --debug --only-show-errors --result-format FullResourcePayloads"
        foreach ($param in $Parameters) {
            $cmd += " `"$param`""
        }
        
        Write-Host "Executing: $cmd" -ForegroundColor Gray
        
        # Capture output for analysis
        $whatIfOutput = [System.IO.Path]::GetTempFileName()
        $whatIfError = [System.IO.Path]::GetTempFileName()
        
        $result = Invoke-Expression "$cmd > `"$whatIfOutput`" 2>`"$whatIfError`""
        $exitCode = $LASTEXITCODE
        
        if ($exitCode -eq 0) {
            Write-Success "What-If analysis completed successfully"
            
            # Display what-if results
            if (Test-Path $whatIfOutput) {
                $output = Get-Content $whatIfOutput -Raw
                if ($output -and $output.Trim()) {
                    Write-Host "What-If analysis results:" -ForegroundColor Cyan
                    Write-Host $output -ForegroundColor Gray
                }
            }
        }
        else {
            Write-Error "What-If analysis failed with exit code: $exitCode"
            
            # Display detailed error information
            if (Test-Path $whatIfError) {
                $errorContent = Get-Content $whatIfError -Raw
                if ($errorContent) {
                    Write-Host "What-If error details:" -ForegroundColor Red
                    Write-Host $errorContent -ForegroundColor Red
                }
            }
            
            throw "What-If analysis failed"
        }
        
        # Clean up temporary files
        Remove-Item $whatIfOutput -ErrorAction SilentlyContinue
        Remove-Item $whatIfError -ErrorAction SilentlyContinue
    }
    catch {
        Write-Error "What-If analysis failed: $_"
        Write-Host "Debug: Check template syntax, parameters, and resource group permissions" -ForegroundColor Red
        throw
    }
}

function Invoke-Deployment {
    param(
        [string]$RgName,
        [string[]]$Parameters
    )
    
    $deploymentName = "member-property-alert-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Write-Step "Starting deployment with enhanced debugging: $deploymentName"
    
    try {
        # Clear cache before deployment
        Write-Host "Clearing cache before deployment..." -ForegroundColor Gray
        az cache purge --verbose 2>$null
        
        $cmd = "az deployment group create --resource-group `"$RgName`" --name `"$deploymentName`" --template-file `"$BicepTemplate`" --verbose --debug --only-show-errors"
        foreach ($param in $Parameters) {
            $cmd += " `"$param`""
        }
        
        Write-Host "Executing: $cmd" -ForegroundColor Gray
        Write-Host "Deployment start time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')" -ForegroundColor Gray
        
        # Capture deployment output for analysis
        $deploymentOutput = [System.IO.Path]::GetTempFileName()
        $deploymentError = [System.IO.Path]::GetTempFileName()
        
        $result = Invoke-Expression "$cmd > `"$deploymentOutput`" 2>`"$deploymentError`""
        $exitCode = $LASTEXITCODE
        
        Write-Host "Deployment end time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')" -ForegroundColor Gray
        Write-Host "Deployment exit code: $exitCode" -ForegroundColor Gray
        
        if ($exitCode -eq 0) {
            # Parse deployment result
            if (Test-Path $deploymentOutput) {
                $outputContent = Get-Content $deploymentOutput -Raw
                if ($outputContent -and $outputContent.Trim()) {
                    try {
                        $deploymentResult = $outputContent | ConvertFrom-Json
                        
                        if ($deploymentResult.properties.provisioningState -eq "Succeeded") {
                            Write-Success "Deployment completed successfully"
                            
                            # Display outputs
                            if ($deploymentResult.properties.outputs) {
                                Write-Step "Deployment outputs:"
                                $deploymentResult.properties.outputs | ConvertTo-Json -Depth 3 | Write-Host
                            }
                            
                            # Clean up temporary files
                            Remove-Item $deploymentOutput -ErrorAction SilentlyContinue
                            Remove-Item $deploymentError -ErrorAction SilentlyContinue
                            
                            return $deploymentResult
                        }
                        else {
                            Write-Error "Deployment failed with state: $($deploymentResult.properties.provisioningState)"
                            
                            # Display error details if available
                            if ($deploymentResult.properties.error) {
                                Write-Host "Deployment error details:" -ForegroundColor Red
                                $deploymentResult.properties.error | ConvertTo-Json -Depth 3 | Write-Host -ForegroundColor Red
                            }
                            
                            throw "Deployment failed"
                        }
                    }
                    catch {
                        Write-Warning "Could not parse deployment output as JSON: $_"
                        Write-Host "Raw deployment output:" -ForegroundColor Gray
                        Write-Host $outputContent -ForegroundColor Gray
                        throw "Deployment result parsing failed"
                    }
                }
                else {
                    Write-Warning "No deployment output received"
                    throw "Empty deployment output"
                }
            }
            else {
                Write-Warning "Deployment output file not found"
                throw "Missing deployment output"
            }
        }
        else {
            Write-Error "Deployment failed with exit code: $exitCode"
            
            # Display detailed error information
            if (Test-Path $deploymentError) {
                $errorContent = Get-Content $deploymentError -Raw
                if ($errorContent) {
                    Write-Host "Deployment error details:" -ForegroundColor Red
                    Write-Host $errorContent -ForegroundColor Red
                }
            }
            
            # Try to get additional deployment details for troubleshooting
            try {
                Write-Step "Retrieving deployment details for troubleshooting..."
                Write-Host "Executing: az deployment group show --resource-group `"$RgName`" --name `"$deploymentName`" --query properties.error --output json --verbose --debug" -ForegroundColor Gray
                az deployment group show --resource-group $RgName --name $deploymentName --query "properties.error" --output json --verbose --debug
                
                Write-Host "Executing: az deployment operation group list --resource-group `"$RgName`" --name `"$deploymentName`" --query [?properties.provisioningState=='Failed'] --output table --verbose" -ForegroundColor Gray
                az deployment operation group list --resource-group $RgName --name $deploymentName --query "[?properties.provisioningState=='Failed']" --output table --verbose
            }
            catch {
                Write-Warning "Could not retrieve additional deployment details: $_"
            }
            
            throw "Deployment execution failed"
        }
        
        # Clean up temporary files
        Remove-Item $deploymentOutput -ErrorAction SilentlyContinue
        Remove-Item $deploymentError -ErrorAction SilentlyContinue
    }
    catch {
        Write-Error "Deployment execution failed: $_"
        Write-Host "Debug: Check Azure permissions, resource quotas, and template parameters" -ForegroundColor Red
        
        # Clean up temporary files in case of exception
        Remove-Item $deploymentOutput -ErrorAction SilentlyContinue
        Remove-Item $deploymentError -ErrorAction SilentlyContinue
        
        throw
    }
}

function Show-DeploymentSummary {
    param($DeploymentResult)
    
    Write-Header "Deployment Summary"
    
    Write-Host "Environment: " -NoNewline
    Write-Host $Environment -ForegroundColor Green
    
    Write-Host "Resource Group: " -NoNewline
    Write-Host $ResourceGroupName -ForegroundColor Green
    
    Write-Host "Location: " -NoNewline
    Write-Host $Location -ForegroundColor Green
    
    Write-Host "Deployment State: " -NoNewline
    Write-Host $DeploymentResult.properties.provisioningState -ForegroundColor Green
    
    if ($DeploymentResult.properties.outputs) {
        Write-Host ""
        Write-Host "Key Resources:" -ForegroundColor Cyan
        
        $outputs = $DeploymentResult.properties.outputs
        
        if ($outputs.functionAppName) {
            Write-Host "  Function App: " -NoNewline
            Write-Host $outputs.functionAppName.value -ForegroundColor Yellow
        }
        
        if ($outputs.functionAppUrl) {
            Write-Host "  Function App URL: " -NoNewline
            Write-Host $outputs.functionAppUrl.value -ForegroundColor Yellow
        }
        
        if ($outputs.webAppName) {
            Write-Host "  Web App: " -NoNewline
            Write-Host $outputs.webAppName.value -ForegroundColor Yellow
        }
        
        if ($outputs.webAppUrl) {
            Write-Host "  Web App URL: " -NoNewline
            Write-Host $outputs.webAppUrl.value -ForegroundColor Yellow
        }
        
        if ($outputs.cosmosAccountName) {
            Write-Host "  Cosmos DB: " -NoNewline
            Write-Host $outputs.cosmosAccountName.value -ForegroundColor Yellow
        }
        
        if ($outputs.storageAccountName) {
            Write-Host "  Storage Account: " -NoNewline
            Write-Host $outputs.storageAccountName.value -ForegroundColor Yellow
        }
    }
}

# Main execution
try {
    Write-Header "Member Property Alert Infrastructure Deployment"
    Write-Host "Environment: $Environment" -ForegroundColor Green
    Write-Host "Location: $Location" -ForegroundColor Green
    
    if ($ValidateOnly) {
        Write-Host "Mode: Validation Only" -ForegroundColor Yellow
    }
    elseif ($WhatIf) {
        Write-Host "Mode: What-If Analysis" -ForegroundColor Yellow
    }
    else {
        Write-Host "Mode: Full Deployment" -ForegroundColor Green
    }
    
    # Execute deployment steps
    Test-Prerequisites
    Set-AzureContext
    
    $rgName = Get-ResourceGroupName
    Write-Host "Target Resource Group: $rgName" -ForegroundColor Green
    
    if (-not $ValidateOnly -and -not $WhatIf) {
        New-ResourceGroup -RgName $rgName
    }
    
    Test-BicepTemplate
    $parameters = Get-DeploymentParameters -RgName $rgName
    
    Invoke-DeploymentValidation -RgName $rgName -Parameters $parameters
    
    if ($WhatIf) {
        Invoke-WhatIfAnalysis -RgName $rgName -Parameters $parameters
    }
    elseif (-not $ValidateOnly) {
        $deploymentResult = Invoke-Deployment -RgName $rgName -Parameters $parameters
        Show-DeploymentSummary -DeploymentResult $deploymentResult
    }
    
    Write-Header "Deployment Completed Successfully"
    
    if ($ValidateOnly) {
        Write-Success "Validation completed successfully"
    }
    elseif ($WhatIf) {
        Write-Success "What-If analysis completed successfully"
    }
    else {
        Write-Success "Infrastructure deployment completed successfully"
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Deploy applications using GitHub Actions or manual deployment scripts"
        Write-Host "2. Configure application settings and secrets"
        Write-Host "3. Run health checks to verify deployment"
    }
}
catch {
    Write-Header "Deployment Failed"
    Write-Error $_.Exception.Message
    
    Write-Host ""
    Write-Host "Troubleshooting tips:" -ForegroundColor Yellow
    Write-Host "1. Check Azure CLI authentication: az account show"
    Write-Host "2. Verify subscription permissions"
    Write-Host "3. Review Bicep template syntax: az bicep build --file $BicepTemplate"
    Write-Host "4. Check parameter file format: $ParameterFile"
    Write-Host "5. Review Azure Activity Log for detailed error messages"
    
    exit 1
}
