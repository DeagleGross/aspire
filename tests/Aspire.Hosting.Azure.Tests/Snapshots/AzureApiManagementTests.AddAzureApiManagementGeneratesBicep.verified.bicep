@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('The email address of the owner of the API Management service.')
param publisherEmail string = 'noreply@aspire.local'

@description('The name of the owner of the API Management service.')
param publisherName string = 'Aspire'

resource apim 'Microsoft.ApiManagement/service@2024-05-01' = {
  name: take('apim${uniqueString(resourceGroup().id)}', 24)
  location: location
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
  sku: {
    name: 'StandardV2'
    capacity: 1
  }
  tags: {
    'aspire-resource-name': 'apim'
  }
}

output gatewayUrl string = 'https://${apim.properties.gatewayUrl}'

output name string = apim.name

output id string = apim.id