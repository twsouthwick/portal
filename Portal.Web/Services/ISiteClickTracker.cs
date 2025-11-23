namespace Portal.Web.Services;

/// <summary>
/// Service for tracking site clicks
/// </summary>
public interface ISiteClickTracker
{
    /// <summary>
    /// Records a click for the specified site
    /// </summary>
    /// <param name="siteId">Unique identifier of the site that was clicked</param>
    Task RecordClickAsync(string siteId);

    /// <summary>
    /// Gets the total number of clicks for a specific site
    /// </summary>
    /// <param name="siteId">Unique identifier of the site</param>
    /// <returns>Total click count for the site</returns>
    Task<int> GetClickCountAsync(string siteId);

    /// <summary>
    /// Gets click counts for all tracked sites
    /// </summary>
    /// <returns>Dictionary mapping site IDs to click counts</returns>
    Task<IReadOnlyDictionary<string, int>> GetAllClickCountsAsync();
}
