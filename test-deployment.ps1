#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test the Bicep deployment locally

.DESCRIPTION
    This script tests the Bicep deployment without actually creating resources.
    It validates the template and shows what would be deployed.

.PARAMETER Environment
    The target environment (dev, test, prod)

.EXAMPLE
    .\test-deployment.ps1 -Environment dev
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'test', 'prod')]
    [string]$Environment
)

# Set error action preference
$ErrorActionPreference = 'Stop'

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White'
    )
    Write-Host $Message -ForegroundColor $Color
}

try {
    Write-ColorOutput "=== Testing Member Property Market Alert Deployment ===" Cyan
    Write-ColorOutput "Environment: $Environment" White
    Write-ColorOutput "" White
    
    # Check if Azure CLI is available
    if (!(Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI not found. Please install Azure CLI first."
    }
    
    # Check if logged in
    $account = az account show --output json 2>$null | ConvertFrom-Json
    if (!$account) {
        throw "Not logged in to Azure CLI. Please run 'az login' first."
    }
    
    Write-ColorOutput "✓ Logged in as $($account.user.name)" Green
    
    # Prepare file paths
    $templateFile = Join-Path $PSScriptRoot "infra\main.bicep"
    $parametersFile = Join-Path $PSScriptRoot "infra\main.$Environment.parameters.json"
    
    # Verify files exist
    if (!(Test-Path $templateFile)) {
        throw "Template file not found: $templateFile"
    }
    
    if (!(Test-Path $parametersFile)) {
        throw "Parameters file not found: $parametersFile"
    }
    
    Write-ColorOutput "✓ Template and parameter files found" Green
    
    # Create temporary resource group for validation
    $tempRgName = "temp-validation-rg-$(Get-Random)"
    Write-ColorOutput "Creating temporary resource group for validation: $tempRgName" Yellow
    
    az group create --name $tempRgName --location "East US" --output none
    
    try {
        # Validate the template
        Write-ColorOutput "Validating Bicep template..." Yellow
        
        $validation = az deployment group validate `
            --resource-group $tempRgName `
            --template-file $templateFile `
            --parameters $parametersFile `
            --output json | ConvertFrom-Json
        
        if ($LASTEXITCODE -ne 0) {
            throw "Template validation failed"
        }
        
        Write-ColorOutput "✓ Template validation passed" Green
        
        # Run what-if to show what would be deployed
        Write-ColorOutput "" White
        Write-ColorOutput "Running what-if analysis..." Yellow
        Write-ColorOutput "This shows what resources would be created/modified:" Yellow
        Write-ColorOutput "" White
        
        az deployment group what-if `
            --resource-group $tempRgName `
            --template-file $templateFile `
            --parameters $parametersFile
        
        if ($LASTEXITCODE -ne 0) {
            throw "What-if analysis failed"
        }
        
    } finally {
        # Clean up temporary resource group
        Write-ColorOutput "" White
        Write-ColorOutput "Cleaning up temporary resource group..." Yellow
        az group delete --name $tempRgName --yes --no-wait --output none
    }
    
    Write-ColorOutput "" White
    Write-ColorOutput "✓ Deployment test completed successfully!" Green
    Write-ColorOutput "" White
    Write-ColorOutput "=== Summary ===" Cyan
    Write-ColorOutput "- Template validation: ✓ Passed" Green
    Write-ColorOutput "- What-if analysis: ✓ Completed" Green
    Write-ColorOutput "- Ready for deployment to $Environment environment" Green
    Write-ColorOutput "" White
    Write-ColorOutput "To deploy for real, run:" Yellow
    Write-ColorOutput "  .\deploy-bicep.ps1 -Environment $Environment" Yellow
    
} catch {
    Write-ColorOutput "" White
    Write-ColorOutput "✗ Test failed: $($_.Exception.Message)" Red
    exit 1
}
