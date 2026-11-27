# Portal Experience Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. No `@spec` annotations were found for this segment.

- [x] **UX-001** When an authenticated user opens the portal home route, the application shall render site discovery through the interactive Blazor experience. Evidence: `Portal.Web/Components/Routes.razor:1-8`, `Portal.Web/Components/Pages/Home.razor:1-33`.
- [x] **UX-002** When the user supplies a search query or changes the site view mode, the UI shall filter or regroup the displayed site cards accordingly. Evidence: `Portal.Web/Components/SiteSearchBar.razor:1-53`, `Portal.Web/Components/SiteViewModeSelector.razor:1-56`, `Portal.Web/Components/SiteCategoryView.razor:1-43`, `Portal.Web/Components/SiteFrequencyView.razor:1-42`.
- [x] **UX-003** When browser information is available, the UI shall hide sites marked private when the browser is not in private mode and shall filter app options by supported platform. Evidence: `Portal.Web/Components/SiteProvider.razor:35-98`.
- [x] **UX-004** When an internal site is rendered with a remote-IP state, the site card shall be disabled. Evidence: `Portal.Web/Components/ConnectionState.razor:1-33`, `Portal.Web/Components/SiteCard.razor:43-56`.
- [x] **UX-005** When a user activates a site card, the UI shall attempt its configured app protocols and open the site's web URL if those attempts fail. Evidence: `Portal.Web/Components/SiteCard.razor:57-84`.
- [x] **UX-006** When a site has usage instructions, the UI shall make those instructions available through the usage modal. Evidence: `Portal.Web/Components/SiteCard.razor:25-37`, `Portal.Web/Components/UsageModal.razor:1-33`.
- [x] **UX-007** When the UI needs browser detection, app launching, or local-storage access, it shall use the browser-session abstraction backed by JavaScript interop. Evidence: `Portal.Web/Browser/IClientBrowserSession.cs:1-19`, `Portal.Web/Browser/JsClientBrowserSession.cs:1-163`.
- [x] **UX-008** When a user activates a platform-specific app-launch button and its protocol launch fails, the UI shall open the configured store destination as provided. The destination may be a URL or a store ID. Evidence: `Portal.Web/Components/SiteLauncher.razor:52-61`, `Portal.Web/Services/SiteInfo.cs:34-40`; test fixture with a bare store ID: `Portal.Web.Tests/KubernetesPortalProviderTests.cs:31-35`.
