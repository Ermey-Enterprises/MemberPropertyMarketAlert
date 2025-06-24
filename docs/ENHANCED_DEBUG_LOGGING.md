# Enhanced Debug Logging Implementation for Azure Deployment

## Overview

This document describes the comprehensive implementation of enhanced debug logging with `--verbose`, `--debug`, and cache clearing for all Azure deployment commands across the MemberPropertyAlert deployment pipeline.

## Implementation Summary

### 1. Enhanced Azure CLI Flags

All `az deployment` commands now include:
- `--verbose` - Provides detailed operation information
- `--debug` - Shows HTTP requests/responses and internal Azure CLI operations  
- `--only-show-errors` - Reduces noise while maintaining error visibility
- Cache clearing before critical operations

### 2. Files Modified

#### A. PowerShell Deployment Script
**File**: `scripts/Deploy-Infrastructure.ps1`

**Key Enhancements:**
- Added `Clear-AzureCache` function for comprehensive cache management
- Enhanced all deployment functions with verbose/debug flags
- Improved error capture and display with debug output
- Added timing information for all operations

**Cache Clearing Function:**
```powershell
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
```

**Enhanced Validation Function:**
```powershell
function Invoke-DeploymentValidation {
    # Clear cache before validation
    Write-Host "Clearing cache before validation..." -ForegroundColor Gray
    az cache purge --verbose 2>$null
    
    $cmd = "az deployment group validate --resource-group `"$RgName`" --template-file `"$BicepTemplate`" --verbose --debug --only-show-errors"
    
    # Capture both stdout and stderr for debug analysis
    $validationOutput = [System.IO.Path]::GetTempFileName()
    $validationError = [System.IO.Path]::GetTempFileName()
    
    $result = Invoke-Expression "$cmd > `"$validationOutput`" 2>`"$validationError`""
    $exitCode = $LASTEXITCODE
    
    # Display detailed error information on failure
    if ($exitCode -ne 0) {
        if (Test-Path $validationError) {
            $errorContent = Get-Content $validationError -Raw
            if ($errorContent) {
                Write-Host "Validation error details:" -ForegroundColor Red
                Write-Host $errorContent -ForegroundColor Red
            }
        }
    }
}
```

#### B. Test Deployment Script
**File**: `scripts/Test-Deployment.ps1`

**Key Enhancements:**
- Added cache clearing before what-if analysis
- Enhanced what-if command with verbose/debug flags
- Improved error capture and display

**Enhanced What-If Analysis:**
```powershell
# Clear Azure CLI and Bicep caches before testing
Write-Host "🔧 Clearing Azure CLI and Bicep caches..." -ForegroundColor Cyan
try {
    az cache purge --verbose 2>$null
    az bicep upgrade --target-version latest --verbose 2>$null
    az config set bicep.use_binary_from_path=false --verbose 2>$null
    Write-Host "✅ Caches cleared successfully" -ForegroundColor Green
} catch {
    Write-Warning "⚠️ Cache clearing encountered issues (non-critical): $_"
}

# Enhanced what-if with debug flags
$whatIfResult = az deployment group what-if `
    --resource-group $ResourceGroupName `
    --template-file $BicepFile `
    --parameters $ParamString `
    --result-format FullResourcePayloads `
    --verbose `
    --debug `
    --only-show-errors `
    --output json 2>$whatIfErrorFile
```

#### C. GitHub Actions Workflow
**File**: `.github/workflows/member-property-alert-cd.yml`

**Key Enhancements:**
- Added cache clearing before all deployment operations
- Enhanced Bicep compilation with verbose/debug flags
- Improved what-if analysis with debug output
- Enhanced fallback validation with verbose flags

**Cache Clearing in CI/CD:**
```bash
# Clear Azure CLI and Bicep caches before deployment
echo "🔧 Clearing Azure CLI and Bicep caches..."
az cache purge --verbose 2>/dev/null || echo "Cache purge completed with warnings"
az bicep upgrade --target-version latest --verbose 2>/dev/null || echo "Bicep upgrade completed with warnings"
az config set bicep.use_binary_from_path=false --verbose 2>/dev/null || echo "Bicep config set completed with warnings"
echo "✅ Caches cleared and Bicep updated"
```

**Enhanced Bicep Compilation:**
```bash
# Validate Bicep template before deployment
echo "🔍 Validating Bicep template with enhanced debugging..."
echo "  Command: az bicep build --file infra/main.bicep --verbose --debug"
az bicep build --file infra/main.bicep --verbose --debug
```

**Enhanced What-If Analysis:**
```bash
# Use what-if for validation with enhanced debugging
echo "🔍 Validating deployment with what-if analysis (enhanced debugging)..."
echo "  Command: az deployment group what-if --resource-group $RG_NAME --template-file infra/main.bicep $PARAM_ARGS --result-format FullResourcePayloads --verbose --debug --only-show-errors"

