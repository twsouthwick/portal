# Arrow: Portal Experience

Interactive site discovery, browser-aware presentation, usage guidance, and
launch behavior in the web portal.

## Status

**MAPPED** — first reconstructed 2026-11-27. Thirteen EARS requirements describe
current UI behavior, including the version footer (UX-012); several have no direct component or browser tests.
The owner approved category-first browsing and always-visible footers on
2026-11-28; updated UX-012 and new UX-013 are implemented.

## References

### HLD
- `docs/high-level-design.md` — Approach and System Design

### LLD
- `docs/intent/portal-experience/portal-experience-design.md`

### EARS
- `docs/intent/portal-experience/portal-experience-specs.md` — 13 specs

### Tests
- `Portal.Web.Tests/PortalExperienceTests.cs` — static component rendering
  regression for card height and heading markup (UX-009, UX-011), version
  metadata stripping, the signed-out version footer (UX-012), and category
  rendering, selection/Back, and filtered membership events (UX-013).
- No browser automation tests cover viewport sizing or computed layout.
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
| Discovery, browser filtering, launch, usage | UX-001, UX-002, UX-003, UX-004, UX-005, UX-006, UX-007, UX-008 | 8 | 0 | 0 known implementation gaps |
| Card layout, header sizing, typography | UX-009, UX-010, UX-011 | 3 | 0 | Automated computed-layout and viewport coverage missing |
| Version footer | UX-012 | 1 | 0 | Browser geometry checked; main-layout rendering, automated viewport, and device safe-area coverage remain missing |
| Category-first navigation | UX-013 | 1 | 0 | Rendering and event regressions cover categories, counts, selection/Back, and filtered membership; actual browser focus verification remains pending |

**Traceability:** Layout entry points cite UX-009–011; the component rendering
test cites UX-009 and UX-011. `VersionFooter.razor` and
`PortalVersion.cs` cite UX-012; tests cover the signed-out layout, while the
main-layout footer is not rendered by a test. UX-013 is cited by the layout,
view selector, category component, and rendering/event tests.
UX-001–008 still lack direct UI test citations.
Computed layout was checked using a temporary browser preview, not automated
viewport tests; the segment has not been audited.

The always-visible footer was checked at 320×640 and 800×600 using the actual
signed-out page and the main shell's scoped CSS with synthetic long content,
including a wrapped 64-character version. These are manual browser checks,
not persisted viewport automation or native safe-area verification.

## Key Findings

1. `SiteProvider` filters by roles, private browsing state, and platform
   (`Portal.Web/Components/SiteProvider.razor:61-98`).
2. Internal-site disabling depends on a remote-IP cascading value and is
   presentation behavior (`Portal.Web/Components/SiteCard.razor:43-56`).
3. App protocol attempts, store fallback, and site navigation are implemented
   in the site launcher/browser abstraction; no direct UI test was found.
4. Fluent UI v5 cards require explicit row filling; header height is automatic,
   and category/site headings use the supported text component. The repair
   preserves grid breakpoints and its 24px gap.

## Work Required

### Must Fix
1. No confirmed implementation defect is asserted by this mapping.

### Should Fix
1. Confirm intended private/internal filtering and app-launch fallback policy.
2. Add component/browser tests and `@spec` annotations as those behaviors are
   next changed.

### Nice to Have
1. Verify Markdown content and browser failure behavior with UI-level tests.
