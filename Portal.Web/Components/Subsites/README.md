# Subsite Components

This folder contains a set of reusable Blazor components for displaying and managing subsites in the intranet portal.

## Components

### 1. SubsiteCard.razor
A card component that displays individual subsite information.

**Parameters:**
- `Subsite` (SubsiteInfo) - The subsite data to display
- `IsSelected` (bool) - Whether the card is currently selected
- `OnSelected` (EventCallback<SubsiteInfo>) - Callback when the card is clicked

**Features:**
- Displays subsite icon, name, description, and category
- Shows a lock badge for restricted subsites
- Visual feedback for selection and hover states
- Direct link to visit the subsite

### 2. SubsiteGrid.razor
A grid layout component that displays multiple subsite cards.

**Parameters:**
- `Subsites` (List<SubsiteInfo>) - List of subsites to display
- `SelectedSubsite` (SubsiteInfo?) - Currently selected subsite
- `OnSubsiteSelected` (EventCallback<SubsiteInfo>) - Callback when a subsite is selected

**Features:**
- Responsive grid layout (adapts to screen size)
- Manages selection state across cards
- Mobile-friendly single-column layout

### 3. SubsiteDetailsPane.razor
A slide-out pane that displays detailed information about a selected subsite.

**Parameters:**
- `Subsite` (SubsiteInfo?) - The subsite to display details for
- `IsOpen` (bool) - Whether the pane is visible
- `OnClose` (EventCallback) - Callback when the pane is closed

**Features:**
- Slide-in animation from the right
- Displays comprehensive subsite information:
  - Target audience
  - Category
  - Usage instructions
  - Key features
  - Contact information
  - Permission requirements
- Close button and overlay click to dismiss
- Mobile-responsive (full-width on small screens)

## Model

### SubsiteInfo.cs
Data model representing a subsite.

**Properties:**
- `Id` - Unique identifier
- `Name` - Display name
- `Description` - Brief description
- `Url` - Link to the subsite
- `IconClass` - CSS class for the icon (supports Bootstrap Icons)
- `Category` - Category/department
- `UsageInstructions` - Detailed usage guide
- `Features` - List of key features
- `Contact` - Contact information
- `RequiresPermission` - Access restriction flag
- `TargetAudience` - Intended users

## Usage Example

```razor
@page "/subsites"
@using Portal.Web.Models

<div class="container">
    <h1>Our Subsites</h1>
    
    <SubsiteGrid 
        Subsites="@subsites" 
        SelectedSubsite="@selectedSubsite"
        OnSubsiteSelected="@HandleSubsiteSelected" />
</div>

<SubsiteDetailsPane 
    Subsite="@selectedSubsite" 
    IsOpen="@isPaneOpen"
    OnClose="@CloseDetailsPane" />

@code {
    private List<SubsiteInfo> subsites = new();
    private SubsiteInfo? selectedSubsite;
    private bool isPaneOpen = false;

    protected override void OnInitialized()
    {
        // Load your subsites data
        subsites = new List<SubsiteInfo>
        {
            new SubsiteInfo
            {
                Id = "1",
                Name = "Employee Portal",
                Description = "Access your personal information",
                Url = "/employee",
                IconClass = "bi bi-person-circle",
                Category = "HR",
                // ... more properties
            }
        };
    }

    private void HandleSubsiteSelected(SubsiteInfo subsite)
    {
        selectedSubsite = subsite;
        isPaneOpen = true;
    }

    private void CloseDetailsPane()
    {
        isPaneOpen = false;
    }
}
```

## Styling

The components include scoped CSS with the following features:
- Modern card design with hover effects
- Smooth transitions and animations
- Responsive grid layout
- Mobile-friendly design
- Accessible color contrast
- Bootstrap icon support

## Customization

### Icons
The components use Bootstrap Icons by default. Update the `IconClass` property to use different icons:
- Bootstrap Icons: `"bi bi-icon-name"`
- Font Awesome: `"fas fa-icon-name"`
- Custom CSS classes

### Colors
The component uses CSS variables and can be customized by overriding the styles:
- Primary color: `#007bff`
- Hover effects: `box-shadow` and `transform`
- Selected state: `border-color` and `background-color`

### Grid Layout
The grid automatically adjusts based on screen size:
- Desktop: Multiple columns (minimum 280px per card)
- Mobile: Single column

## Dependencies

- Bootstrap 5+ (for styling and icons)
- Bootstrap Icons (for icon support)
- .NET 10 / Blazor

## Future Enhancements

Potential improvements:
- Search and filter functionality
- Sorting options
- Favorites/bookmarks
- Recently visited tracking
- Analytics integration
- Deep linking to details pane
- Keyboard navigation
- Accessibility improvements (ARIA labels)
