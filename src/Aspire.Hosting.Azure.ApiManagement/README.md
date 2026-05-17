# Aspire.Hosting.Azure.ApiManagement library

Provides extension methods and resource definitions for an Aspire AppHost to configure an Azure API Management resource.

## Getting started

### Prerequisites

- An Azure subscription - [create one for free](https://azure.microsoft.com/free/)

### Install the package

In your AppHost project, install the `Aspire.Hosting.Azure.ApiManagement` library via NuGet:

```dotnetcli
dotnet add package Aspire.Hosting.Azure.ApiManagement
```

Or using the Aspire CLI:

```bash
aspire add Aspire.Hosting.Azure.ApiManagement
```

## Usage example

In the _AppHost.cs_ file of `AppHost`, add an Azure API Management resource and register an API backed by an Aspire project:

```csharp
var orders = builder.AddProject<Projects.OrdersApi>("orders-api")
    .WithExternalHttpEndpoints();

var apim = builder.AddAzureApiManagement("apim");

apim.AddApi("orders", path: "v1/orders")
    .WithBackend(orders);
```

This provisions an Azure API Management service, registers an API named `orders` on path `v1/orders`, and configures a backend pointing at the deployed `orders-api` project. Incoming requests to the gateway under `/v1/orders/...` are forwarded to the project via a `set-backend-service` policy.

## SKU choice

By default the integration provisions API Management with the `StandardV2` SKU. StandardV2 typically provisions in under a minute and is suitable for production workloads. Override the SKU (or any other service property) using `ConfigureInfrastructure`:

```csharp
using Azure.Provisioning.ApiManagement;

var apim = builder.AddAzureApiManagement("apim")
    .ConfigureInfrastructure(infra =>
    {
        var service = infra.GetProvisionableResources().OfType<ApiManagementService>().Single();
        service.Sku = new ApiManagementServiceSkuProperties
        {
            Name = ApiManagementServiceSkuType.BasicV2,
            Capacity = 1
        };
    });
```

## Attaching a policy

Use `WithPolicy` to attach a raw APIM policy XML document to an API. The integration injects the appropriate `set-backend-service` policy into the inbound section automatically so the configured backend continues to receive traffic.

```csharp
const string ratePolicy = """
    <policies>
      <inbound>
        <base />
        <rate-limit-by-key calls="100" renewal-period="60" counter-key="@(context.Subscription?.Id)" />
      </inbound>
      <backend><base /></backend>
      <outbound><base /></outbound>
      <on-error><base /></on-error>
    </policies>
    """;

apim.AddApi("orders", path: "v1/orders")
    .WithBackend(orders)
    .WithPolicy(ratePolicy);
```

## Inner-loop note

Azure API Management deployed in the cloud cannot reach your locally-running projects on `localhost`, so the API gateway is most useful at publish time. For local-loop testing of policy-driven gateway behavior, consider using a self-hosted gateway (requires Developer or Premium SKU) — support for which may be added in a future release.

## Additional documentation

* https://learn.microsoft.com/azure/api-management/

## Feedback & contributing

https://github.com/microsoft/aspire