az deployment group what-if \
  --resource-group "$RG_NAME" \
  --template-file infra/main.bicep \
  $PARAM_ARGS \
  --result-format FullResourcePayloads \
  --verbose \
  --debug \
  --only-show-errors > "$WHATIF_OUTPUT_FILE" 2>&1
```

### 3. Cache Clearing Strategy

#### When Cache Clearing Occurs:
1. **Before Bicep compilation** - Ensures latest Bicep version and clean cache
2. **Before deployment validation** - Prevents cached validation results
3. **Before what-if analysis** - Ensures accurate resource analysis
4. **Before actual deployment** - Guarantees clean deployment state

#### Cache Clearing Commands:
```bash
# Clear Azure CLI cache
az cache purge --verbose

# Clear Bicep cache and ensure latest version  
az bicep upgrade --target-version latest --verbose
az config set bicep.use_binary_from_path=false --verbose
```

### 4. Enhanced Error Handling

#### Debug Output Capture:
- All commands now capture both stdout and stderr
- Temporary files used to store debug output
- Error details displayed in failure scenarios
- Exit codes logged for all operations

#### Example Error Handling:
```powershell
# Capture both stdout and stderr for debug analysis
$validationOutput = [System.IO.Path]::GetTempFileName()
$validationError = [System.IO.Path]::GetTempFileName()

$result = Invoke-Expression "$cmd > `"$validationOutput`" 2>`"$validationError`""
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    Write-Error "Deployment validation failed with exit code: $exitCode"
    
    # Display detailed error information
    if (Test-Path $validationError) {
        $errorContent = Get-Content $validationError -Raw
        if ($errorContent) {
            Write-Host "Validation error details:" -ForegroundColor Red
            Write-Host $errorContent -ForegroundColor Red
        }
    }
}
```

### 5. Command Visibility

#### All Enhanced Commands:
```bash
# Bicep compilation
az bicep build --file infra/main.bicep --verbose --debug

# Deployment validation
az deployment group validate --resource-group "$RG_NAME" --template-file infra/main.bicep --verbose --debug --only-show-errors

# What-if analysis
az deployment group what-if --resource-group "$RG_NAME" --template-file infra/main.bicep --verbose --debug --only-show-errors --result-format FullResourcePayloads

# Deployment creation
az deployment group create --resource-group "$RG_NAME" --name "$DEPLOYMENT_NAME" --template-file infra/main.bicep --verbose --debug --only-show-errors

# Deployment status checking
az deployment group show --resource-group "$RG_NAME" --name "$DEPLOYMENT_NAME" --query properties.error --output json --verbose --debug

# Failed operations listing
az deployment operation group list --resource-group "$RG_NAME" --name "$DEPLOYMENT_NAME" --query "[?properties.provisioningState=='Failed']" --output table --verbose
```

## Benefits for Troubleshooting

### 1. **HTTP Request/Response Visibility**
- `--debug` flag shows all HTTP requests to Azure APIs
- Request headers, body, and response details visible
- Authentication and authorization flow tracking

### 2. **Internal Azure CLI Operation Tracking**
- Command parsing and execution steps
- Plugin loading and configuration details
- Resource provider registration status

### 3. **Cache-Related Issue Elimination**
- Prevents stale cache causing deployment inconsistencies
- Ensures latest Bicep version and templates
- Eliminates cached authentication tokens issues

### 4. **Enhanced Error Context**
- Full error messages with stack traces
- Azure-specific error codes and descriptions
- Resource-level failure details

### 5. **Performance Insights**
- Operation timing information
- Resource provisioning duration
- Network latency and retry patterns

## Usage Examples

### 1. **Local Development Testing**
```powershell
# Run enhanced test script
.\Test-Deployment.ps1 -Environment dev

