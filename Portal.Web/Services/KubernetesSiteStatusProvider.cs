using System.Net;
using System.Text.RegularExpressions;
using k8s;
using k8s.Autorest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Portal.Web.Services;

public sealed partial class KubernetesSiteStatusProvider(
    IKubernetes kubernetes, ILogger? logger = null)
    : ISiteStatusProvider<KubernetesPortalSiteResource>
{
    private readonly ILogger _logger =
        logger ?? NullLogger<KubernetesSiteStatusProvider>.Instance;

    // @spec SITE-021 SITE-022 SITE-023 SITE-024 SITE-026
    public async ValueTask<SiteStatus> GetSiteStatusAsync(
        KubernetesPortalSiteResource site, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var spec = site.Spec ?? throw new InvalidDataException("PortalSite is missing its spec.");
        if (spec.WorkloadRef is not { } reference)
        {
            return SiteStatus.Available;
        }

        if (reference.Kind is not ("Deployment" or "StatefulSet" or "ReplicaSet") ||
            string.IsNullOrEmpty(reference.Name) || reference.Name.Length > 253 ||
            !WorkloadNamePattern().IsMatch(reference.Name) || reference.AdditionalProperties?.Count > 0)
        {
            throw new InvalidDataException($"PortalSite '{spec.Id}' has an invalid workloadRef.");
        }

        var resourceNamespace = site.Metadata?.NamespaceProperty;
        if (string.IsNullOrWhiteSpace(resourceNamespace))
        {
            throw new InvalidDataException($"PortalSite '{spec.Id}' is missing its namespace for workloadRef.");
        }

        int? replicas;
        try
        {
            switch (reference.Kind)
            {
                case "Deployment":
                    var deployment = await kubernetes.AppsV1.ReadNamespacedDeploymentAsync(
                        reference.Name, resourceNamespace, cancellationToken: cancellationToken);
                    replicas = (deployment?.Spec ??
                        throw InvalidWorkload(spec.Id, reference)).Replicas;
                    break;
                case "StatefulSet":
                    var statefulSet = await kubernetes.AppsV1.ReadNamespacedStatefulSetAsync(
                        reference.Name, resourceNamespace, cancellationToken: cancellationToken);
                    replicas = (statefulSet?.Spec ??
                        throw InvalidWorkload(spec.Id, reference)).Replicas;
                    break;
                default:
                    var replicaSet = await kubernetes.AppsV1.ReadNamespacedReplicaSetAsync(
                        reference.Name, resourceNamespace, cancellationToken: cancellationToken);
                    replicas = (replicaSet?.Spec ??
                        throw InvalidWorkload(spec.Id, reference)).Replicas;
                    break;
            }
        }
        catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "PortalSite '{SiteId}' references missing {WorkloadKind} '{Namespace}/{WorkloadName}'.",
                spec.Id, reference.Kind, resourceNamespace, reference.Name);
            return SiteStatus.Unavailable;
        }

        if (replicas < 0)
        {
            throw InvalidWorkload(spec.Id, reference);
        }

        return (replicas ?? 1) > 0 ? SiteStatus.Available : SiteStatus.Unavailable;
    }

    private static InvalidDataException InvalidWorkload(string siteId, KubernetesPortalWorkloadRef reference) =>
        new($"PortalSite '{siteId}' references {reference.Kind} '{reference.Name}' with invalid spec or replicas.");

    [GeneratedRegex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?(\\.[a-z0-9]([-a-z0-9]*[a-z0-9])?)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex WorkloadNamePattern();
}
