#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test script for validating MemberPropertyAlert deployment locally
.DESCRIPTION
    This script validates the Bicep template and parameters locally before deployment
    to catch issues early and avoid deployment failures.
.PARAMETER Environment
    The environment to test (dev, test, prod)
.PARAMETER ResourceGroupName
    Optional resource group name override
.EXAMPLE
    ./Test-Deployment.ps1 -Environment dev
.EXAMPLE
    ./Test-Deployment.ps1 -Environment test -ResourceGroupName "my-custom-rg"
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment,
    
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "eastus2"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "🧪 Testing MemberPropertyAlert Deployment" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Location: $Location" -ForegroundColor Yellow

# Set default resource group name if not provided
if (-not $ResourceGroupName) {
    $ResourceGroupName = "rg-member-property-alert-$Environment-$Location"
}
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Yellow

# Check if Azure CLI is installed
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "✅ Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Error "❌ Azure CLI is not installed or not in PATH. Please install Azure CLI first."
    exit 1
}

# Check if user is logged in to Azure
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "✅ Logged in as: $($account.user.name)" -ForegroundColor Green
    Write-Host "✅ Subscription: $($account.name) ($($account.id))" -ForegroundColor Green
} catch {
    Write-Error "❌ Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Validate Bicep template exists
$BicepFile = Join-Path $ProjectRoot "infra" "main.bicep"
if (-not (Test-Path $BicepFile)) {
    Write-Error "❌ Bicep template not found: $BicepFile"
    exit 1
}
Write-Host "✅ Bicep template found: $BicepFile" -ForegroundColor Green

# Test Bicep compilation
Write-Host "🔍 Compiling Bicep template..." -ForegroundColor Cyan
try {
    az bicep build --file $BicepFile
    Write-Host "✅ Bicep template compiled successfully" -ForegroundColor Green
} catch {
    Write-Error "❌ Bicep template compilation failed: $_"
    exit 1
}

# Prepare test parameters
Write-Host "📋 Preparing test parameters..." -ForegroundColor Cyan
$TestParams = @{
    environment = $Environment
    location = $Location
    appName = "member-property-alert"
    rentCastApiKey = "test-api-key-placeholder"
    adminApiKey = "test-admin-key-placeholder"
}

# Create parameter string for Azure CLI
$ParamString = ""
foreach ($param in $TestParams.GetEnumerator()) {
    $ParamString += "$($param.Key)=$($param.Value) "
}
$ParamString = $ParamString.Trim()

Write-Host "✅ Test parameters prepared" -ForegroundColor Green

# Test what-if deployment (this is what the CI/CD pipeline will do)
Write-Host "🔍 Running what-if analysis..." -ForegroundColor Cyan
Write-Host "  Command: az deployment group what-if --resource-group $ResourceGroupName --template-file $BicepFile --parameters $ParamString --result-format FullResourcePayloads" -ForegroundColor Gray

try {
    # Create temporary files for capturing output
    $whatIfOutputFile = [System.IO.Path]::GetTempFileName()
    $whatIfErrorFile = [System.IO.Path]::GetTempFileName()
    
    Write-Host "  Executing what-if analysis..." -ForegroundColor Gray
    $whatIfResult = az deployment group what-if `
        --resource-group $ResourceGroupName `
        --template-file $BicepFile `
        --parameters $ParamString `
        --result-format FullResourcePayloads `
        --output json 2>$whatIfErrorFile
    
    $whatIfExitCode = $LASTEXITCODE
    Write-Host "  What-if exit code: $whatIfExitCode" -ForegroundColor Gray
    
    if ($whatIfExitCode -eq 0) {
        Write-Host "✅ What-if analysis completed successfully" -ForegroundColor Green
        
        # Parse and display what-if results
        if ($whatIfResult) {
            try {
                $whatIfData = $whatIfResult | ConvertFrom-Json
                if ($whatIfData.changes) {
                    Write-Host "📊 What-if analysis results:" -ForegroundColor Cyan
                    $changeCount = $whatIfData.changes.Count
                    Write-Host "  Total changes detected: $changeCount" -ForegroundColor Gray
                    
                    foreach ($change in $whatIfData.changes) {
                        $changeType = $change.changeType
                        $resourceName = $change.resourceId -split '/' | Select-Object -Last 1
                        $resourceType = $change.resourceType
                        
                        switch ($changeType) {
                            "Create" { Write-Host "  + $resourceType/$resourceName" -ForegroundColor Green }
                            "Modify" { Write-Host "  ~ $resourceType/$resourceName" -ForegroundColor Yellow }
                            "Delete" { Write-Host "  - $resourceType/$resourceName" -ForegroundColor Red }
                            default { Write-Host "  ? $resourceType/$resourceName ($changeType)" -ForegroundColor Gray }
                        }
                    }
                } else {
                    Write-Host "  No changes detected" -ForegroundColor Gray
                }
            } catch {
                Write-Warning "⚠️ Could not parse what-if results as JSON: $_"
                Write-Host "Raw what-if output:" -ForegroundColor Gray
                Write-Host $whatIfResult -ForegroundColor Gray
            }
        } else {
            Write-Host "  No what-if output received" -ForegroundColor Gray
        }
    } else {
        Write-Warning "⚠️ What-if analysis failed with exit code: $whatIfExitCode"
        
        # Display error output if available
        if (Test-Path $whatIfErrorFile) {
            $errorContent = Get-Content $whatIfErrorFile -Raw
            if ($errorContent) {
                Write-Host "🔍 What-if error output:" -ForegroundColor Red
                Write-Host $errorContent -ForegroundColor Red
            }
        }
        
        Write-Host "🔍 Trying basic validation as fallback..." -ForegroundColor Cyan
        
        # Fallback to basic validation
        $validateErrorFile = [System.IO.Path]::GetTempFileName()
        az deployment group validate `
            --resource-group $ResourceGroupName `
            --template-file $BicepFile `
            --parameters $ParamString `
            --output none 2>$validateErrorFile
        
        $validateExitCode = $LASTEXITCODE
        Write-Host "  Basic validation exit code: $validateExitCode" -ForegroundColor Gray
        
        if ($validateExitCode -eq 0) {
            Write-Host "✅ Basic validation completed successfully" -ForegroundColor Green
        } else {
            Write-Error "❌ Both what-if and basic validation failed"
            
            # Display validation error output
            if (Test-Path $validateErrorFile) {
                $validateErrorContent = Get-Content $validateErrorFile -Raw
                if ($validateErrorContent) {
                    Write-Host "🔍 Basic validation error output:" -ForegroundColor Red
                    Write-Host $validateErrorContent -ForegroundColor Red
                }
            }
            
            # Clean up temp files
            Remove-Item $whatIfErrorFile -ErrorAction SilentlyContinue
            Remove-Item $validateErrorFile -ErrorAction SilentlyContinue
            exit 1
        }
        
        # Clean up temp files
        Remove-Item $validateErrorFile -ErrorAction SilentlyContinue
    }
    
    # Clean up temp files
    Remove-Item $whatIfErrorFile -ErrorAction SilentlyContinue
    
} catch {
    Write-Error "❌ Deployment validation failed with exception: $_"
    Write-Host "Exception details:" -ForegroundColor Red
    Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  StackTrace: $($_.Exception.StackTrace)" -ForegroundColor Red
    exit 1
}

# Test parameter file validation (if exists)
$ParamFile = Join-Path $ProjectRoot "infra" "main.$Environment.parameters.json"
if (Test-Path $ParamFile) {
    Write-Host "🔍 Validating parameter file: $ParamFile" -ForegroundColor Cyan
    try {
        $paramContent = Get-Content $ParamFile -Raw | ConvertFrom-Json
        Write-Host "✅ Parameter file is valid JSON" -ForegroundColor Green
        
        # Check for required parameters
        $requiredParams = @("environment", "location", "appName")
        foreach ($param in $requiredParams) {
            if ($paramContent.parameters.$param) {
                Write-Host "  ✅ $param parameter found" -ForegroundColor Green
            } else {
                Write-Warning "  ⚠️ $param parameter not found in parameter file"
            }
        }
    } catch {
        Write-Error "❌ Parameter file validation failed: $_"
        exit 1
    }
} else {
    Write-Host "ℹ️ No parameter file found for environment: $Environment" -ForegroundColor Gray
}

# Summary
Write-Host ""
Write-Host "🎉 Deployment validation completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Summary:" -ForegroundColor Cyan
Write-Host "  ✅ Azure CLI is available and logged in" -ForegroundColor Green
Write-Host "  ✅ Bicep template compiles without errors" -ForegroundColor Green
Write-Host "  ✅ Deployment validation passes" -ForegroundColor Green
Write-Host "  ✅ Template is ready for deployment" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 To deploy this configuration:" -ForegroundColor Yellow
Write-Host "  1. Ensure GitHub secrets are configured (RENTCAST_API_KEY, ADMIN_API_KEY)" -ForegroundColor Yellow
Write-Host "  2. Push changes to trigger automatic deployment" -ForegroundColor Yellow
Write-Host "  3. Or run manual deployment: gh workflow run member-property-alert-cd.yml -f environment=$Environment" -ForegroundColor Yellow
Write-Host ""
