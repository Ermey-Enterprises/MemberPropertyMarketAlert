// Member Property Market Alert Platform
// Modern Azure infrastructure template following Microsoft Cloud Adoption Framework
// Based on industry best practices for security, scalability, and maintainability

@description('Environment designation (dev, test, prod)')
@allowed(['dev', 'test', 'prod'])
param environment string = 'dev'

@description('Azure region for resource deployment')
param location string = resourceGroup().location

@description('Application name prefix for resource naming')
param appName string = 'member-property-alert'

@description('Deploy Function App component')
param deployFunctionApp bool = true

@description('Deploy Web App component')
param deployWebApp bool = true

@description('RentCast API key for property data (will be stored in Key Vault)')
@secure()
param rentCastApiKey string = ''

@description('Admin API key for secure endpoints (will be stored in Key Vault)')
@secure()
param adminApiKey string = ''

@description('Enable advanced security features')
param enableAdvancedSecurity bool = true

@description('Azure Client ID for deployment (will be stored in Key Vault)')
@secure()
param azureClientId string = ''

@description('Azure Tenant ID for deployment (will be stored in Key Vault)')
@secure()
param azureTenantId string = ''

@description('Azure Subscription ID for deployment (will be stored in Key Vault)')
@secure()
param azureSubscriptionId string = ''

// Azure naming convention variables following Microsoft Cloud Adoption Framework
// Pattern: <resource-type>-<workload>-<environment>-<region>-<instance>
var locationAbbreviation = {
  eastus: 'eus'
  eastus2: 'eus2'
  westus: 'wus'
  westus2: 'wus2'
  centralus: 'cus'
  northcentralus: 'ncus'
  southcentralus: 'scus'
  westcentralus: 'wcus'
  northeurope: 'neu'
  westeurope: 'weu'
}

var locationCode = locationAbbreviation[location] ?? 'unk'
var uniqueSuffix = substring(uniqueString(resourceGroup().id), 0, 4)
var workloadName = 'mpa' // member-property-alert abbreviated

// Resource naming following Microsoft Cloud Adoption Framework recommendations
var resourceNames = {
  // Storage accounts have strict naming rules: 3-24 chars, lowercase letters and numbers only
  storageAccount: 'st${workloadName}${environment}${locationCode}${uniqueSuffix}'
  
  // Key Vault: 3-24 chars, alphanumeric and hyphens
  keyVault: 'kv-${workloadName}-${environment}-${locationCode}-${uniqueSuffix}'
  
  // Cosmos DB: 3-44 chars, lowercase letters, numbers, and hyphens
  cosmosDb: 'cosmos-${workloadName}-${environment}-${locationCode}-${uniqueSuffix}'
  
  // Application Insights: 1-260 chars
  applicationInsights: 'appi-${workloadName}-${environment}-${locationCode}-${uniqueSuffix}'
  
  // Log Analytics: 4-63 chars
  logAnalytics: 'log-${workloadName}-${environment}-${locationCode}-${uniqueSuffix}'
  
  // App Service Plan: 1-40 chars for Windows, 1-60 for Linux
  appServicePlan: 'asp-${workloadName}-${environment}-${locationCode}-${uniqueSuffix}'
  
  // Function App: 2-60 chars
  functionApp: 'func-${workloadName}-${environment}-${locationCode}-${uniqueSuffix}'
  
  // Web App: 2-60 chars
  webApp: 'web-${workloadName}-${environment}-${locationCode}-${uniqueSuffix}'
}

// Environment-specific configuration
var environmentConfig = {
  dev: {
    appServicePlanSku: {
      name: 'B1'
      tier: 'Basic'
    }
    cosmosFreeTier: true
    logRetentionDays: 30
    backupIntervalMinutes: 1440
    backupRetentionHours: 168
    functionAppScaleLimit: 10
    alwaysOn: false
  }
  test: {
    appServicePlanSku: {
      name: 'S1'
      tier: 'Standard'
    }
    cosmosFreeTier: false
    logRetentionDays: 60
    backupIntervalMinutes: 720
    backupRetentionHours: 336
    functionAppScaleLimit: 50
    alwaysOn: true
  }
  prod: {
    appServicePlanSku: {
      name: 'P1v3'
      tier: 'PremiumV3'
    }
    cosmosFreeTier: false
    logRetentionDays: 90
    backupIntervalMinutes: 240
    backupRetentionHours: 720
    functionAppScaleLimit: 200
    alwaysOn: true
  }
}

var currentConfig = environmentConfig[environment]

// Comprehensive resource tagging strategy
var commonTags = {
  Environment: environment
  Application: 'MemberPropertyAlert'
  Workload: workloadName
  ManagedBy: 'Bicep'
  CostCenter: 'IT'
  Owner: 'DevOps'
  Project: 'PropertyMarketAlert'
}

