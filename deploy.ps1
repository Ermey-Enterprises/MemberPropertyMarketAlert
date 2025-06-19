#!/usr/bin/env pwsh
# Member Property Market Alert - Azure Deployment Script
# Run this script to deploy the service to Azure

Write-Host "🚀 Member Property Market Alert - Azure Deployment" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# Check if Azure CLI is installed
if (!(Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Azure CLI not found. Please install Azure CLI first:" -ForegroundColor Red
    Write-Host "   https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
    exit 1
}

# Login to Azure
Write-Host "🔐 Logging into Azure..." -ForegroundColor Cyan
az login

# Set variables with proper naming for Member Property Market Alert
$resourceGroup = "MemberPropertyMarketAlert-rg"
$location = "East US 2"
$randomSuffix = Get-Random -Minimum 1000 -Maximum 9999
$functionAppName = "func-memberpropertymarketalert-$randomSuffix"
$storageAccount = "stmemberpropertyalert$randomSuffix"
$cosmosAccount = "cosmos-memberpropertymarketalert-$randomSuffix"
$serviceBusNamespace = "sb-memberpropertymarketalert-$randomSuffix"

Write-Host "📋 Deployment Configuration:" -ForegroundColor Yellow
Write-Host "   Resource Group: $resourceGroup" -ForegroundColor White
Write-Host "   Location: $location" -ForegroundColor White
Write-Host "   Function App: $functionAppName" -ForegroundColor White
Write-Host "   Storage Account: $storageAccount" -ForegroundColor White
Write-Host "   Cosmos DB: $cosmosAccount" -ForegroundColor White
Write-Host "   Service Bus: $serviceBusNamespace" -ForegroundColor White

# Create resource group
Write-Host "🏗️  Creating resource group..." -ForegroundColor Cyan
& az group create --name "$resourceGroup" --location "$location"

# Create storage account
Write-Host "💾 Creating storage account..." -ForegroundColor Cyan
& az storage account create --name "$storageAccount" --resource-group "$resourceGroup" --location "$location" --sku Standard_LRS

# Create Cosmos DB account
Write-Host "🌍 Creating Cosmos DB account..." -ForegroundColor Cyan
& az cosmosdb create --name "$cosmosAccount" --resource-group "$resourceGroup" --location "$location" --kind GlobalDocumentDB

# Create Cosmos DB database
Write-Host "📊 Creating Cosmos DB database..." -ForegroundColor Cyan
& az cosmosdb sql database create --account-name "$cosmosAccount" --resource-group "$resourceGroup" --name "MemberPropertyMarketAlert"

# Create Cosmos DB containers
Write-Host "📦 Creating Cosmos DB containers..." -ForegroundColor Cyan
& az cosmosdb sql container create --account-name "$cosmosAccount" --resource-group "$resourceGroup" --database-name "MemberPropertyMarketAlert" --name "MemberAddresses" --partition-key-path "/institutionId"
& az cosmosdb sql container create --account-name "$cosmosAccount" --resource-group "$resourceGroup" --database-name "MemberPropertyMarketAlert" --name "PropertyListings" --partition-key-path "/state"
& az cosmosdb sql container create --account-name "$cosmosAccount" --resource-group "$resourceGroup" --database-name "MemberPropertyMarketAlert" --name "PropertyMatches" --partition-key-path "/institutionId"

# Create Service Bus namespace
Write-Host "🚌 Creating Service Bus namespace..." -ForegroundColor Cyan
& az servicebus namespace create --name "$serviceBusNamespace" --resource-group "$resourceGroup" --location "$location" --sku Standard

# Create Service Bus queue
Write-Host "📬 Creating Service Bus queue..." -ForegroundColor Cyan
& az servicebus queue create --name "property-matches" --namespace-name "$serviceBusNamespace" --resource-group "$resourceGroup"

# Create Function App
Write-Host "⚡ Creating Function App..." -ForegroundColor Cyan
& az functionapp create --name "$functionAppName" --resource-group "$resourceGroup" --storage-account "$storageAccount" --consumption-plan-location "$location" --runtime dotnet --functions-version 4

# Get connection strings
Write-Host "🔗 Getting connection strings..." -ForegroundColor Cyan
$cosmosConnectionString = & az cosmosdb keys list --name "$cosmosAccount" --resource-group "$resourceGroup" --type connection-strings --query "connectionStrings[0].connectionString" --output tsv
$serviceBusConnectionString = & az servicebus namespace authorization-rule keys list --name RootManageSharedAccessKey --namespace-name "$serviceBusNamespace" --resource-group "$resourceGroup" --query primaryConnectionString --output tsv

# Configure Function App settings
Write-Host "⚙️  Configuring Function App settings..." -ForegroundColor Cyan
& az functionapp config appsettings set --name "$functionAppName" --resource-group "$resourceGroup" --settings "CosmosDb:ConnectionString=$cosmosConnectionString" "CosmosDb:DatabaseName=MemberPropertyMarketAlert" "ServiceBusConnection=$serviceBusConnectionString" "RealEstate:RentSpreeApiKey=" "RealEstate:ZillowApiKey=" "Notifications:SendGridApiKey=" "Notifications:DefaultFromEmail=noreply@memberpropertymarketalert.com"

# Build and deploy
Write-Host "🔨 Building and deploying Function App..." -ForegroundColor Cyan
& dotnet build src/MemberPropertyMarketAlert.Functions/MemberPropertyMarketAlert.Functions.csproj --configuration Release
& func azure functionapp publish "$functionAppName" --csharp

Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
Write-Host "🌐 Function App URL: https://$functionAppName.azurewebsites.net" -ForegroundColor Yellow
Write-Host "📊 Cosmos DB Account: $cosmosAccount" -ForegroundColor Yellow
Write-Host "🚌 Service Bus Namespace: $serviceBusNamespace" -ForegroundColor Yellow

Write-Host ""
Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Add your real estate API keys to the Function App settings" -ForegroundColor White
Write-Host "2. Configure SendGrid for email notifications" -ForegroundColor White
Write-Host "3. Test the API endpoints" -ForegroundColor White
Write-Host "4. Deploy the web interface if needed" -ForegroundColor White

Write-Host ""
Write-Host "🚀 Your Member Property Market Alert service is now live!" -ForegroundColor Green
