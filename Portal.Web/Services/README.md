# Subsite Service - Dependency Injection

## Overview
The subsite data has been refactored to use dependency injection, following best practices for Blazor applications. This provides better separation of concerns, testability, and maintainability.

## Architecture

### Service Interface: `ISubsiteService`
Located at: `Portal.Web\Services\ISubsiteService.cs`

Defines the contract for subsite data operations:
- `GetAllSubsitesAsync()` - Retrieves all subsites
- `GetSubsitesByCategoryAsync(string category)` - Filters subsites by category
- `GetSubsiteByIdAsync(string id)` - Gets a specific subsite
- `GetCategoriesAsync()` - Returns all unique categories

### Service Implementation: `SubsiteService`
Located at: `Portal.Web\Services\SubsiteService.cs`

Current implementation uses in-memory data storage. This can be easily replaced with:
- Database access (Entity Framework Core)
- REST API calls
- Azure Table Storage
- Any other data source

## Registration

The service is registered in `Program.cs`:

```csharp
builder.Services.AddSingleton<ISubsiteService, SubsiteService>();
```

**Singleton Lifetime**: The service is registered as a singleton because the data is static and shared across all users. If you need user-specific data or database access, consider using `Scoped` lifetime instead:

```csharp
builder.Services.AddScoped<ISubsiteService, SubsiteService>();
```

## Usage in Components

### Inject the Service
```razor
@inject ISubsiteService SubsiteService
```

### Use in Code
```csharp
protected override async Task OnInitializedAsync()
{
    var subsites = await SubsiteService.GetAllSubsitesAsync();
    var categories = await SubsiteService.GetCategoriesAsync();
}
```

## Benefits

1. **Separation of Concerns**: UI components don't contain business logic or data
2. **Testability**: Easy to mock the service for unit testing
3. **Flexibility**: Swap implementations without changing components
4. **Reusability**: Multiple components can use the same service
5. **Maintainability**: Data logic is centralized in one place

## Future Enhancements

### Database Integration
Replace `SubsiteService` with a database-backed implementation:

```csharp
public class DatabaseSubsiteService : ISubsiteService
{
    private readonly ApplicationDbContext _context;
    
    public DatabaseSubsiteService(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<SubsiteInfo>> GetAllSubsitesAsync()
    {
        return await _context.Subsites.ToListAsync();
    }
    
    // ... other methods
}
```

### API Integration
Call an external API:

```csharp
public class ApiSubsiteService : ISubsiteService
{
    private readonly HttpClient _httpClient;
    
    public ApiSubsiteService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<List<SubsiteInfo>> GetAllSubsitesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<SubsiteInfo>>("/api/subsites");
    }
    
    // ... other methods
}
```

### Caching
Add caching for better performance:

```csharp
public class CachedSubsiteService : ISubsiteService
{
    private readonly ISubsiteService _innerService;
    private readonly IMemoryCache _cache;
    
    public CachedSubsiteService(ISubsiteService innerService, IMemoryCache cache)
    {
        _innerService = innerService;
        _cache = cache;
    }
    
    public async Task<List<SubsiteInfo>> GetAllSubsitesAsync()
    {
        return await _cache.GetOrCreateAsync("all-subsites", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await _innerService.GetAllSubsitesAsync();
        });
    }
}
```

## Testing

With dependency injection, components are easy to test:

```csharp
public class HomeTests
{
    [Fact]
    public async Task LoadsSubsitesOnInitialization()
    {
        // Arrange
        var mockService = new Mock<ISubsiteService>();
        mockService.Setup(s => s.GetAllSubsitesAsync())
            .ReturnsAsync(new List<SubsiteInfo> { /* test data */ });
        
        // Act & Assert
        using var ctx = new TestContext();
        ctx.Services.AddSingleton(mockService.Object);
        var component = ctx.RenderComponent<Home>();
        
        // Verify component rendered with data
    }
}
```

## Migration Notes

Components now use `OnInitializedAsync()` instead of `OnInitialized()` to support asynchronous data loading. This is a Blazor best practice when working with external data sources.
