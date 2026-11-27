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
