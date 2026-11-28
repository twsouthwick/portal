# Portal arrow navigation

This Full LID overlay maps the current repository into seven behavior-oriented
leaf segments. Start with [`index.yaml`](index.yaml), then open the segment's
detail document and follow its HLD, LLD, EARS, test, and code references.

All segments are initially `MAPPED`: their current structure and behavior have
been reverse-engineered, but the owner has not confirmed the inferred design
decisions and the specs are not yet mechanically linked to tests or code.
`audited` and `audited_sha` therefore remain null. Do not treat the coverage
summaries as a passing coherence audit.

The breakdown is an authorized agent-selected medium slicing. It groups code
by user-visible behavior and runtime responsibility rather than by individual
directories. Cross-segment dependencies are documented in the relevant LLDs.

## Segmentation choices

The selected lens is **behavioral boundary / user capability**: identity and
access, site discovery/data, portal interaction, native shell, local runtime,
Kubernetes deployment, and build/delivery. It keeps implementation layers
together when they jointly provide one behavior and separates independently
verifiable behavior. Alternatives considered:

| Lens | Likely result | Why not selected |
|---|---|---|
| Data flow | Identity claims → site sources → UI/content → client and deployment | Useful for tracing data, but less direct for feature-level change ownership. |
| Domain concepts | Identity, site, portal, client, runtime, release | Reasonable alternative; runtime and release boundaries still need a separate operational view. |
| User-facing capability | Sign in, discover sites, open content, launch apps, use native shell | Strong for user journeys, but deployment/build responsibilities do not map cleanly to user capabilities. |
| Deployment/artifact groups | .NET server, Tauri client, Helm, workflows | Rejected as the primary lens: it groups by implementation/deployment location rather than independent behavior. |

For granularity, **medium (7 leaves)** is selected. Coarse (3–4 leaves) would
combine identity/catalog/experience and could hide authorization seams; fine
(10+ leaves) could split individual providers, UI capabilities, and platform
jobs into small maps that increase cross-links and maintenance cost. The
selected seven leaves keep each segment traceable without introducing
sub-HLDs.

## Maintaining this map

For a change, begin at the index, load the affected segment's detail doc and
references, then cascade the change from HLD through LLD and EARS to tests and
implementation. Add `@spec` citations in tests and implementation as those
segments are brought under change. Regenerate references and coverage from
source scans during review; do not mark a segment `AUDITED` or `OK` until its
specification-to-test/code coherence has actually been checked.

When a change crosses a segment boundary, review both segments and resolve
unsettled intent before implementation. Update this index and the affected
arrow documents in the same change. A rename or re-parenting must update
path-concatenated spec IDs and every citation atomically.

## Known deferred discrepancies

- `AGENTS.md` describes .NET 10 and says there are no test projects; the
  project files and test directory show .NET 11 and an xUnit test project.
- Root README examples refer to `Portal.Web/TypeScript/`, while current sources
  are in `Portal.Web/ts/`.
- The broader product intent behind the label “Digital Fortress” is not
  represented in the inspected repository; no implementation is claimed.

These discrepancies are recorded for later reconciliation; their intended
resolution is not inferred by this map.
