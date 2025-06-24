# Storage Permissions Fix

## Issue
The Azure Functions deployment was failing with a 403 Forbidden error when trying to access the Azure Storage account during deployment. This was happening during the file deployment stage of the CI/CD pipeline.

## Root Cause
The GitHub Actions service principal was missing the "Storage File Data SMB Share Contributor" role on the storage account, which is required for file operations on Azure Storage from Azure Functions.

## Solution
Assigned the "Storage File Data SMB Share Contributor" role to the GitHub Actions service principal for the storage account:

```bash
az role assignment create \
  --assignee "0fc4ac8e-c0ad-4f6f-bb8b-a98420219372" \
  --role "Storage File Data SMB Share Contributor" \
  --scope "/subscriptions/{subscription-id}/resourceGroups/rg-member-property-alert-dev-eastus2/providers/Microsoft.Storage/storageAccounts/stmpadeveus26ih6"
```

## Verification
- Role assignment created successfully on 2025-06-24
- Service principal now has both Owner permissions (for resource management) and Storage File Data SMB Share Contributor (for file operations)

## Next Steps
- Re-run deployment to verify the fix
- Monitor future deployments for any remaining permission issues
