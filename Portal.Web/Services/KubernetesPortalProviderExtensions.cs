using k8s;

namespace Portal.Web.Services;

public static class KubernetesPortalProviderExtensions
{
    // @spec SITE-005 SITE-026
    public static PortalProviderBuilder AddKubernetes(this PortalProviderBuilder portal)
    {
        return portal.AddProvider(services =>
        {
            if (!HasKubernetesConfiguration())
            {
                return null;
            }

            var configuration = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"))
                ? KubernetesClientConfiguration.InClusterConfig()
                : KubernetesClientConfiguration.BuildDefaultConfig();
            var client = new Kubernetes(configuration);
            return new KubernetesPortalProvider(client,
                logger: services.GetRequiredService<ILogger<KubernetesPortalProvider>>(),
                siteStatusProvider: services.GetService<ISiteStatusProvider<KubernetesPortalSiteResource>>() ??
                    new KubernetesSiteStatusProvider(client,
                        services.GetRequiredService<ILogger<KubernetesSiteStatusProvider>>()));
        });
    }

    private static bool HasKubernetesConfiguration() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBECONFIG")) ||
        File.Exists(KubernetesClientConfiguration.KubeConfigDefaultLocation);
}
