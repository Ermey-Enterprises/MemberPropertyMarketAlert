#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Deploy Member Property Market Alert to Azure using Bicep

.DESCRIPTION
    This script deploys the Member Property Market Alert application to Azure using Bicep templates.
    It creates the resource group and deploys all necessary Azure resources.

.PARAMETER Environment
    The target environment (dev, test, prod)

.PARAMETER Location
    The Azure region for deployment

.PARAMETER ResourceGroupName
    The name of the resource group (defaults to MemberPropertyMarketAlert-rg)

.PARAMETER SubscriptionId
    The Azure subscription ID (optional)

.PARAMETER WhatIf
    Preview the deployment without making changes

.EXAMPLE
    .\deploy-bicep.ps1 -Environment dev
    
.EXAMPLE
    .\deploy-bicep.ps1 -Environment prod -Location "West US 2" -WhatIf
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'test', 'prod')]
    [string]$Environment,
    
    [Parameter(Mandatory = $false)]
    [string]$Location = 'East US',
    
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = 'MemberPropertyMarketAlert-rg',
    
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

# Set error action preference
$ErrorActionPreference = 'Stop'

# Function to write colored output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White'
    )
    Write-Host $Message -ForegroundColor $Color
}

# Function to check if Azure CLI is installed and logged in
function Test-AzureCLI {
    try {
        $azVersion = az version --output json | ConvertFrom-Json
        Write-ColorOutput "✓ Azure CLI version $($azVersion.'azure-cli') detected" Green
        
        $account = az account show --output json 2>$null | ConvertFrom-Json
        if ($account) {
            Write-ColorOutput "✓ Logged in as $($account.user.name) on subscription $($account.name)" Green
            return $true
        } else {
            Write-ColorOutput "✗ Not logged in to Azure CLI" Red
            return $false
        }
    } catch {
        Write-ColorOutput "✗ Azure CLI not found. Please install Azure CLI first." Red
        return $false
    }
}

# Function to set subscription
function Set-AzureSubscription {
    param([string]$SubscriptionId)
    
    if ($SubscriptionId) {
        Write-ColorOutput "Setting subscription to $SubscriptionId..." Yellow
        az account set --subscription $SubscriptionId
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to set subscription"
        }
    }
}

# Function to create or update resource group
function New-ResourceGroup {
    param(
        [string]$Name,
        [string]$Location
    )
    
    Write-ColorOutput "Creating/updating resource group '$Name' in '$Location'..." Yellow
    
    $rg = az group create --name $Name --location $Location --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create resource group"
    }
    
    Write-ColorOutput "✓ Resource group '$Name' ready" Green
    return $rg
}

# Function to validate Bicep template
function Test-BicepTemplate {
    param(
        [string]$TemplateFile,
        [string]$ParametersFile,
        [string]$ResourceGroupName
    )
    
    Write-ColorOutput "Validating Bicep template..." Yellow
    
    $validation = az deployment group validate `
        --resource-group $ResourceGroupName `
        --template-file $TemplateFile `
        --parameters $ParametersFile `
        --output json | ConvertFrom-Json
    
    if ($LASTEXITCODE -ne 0) {
        throw "Template validation failed"
    }
    
    Write-ColorOutput "✓ Template validation passed" Green
    return $validation
}

# Function to deploy Bicep template
function Deploy-BicepTemplate {
    param(
        [string]$TemplateFile,
        [string]$ParametersFile,
        [string]$ResourceGroupName,
        [bool]$WhatIf = $false
    )
    
    $deploymentName = "memberpropertyalert-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    
    if ($WhatIf) {
        Write-ColorOutput "Running what-if deployment..." Yellow
        az deployment group what-if `
            --resource-group $ResourceGroupName `
            --template-file $TemplateFile `
            --parameters $ParametersFile `
            --name $deploymentName
    } else {
        Write-ColorOutput "Deploying infrastructure..." Yellow
        $deployment = az deployment group create `
            --resource-group $ResourceGroupName `
            --template-file $TemplateFile `
            --parameters $ParametersFile `
            --name $deploymentName `
            --output json | ConvertFrom-Json
        
        if ($LASTEXITCODE -ne 0) {
            throw "Deployment failed"
        }
        
        Write-ColorOutput "✓ Infrastructure deployment completed" Green
        return $deployment
    }
}

# Main execution
try {
    Write-ColorOutput "=== Member Property Market Alert Deployment ===" Cyan
    Write-ColorOutput "Environment: $Environment" White
    Write-ColorOutput "Location: $Location" White
    Write-ColorOutput "Resource Group: $ResourceGroupName" White
    
    if ($WhatIf) {
        Write-ColorOutput "Mode: What-If (preview only)" Yellow
    }
    
    Write-ColorOutput "" White
    
    # Check prerequisites
    Write-ColorOutput "Checking prerequisites..." Yellow
    if (-not (Test-AzureCLI)) {
        Write-ColorOutput "Please run 'az login' to authenticate with Azure" Red
        exit 1
    }
    
    # Set subscription if provided
    if ($SubscriptionId) {
        Set-AzureSubscription -SubscriptionId $SubscriptionId
    }
    
    # Prepare file paths
    $templateFile = Join-Path $PSScriptRoot "infra\main.bicep"
    $parametersFile = Join-Path $PSScriptRoot "infra\main.$Environment.parameters.json"
    
    # Verify files exist
    if (-not (Test-Path $templateFile)) {
        throw "Template file not found: $templateFile"
    }
    
    if (-not (Test-Path $parametersFile)) {
        throw "Parameters file not found: $parametersFile"
    }
    
    # Create resource group (only if not what-if)
    if (-not $WhatIf) {
        New-ResourceGroup -Name $ResourceGroupName -Location $Location
    }
    
    # Validate template
    Test-BicepTemplate -TemplateFile $templateFile -ParametersFile $parametersFile -ResourceGroupName $ResourceGroupName
    
    # Deploy template
    $deployment = Deploy-BicepTemplate -TemplateFile $templateFile -ParametersFile $parametersFile -ResourceGroupName $ResourceGroupName -WhatIf $WhatIf
    
    # Display outputs (only for actual deployments)
    if (-not $WhatIf -and $deployment) {
        Write-ColorOutput "" White
        Write-ColorOutput "=== Deployment Outputs ===" Cyan
        
        $outputs = $deployment.properties.outputs
        foreach ($output in $outputs.PSObject.Properties) {
            Write-ColorOutput "$($output.Name): $($output.Value.value)" Green
        }
        
        Write-ColorOutput "" White
        Write-ColorOutput "=== Next Steps ===" Cyan
        Write-ColorOutput "1. Configure application settings in Key Vault" Yellow
        Write-ColorOutput "2. Deploy application code using CI/CD pipeline" Yellow
        Write-ColorOutput "3. Test the deployment" Yellow
        Write-ColorOutput "4. Configure monitoring and alerts" Yellow
    }
    
    if ($WhatIf) {
        Write-ColorOutput "" White
        Write-ColorOutput "✓ What-if preview completed successfully" Green
        Write-ColorOutput "Run without -WhatIf to execute the deployment" Yellow
    } else {
        Write-ColorOutput "" White
        Write-ColorOutput "✓ Deployment completed successfully!" Green
    }
    
} catch {
    Write-ColorOutput "" White
    Write-ColorOutput "✗ Deployment failed: $($_.Exception.Message)" Red
    Write-ColorOutput "Check the error details above for more information" Yellow
    exit 1
}
