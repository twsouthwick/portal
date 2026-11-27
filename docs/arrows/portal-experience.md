# Arrow: Portal Experience

Interactive site discovery, browser-aware presentation, usage guidance, and
launch behavior in the web portal.

## Status

**MAPPED** — first reconstructed 2026-11-27. Seven EARS requirements describe
current UI behavior; several have no direct component or browser tests.

## References

### HLD
- `docs/high-level-design.md` — Approach and System Design

### LLD
- `docs/intent/portal-experience/portal-experience-design.md`

### EARS
- `docs/intent/portal-experience/portal-experience-specs.md` — 7 specs

### Tests
- No direct component or browser automation tests were found for the web UI.
- Related server test: `Portal.Web.Tests/AuthenticationTests.cs`

### Code
- `Portal.Web/Components/`
- `Portal.Web/Browser/`
- `Portal.Web/ts/`
- `Portal.Web/css/`
- `Portal.Web/sites/site1/usage.md`

## Architecture

**Purpose:** Let authenticated users search, group, inspect, and launch the
sites made available by the catalog.

**Key components:** Interactive Blazor routes and layout; site provider/filter;
search, grouping, and card components; Markdown usage modal; browser-session
abstraction; TypeScript launch, detection, and storage modules.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Discovery, browser filtering, launch, usage | UX-001, UX-002, UX-003, UX-004, UX-005, UX-006, UX-007 | 7 | 0 | 0 known implementation gaps |

**Traceability:** UI code and tests have no `@spec` annotations. The inspected
test projects contain no direct web component/browser tests; the status is
based on implementation inspection.

## Key Findings

1. `SiteProvider` filters by roles, private browsing state, and platform
   (`Portal.Web/Components/SiteProvider.razor:61-98`).
2. Internal-site disabling depends on a remote-IP cascading value and is
   presentation behavior (`Portal.Web/Components/SiteCard.razor:43-56`).
3. App protocol attempts, store fallback, and site navigation are implemented
   in the site launcher/browser abstraction; no direct UI test was found.

## Work Required

### Must Fix
1. No confirmed implementation defect is asserted by this mapping.

### Should Fix
1. Confirm intended private/internal filtering and app-launch fallback policy.
2. Add component/browser tests and `@spec` annotations as those behaviors are
   next changed.

### Nice to Have
1. Verify Markdown content and browser failure behavior with UI-level tests.