// Log Analytics Workspace (required for Application Insights)
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: resourceNames.logAnalytics
  location: location
  tags: commonTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: currentConfig.logRetentionDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
      disableLocalAuth: enableAdvancedSecurity
    }
    workspaceCapping: {
      dailyQuotaGb: environment == 'prod' ? 10 : 5
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: resourceNames.applicationInsights
  location: location
  tags: commonTags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    DisableIpMasking: false
    DisableLocalAuth: enableAdvancedSecurity
  }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: resourceNames.storageAccount
  location: location
  tags: commonTags
  sku: {
    name: environment == 'prod' ? 'Standard_ZRS' : 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    dnsEndpointType: 'Standard'
    defaultToOAuthAuthentication: enableAdvancedSecurity
    publicNetworkAccess: 'Enabled'
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: !enableAdvancedSecurity
    networkAcls: {
      bypass: 'AzureServices'
      virtualNetworkRules: []
      ipRules: []
      defaultAction: 'Allow'
    }
    supportsHttpsTrafficOnly: true
    encryption: {
      requireInfrastructureEncryption: enableAdvancedSecurity
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
}

// Key Vault for secure secret management
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: resourceNames.keyVault
  location: location
  tags: commonTags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: false
    enabledForDeployment: false
    enableSoftDelete: true
    softDeleteRetentionInDays: environment == 'prod' ? 90 : 7
    enablePurgeProtection: environment == 'prod'
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
      ipRules: []
      virtualNetworkRules: []
    }
  }
}

// Cosmos DB Account
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: resourceNames.cosmosDb
  location: location
  tags: commonTags
  kind: 'GlobalDocumentDB'
  properties: {
    enableFreeTier: currentConfig.cosmosFreeTier
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: environment == 'prod'
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: currentConfig.backupIntervalMinutes
        backupRetentionIntervalInHours: currentConfig.backupRetentionHours
        backupStorageRedundancy: environment == 'prod' ? 'Zone' : 'Local'
      }
    }
    networkAclBypass: 'AzureServices'
    publicNetworkAccess: 'Enabled'
    enableAnalyticalStorage: false
    enablePartitionMerge: false
    enableBurstCapacity: false
    minimalTlsVersion: 'Tls12'
    disableKeyBasedMetadataWriteAccess: enableAdvancedSecurity
  }
}

// Cosmos DB Database
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosDbAccount
  name: 'MemberPropertyAlert'
  properties: {
    resource: {
      id: 'MemberPropertyAlert'
    }
  }
}

// Cosmos DB Containers with optimized configurations
resource institutionsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'Institutions'
  properties: {
    resource: {
      id: 'Institutions'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      defaultTtl: -1
    }
  }
}

resource addressesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'Addresses'
  properties: {
    resource: {
      id: 'Addresses'
      partitionKey: {
        paths: ['/institutionId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      defaultTtl: -1
    }
  }
}

resource alertsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'Alerts'
  properties: {
    resource: {
      id: 'Alerts'
      partitionKey: {
        paths: ['/institutionId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      defaultTtl: -1
    }
  }
}

resource scanLogsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDatabase
  name: 'ScanLogs'
  properties: {
    resource: {
      id: 'ScanLogs'
      partitionKey: {
        paths: ['/institutionId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      defaultTtl: environment == 'prod' ? -1 : 2592000 // 30 days for non-prod
    }
  }
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: resourceNames.appServicePlan
  location: location
  tags: commonTags
  sku: currentConfig.appServicePlanSku
  kind: 'linux'
  properties: {
    reserved: true
    zoneRedundant: environment == 'prod'
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-12-01' = if (deployFunctionApp) {
  name: resourceNames.functionApp
  location: location
  tags: union(commonTags, {
    Component: 'FunctionApp'
    Purpose: 'API'
  })
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    reserved: true
    isXenon: false
    hyperV: false
    vnetRouteAllEnabled: false
    vnetImagePullEnabled: false
    vnetContentShareEnabled: false
    siteConfig: {
      numberOfWorkers: 1
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      acrUseManagedIdentityCreds: false
      alwaysOn: currentConfig.alwaysOn
      http20Enabled: true
      functionAppScaleLimit: currentConfig.functionAppScaleLimit
      minimumElasticInstanceCount: 0
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      healthCheckPath: '/api/health'
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
          deployWebApp ? 'https://${resourceNames.webApp}.azurewebsites.net' : ''
        ]
        supportCredentials: false
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=STORAGE-CONNECTION-STRING)'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=STORAGE-CONNECTION-STRING)'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(resourceNames.functionApp)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: '~18'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=APPLICATION-INSIGHTS-CONNECTION-STRING)'
        }
        {
          name: 'CosmosDb__ConnectionString'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=COSMOS-CONNECTION-STRING)'
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: cosmosDatabase.name
        }
        {
          name: 'RentCast__ApiKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=RENTCAST-API-KEY)'
        }
        {
          name: 'RentCast__BaseUrl'
          value: 'https://api.rentcast.io/v1'
        }
        {
          name: 'AdminApiKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=ADMIN-API-KEY)'
        }
        {
          name: 'AZURE_KEY_VAULT_URI'
          value: keyVault.properties.vaultUri
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
    httpsOnly: true
    redundancyMode: 'None'
    storageAccountRequired: false
    keyVaultReferenceIdentity: 'SystemAssigned'
    clientAffinityEnabled: false
  }
}

