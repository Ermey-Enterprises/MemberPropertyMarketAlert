# Admin Commands for Service Principal Creation

Dear Azure AD Administrator,

The user `michaelrstewart1@gmail.com` needs a service principal created for CI/CD deployment of the MemberPropertyAlert Azure Function. They have Owner rights on the subscription but lack Azure AD permissions to create service principals.

Please run these commands:

## Create Service Principal

```powershell
# Create service principal with Contributor role scoped to resource group
az ad sp create-for-rbac --name "MemberPropertyAlert-CI" \
  --role "Contributor" \
  --scopes "/subscriptions/f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6/resourceGroups/rg-member-property-alert-dev" \
  --json-auth

# Alternative: Create with subscription scope
az ad sp create-for-rbac --name "MemberPropertyAlert-CI" \
  --role "Contributor" \
  --scopes "/subscriptions/f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6" \
  --json-auth
```

## Expected Output

The command will return JSON like this:
```json
{
  "clientId": "12345678-1234-1234-1234-123456789012",
  "clientSecret": "your-secret-here",
  "subscriptionId": "f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6",
  "tenantId": "e788f8d8-5971-4072-8cd3-06f6957b71f9",
  "activeDirectoryEndpointUrl": "https://login.microsoftonline.com",
  "resourceManagerEndpointUrl": "https://management.azure.com/",
  "activeDirectoryGraphResourceId": "https://graph.windows.net/",
  "sqlManagementEndpointUrl": "https://management.core.windows.net:8443/",
  "galleryEndpointUrl": "https://gallery.azure.com/",
  "managementEndpointUrl": "https://management.core.windows.net/"
}
```

## Alternative: Grant User Azure AD Permissions

Instead of creating the service principal yourself, you could grant the user the necessary Azure AD role:

```powershell
# Grant Application Administrator role
az role assignment create \
  --assignee "michaelrstewart1@gmail.com" \
  --role "Application Administrator" \
  --scope "/subscriptions/f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6"
```

This would allow them to create service principals themselves in the future.

## Security Note

The service principal will have Contributor access to the specified resource group, allowing it to:
- Deploy and manage Azure Functions
- Read and write to Azure resources in the resource group
- Cannot create or delete the resource group itself
- Cannot access other resource groups

This follows the principle of least privilege for CI/CD operations.
