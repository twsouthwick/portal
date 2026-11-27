using k8s;

namespace Portal.Web.Services;

public static class KubernetesPortalProviderExtensions
{
    public static PortalProviderBuilder AddKubernetes(this PortalProviderBuilder portal)
    {
        return portal.AddProvider(_ =>
        {
            if (!HasKubernetesConfiguration())
            {
                return null;
            }

            var configuration = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"))
                ? KubernetesClientConfiguration.InClusterConfig()
                : KubernetesClientConfiguration.BuildDefaultConfig();
            return new KubernetesPortalProvider(new Kubernetes(configuration),
                portal.Host.Configuration["PORTAL_KUBERNETES_NAMESPACE"]);
        });
    }

    private static bool HasKubernetesConfiguration() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBECONFIG")) ||
        File.Exists(KubernetesClientConfiguration.KubeConfigDefaultLocation);
}
