// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure;

/// <summary>
/// Annotation associating an Aspire compute resource with an APIM API as its backend.
/// </summary>
internal sealed class AzureApiManagementBackendAnnotation(IResourceWithEndpoints backend) : IResourceAnnotation
{
    /// <summary>
    /// Gets the backend resource that incoming API requests will be forwarded to.
    /// </summary>
    public IResourceWithEndpoints Backend { get; } = backend ?? throw new ArgumentNullException(nameof(backend));
}
