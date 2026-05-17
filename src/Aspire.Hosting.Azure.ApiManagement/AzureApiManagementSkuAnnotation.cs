// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable AZPROVISION001 // Azure.Provisioning.ApiManagement is for evaluation purposes only.
#pragma warning disable ASPIREAZURE003

using Aspire.Hosting.ApplicationModel;
using Azure.Provisioning.ApiManagement;

namespace Aspire.Hosting.Azure;

/// <summary>
/// Annotation that carries the desired Azure API Management SKU + capacity for the parent resource.
/// When absent, the integration defaults to <see cref="ApiManagementServiceSkuType.StandardV2"/> capacity 1.
/// </summary>
internal sealed class AzureApiManagementSkuAnnotation(ApiManagementServiceSkuType sku, int capacity) : IResourceAnnotation
{
    /// <summary>
    /// The APIM SKU.
    /// </summary>
    public ApiManagementServiceSkuType Sku { get; } = sku;

    /// <summary>
    /// The number of deployed units. Most SKUs support 1; Premium and PremiumV2 support more for HA / throughput.
    /// </summary>
    public int Capacity { get; } = capacity > 0
        ? capacity
        : throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
}
