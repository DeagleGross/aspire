// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREAZURE003 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable ASPIRECOMPUTE002 // IComputeEnvironmentResource.GetHostAddressExpression is experimental.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.ApiManagement;
using Azure.Provisioning.Expressions;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Azure API Management resources to the application model.
/// </summary>
public static class AzureApiManagementExtensions
{
    private const string DefaultPublisherEmail = "noreply@aspire.local";
    private const string DefaultPublisherName = "Aspire";

    /// <summary>
    /// Adds an Azure API Management service resource to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The service is provisioned with the <c>StandardV2</c> SKU by default, which provisions in roughly a minute
    /// and supports production workloads. Override the SKU or any other property by calling
    /// <see cref="AzureProvisioningResourceExtensions.ConfigureInfrastructure{T}"/>.
    /// </para>
    /// <para>
    /// Use <see cref="AddApi"/> to register an API on the service and <see cref="WithBackend"/> to wire
    /// an Aspire compute resource as the backend.
    /// </para>
    /// <example>
    /// <code lang="C#">
    /// var orders = builder.AddProject&lt;Projects.OrdersApi&gt;("orders-api")
    ///     .WithExternalHttpEndpoints();
    ///
    /// var apim = builder.AddAzureApiManagement("apim");
    /// apim.AddApi("orders", path: "v1/orders").WithBackend(orders);
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExportIgnore(Reason = "v1 of the integration is publish-focused; polyglot ATS export will be added in a follow-up.")]
    public static IResourceBuilder<AzureApiManagementResource> AddAzureApiManagement(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.AddAzureProvisioning();

        var configureInfrastructure = (AzureResourceInfrastructure infrastructure) =>
        {
            var azureResource = (AzureApiManagementResource)infrastructure.AspireResource;

            var publisherEmailParam = new ProvisioningParameter("publisherEmail", typeof(string))
            {
                Description = "The email address of the owner of the API Management service.",
                Value = DefaultPublisherEmail
            };
            infrastructure.Add(publisherEmailParam);

            var publisherNameParam = new ProvisioningParameter("publisherName", typeof(string))
            {
                Description = "The name of the owner of the API Management service.",
                Value = DefaultPublisherName
            };
            infrastructure.Add(publisherNameParam);

            var skuAnnotation = azureResource.Annotations.OfType<AzureApiManagementSkuAnnotation>().LastOrDefault();
            var sku = skuAnnotation?.Sku ?? ApiManagementServiceSkuType.StandardV2;
            var capacity = skuAnnotation?.Capacity ?? 1;

            var service = new ApiManagementService(azureResource.GetBicepIdentifier())
            {
                Sku = new ApiManagementServiceSkuProperties
                {
                    Name = sku,
                    Capacity = capacity
                },
                PublisherEmail = publisherEmailParam,
                PublisherName = publisherNameParam,
                Tags = { { "aspire-resource-name", azureResource.Name } }
            };
            infrastructure.Add(service);

            // Walk every API child registered via AddApi(...). For each one we emit:
            //   - ApiManagementApi  (the API definition: name, path, protocols)
            //   - ApiManagementBackend  (a backend pointing at the Aspire compute resource)
            //   - ApiPolicy  (an inbound policy that does <set-backend-service backend-id="..."/>,
            //                 optionally merged with the user-provided policy XML)
            foreach (var apiChild in azureResource.Apis)
            {
                var apiBicepId = Infrastructure.NormalizeBicepIdentifier(apiChild.Name);

                var apimApi = new ApiManagementApi($"{apiBicepId}Api")
                {
                    Parent = service,
                    Name = apiChild.Name,
                    DisplayName = apiChild.Name,
                    Path = apiChild.Path,
                    Protocols = { ApiOperationInvokableProtocol.Https }
                };
                infrastructure.Add(apimApi);

                ApiManagementBackend? apimBackend = null;
                string? backendName = null;
                var backendAnnotation = apiChild.Annotations.OfType<AzureApiManagementBackendAnnotation>().SingleOrDefault();
                if (backendAnnotation is not null)
                {
                    var backendUrlExpression = ResolveBackendUrlExpression(backendAnnotation.Backend, infrastructure, apiBicepId);
                    backendName = $"{apiChild.Name}-backend";

                    apimBackend = new ApiManagementBackend($"{apiBicepId}Backend")
                    {
                        Parent = service,
                        Name = backendName,
                        Protocol = BackendProtocol.Http,
                        Uri = backendUrlExpression
                    };
                    infrastructure.Add(apimBackend!);
                }

                var userPolicyAnnotation = apiChild.Annotations.OfType<AzureApiManagementPolicyAnnotation>().SingleOrDefault();
                var policyXml = ComposePolicyXml(userPolicyAnnotation?.PolicyXml, backendName);

                if (policyXml is not null)
                {
                    var apiPolicy = new ApiPolicy($"{apiBicepId}Policy")
                    {
                        Parent = apimApi,
                        // APIM requires the ARM child name of a policy resource to be the literal "policy".
                        // The bicep identifier ($"{apiBicepId}Policy") is only the CDK-internal handle.
                        Name = "policy",
                        Value = policyXml,
                        Format = PolicyContentFormat.Xml
                    };
                    infrastructure.Add(apiPolicy);
                }

                var operationAnnotations = apiChild.Annotations.OfType<AzureApiManagementOperationAnnotation>().ToList();

                // If a backend is wired but the user declared no operations, register a catch-all GET so
                // requests to the API path actually reach the backend out of the box. Without any operation,
                // APIM returns 404 for every request — that's a poor default for the simple "proxy this project"
                // case the integration targets.
                if (operationAnnotations.Count == 0 && backendAnnotation is not null)
                {
                    operationAnnotations.Add(new AzureApiManagementOperationAnnotation(
                        name: "catchall",
                        method: "GET",
                        urlTemplate: "/*",
                        displayName: "Catch-all"));
                }

                foreach (var op in operationAnnotations)
                {
                    var opBicepId = Infrastructure.NormalizeBicepIdentifier($"{apiBicepId}_{op.Name}");
                    var apiOperation = new ApiOperation($"{opBicepId}Operation")
                    {
                        Parent = apimApi,
                        Name = op.Name,
                        DisplayName = op.DisplayName,
                        Method = op.Method,
                        UriTemplate = op.UrlTemplate
                    };

                    // APIM requires every {placeholder} in the URL template to have a matching templateParameter
                    // declaration on the operation, otherwise it rejects the deployment. We derive them from the
                    // template (e.g. /orders/{id} → one templateParameter named "id", string, required).
                    foreach (var paramName in ExtractTemplateParameters(op.UrlTemplate))
                    {
                        apiOperation.TemplateParameters.Add(new ParameterContract
                        {
                            Name = paramName,
                            TypeName = "string",
                            IsRequired = true
                        });
                    }

                    infrastructure.Add(apiOperation);
                }
            }

            infrastructure.Add(new ProvisioningOutput("gatewayUrl", typeof(string))
            {
                Value = BicepFunction.Interpolate($"https://{service.GatewayUri}")
            });

            infrastructure.Add(new ProvisioningOutput("name", typeof(string))
            {
                Value = service.Name
            });

            infrastructure.Add(new ProvisioningOutput("id", typeof(string))
            {
                Value = service.Id
            });
        };

        var resource = new AzureApiManagementResource(name, configureInfrastructure);

        // APIM only makes sense at publish time. In run mode the cloud APIM cannot
        // reach locally-running backends, so we register the resource builder for chaining
        // but skip adding it to the model (mirrors the Azure Front Door pattern).
        return builder.ExecutionContext.IsPublishMode
            ? builder.AddResource(resource)
            : builder.CreateResourceBuilder(resource);
    }

    /// <summary>
    /// Sets the SKU and capacity of the Azure API Management service. When not called, the integration
    /// defaults to <see cref="ApiManagementServiceSkuType.StandardV2"/> with capacity 1.
    /// </summary>
    /// <param name="builder">The Azure API Management resource builder.</param>
    /// <param name="sku">The APIM SKU.</param>
    /// <param name="capacity">The number of deployed units. Most SKUs require 1; Premium / PremiumV2 support more for HA.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// SKU choice has significant cost, provisioning-time, and feature implications. See the
    /// <see href="https://learn.microsoft.com/azure/api-management/api-management-features">feature comparison of API Management tiers</see>
    /// for details.
    /// </para>
    /// <example>
    /// <code lang="C#">
    /// using Azure.Provisioning.ApiManagement;
    ///
    /// var apim = builder.AddAzureApiManagement("apim")
    ///     .WithSku(ApiManagementServiceSkuType.BasicV2);
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExportIgnore(Reason = "Takes Azure.Provisioning.ApiManagement enum which is not ATS-compatible. A polyglot string-based overload may be added later.")]
    public static IResourceBuilder<AzureApiManagementResource> WithSku(
        this IResourceBuilder<AzureApiManagementResource> builder,
        ApiManagementServiceSkuType sku,
        int capacity = 1)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
        }

        return builder.WithAnnotation(new AzureApiManagementSkuAnnotation(sku, capacity), ResourceAnnotationMutationBehavior.Replace);
    }

    /// <summary>
    /// Registers an API on the Azure API Management service.
    /// </summary>
    /// <param name="builder">The Azure API Management resource builder.</param>
    /// <param name="name">The name of the API resource.</param>
    /// <param name="path">The URL path the API will be served from on the gateway (e.g. <c>"v1/orders"</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for the new API resource for chaining.</returns>
    /// <remarks>
    /// <example>
    /// <code lang="C#">
    /// var apim = builder.AddAzureApiManagement("apim");
    /// apim.AddApi("orders", path: "v1/orders").WithBackend(ordersApi);
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExportIgnore(Reason = "v1 of the integration is publish-focused; polyglot ATS export will be added in a follow-up.")]
    public static IResourceBuilder<AzureApiManagementApiResource> AddApi(
        this IResourceBuilder<AzureApiManagementResource> builder,
        [ResourceName] string name,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var api = new AzureApiManagementApiResource(name, path, builder.Resource);
        builder.Resource.AddApi(api);

        // The API child is a metadata/annotation carrier consumed by the parent's
        // configureInfrastructure callback — it is not provisioned independently and
        // therefore must not be added to the application model (azd would reject it).
        return builder.ApplicationBuilder.CreateResourceBuilder(api);
    }

    /// <summary>
    /// Wires an Aspire compute resource as the backend for an APIM API. Incoming requests to the API
    /// will be forwarded to the backend's external HTTP/HTTPS endpoint via a <c>set-backend-service</c> policy.
    /// </summary>
    /// <typeparam name="T">The type of the backend resource.</typeparam>
    /// <param name="builder">The APIM API resource builder.</param>
    /// <param name="backend">The backend resource (e.g. a project, container, or other compute resource with endpoints).</param>
    /// <returns>The same builder, for chaining.</returns>
    [AspireExportIgnore(Reason = "v1 of the integration is publish-focused; polyglot ATS export will be added in a follow-up.")]
    public static IResourceBuilder<AzureApiManagementApiResource> WithBackend<T>(
        this IResourceBuilder<AzureApiManagementApiResource> builder,
        IResourceBuilder<T> backend)
        where T : IComputeResource, IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(backend);

        if (builder.Resource.Annotations.OfType<AzureApiManagementBackendAnnotation>().Any())
        {
            throw new InvalidOperationException(
                $"API '{builder.Resource.Name}' already has a backend configured. Each API can only have one backend.");
        }

        return builder.WithAnnotation(new AzureApiManagementBackendAnnotation(backend.Resource));
    }

    /// <summary>
    /// Registers an HTTP operation on an APIM API. APIM matches incoming requests against operations;
    /// without at least one operation declared (or auto-injected via <see cref="WithBackend"/>) the API
    /// returns 404 for every request.
    /// </summary>
    /// <param name="builder">The APIM API resource builder.</param>
    /// <param name="name">A unique name for the operation within this API (becomes the ARM resource name).</param>
    /// <param name="method">The HTTP method, e.g. <c>"GET"</c>, <c>"POST"</c>.</param>
    /// <param name="urlTemplate">
    /// The URL template relative to the API path. Use <c>"/"</c> for the root, <c>"/{id}"</c> for path
    /// parameters, or <c>"/*"</c> as a wildcard catch-all.
    /// </param>
    /// <param name="displayName">Optional human-readable name shown in the APIM developer portal. Defaults to <paramref name="name"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <example>
    /// <code lang="C#">
    /// apim.AddApi("orders", path: "v1/orders")
    ///     .WithBackend(ordersApi)
    ///     .WithOperation("getAll", "GET", "/")
    ///     .WithOperation("getById", "GET", "/{id}");
    /// </code>
    /// </example>
    [AspireExportIgnore(Reason = "v1 of the integration is publish-focused; polyglot ATS export will be added in a follow-up.")]
    public static IResourceBuilder<AzureApiManagementApiResource> WithOperation(
        this IResourceBuilder<AzureApiManagementApiResource> builder,
        [ResourceName] string name,
        string method,
        string urlTemplate,
        string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(urlTemplate);

        if (builder.Resource.Annotations.OfType<AzureApiManagementOperationAnnotation>().Any(a => a.Name == name))
        {
            throw new InvalidOperationException(
                $"API '{builder.Resource.Name}' already has an operation named '{name}'. Operation names must be unique within an API.");
        }

        return builder.WithAnnotation(new AzureApiManagementOperationAnnotation(name, method, urlTemplate, displayName));
    }

    /// <summary>
    /// Attaches a raw APIM policy XML document to the API. The XML must be a complete <c>&lt;policies&gt;</c> document.
    /// </summary>
    /// <param name="builder">The APIM API resource builder.</param>
    /// <param name="policyXml">The full policy XML.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// If a backend is also configured via <see cref="WithBackend"/>, a <c>set-backend-service</c> instruction
    /// is automatically injected into the inbound section of the policy XML so requests are routed correctly.
    /// </remarks>
    [AspireExportIgnore(Reason = "v1 of the integration is publish-focused; polyglot ATS export will be added in a follow-up.")]
    public static IResourceBuilder<AzureApiManagementApiResource> WithPolicy(
        this IResourceBuilder<AzureApiManagementApiResource> builder,
        string policyXml)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(policyXml);

        if (builder.Resource.Annotations.OfType<AzureApiManagementPolicyAnnotation>().Any())
        {
            throw new InvalidOperationException(
                $"API '{builder.Resource.Name}' already has a policy configured. Each API can only have one policy.");
        }

        return builder.WithAnnotation(new AzureApiManagementPolicyAnnotation(policyXml));
    }

    private static BicepValue<Uri> ResolveBackendUrlExpression(
        IResourceWithEndpoints backend,
        AzureResourceInfrastructure infrastructure,
        string bicepIdPrefix)
    {
        var endpoint = backend.GetEndpoints()
            .Where(e => e.EndpointAnnotation.UriScheme is "http" or "https")
            .FirstOrDefault(e => e.EndpointAnnotation.IsExternal);

        if (endpoint is null)
        {
            throw new InvalidOperationException(
                $"Backend resource '{backend.Name}' does not have an external HTTP or HTTPS endpoint. " +
                "Azure API Management requires an external HTTP or HTTPS endpoint to forward requests. " +
                "Call .WithExternalHttpEndpoints() on the resource before adding it as an APIM backend.");
        }

        var computeEnv = backend.GetComputeEnvironment()
            ?? backend.GetDeploymentTargetAnnotation()?.ComputeEnvironment
            ?? throw new InvalidOperationException(
                $"Backend resource '{backend.Name}' does not have a compute environment. " +
                "Ensure a compute environment (e.g., Azure Container Apps) is configured in the application model.");

        // v1 only supports Azure Container Apps as the backend compute environment. Other Azure compute
        // environments (App Service, Functions) will be added in follow-ups.
        //
        // We deliberately avoid IComputeEnvironmentResource.GetHostAddressExpression here. That helper
        // composes a literal app name with a Bicep output reference, which serializes to a manifest value
        // like "orders-api.{env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN}". azd's manifest
        // interpolator currently mishandles such mixed literal+token values and emits malformed Bicep
        // (the curly brace gets double-escaped and the string is never terminated, producing BCP004).
        // Splitting the value into two pure-token / pure-literal module parameters and composing them
        // inside the Bicep module side-steps the bug.
        if (computeEnv is not AzureProvisioningResource computeEnvProvisioningResource)
        {
            throw new InvalidOperationException(
                $"Backend resource '{backend.Name}' uses a compute environment of type '{computeEnv.GetType().Name}' which is not supported by Azure API Management in this release. " +
                "Use AddAzureContainerAppEnvironment.");
        }

        var envDomainOutput = new BicepOutputReference("AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN", computeEnvProvisioningResource);
        var envDomainParam = envDomainOutput.AsProvisioningParameter(infrastructure, $"{bicepIdPrefix}_backendDomain");

        var appNameParam = new ProvisioningParameter($"{bicepIdPrefix}_backendAppName", typeof(string))
        {
            Description = $"The name of the {backend.Name} container app.",
            Value = backend.Name.ToLowerInvariant()
        };
        infrastructure.Add(appNameParam);

        var expr = BicepFunction.Interpolate($"{endpoint.Scheme}://{appNameParam}.{envDomainParam}");
        return new BicepValue<Uri>(((BicepExpression?)expr)!);
    }

    private static string? ComposePolicyXml(string? userPolicyXml, string? backendName)
    {
        if (userPolicyXml is null && backendName is null)
        {
            return null;
        }

        if (userPolicyXml is null)
        {
            // Backend wired but no user policy — emit a compact set-backend-service policy.
            // Single-line XML avoids any chance of newline characters interacting badly with
            // Bicep string serialization in downstream tools (azd consolidates module strings
            // into a single main.bicep, and embedded newlines have historically caused BCP004).
            return $"<policies><inbound><base /><set-backend-service backend-id=\"{backendName}\" /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>";
        }

        // Normalize whitespace in the user policy so the resulting Bicep string literal is single-line.
        var normalizedUserPolicy = CollapseWhitespace(userPolicyXml);

        if (backendName is null)
        {
            return normalizedUserPolicy;
        }

        const string inboundOpenTag = "<inbound>";
        var idx = normalizedUserPolicy.IndexOf(inboundOpenTag, StringComparison.Ordinal);
        if (idx < 0)
        {
            throw new InvalidOperationException(
                $"Policy XML provided via WithPolicy must contain an <inbound> element so that set-backend-service can be injected for the configured backend '{backendName}'.");
        }

        var insertionPoint = idx + inboundOpenTag.Length;
        var injected = $"<set-backend-service backend-id=\"{backendName}\" />";
        return normalizedUserPolicy.Insert(insertionPoint, injected);
    }

    private static string CollapseWhitespace(string xml)
    {
        // Collapse any sequence of whitespace (including newlines) between tags or attributes to a single space.
        // This keeps policy XML semantically equivalent while ensuring the serialized Bicep string stays on one line.
        var sb = new System.Text.StringBuilder(xml.Length);
        var inWhitespace = false;
        foreach (var ch in xml)
        {
            if (ch is ' ' or '\t' or '\r' or '\n')
            {
                if (!inWhitespace)
                {
                    sb.Append(' ');
                    inWhitespace = true;
                }
            }
            else
            {
                sb.Append(ch);
                inWhitespace = false;
            }
        }
        return sb.ToString().Replace("> <", "><").Trim();
    }

    private static IEnumerable<string> ExtractTemplateParameters(string urlTemplate)
    {
        // Pull out the names of {placeholder} segments in an APIM URL template. Wildcards (/*) are
        // not template parameters and must be skipped. Duplicates are returned once.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var matches = System.Text.RegularExpressions.Regex.Matches(urlTemplate, @"\{([^}]+)\}");
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            var name = m.Groups[1].Value;
            if (seen.Add(name))
            {
                yield return name;
            }
        }
    }
}