// Web App for Admin UI
resource webApp 'Microsoft.Web/sites@2023-12-01' = if (deployWebApp) {
  name: resourceNames.webApp
  location: location
  tags: union(commonTags, {
    Component: 'WebApp'
    Purpose: 'UI'
  })
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    reserved: true
    isXenon: false
    hyperV: false
    vnetRouteAllEnabled: false
    vnetImagePullEnabled: false
    vnetContentShareEnabled: false
    siteConfig: {
      numberOfWorkers: 1
      linuxFxVersion: 'NODE|18-lts'
      acrUseManagedIdentityCreds: false
      alwaysOn: currentConfig.alwaysOn
      http20Enabled: true
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'REACT_APP_API_BASE_URL'
          value: deployFunctionApp ? 'https://${resourceNames.functionApp}.azurewebsites.net/api' : ''
        }
        {
          name: 'REACT_APP_ENVIRONMENT'
          value: environment
        }
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: '18-lts'
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'true'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'AZURE_KEY_VAULT_URI'
          value: keyVault.properties.vaultUri
        }
      ]
    }
    httpsOnly: true
    redundancyMode: 'None'
    storageAccountRequired: false
    keyVaultReferenceIdentity: 'SystemAssigned'
    clientAffinityEnabled: false
  }
}

// Key Vault Secrets
resource applicationInsightsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'APPLICATION-INSIGHTS-CONNECTION-STRING'
  properties: {
    value: applicationInsights.properties.ConnectionString
    contentType: 'text/plain'
  }
}

resource cosmosConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'COSMOS-CONNECTION-STRING'
  properties: {
    value: cosmosDbAccount.listConnectionStrings().connectionStrings[0].connectionString
    contentType: 'text/plain'
  }
}

resource storageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'STORAGE-CONNECTION-STRING'
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
    contentType: 'text/plain'
  }
}

resource rentCastApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(rentCastApiKey)) {
  parent: keyVault
  name: 'RENTCAST-API-KEY'
  properties: {
    value: rentCastApiKey
    contentType: 'text/plain'
  }
}

resource adminApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(adminApiKey)) {
  parent: keyVault
  name: 'ADMIN-API-KEY'
  properties: {
    value: adminApiKey
    contentType: 'text/plain'
  }
}

// Deployment credential secrets (auto-populated from GitHub secrets)
resource azureClientIdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(azureClientId)) {
  parent: keyVault
  name: 'AZURE-CLIENT-ID'
  properties: {
    value: azureClientId
    contentType: 'text/plain'
  }
}

resource azureTenantIdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(azureTenantId)) {
  parent: keyVault
  name: 'AZURE-TENANT-ID'
  properties: {
    value: azureTenantId
    contentType: 'text/plain'
  }
}

resource azureSubscriptionIdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(azureSubscriptionId)) {
  parent: keyVault
  name: 'AZURE-SUBSCRIPTION-ID'
  properties: {
    value: azureSubscriptionId
    contentType: 'text/plain'
  }
}

// RBAC Role Assignments for Key Vault access
resource functionAppKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployFunctionApp) {
  name: guid(keyVault.id, resourceNames.functionApp, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: deployFunctionApp ? functionApp.identity.principalId : ''
    principalType: 'ServicePrincipal'
  }
}

resource webAppKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployWebApp) {
  name: guid(keyVault.id, resourceNames.webApp, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: deployWebApp ? webApp.identity.principalId : ''
    principalType: 'ServicePrincipal'
  }
}

// Outputs for CI/CD pipeline integration
output infrastructure object = {
  resourceGroupName: resourceGroup().name
  location: location
  environment: environment
  keyVaultName: keyVault.name
  keyVaultUri: keyVault.properties.vaultUri
  storageAccountName: storageAccount.name
  cosmosDbAccountName: cosmosDbAccount.name
  cosmosDbEndpoint: cosmosDbAccount.properties.documentEndpoint
  applicationInsightsName: applicationInsights.name
  applicationInsightsConnectionString: applicationInsights.properties.ConnectionString
  appServicePlanName: appServicePlan.name
}

output functionAppDeployment object = deployFunctionApp ? {
  appServiceName: functionApp.name
  appServiceUrl: 'https://${functionApp.properties.defaultHostName}'
  principalId: functionApp.identity.principalId
  healthCheckUrl: 'https://${functionApp.properties.defaultHostName}/api/health'
} : {}

output webAppDeployment object = deployWebApp ? {
  appServiceName: webApp.name
  appServiceUrl: 'https://${webApp.properties.defaultHostName}'
  principalId: webApp.identity.principalId
  healthCheckUrl: 'https://${webApp.properties.defaultHostName}/health'
} : {}

output security object = {
  keyVaultEnabled: true
  managedIdentityEnabled: true
  advancedSecurityEnabled: enableAdvancedSecurity
  rbacEnabled: true
}
