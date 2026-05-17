// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure;

/// <summary>
/// Annotation describing an operation to register on an Azure API Management API.
/// </summary>
internal sealed class AzureApiManagementOperationAnnotation : IResourceAnnotation
{
    public AzureApiManagementOperationAnnotation(string name, string method, string urlTemplate, string? displayName)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(urlTemplate);

        Name = name;
        Method = method.ToUpperInvariant();
        UrlTemplate = urlTemplate;
        DisplayName = displayName ?? name;
    }

    /// <summary>
    /// The Aspire-side name of the operation. Used as the ARM resource name (must be unique within the parent API).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The HTTP method, e.g. <c>GET</c>, <c>POST</c>.
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// The URL template relative to the API path (e.g. <c>/</c>, <c>/{id}</c>, <c>/*</c> for a catch-all).
    /// </summary>
    public string UrlTemplate { get; }

    /// <summary>
    /// Human-readable display name shown in the APIM portal.
    /// </summary>
    public string DisplayName { get; }
}
