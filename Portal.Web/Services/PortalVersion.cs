using System.Reflection;

namespace Portal.Web.Services;

/// <summary>
/// The Portal.Web version shown in the footer and the manifest <c>Portal-Version</c> header.
/// </summary>
/// @spec UX-012 AUTH-023
public sealed class PortalVersion(string informationalVersion)
{
    public const string HeaderName = "Portal-Version";

    public string Value { get; } = informationalVersion.Split('+', 2)[0];

    public static PortalVersion FromAssembly(Assembly assembly) =>
        new(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0");
}
