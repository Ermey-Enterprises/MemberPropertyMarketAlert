# Verbose Logging Implementation for Azure Deployment

## Overview

This document describes the comprehensive verbose logging implementation added to the MemberPropertyAlert deployment pipeline to help diagnose deployment failures and provide detailed insights into the deployment process.

## Implementation Summary

### 1. GitHub Actions Workflow Enhancements

**File**: `.github/workflows/member-property-alert-cd.yml`

#### Enhanced Deployment Context Logging
```bash
echo "🔍 VERBOSE LOGGING ENABLED"
echo "=================================================="

echo "📋 Deployment Configuration:"
echo "  Resource Group: $RG_NAME"
echo "  Environment: $ENV"
echo "  Location: $LOC"
echo "  GitHub Actor: ${{ github.actor }}"
echo "  GitHub Ref: ${{ github.ref }}"
echo "  GitHub SHA: ${{ github.sha }}"
echo "  Workflow Run ID: ${{ github.run_id }}"
echo "  Workflow Run Number: ${{ github.run_number }}"
```

#### Azure CLI Context Information
```bash
echo "🔧 Azure CLI Information:"
az version --output table || echo "Failed to get Azure CLI version"

echo "👤 Azure Account Information:"
az account show --output table || echo "Failed to get account info"

echo "📍 Azure Location Information:"
az account list-locations --query "[?name=='$LOC']" --output table || echo "Failed to get location info"
```

#### Secret Configuration Validation
```bash
echo "🔐 Secret Configuration Check:"
if [ -z "${{ secrets.RENTCAST_API_KEY }}" ]; then
    echo "❌ RENTCAST_API_KEY secret is not configured"
    exit 1
elif [ "${{ secrets.RENTCAST_API_KEY }}" = "placeholder" ]; then
    echo "⚠️ RENTCAST_API_KEY is set to placeholder value"
else
    echo "✅ RENTCAST_API_KEY is configured (length: ${#{{ secrets.RENTCAST_API_KEY }}} characters)"
fi
```

#### Enhanced What-If Analysis Logging
```bash
echo "🔍 Validating deployment with what-if analysis..."
echo "  Command: az deployment group what-if --resource-group $RG_NAME --template-file infra/main.bicep $PARAM_ARGS --result-format FullResourcePayloads"

# Capture what-if output for debugging
WHATIF_OUTPUT_FILE=$(mktemp)
az deployment group what-if \
    --resource-group "$RG_NAME" \
    --template-file infra/main.bicep \
    $PARAM_ARGS \
    --result-format FullResourcePayloads > "$WHATIF_OUTPUT_FILE" 2>&1

WHATIF_EXIT_CODE=$?
echo "  What-if exit code: $WHATIF_EXIT_CODE"

if [ $WHATIF_EXIT_CODE -ne 0 ]; then
    echo "❌ Deployment what-if validation failed with exit code: $WHATIF_EXIT_CODE"
    echo "🔍 What-if error output:"
    cat "$WHATIF_OUTPUT_FILE" || echo "Could not read what-if output"
else
    echo "✅ What-if validation succeeded"
    echo "🔍 What-if analysis preview (first 50 lines):"
    head -50 "$WHATIF_OUTPUT_FILE" || echo "Could not read what-if output"
fi
```

#### Detailed Deployment Execution Logging
```bash
echo "🚀 Executing Azure deployment..."
echo "  Command: az deployment group create --resource-group $RG_NAME --name $DEPLOYMENT_NAME --template-file infra/main.bicep $PARAM_ARGS --no-wait"
echo "  Timestamp: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"

DEPLOY_OUTPUT_FILE=$(mktemp)
az deployment group create \
    --resource-group "$RG_NAME" \
    --name "$DEPLOYMENT_NAME" \
    --template-file infra/main.bicep \
    $PARAM_ARGS \
    --no-wait > "$DEPLOY_OUTPUT_FILE" 2>&1

DEPLOY_INIT_EXIT_CODE=$?
echo "  Deployment initiation exit code: $DEPLOY_INIT_EXIT_CODE"

if [ $DEPLOY_INIT_EXIT_CODE -ne 0 ]; then
    echo "❌ Failed to initiate Azure deployment with exit code: $DEPLOY_INIT_EXIT_CODE"
    echo "🔍 Deployment initiation error output:"
    cat "$DEPLOY_OUTPUT_FILE" || echo "Could not read deployment output"
    exit 1
fi

echo "✅ Deployment initiated successfully"
cat "$DEPLOY_OUTPUT_FILE" || echo "Could not read deployment output"
```

