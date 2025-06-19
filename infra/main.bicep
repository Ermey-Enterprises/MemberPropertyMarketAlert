@description('The environment for deployment (dev, test, prod)')
@allowed(['dev', 'test', 'prod'])
param environment string = 'dev'

@description('The Azure region for deployment')
param location string = resourceGroup().location

@description('The base name for all resources')
param baseName string = 'memberpropertyalert'

@description('Administrator email address for notifications')
param adminEmail string

@description('Tags to apply to all resources')
param tags object = {
  Environment: environment
  Project: 'MemberPropertyMarketAlert'
  ManagedBy: 'Bicep'
}

// Variables
var resourcePrefix = '${baseName}-${environment}'
var cosmosAccountName = '${resourcePrefix}-cosmos'
var functionAppName = '${resourcePrefix}-func'
var webAppName = '${resourcePrefix}-web'
var appServicePlanName = '${resourcePrefix}-plan'
var storageAccountName = replace('${take(baseName, 10)}${environment}st', '-', '')
var serviceBusNamespaceName = '${resourcePrefix}-sb'
var appInsightsName = '${resourcePrefix}-ai'
var logAnalyticsName = '${resourcePrefix}-logs'
var keyVaultName = '${resourcePrefix}-kv'
var containerRegistryName = replace('${take(baseName, 10)}${environment}acr', '-', '')

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      searchVersion: 1
    }
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: true
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: []
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enableRbacAuthorization: true
  }
}

// Cosmos DB Account
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosAccountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    databaseAccountOfferType: 'Standard'
    enableAutomaticFailover: false
    capabilities: environment == 'prod' ? [] : [
      {
        name: 'EnableServerless'
      }
    ]
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 240
        backupRetentionIntervalInHours: 8
        backupStorageRedundancy: 'Local'
      }
    }
  }
}

// Cosmos DB Database
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: 'MemberPropertyMarketAlert'
  properties: {
    resource: {
      id: 'MemberPropertyMarketAlert'
    }
    options: environment == 'prod' ? {
      throughput: 400
    } : {}
  }
}

// Cosmos DB Containers
resource monitoredPropertiesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'MonitoredProperties'
  properties: {
    resource: {
      id: 'MonitoredProperties'
      partitionKey: {
        paths: ['/financialInstitutionId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
    options: environment == 'prod' ? {
      throughput: 400
    } : {}
  }
}

resource propertyListingsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'PropertyListings'
  properties: {
    resource: {
      id: 'PropertyListings'
      partitionKey: {
        paths: ['/source']
        kind: 'Hash'
      }
    }
    options: environment == 'prod' ? {
      throughput: 400
    } : {}
  }
}

resource propertyMatchesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'PropertyMatches'
  properties: {
    resource: {
      id: 'PropertyMatches'
      partitionKey: {
        paths: ['/financialInstitutionId']
        kind: 'Hash'
      }
    }
    options: environment == 'prod' ? {
      throughput: 400
    } : {}
  }
}

resource financialInstitutionsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'FinancialInstitutions'
  properties: {
    resource: {
      id: 'FinancialInstitutions'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
    options: environment == 'prod' ? {
      throughput: 400
    } : {}
  }
}

// Service Bus Namespace
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

// Service Bus Queues
resource propertyCheckQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'property-check-queue'
  properties: {
    defaultMessageTimeToLive: 'P14D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    requiresSession: false
    enablePartitioning: false
    deadLetteringOnMessageExpiration: true
  }
}

resource notificationQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'notification-queue'
  properties: {
    defaultMessageTimeToLive: 'P14D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    requiresSession: false
    enablePartitioning: false
    deadLetteringOnMessageExpiration: true
  }
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: environment == 'prod' ? 'P1v3' : 'B1'
    tier: environment == 'prod' ? 'PremiumV3' : 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      alwaysOn: environment == 'prod' ? true : false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'CosmosDb__ConnectionString'
          value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'ServiceBus__ConnectionString'
          value: listKeys('${serviceBusNamespace.id}/AuthorizationRules/RootManageSharedAccessKey', serviceBusNamespace.apiVersion).primaryConnectionString
        }
        {
          name: 'Environment'
          value: environment
        }
      ]
    }
    httpsOnly: true
  }
}

// Web App
resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: webAppName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: environment == 'prod' ? true : false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'CosmosDb__ConnectionString'
          value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'FunctionApp__BaseUrl'
          value: 'https://${functionApp.properties.defaultHostName}'
        }
        {
          name: 'Environment'
          value: environment
        }
      ]
    }
    httpsOnly: true
  }
}

// Role assignments for managed identities (requires User Access Administrator role)
// Uncomment and deploy separately with elevated permissions if needed
/*
resource functionAppCosmosRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: cosmosAccount
  name: guid(cosmosAccount.id, functionApp.id, 'DocumentDB Account Contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5bd9cd88-fe45-4216-938b-f97437e15450') // DocumentDB Account Contributor
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource webAppCosmosRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: cosmosAccount
  name: guid(cosmosAccount.id, webApp.id, 'DocumentDB Account Contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5bd9cd88-fe45-4216-938b-f97437e15450') // DocumentDB Account Contributor
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
*/

// Key Vault Secrets
resource cosmosConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'CosmosConnectionString'
  properties: {
    value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
  }
}

resource serviceBusConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ServiceBusConnectionString'
  properties: {
    value: listKeys('${serviceBusNamespace.id}/AuthorizationRules/RootManageSharedAccessKey', serviceBusNamespace.apiVersion).primaryConnectionString
  }
}

// Outputs
output resourceGroupName string = resourceGroup().name
output functionAppName string = functionApp.name
output webAppName string = webApp.name
output cosmosAccountName string = cosmosAccount.name
output serviceBusNamespaceName string = serviceBusNamespace.name
output storageAccountName string = storageAccount.name
output containerRegistryName string = containerRegistry.name
output keyVaultName string = keyVault.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
