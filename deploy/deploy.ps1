#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Deploys the Member Property Market Alert service to Azure
.DESCRIPTION
    This script deploys the complete Member Property Market Alert service infrastructure and application to Azure.
.PARAMETER ResourceGroupName
    The name of the Azure resource group to deploy to
.PARAMETER Location
    The Azure region to deploy to (default: East US)
.PARAMETER Environment
    The environment name (dev, test, prod)
.PARAMETER SubscriptionId
    The Azure subscription ID
.EXAMPLE
    ./deploy.ps1 -ResourceGroupName "rg-member-property-alert-dev" -Environment "dev"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory = $true)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment,
    
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting deployment of Member Property Market Alert service..." -ForegroundColor Green
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor Cyan
Write-Host "🌍 Location: $Location" -ForegroundColor Cyan
Write-Host "🏷️  Environment: $Environment" -ForegroundColor Cyan

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "🔄 Setting Azure subscription..." -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set Azure subscription"
    }
}

# Verify Azure CLI login
Write-Host "🔐 Verifying Azure CLI authentication..." -ForegroundColor Yellow
$account = az account show --query "user.name" -o tsv 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Not logged in to Azure CLI. Please run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "✅ Logged in as: $account" -ForegroundColor Green

# Create resource group if it doesn't exist
Write-Host "📦 Creating resource group if it doesn't exist..." -ForegroundColor Yellow
az group create --name $ResourceGroupName --location $Location --output none
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create resource group"
}
Write-Host "✅ Resource group ready" -ForegroundColor Green

# Generate unique suffix for resources
$suffix = Get-Random -Minimum 1000 -Maximum 9999
$appName = "member-property-alert-$Environment-$suffix"

Write-Host "🏗️  Deploying Azure resources..." -ForegroundColor Yellow

# Deploy infrastructure using Azure CLI (since we do not have Bicep templates yet)
Write-Host "📊 Creating Cosmos DB account..." -ForegroundColor Yellow
$cosmosAccountName = "cosmos-$appName"
az cosmosdb create `
    --name $cosmosAccountName `
    --resource-group $ResourceGroupName `
    --locations regionName="$Location" failoverPriority=0 isZoneRedundant=false `
    --default-consistency-level "Session" `
    --enable-automatic-failover false `
    --output none

if ($LASTEXITCODE -ne 0) {
    throw "Failed to create Cosmos DB account"
}

Write-Host "🗄️  Creating Cosmos DB database..." -ForegroundColor Yellow
az cosmosdb sql database create `
    --account-name $cosmosAccountName `
    --resource-group $ResourceGroupName `
    --name "MemberPropertyAlert" `
    --output none

# Create containers
$containers = @(
    @{name="Institutions"; partitionKey="/id"},
    @{name="Addresses"; partitionKey="/institutionId"},
    @{name="Alerts"; partitionKey="/institutionId"},
    @{name="ScanLogs"; partitionKey="/institutionId"}
)