#### Enhanced Deployment Monitoring
```bash
echo "⏳ Waiting for deployment to complete..."
echo "  Command: az deployment group wait --resource-group $RG_NAME --name $DEPLOYMENT_NAME --created --timeout 1800"
echo "  Timeout: 30 minutes (1800 seconds)"
echo "  Start time: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"

WAIT_OUTPUT_FILE=$(mktemp)
az deployment group wait \
    --resource-group "$RG_NAME" \
    --name "$DEPLOYMENT_NAME" \
    --created \
    --timeout 1800 > "$WAIT_OUTPUT_FILE" 2>&1

DEPLOYMENT_EXIT_CODE=$?
echo "  Deployment wait exit code: $DEPLOYMENT_EXIT_CODE"
echo "  End time: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"

if [ $DEPLOYMENT_EXIT_CODE -ne 0 ]; then
    echo "❌ Azure deployment failed with exit code: $DEPLOYMENT_EXIT_CODE"
    echo "🔍 Deployment wait error output:"
    cat "$WAIT_OUTPUT_FILE" || echo "Could not read wait output"
    
    echo "🔍 Checking detailed deployment status..."
    az deployment group show \
        --resource-group "$RG_NAME" \
        --name "$DEPLOYMENT_NAME" \
        --query "properties" \
        --output json || echo "Could not retrieve detailed deployment status"
    
    echo "🔍 Checking deployment operations..."
    az deployment operation group list \
        --resource-group "$RG_NAME" \
        --name "$DEPLOYMENT_NAME" \
        --query "[?properties.provisioningState=='Failed']" \
        --output table || echo "Could not retrieve failed operations"
    
    exit 1
fi
```

### 2. Test Script Enhancements

**File**: `scripts/Test-Deployment.ps1`

#### Enhanced What-If Analysis with Error Capture
```powershell
Write-Host "🔍 Running what-if analysis..." -ForegroundColor Cyan
Write-Host "  Command: az deployment group what-if --resource-group $ResourceGroupName --template-file $BicepFile --parameters $ParamString --result-format FullResourcePayloads" -ForegroundColor Gray

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
```

#### Detailed Error Output Display
```powershell
if ($whatIfExitCode -ne 0) {
    Write-Warning "⚠️ What-if analysis failed with exit code: $whatIfExitCode"
    
    # Display error output if available
    if (Test-Path $whatIfErrorFile) {
        $errorContent = Get-Content $whatIfErrorFile -Raw
        if ($errorContent) {
            Write-Host "🔍 What-if error output:" -ForegroundColor Red
            Write-Host $errorContent -ForegroundColor Red
        }
    }
    
    # Fallback validation with error capture
    $validateErrorFile = [System.IO.Path]::GetTempFileName()
    az deployment group validate `
        --resource-group $ResourceGroupName `
        --template-file $BicepFile `
        --parameters $ParamString `
        --output none 2>$validateErrorFile
    
    $validateExitCode = $LASTEXITCODE
    Write-Host "  Basic validation exit code: $validateExitCode" -ForegroundColor Gray
    
    if ($validateExitCode -ne 0) {
        # Display validation error output
        if (Test-Path $validateErrorFile) {
            $validateErrorContent = Get-Content $validateErrorFile -Raw
            if ($validateErrorContent) {
                Write-Host "🔍 Basic validation error output:" -ForegroundColor Red
                Write-Host $validateErrorContent -ForegroundColor Red
            }
        }
    }
}
```

#### Exception Handling with Stack Traces
```powershell
} catch {
    Write-Error "❌ Deployment validation failed with exception: $_"
    Write-Host "Exception details:" -ForegroundColor Red
    Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  StackTrace: $($_.Exception.StackTrace)" -ForegroundColor Red
    exit 1
}
```

## Key Logging Features

### 1. **Comprehensive Context Information**
- GitHub workflow metadata (actor, ref, SHA, run ID)
- Azure CLI version and account information
- Deployment configuration details
- Timestamp tracking for all operations

### 2. **Command Visibility**
- Full Azure CLI commands being executed
- Exit codes for all operations
- Parameter values (with secret masking)

### 3. **Error Capture and Display**
- Temporary files to capture stderr output
- Full error messages displayed in logs
- Fallback validation with separate error tracking

### 4. **Deployment Operation Tracking**
- What-if analysis results with change summaries
- Deployment initiation confirmation
- Real-time deployment progress monitoring
- Failed operation details from Azure

### 5. **Output Validation**
- JSON validity checking for deployment outputs
- File existence verification
- Content validation before processing

## Benefits for Troubleshooting

