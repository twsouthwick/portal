using Portal.Web.Browser;

namespace Portal.Web.Services;

/// <summary>
/// LocalStorage-based implementation of site click tracking
/// </summary>
public sealed class LocalStorageSiteClickTracker : ISiteClickTracker
{
    private const string StorageKey = "portal:site-clicks";
    private readonly ILocalStorageProvider _storage;
    private readonly ILogger<LocalStorageSiteClickTracker> _logger;

    public LocalStorageSiteClickTracker(
        ILocalStorageProvider storage,
        ILogger<LocalStorageSiteClickTracker> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task RecordClickAsync(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            _logger.LogWarning("Attempted to record click with null or empty site ID");
            return;
        }

        try
        {
            var data = await GetClickDataAsync();
            var currentCount = data.ClickCounts.GetValueOrDefault(siteId, 0);
            var newCount = currentCount + 1;
            data.ClickCounts[siteId] = newCount;

            await _storage.SetItemAsync(StorageKey, data);
            _logger.LogInformation("Recorded click for site '{SiteId}'. Total clicks: {Count}", siteId, newCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record click for site '{SiteId}'", siteId);
        }
    }

    public async Task<int> GetClickCountAsync(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return 0;
        }

        try
        {
            var data = await GetClickDataAsync();
            return data.ClickCounts.GetValueOrDefault(siteId, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get click count for site '{SiteId}'", siteId);
            return 0;
        }
    }

    public async Task<IReadOnlyDictionary<string, int>> GetAllClickCountsAsync()
    {
        try
        {
            var data = await GetClickDataAsync();
            return data.ClickCounts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all click counts");
            return new Dictionary<string, int>();
        }
    }

    private async Task<SiteClickData> GetClickDataAsync()
    {
        var data = await _storage.GetItemAsync<SiteClickData>(StorageKey);
        return data ?? new SiteClickData();
    }

    private sealed class SiteClickData
    {
        public Dictionary<string, int> ClickCounts { get; set; } = new();
    }
}
