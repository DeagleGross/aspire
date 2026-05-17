// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

// Required when targeting Azure for publish — Container Apps will host the backend project.
builder.AddAzureContainerAppEnvironment("env");

var ordersApi = builder.AddProject<Projects.AzureApiManagement_ApiService>("orders-api")
    .WithExternalHttpEndpoints();

// Sample policy: stamps an `X-Source` header on every inbound request before forwarding to the backend.
// The integration auto-injects <set-backend-service backend-id="orders-backend" /> into the inbound block
// based on the WithBackend(...) call below, so requests routed by APIM end up at the orders-api container.
const string ratePolicyXml = """
    <policies>
      <inbound>
        <base />
        <set-header name="X-Source" exists-action="override">
          <value>aspire</value>
        </set-header>
      </inbound>
      <backend>
        <base />
      </backend>
      <outbound>
        <base />
      </outbound>
      <on-error>
        <base />
      </on-error>
    </policies>
    """;

var apim = builder.AddAzureApiManagement("apim");

// Register the orders API on APIM. APIM matches incoming requests against operations — without at
// least one operation the gateway returns 404. Each WithOperation(...) declares one HTTP route the
// gateway should expose. URL templates are relative to the API path (here: /v1) and APIM forwards
// the unmodified template segment to the backend, so /v1/orders → backend's /orders.
apim.AddApi("orders", path: "v1")
    .WithBackend(ordersApi)
    .WithPolicy(ratePolicyXml)
    .WithOperation("getAll", "GET", "/orders", displayName: "List orders")
    .WithOperation("getById", "GET", "/orders/{id}", displayName: "Get order by id");

builder.Build().Run();

