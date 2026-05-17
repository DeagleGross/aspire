// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure;

/// <summary>
/// Represents an Azure API Management service resource in the distributed application model.
/// </summary>
/// <remarks>
/// Azure API Management is a hybrid, multicloud management platform for APIs across all environments.
/// As an API gateway, it accepts API calls and routes them to the appropriate backend, while applying
/// configured policies (such as authentication, rate limiting, and transformations) along the way.
/// <para>
/// Use <see cref="AzureApiManagementExtensions.AddApi"/> to register APIs on this service and
/// <see cref="AzureApiManagementExtensions.WithBackend"/> to wire an Aspire compute resource as the backend.
/// </para>
/// </remarks>
/// <param name="name">The name of the resource.</param>
/// <param name="configureInfrastructure">Callback to configure the Azure resources.</param>
public class AzureApiManagementResource(string name, Action<AzureResourceInfrastructure> configureInfrastructure)
    : AzureProvisioningResource(name, configureInfrastructure)
{
    private readonly List<AzureApiManagementApiResource> _apis = [];

    /// <summary>
    /// Gets the "gatewayUrl" output reference exposing the gateway URL of the API Management service.
    /// </summary>
    public BicepOutputReference GatewayUrl => new("gatewayUrl", this);

    /// <summary>
    /// Gets the "name" output reference for the API Management service.
    /// </summary>
    public BicepOutputReference NameOutputReference => new("name", this);

    /// <summary>
    /// Gets the "id" output reference for the API Management service.
    /// </summary>
    public BicepOutputReference Id => new("id", this);

    /// <summary>
    /// Gets the APIs registered on this API Management service via <see cref="AzureApiManagementExtensions.AddApi"/>.
    /// </summary>
    public IReadOnlyList<AzureApiManagementApiResource> Apis => _apis;

    internal void AddApi(AzureApiManagementApiResource api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _apis.Add(api);
    }
}
