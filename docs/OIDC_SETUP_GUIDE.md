# OIDC Authentication Setup Guide

## Overview
OpenID Connect (OIDC) allows GitHub Actions to authenticate with Azure without storing credentials. This is the modern, secure approach that doesn't require creating service principals.

## Prerequisites
- Azure subscription with resources deployed
- GitHub repository
- Azure AD tenant access (you already have this)

## Step 1: Register Application in Azure AD

```powershell
# Create an Azure AD application
az ad app create --display-name "MemberPropertyAlert-OIDC" --query "{appId: appId, objectId: id}"
```

## Step 2: Create Service Principal

```powershell
# Create service principal from the app
$appId = "YOUR_APP_ID_FROM_STEP_1"
az ad sp create --id $appId
```

## Step 3: Configure Federated Credentials

```powershell
# Create federated credential for main branch
az ad app federated-credential create --id $appId --parameters '{
    "name": "main-branch",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:YOUR_GITHUB_USERNAME/MemberPropertyMarketAlert:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
}'

# Create federated credential for pull requests
az ad app federated-credential create --id $appId --parameters '{
    "name": "pull-requests",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:YOUR_GITHUB_USERNAME/MemberPropertyMarketAlert:pull_request",
    "audiences": ["api://AzureADTokenExchange"]
}'
```

## Step 4: Assign Azure Roles

```powershell
# Assign Contributor role to the service principal
az role assignment create --assignee $appId --role "Contributor" --scope "/subscriptions/f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6/resourceGroups/rg-member-property-alert-dev"
```

## Step 5: Configure GitHub Secrets

Add these secrets to your GitHub repository:
- `AZURE_CLIENT_ID`: The App ID from step 1
- `AZURE_TENANT_ID`: e788f8d8-5971-4072-8cd3-06f6957b71f9
- `AZURE_SUBSCRIPTION_ID`: f70bb2e4-e7aa-49ac-b0b2-3fded27fbba6

## Step 6: Update GitHub Actions Workflow

```yaml
name: Deploy to Azure Functions

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

permissions:
  id-token: write
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    
    - name: Azure Login
      uses: azure/login@v1
      with:
        client-id: ${{ secrets.AZURE_CLIENT_ID }}
        tenant-id: ${{ secrets.AZURE_TENANT_ID }}
        subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Build and publish
      run: |
        dotnet publish src/MemberPropertyAlert.Functions/MemberPropertyAlert.Functions.csproj \
          --configuration Release \
          --output ./publish
    
    - name: Deploy to Azure Functions
      uses: Azure/functions-action@v1
      with:
        app-name: 'func-member-property-alert-dev'
        package: './publish'
```

## Troubleshooting

If OIDC setup fails due to permissions, you have the same Azure AD permission issue. In that case, ask your admin to:

1. Create the app registration for you
2. Configure the federated credentials
3. Assign the necessary roles

## Benefits of OIDC
- No stored credentials in GitHub
- Automatic token rotation
- More secure than service principals
- Better audit trail
