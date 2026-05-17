// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an API registered on an <see cref="Azure.AzureApiManagementResource"/>.
/// </summary>
/// <remarks>
/// Created via <see cref="AzureApiManagementExtensions.AddApi"/>. Wire a backend with
/// <see cref="AzureApiManagementExtensions.WithBackend"/> and attach a policy with
/// <see cref="AzureApiManagementExtensions.WithPolicy"/>.
/// </remarks>
public class AzureApiManagementApiResource : Resource, IResourceWithParent<Azure.AzureApiManagementResource>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureApiManagementApiResource"/> class.
    /// </summary>
    /// <param name="name">The name of the API resource.</param>
    /// <param name="path">The URL path the API will be served from on the API Management gateway (e.g. "v1/orders").</param>
    /// <param name="parent">The parent <see cref="Azure.AzureApiManagementResource"/>.</param>
    public AzureApiManagementApiResource(string name, string path, Azure.AzureApiManagementResource parent)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(parent);

        Path = path;
        Parent = parent;
    }

    /// <summary>
    /// Gets the URL path the API is served from on the gateway.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the parent <see cref="Azure.AzureApiManagementResource"/>.
    /// </summary>
    public Azure.AzureApiManagementResource Parent { get; }
}