foreach ($container in $containers) {
    Write-Host "📋 Creating container: $($container.name)..." -ForegroundColor Yellow
    az cosmosdb sql container create `
        --account-name $cosmosAccountName `
        --resource-group $ResourceGroupName `
        --database-name "MemberPropertyAlert" `
        --name $container.name `
        --partition-key-path $container.partitionKey `
        --throughput 400 `
        --output none
}

Write-Host "🚌 Creating Service Bus namespace..." -ForegroundColor Yellow
$serviceBusName = "sb-$appName"
az servicebus namespace create `
    --name $serviceBusName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Standard `
    --output none

Write-Host "📬 Creating Service Bus queue..." -ForegroundColor Yellow
az servicebus queue create `
    --namespace-name $serviceBusName `
    --resource-group $ResourceGroupName `
    --name "property-alerts" `
    --output none

Write-Host "📡 Creating SignalR service..." -ForegroundColor Yellow
$signalRName = "signalr-$appName"
az signalr create `
    --name $signalRName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Free_F1 `
    --service-mode Default `
    --output none

Write-Host "📊 Creating Application Insights..." -ForegroundColor Yellow
$appInsightsName = "ai-$appName"
az monitor app-insights component create `
    --app $appInsightsName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --kind web `
    --output none

Write-Host "🔐 Creating Key Vault..." -ForegroundColor Yellow
$keyVaultName = "kv-$appName"
az keyvault create `
    --name $keyVaultName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku standard `
    --output none

Write-Host "💾 Creating Storage Account..." -ForegroundColor Yellow
$storageAccountName = "st$($appName.Replace('-', ''))"
az storage account create `
    --name $storageAccountName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Standard_LRS `
    --kind StorageV2 `
    --output none

Write-Host "⚡ Creating Function App..." -ForegroundColor Yellow
$functionAppName = "func-$appName"
az functionapp create `
    --name $functionAppName `
    --resource-group $ResourceGroupName `
    --storage-account $storageAccountName `
    --consumption-plan-location $Location `
    --runtime dotnet `
    --runtime-version 8 `
    --functions-version 4 `
    --output none

# Get connection strings
Write-Host "🔗 Retrieving connection strings..." -ForegroundColor Yellow

$cosmosConnectionString = az cosmosdb keys list --name $cosmosAccountName --resource-group $ResourceGroupName --type connection-strings --query "connectionStrings[0].connectionString" -o tsv
$serviceBusConnectionString = az servicebus namespace authorization-rule keys list --namespace-name $serviceBusName --resource-group $ResourceGroupName --name RootManageSharedAccessKey --query primaryConnectionString -o tsv
$signalRConnectionString = az signalr key list --name $signalRName --resource-group $ResourceGroupName --query primaryConnectionString -o tsv
$appInsightsConnectionString = az monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroupName --query connectionString -o tsv

# Configure Function App settings
Write-Host "⚙️  Configuring Function App settings..." -ForegroundColor Yellow

$appSettings = @(
    "CosmosDB__ConnectionString=$cosmosConnectionString",
    "CosmosDB__DatabaseName=MemberPropertyAlert",
    "ServiceBus__ConnectionString=$serviceBusConnectionString",
    "ServiceBus__AlertQueueName=property-alerts",
    "SignalR__ConnectionString=$signalRConnectionString",
    "SignalR__HubName=PropertyAlertHub",
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString",
    "RentCast__BaseUrl=https://api.rentcast.io/v1",
    "RentCast__TimeoutSeconds=30",
    "RentCast__MaxRetries=3",
    "RentCast__RateLimitDelayMs=1000",
    "ApiKey__HeaderName=X-API-Key",
    "ApiKey__ValidKeys=prod-key-$(Get-Random -Minimum 100000 -Maximum 999999)"
)

foreach ($setting in $appSettings) {
    az functionapp config appsettings set --name $functionAppName --resource-group $ResourceGroupName --settings $setting --output none
}

# Build and deploy the application
Write-Host "🔨 Building the application..." -ForegroundColor Yellow
Push-Location "../src/MemberPropertyAlert.Functions"
try {
    dotnet publish --configuration Release --output "./publish"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build application"
    }

    Write-Host "📦 Creating deployment package..." -ForegroundColor Yellow
    Compress-Archive -Path "./publish/*" -DestinationPath "./deploy.zip" -Force

    Write-Host "🚀 Deploying to Azure Functions..." -ForegroundColor Yellow
    az functionapp deployment source config-zip --name $functionAppName --resource-group $ResourceGroupName --src "./deploy.zip" --output none
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to deploy application"
    }

    # Clean up
    Remove-Item "./deploy.zip" -Force -ErrorAction SilentlyContinue
    Remove-Item "./publish" -Recurse -Force -ErrorAction SilentlyContinue
}
finally {
    Pop-Location
}

Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Deployment Summary:" -ForegroundColor Cyan
Write-Host "  🌐 Function App: https://$functionAppName.azurewebsites.net" -ForegroundColor White
Write-Host "  📊 Cosmos DB: $cosmosAccountName" -ForegroundColor White
Write-Host "  🚌 Service Bus: $serviceBusName" -ForegroundColor White
Write-Host "  📡 SignalR: $signalRName" -ForegroundColor White
Write-Host "  🔐 Key Vault: $keyVaultName" -ForegroundColor White
Write-Host "  📊 App Insights: $appInsightsName" -ForegroundColor White
Write-Host ""
Write-Host "🔑 API Key: $(($appSettings | Where-Object { $_ -like '*ApiKey__ValidKeys*' }).Split('=')[1])" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Add your RentCast API key to the Function App settings" -ForegroundColor White
Write-Host "  2. Test the API endpoints using the provided API key" -ForegroundColor White
Write-Host "  3. Configure webhook URLs for your institutions" -ForegroundColor White
Write-Host ""
Write-Host "🎉 Your Member Property Market Alert service is ready!" -ForegroundColor Green
