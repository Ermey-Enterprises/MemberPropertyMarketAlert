#!/bin/bash

# Get Azure Authentication Values for GitHub Actions CI/CD
# This script retrieves the values you need to configure GitHub repository variables

echo "🔍 Getting Azure Authentication Values for GitHub Actions..."
echo ""

# Check if Azure CLI is installed and logged in
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found. Please install Azure CLI first:"
    echo "   https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo "❌ Please login to Azure CLI first:"
    echo "   az login"
    exit 1
fi

ACCOUNT=$(az account show --output json)
USER_NAME=$(echo $ACCOUNT | jq -r '.user.name')
echo "✅ Azure CLI is logged in as: $USER_NAME"
echo ""

# Get Subscription ID
SUBSCRIPTION_ID=$(echo $ACCOUNT | jq -r '.id')
echo "📋 AZURE_SUBSCRIPTION_ID:"
echo "   $SUBSCRIPTION_ID"
echo ""

# Get Tenant ID
TENANT_ID=$(echo $ACCOUNT | jq -r '.tenantId')
echo "📋 AZURE_TENANT_ID:"
echo "   $TENANT_ID"
echo ""

# Check if App Registration exists
APP_NAME="MemberPropertyAlert-GitHub-Actions"
echo "🔍 Checking for existing App Registration: $APP_NAME..."

APP_LIST=$(az ad app list --display-name "$APP_NAME" --output json)
APP_COUNT=$(echo $APP_LIST | jq length)

if [ "$APP_COUNT" -gt 0 ]; then
    CLIENT_ID=$(echo $APP_LIST | jq -r '.[0].appId')
    echo "✅ Found existing App Registration"
    echo "📋 AZURE_CLIENT_ID:"
    echo "   $CLIENT_ID"
else
    echo "❌ App Registration '$APP_NAME' not found"
    echo "🔧 Creating App Registration..."
    
    # Create App Registration
    NEW_APP=$(az ad app create --display-name "$APP_NAME" --output json)
    CLIENT_ID=$(echo $NEW_APP | jq -r '.appId')
    
    echo "✅ Created App Registration"
    echo "📋 AZURE_CLIENT_ID:"
    echo "   $CLIENT_ID"
    
    # Create Service Principal
    echo "🔧 Creating Service Principal..."
    az ad sp create --id $CLIENT_ID --output none
    echo "✅ Created Service Principal"
    
    # Add Federated Credential
    echo "🔧 Adding Federated Credential for GitHub Actions..."
    FEDERATED_CREDENTIAL='{
        "name": "MemberPropertyAlert-Main-Branch",
        "issuer": "https://token.actions.githubusercontent.com",
        "subject": "repo:Ermey-Enterprises/MemberPropertyMarketAlert:ref:refs/heads/main",
        "audiences": ["api://AzureADTokenExchange"]
    }'
    
    echo $FEDERATED_CREDENTIAL | az ad app federated-credential create --id $CLIENT_ID --parameters '@-' --output none
    echo "✅ Added Federated Credential"
    
    # Assign Contributor role
    echo "🔧 Assigning Contributor role..."
    az role assignment create --assignee $CLIENT_ID --role Contributor --scope "/subscriptions/$SUBSCRIPTION_ID" --output none
    echo "✅ Assigned Contributor role"
fi

echo ""
echo "🎯 GitHub Repository Variables Configuration:"
echo "============================================="
echo ""
echo "Go to: https://github.com/Ermey-Enterprises/MemberPropertyMarketAlert/settings/secrets/actions"
echo ""
echo "Click 'Variables' tab and add these Repository Variables:"
echo ""
echo "Variable Name: AZURE_CLIENT_ID"
echo "Value: $CLIENT_ID"
echo ""
echo "Variable Name: AZURE_TENANT_ID"
echo "Value: $TENANT_ID"
echo ""
echo "Variable Name: AZURE_SUBSCRIPTION_ID"
echo "Value: $SUBSCRIPTION_ID"
echo ""

# Check for Static Web App to get deployment token
echo "🔍 Checking for Static Web App deployment token..."
RESOURCE_GROUP="memberpropertyalert-eastus2-rg"

STATIC_WEB_APPS=$(az staticwebapp list --resource-group $RESOURCE_GROUP --output json 2>/dev/null)
if [ $? -eq 0 ] && [ "$(echo $STATIC_WEB_APPS | jq length)" -gt 0 ]; then
    STATIC_WEB_APP_NAME=$(echo $STATIC_WEB_APPS | jq -r '.[0].name')
    echo "✅ Found Static Web App: $STATIC_WEB_APP_NAME"
    
    DEPLOYMENT_TOKEN=$(az staticwebapp secrets list --name $STATIC_WEB_APP_NAME --resource-group $RESOURCE_GROUP --query "properties.apiKey" -o tsv 2>/dev/null)
    if [ ! -z "$DEPLOYMENT_TOKEN" ]; then
        echo ""
        echo "Click 'Secrets' tab and add this Repository Secret:"
        echo ""
        echo "Secret Name: AZURE_STATIC_WEB_APPS_API_TOKEN"
        echo "Value: $DEPLOYMENT_TOKEN"
    else
        echo "⚠️  Could not retrieve Static Web App deployment token"
        echo "   You'll need to deploy infrastructure first, then get the token manually"
    fi
else
    echo "⚠️  Static Web App not found in resource group: $RESOURCE_GROUP"
    echo "   You'll need to deploy infrastructure first to get the deployment token"
    echo ""
    echo "After infrastructure deployment, run this command to get the token:"
    echo "az staticwebapp secrets list --name [STATIC_WEB_APP_NAME] --resource-group $RESOURCE_GROUP --query \"properties.apiKey\" -o tsv"
fi

echo ""
echo "🚀 Next Steps:"
echo "1. Copy the values above into GitHub repository variables/secrets"
echo "2. Push a commit or manually trigger the CI/CD workflow"
echo "3. Watch the 4-stage pipeline complete successfully!"
echo ""
echo "✅ Script completed successfully!"
