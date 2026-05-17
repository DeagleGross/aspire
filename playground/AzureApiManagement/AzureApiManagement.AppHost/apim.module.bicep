@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('The email address of the owner of the API Management service.')
param publisherEmail string = 'noreply@aspire.local'

@description('The name of the owner of the API Management service.')
param publisherName string = 'Aspire'

param orders_backendDomain string

@description('The name of the orders-api container app.')
param orders_backendAppName string = 'orders-api'

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

resource ordersApi 'Microsoft.ApiManagement/service/apis@2024-05-01' = {
  name: 'orders'
  properties: {
    displayName: 'orders'
    path: 'v1'
    protocols: [
      'https'
    ]
  }
  parent: apim
}

resource ordersBackend 'Microsoft.ApiManagement/service/backends@2024-05-01' = {
  name: 'orders-backend'
  properties: {
    protocol: 'http'
    url: 'http://${orders_backendAppName}.${orders_backendDomain}'
  }
  parent: apim
}

resource ordersPolicy 'Microsoft.ApiManagement/service/apis/policies@2024-05-01' = {
  name: 'policy'
  properties: {
    format: 'xml'
    value: '<policies><inbound><set-backend-service backend-id="orders-backend" /><base /><set-header name="X-Source" exists-action="override"><value>aspire</value></set-header></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
  }
  parent: ordersApi
}

resource orders_getAllOperation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' = {
  name: 'getAll'
  properties: {
    displayName: 'List orders'
    method: 'GET'
    urlTemplate: '/orders'
  }
  parent: ordersApi
}

resource orders_getByIdOperation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' = {
  name: 'getById'
  properties: {
    displayName: 'Get order by id'
    method: 'GET'
    templateParameters: [
      {
        name: 'id'
        required: true
        typeName: 'string'
      }
    ]
    urlTemplate: '/orders/{id}'
  }
  parent: ordersApi
}

output gatewayUrl string = 'https://${apim.properties.gatewayUrl}'

output name string = apim.name

output id string = apim.id