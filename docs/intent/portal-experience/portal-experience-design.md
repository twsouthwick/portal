---
parent: high-level-design
prefix: UX
---
# Portal Experience

## Context and Design Philosophy

The web experience presents the composed site catalog through an interactive
Blazor Server UI. Browser capabilities affect which site and app options are
shown. Browser and JavaScript interop are isolated behind browser-session
interfaces; the components do not own provider source configuration.

## Discovery and Navigation

The home page renders the site grid. Users can search, switch view modes, and
group/order sites by the available categories and usage frequency. Cards
display site metadata and may open usage instructions in a Markdown modal.
Main layout provides the portal header and logout action.

### Category-first navigation (owner-approved 2026-11-28)

Initial catalog browsing shows category buttons and site counts instead of
all site cards. Selecting a category shows only that category's sites, with
a Back to categories action. Categories keep the web portal's alphabetical
ordering; uncategorized sites use the visible name `Other`. A site belonging
to multiple categories remains available in each. Search continues to filter
the catalog and counts; a category removed by filtering returns navigation to
the category chooser. Keyboard focus follows navigation and returns to the
activating category. Existing card launch and usage-modal behavior is unchanged.

Categories is the initial view, regardless of a previously saved view mode.
The existing Most Used view remains available as an explicit user choice.

### Owner-approved layout restoration

The Fluent UI v5 migration preserves the existing responsive grid and its
24px inter-card gap, rather than introducing a new layout. Cards fill their
grid row (at least 250px high), with launch actions at the bottom; longer
content can increase the row height without clipping. The header sizes to its
contents so its title, view selector, and sign-out action remain visible,
including when they stack on narrow screens. Category and site headings use
the v5 text component to retain a visual hierarchy above body descriptions.

The deployed regression was confirmed in the browser: v5 cards use
`height: fit-content` inside 250px wrappers, while the layout header defaults
to a 44px row and the old label `Typo` parameter is no longer supported.
Responsive column counts and the grid gap were working and are not changed.

### Version footer (owner-approved 2026-11-28)

Both the signed-in main layout and the signed-out layout end with a footer
showing `Version {version}`. The version is Portal.Web's MinVer SemVer: the
assembly informational version with any `+` build metadata (such as the
commit SHA appended by the .NET SDK) removed. A single `PortalVersion`
service supplies the value so the footer and the manifest `Portal-Version`
header (AUTH-023) always agree. Local unversioned builds show MinVer's
default pre-release version rather than hiding the footer.

**[owner-approved 2026-11-28]** Both layouts reserve a non-scrolling footer
row inside a viewport-height shell. The main content scrolls independently,
so the version remains visible even with a large catalog or long signed-out
content. The footer can wrap and respects mobile safe-area insets without
covering the content.

## Browser-Aware Presentation and Launch

`SiteProvider` obtains browser information after first render, filters private
sites based on incognito detection, filters app-store options by platform, and
provides site/browser state to descendants. `SiteCard` disables internal sites
when a remote-IP state is present. These are UI behaviors, not authorization
boundaries.

Clicking the site card attempts configured app protocols in order and opens the
site URL if those attempts fail. The separate app-launch button attempts its
configured protocol and opens the corresponding store URL if it fails. Click
frequency is stored in browser local storage. Browser detection and app launch
are implemented in `Portal.Web/ts/` and called through `IClientBrowserSession`.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Rendering model | Interactive Server Blazor components. **[inferred]** | Static pages or client-side SPA. | Configured in `Portal.Web/Program.cs` and component routes. |
| Browser boundary | Components consume `IClientBrowserSession`; TypeScript is bundled separately. **[inferred]** | Direct JS calls from each component. | Existing interface isolates JS interop (`Portal.Web/Browser/`). |
| Version display | Footer in every layout with MinVer SemVer, build metadata removed. **[owner-selected 2026-11-28]** | Signed-in layout only; SemVer plus commit SHA. | Owner chose SemVer only, shown on both main and signed-out pages. |
| Private/internal visibility | Filter or disable in the UI based on browser state. **[inferred]** | Enforce only on the server or expose all sites. | Observed in `SiteProvider.razor` and `SiteCard.razor`; security implications remain open. |

## Open Questions & Future Decisions

### Deferred

1. Is private-mode filtering a product requirement or only a current browser
   behavior?
2. What is the intended source and trust model for `RemoteIP`?
3. Should app launches fall back to the website, store link, or both for each
   platform?
4. What behavior should apply when local-storage or browser detection fails?
5. UI filtering, Markdown rendering, and launch interactions have no direct
   component/browser tests in the inspected test projects.

## References

- HLD: `docs/high-level-design.md` — Approach and System Design
- EARS: `docs/intent/portal-experience/portal-experience-specs.md`
- UI components: `Portal.Web/Components/`
- Browser abstractions: `Portal.Web/Browser/`
- Browser modules and styles: `Portal.Web/ts/`, `Portal.Web/css/`,
  `Portal.Web/webpack.config.js`, `Portal.Web/tsconfig.json`,
  `Portal.Web/package.json`
- Usage example: `Portal.Web/sites/site1/usage.md`
- Related access policy: `docs/intent/identity-and-access/identity-and-access-design.md`