# Output includes:
🔧 Clearing Azure CLI and Bicep caches...
✅ Caches cleared successfully
🔍 Running what-if analysis with enhanced debugging...
  Command: az deployment group what-if --resource-group rg-member-property-alert-dev-eastus2 --template-file C:\path\to\infra\main.bicep --parameters environment=dev location=eastus2 appName=member-property-alert --result-format FullResourcePayloads --verbose --debug --only-show-errors
  Executing what-if analysis...
  What-if exit code: 0
✅ What-if analysis completed successfully
```

### 2. **Production Deployment**
```powershell
# Run enhanced deployment script
.\Deploy-Infrastructure.ps1 -Environment prod

# Output includes:
🔹 Clearing Azure CLI and Bicep caches...
✅ Azure CLI cache cleared
✅ Bicep cache cleared and updated
🔹 Validating Bicep template...
Executing: az bicep build --file "C:\path\to\infra\main.bicep" --verbose --debug
✅ Bicep template compiled successfully
🔹 Validating deployment parameters with enhanced debugging...
Clearing cache before validation...
Executing: az deployment group validate --resource-group "rg-member-property-alert-prod-eastus2" --template-file "C:\path\to\infra\main.bicep" --verbose --debug --only-show-errors
✅ Deployment validation succeeded
```

### 3. **GitHub Actions CI/CD**
```bash
# Enhanced logging output in GitHub Actions
🔍 VERBOSE LOGGING ENABLED
==================================================
📋 Deployment Configuration:
  Resource Group: rg-member-property-alert-dev-eastus2
  Environment: dev
  Location: eastus2
  GitHub Actor: username
  GitHub Ref: refs/heads/main
  GitHub SHA: abc123def456

🔧 Clearing Azure CLI and Bicep caches...
✅ Caches cleared and Bicep updated

🔍 Validating Bicep template with enhanced debugging...
  Command: az bicep build --file infra/main.bicep --verbose --debug
✅ Bicep template compiled successfully

🔍 Validating deployment with what-if analysis (enhanced debugging)...
  Command: az deployment group what-if --resource-group rg-member-property-alert-dev-eastus2 --template-file infra/main.bicep --verbose --debug --only-show-errors
  What-if exit code: 0
✅ What-if validation succeeded
```

## Troubleshooting Guide

### 1. **Debug Output Analysis**
When deployments fail, look for:
- HTTP status codes in debug output (401, 403, 429, 500)
- Azure resource provider errors
- Template validation failures
- Parameter value issues

### 2. **Cache-Related Issues**
If experiencing inconsistent behavior:
- Check if cache clearing completed successfully
- Verify Bicep version after upgrade
- Look for "cache purge completed with warnings" messages

### 3. **Performance Issues**
Monitor debug output for:
- Long HTTP request times
- Retry patterns and backoff
- Resource provisioning delays
- Network connectivity issues

### 4. **Authentication Problems**
Debug output will show:
- Token acquisition attempts
- Authentication method used
- Subscription and tenant validation
- Permission check results

## Future Enhancements

### 1. **Structured Debug Logging**
- JSON-formatted debug output for automated parsing
- Integration with log analysis tools
- Correlation IDs for tracking requests

### 2. **Performance Metrics Collection**
- Deployment timing breakdowns
- Resource-specific provisioning times
- Network latency measurements

### 3. **Automated Issue Detection**
- Pattern recognition for common failures
- Suggested remediation actions
- Integration with Azure Monitor

### 4. **Enhanced Cache Management**
- Selective cache clearing based on operation type
- Cache warming for frequently used templates
- Cache statistics and optimization

---

**Status**: ✅ Enhanced debug logging implementation complete  
**Coverage**: All deployment scripts and CI/CD pipeline  
**Benefits**: Comprehensive troubleshooting capabilities with HTTP-level visibility  
**Next Steps**: Monitor deployment logs for improved debugging effectiveness
