using Portal.Web.Models;

namespace Portal.Web.Services;

/// <summary>
/// Service for managing subsite information
/// </summary>
public interface ISubsiteService
{
    /// <summary>
    /// Get all available subsites
    /// </summary>
    Task<List<SubsiteInfo>> GetAllSubsitesAsync();

    /// <summary>
    /// Get subsites by category
    /// </summary>
    Task<List<SubsiteInfo>> GetSubsitesByCategoryAsync(string category);

    /// <summary>
    /// Get a specific subsite by ID
    /// </summary>
    Task<SubsiteInfo?> GetSubsiteByIdAsync(string id);

    /// <summary>
    /// Get all unique categories
    /// </summary>
    Task<List<string>> GetCategoriesAsync();
}
