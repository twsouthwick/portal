# Portal Experience Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. No `@spec` annotations were found in the repository.

- [x] **UX-001** When an authenticated user opens the portal home route, the application shall render site discovery through the interactive Blazor experience.
- [x] **UX-002** When the user supplies a search query or changes the site view mode, the UI shall filter or regroup the displayed site cards accordingly.
- [x] **UX-003** When browser information is available, the UI shall filter private sites and platform-specific app options using that information.
- [x] **UX-004** When an internal site is rendered with a remote-IP state, the site card shall be disabled.
- [x] **UX-005** When a user opens a site card, the UI shall attempt configured app protocols and use configured web/store destinations as fallback.
- [x] **UX-006** When a site has usage instructions, the UI shall make those instructions available through the usage modal.
- [x] **UX-007** When the UI needs browser detection, app launching, or local-storage access, it shall use the browser-session abstraction backed by JavaScript interop.
