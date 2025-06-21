// RBAC and Role Assignments - Deploy after main infrastructure
@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Function App principal ID (from main deployment output)')
param functionAppPrincipalId string

@description('Cosmos DB account name (from main deployment output)')
param cosmosAccountName string

@description('Storage account name (from main deployment output)')
param storageAccountName string

// Variables
var cosmosDbDataContributorRoleId = '00000000-0000-0000-0000-000000000002' // Cosmos DB Built-in Data Contributor
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor

// Get existing resources
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// Role Assignment: Function App -> Cosmos DB Data Contributor
resource functionAppCosmosRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cosmosAccount.id, functionAppPrincipalId, cosmosDbDataContributorRoleId)
  scope: cosmosAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cosmosDbDataContributorRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role Assignment: Function App -> Storage Blob Data Contributor (for function storage)
resource functionAppStorageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output cosmosRoleAssignmentId string = functionAppCosmosRoleAssignment.id
output storageRoleAssignmentId string = functionAppStorageRoleAssignment.id
output message string = 'RBAC roles assigned successfully for ${environment} environment'
