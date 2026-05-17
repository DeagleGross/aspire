// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure;

/// <summary>
/// Annotation carrying a raw APIM policy XML to be attached to an API.
/// </summary>
internal sealed class AzureApiManagementPolicyAnnotation(string policyXml) : IResourceAnnotation
{
    /// <summary>
    /// Gets the raw APIM policy XML.
    /// </summary>
    public string PolicyXml { get; } = !string.IsNullOrEmpty(policyXml)
        ? policyXml
        : throw new ArgumentException("Policy XML must not be null or empty.", nameof(policyXml));
}