### 1. **Immediate Issue Identification**
- Clear error messages with context
- Exit codes for pinpointing failure points
- Command visibility for reproduction

### 2. **Azure-Specific Diagnostics**
- Deployment operation status
- Failed resource details
- Azure CLI connectivity verification

### 3. **Timeline Tracking**
- UTC timestamps for all operations
- Duration tracking for long-running operations
- Progress indicators for deployment phases

### 4. **Local Testing Capabilities**
- Same validation logic as CI/CD pipeline
- Detailed error output for local debugging
- Parameter file validation

## Usage Examples

### 1. **GitHub Actions Deployment**
The enhanced logging will automatically provide detailed output in the GitHub Actions logs:

```
🔍 VERBOSE LOGGING ENABLED
==================================================
📋 Deployment Configuration:
  Resource Group: rg-member-property-alert-dev-eastus2
  Environment: dev
  Location: eastus2
  GitHub Actor: username
  GitHub Ref: refs/heads/main
  GitHub SHA: abc123def456
  Workflow Run ID: 1234567890
  Workflow Run Number: 42

🔧 Azure CLI Information:
Name      Version
--------  ---------
azure-cli 2.55.0

👤 Azure Account Information:
Name                CloudName    SubscriptionId                        State    IsDefault
------------------  -----------  ------------------------------------  -------  -----------
My Subscription     AzureCloud   12345678-1234-1234-1234-123456789012  Enabled  True

🔐 Secret Configuration Check:
✅ RENTCAST_API_KEY is configured (length: 32 characters)
✅ ADMIN_API_KEY is configured (length: 24 characters)
```

### 2. **Local Testing with Test-Deployment.ps1**
```powershell
PS> .\Test-Deployment.ps1 -Environment dev

🧪 Testing MemberPropertyAlert Deployment
Environment: dev
Location: eastus2
Resource Group: rg-member-property-alert-dev-eastus2
✅ Azure CLI version: 2.55.0
✅ Logged in as: user@domain.com
✅ Subscription: My Subscription (12345678-1234-1234-1234-123456789012)
✅ Bicep template found: C:\path\to\infra\main.bicep
🔍 Compiling Bicep template...
✅ Bicep template compiled successfully
📋 Preparing test parameters...
✅ Test parameters prepared
🔍 Running what-if analysis...
  Command: az deployment group what-if --resource-group rg-member-property-alert-dev-eastus2 --template-file C:\path\to\infra\main.bicep --parameters environment=dev location=eastus2 appName=member-property-alert rentCastApiKey=test-api-key-placeholder adminApiKey=test-admin-key-placeholder --result-format FullResourcePayloads
  Executing what-if analysis...
  What-if exit code: 0
✅ What-if analysis completed successfully
📊 What-if analysis results:
  Total changes detected: 12
  + Microsoft.Storage/storageAccounts/stmemberpropertyalertdev123456
  + Microsoft.DocumentDB/databaseAccounts/cosmos-member-property-alert-dev
  + Microsoft.Web/serverfarms/asp-member-property-alert-dev
  + Microsoft.Web/sites/func-member-property-alert-dev
  + Microsoft.Web/sites/web-member-property-alert-dev
  ...
```

## Troubleshooting Guide

### 1. **Response Stream Errors**
If you see "The content for this response was already consumed":
- Check the verbose logs for the exact command that failed
- Look for timing information to identify concurrent operations
- Review the what-if analysis output for validation issues

### 2. **Deployment Validation Failures**
If what-if or validation fails:
- Review the error output captured in temporary files
- Check the Bicep template compilation logs
- Verify parameter values in the verbose output

### 3. **Deployment Timeout Issues**
If deployment times out:
- Check the start/end timestamps in the logs
- Review the deployment operations list for stuck resources
- Look for specific resource provisioning failures

### 4. **Output Extraction Failures**
If deployment outputs cannot be retrieved:
- Check if the deployment actually completed successfully
- Review the JSON validity checks in the logs
- Verify the deployment name and resource group in the verbose output

## Future Enhancements

1. **Structured Logging**: Implement JSON-formatted logs for better parsing
2. **Performance Metrics**: Add timing measurements for each deployment phase
3. **Resource-Specific Logging**: Enhanced logging for individual resource deployments
4. **Integration with Application Insights**: Send deployment metrics to Azure monitoring
5. **Automated Log Analysis**: Scripts to parse and analyze deployment logs for common issues

---

**Status**: ✅ Verbose logging implementation complete
**Next Steps**: Test deployment with enhanced logging and monitor for improved troubleshooting capabilities
