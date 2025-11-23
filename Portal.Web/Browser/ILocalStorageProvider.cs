namespace Portal.Web.Browser;

/// <summary>
/// Service for browser localStorage operations with JSON serialization
/// </summary>
public interface ILocalStorageProvider
{
    /// <summary>
    /// Gets a value from localStorage and deserializes it from JSON
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="key">The storage key</param>
    /// <returns>The deserialized value, or null if not found or error occurred</returns>
    Task<T?> GetItemAsync<T>(string key);

    /// <summary>
    /// Serializes a value to JSON and saves it to localStorage
    /// </summary>
    /// <typeparam name="T">The type to serialize</typeparam>
    /// <param name="key">The storage key</param>
    /// <param name="value">The value to store</param>
    Task SetItemAsync<T>(string key, T value);

    /// <summary>
    /// Removes a value from localStorage
    /// </summary>
    /// <param name="key">The storage key</param>
    Task RemoveItemAsync(string key);

    /// <summary>
    /// Clears all values from localStorage
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Gets all keys from localStorage
    /// </summary>
    /// <returns>Array of all keys</returns>
    Task<string[]> GetKeysAsync();

    /// <summary>
    /// Checks if a key exists in localStorage
    /// </summary>
    /// <param name="key">The storage key</param>
    /// <returns>True if the key exists, false otherwise</returns>
    Task<bool> HasKeyAsync(string key);
}
