namespace Portal.Web.Components;

/// <summary>
/// Defines how sites are displayed and organized
/// </summary>
public enum SiteViewMode
{
    None,

    /// <summary>
    /// Group sites by their categories
    /// </summary>
    Categories,

    /// <summary>
    /// Sort sites by frequency of use (most clicked first)
    /// </summary>
    Frequency
}
