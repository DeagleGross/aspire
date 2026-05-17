// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002
#pragma warning disable AZPROVISION001

using Aspire.Hosting.Utils;
using Azure.Provisioning.ApiManagement;
using static Aspire.Hosting.Utils.AzureManifestUtils;

namespace Aspire.Hosting.Azure.Tests;

public class AzureApiManagementTests
{
    [Fact]
    public void AddAzureApiManagementCreatesResource()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim");

        Assert.NotNull(apim);
        Assert.Equal("apim", apim.Resource.Name);
        Assert.IsType<AzureApiManagementResource>(apim.Resource);
    }

    [Fact]
    public void AddAzureApiManagementThrowsOnNullName()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        Assert.Throws<ArgumentNullException>(() => builder.AddAzureApiManagement(null!));
    }

    [Fact]
    public void AddApiRegistersChildResource()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim");
        var orders = apim.AddApi("orders", path: "v1/orders");

        Assert.Single(apim.Resource.Apis);
        Assert.Equal("orders", apim.Resource.Apis[0].Name);
        Assert.Equal("v1/orders", apim.Resource.Apis[0].Path);
        Assert.Same(apim.Resource, orders.Resource.Parent);
    }

    [Fact]
    public void AddApiThrowsOnNullArguments()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var apim = builder.AddAzureApiManagement("apim");

        Assert.Throws<ArgumentNullException>(() => apim.AddApi(null!, "v1/orders"));
        Assert.Throws<ArgumentNullException>(() => apim.AddApi("orders", null!));
        Assert.Throws<ArgumentException>(() => apim.AddApi("orders", ""));
    }

    [Fact]
    public void WithBackendAddsAnnotation()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var orders = builder.AddProject<Project>("orders-api", launchProfileName: null)
            .WithHttpsEndpoint()
            .WithExternalHttpEndpoints();

        var apim = builder.AddAzureApiManagement("apim");
        apim.AddApi("orders", path: "v1/orders").WithBackend(orders);

        var annotations = apim.Resource.Apis[0].Annotations.OfType<AzureApiManagementBackendAnnotation>().ToList();
        Assert.Single(annotations);
        Assert.Same(orders.Resource, annotations[0].Backend);
    }

    [Fact]
    public void WithBackendThrowsOnSecondCall()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var orders = builder.AddProject<Project>("orders-api", launchProfileName: null)
            .WithHttpsEndpoint()
            .WithExternalHttpEndpoints();

        var apim = builder.AddAzureApiManagement("apim");
        var api = apim.AddApi("orders", path: "v1/orders").WithBackend(orders);

        var ex = Assert.Throws<InvalidOperationException>(() => api.WithBackend(orders));
        Assert.Contains("already has a backend configured", ex.Message);
    }

    [Fact]
    public void WithPolicyThrowsOnSecondCall()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim");
        var api = apim.AddApi("orders", path: "v1/orders")
            .WithPolicy("<policies><inbound><base /></inbound><backend><base /></backend><outbound><base /></outbound></policies>");

        var ex = Assert.Throws<InvalidOperationException>(
            () => api.WithPolicy("<policies><inbound><base /></inbound><backend><base /></backend><outbound><base /></outbound></policies>"));
        Assert.Contains("already has a policy configured", ex.Message);
    }

    [Fact]
    public async Task AddAzureApiManagementGeneratesBicep()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var (_, bicep) = await GetManifestWithBicep(apim.Resource);

        await Verify(bicep, "bicep");
    }

    [Fact]
    public async Task AddAzureApiManagementWithApiAndBackendGeneratesBicep()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddAzureContainerAppEnvironment("env");

        var orders = builder.AddProject<Project>("orders-api", launchProfileName: null)
            .WithHttpsEndpoint()
            .WithExternalHttpEndpoints();

        var apim = builder.AddAzureApiManagement("apim");
        apim.AddApi("orders", path: "v1/orders").WithBackend(orders);

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var (_, bicep) = await GetManifestWithBicep(apim.Resource);

        await Verify(bicep, "bicep");
    }

    [Fact]
    public async Task AddAzureApiManagementWithMultipleApisGeneratesBicep()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddAzureContainerAppEnvironment("env");

        var orders = builder.AddProject<Project>("orders-api", launchProfileName: null)
            .WithHttpsEndpoint()
            .WithExternalHttpEndpoints();

        var inventory = builder.AddProject<Project>("inventory-api", launchProfileName: null)
            .WithHttpsEndpoint()
            .WithExternalHttpEndpoints();

        var apim = builder.AddAzureApiManagement("apim");
        apim.AddApi("orders", path: "v1/orders").WithBackend(orders);
        apim.AddApi("inventory", path: "v1/inventory").WithBackend(inventory);

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var (_, bicep) = await GetManifestWithBicep(apim.Resource);

        await Verify(bicep, "bicep");
    }

    [Fact]
    public async Task AddAzureApiManagementWithPolicyGeneratesBicep()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddAzureContainerAppEnvironment("env");

        var orders = builder.AddProject<Project>("orders-api", launchProfileName: null)
            .WithHttpsEndpoint()
            .WithExternalHttpEndpoints();

        const string policyXml = """
            <policies>
              <inbound>
                <base />
                <rate-limit-by-key calls="100" renewal-period="60" counter-key="@(context.Subscription?.Id)" />
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
        apim.AddApi("orders", path: "v1/orders")
            .WithBackend(orders)
            .WithPolicy(policyXml);

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var (_, bicep) = await GetManifestWithBicep(apim.Resource);

        await Verify(bicep, "bicep");
    }

    [Fact]
    public async Task WithBackendThrowsWhenBackendHasNoExternalEndpoints()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddAzureContainerAppEnvironment("env");

        var orders = builder.AddProject<Project>("orders-api", launchProfileName: null);

        var apim = builder.AddAzureApiManagement("apim");
        apim.AddApi("orders", path: "v1/orders").WithBackend(orders);

        using var app = builder.Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteBeforeStartHooksAsync(app, default));
        Assert.Contains("does not have an external HTTP or HTTPS endpoint", exception.ToString());
    }

    [Fact]
    public void WithSkuAddsAnnotation()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim")
            .WithSku(ApiManagementServiceSkuType.BasicV2, capacity: 2);

        var ann = apim.Resource.Annotations.OfType<AzureApiManagementSkuAnnotation>().Single();
        Assert.Equal(ApiManagementServiceSkuType.BasicV2, ann.Sku);
        Assert.Equal(2, ann.Capacity);
    }

    [Fact]
    public void WithSkuReplacesPreviousValue()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim")
            .WithSku(ApiManagementServiceSkuType.Developer)
            .WithSku(ApiManagementServiceSkuType.Premium, capacity: 3);

        var ann = apim.Resource.Annotations.OfType<AzureApiManagementSkuAnnotation>().Single();
        Assert.Equal(ApiManagementServiceSkuType.Premium, ann.Sku);
        Assert.Equal(3, ann.Capacity);
    }

    [Fact]
    public void WithSkuThrowsOnInvalidCapacity()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var apim = builder.AddAzureApiManagement("apim");

        Assert.Throws<ArgumentOutOfRangeException>(() => apim.WithSku(ApiManagementServiceSkuType.Premium, capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => apim.WithSku(ApiManagementServiceSkuType.Premium, capacity: -1));
    }

    [Fact]
    public async Task WithSkuGeneratesBicepWithCustomSku()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim")
            .WithSku(ApiManagementServiceSkuType.Developer);

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var (_, bicep) = await GetManifestWithBicep(apim.Resource);

        await Verify(bicep, "bicep");
    }

    [Fact]
    public void WithOperationAddsAnnotation()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim");
        var api = apim.AddApi("orders", path: "v1/orders")
            .WithOperation("getAll", "GET", "/")
            .WithOperation("getById", "GET", "/{id}");

        var annotations = api.Resource.Annotations.OfType<AzureApiManagementOperationAnnotation>().ToList();
        Assert.Equal(2, annotations.Count);
        Assert.Equal("getAll", annotations[0].Name);
        Assert.Equal("GET", annotations[0].Method);
        Assert.Equal("/", annotations[0].UrlTemplate);
        Assert.Equal("getById", annotations[1].Name);
        Assert.Equal("/{id}", annotations[1].UrlTemplate);
    }

    [Fact]
    public void WithOperationLowercaseMethodIsNormalized()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim");
        var api = apim.AddApi("orders", path: "v1/orders")
            .WithOperation("getAll", "get", "/");

        var ann = api.Resource.Annotations.OfType<AzureApiManagementOperationAnnotation>().Single();
        Assert.Equal("GET", ann.Method);
    }

    [Fact]
    public void WithOperationThrowsOnDuplicateName()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim");
        var api = apim.AddApi("orders", path: "v1/orders").WithOperation("getAll", "GET", "/");

        var ex = Assert.Throws<InvalidOperationException>(() => api.WithOperation("getAll", "POST", "/"));
        Assert.Contains("already has an operation named 'getAll'", ex.Message);
    }

    [Fact]
    public async Task AddAzureApiManagementWithOperationsGeneratesBicep()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddAzureContainerAppEnvironment("env");

        var orders = builder.AddProject<Project>("orders-api", launchProfileName: null)
            .WithHttpsEndpoint()
            .WithExternalHttpEndpoints();

        var apim = builder.AddAzureApiManagement("apim");
        apim.AddApi("orders", path: "v1/orders")
            .WithBackend(orders)
            .WithOperation("getAll", "GET", "/")
            .WithOperation("getById", "GET", "/{id}");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var (_, bicep) = await GetManifestWithBicep(apim.Resource);

        await Verify(bicep, "bicep");
    }

    [Fact]
    public void OutputReferencesAreAvailable()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var apim = builder.AddAzureApiManagement("apim");

        Assert.Equal("gatewayUrl", apim.Resource.GatewayUrl.Name);
        Assert.Equal("name", apim.Resource.NameOutputReference.Name);
        Assert.Equal("id", apim.Resource.Id.Name);
    }

    private sealed class Project : IProjectMetadata
    {
        public string ProjectPath => "project";
    }
}
