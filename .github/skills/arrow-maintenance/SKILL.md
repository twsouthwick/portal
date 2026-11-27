---
name: arrow-maintenance
description: Use when orienting through Portal's docs/arrows maps, auditing spec-to-test/code coherence, updating map references or status, or restructuring mapped segments.
---

# Portal Arrow Maintenance

This project skill adapts the arrow-maintenance workflow from Linked-Intent
Development (LID) v1.4.7 for GitHub Copilot. It is not the upstream plugin and
does not provide its `/arrow-maintenance` command or automated phase gates.

## Orientation

1. Always begin with `docs/arrows/index.yaml`; its segment state is authoritative.
2. Open only the relevant segment document, then follow its HLD, LLD, EARS,
   tests, and code references.
3. Read `docs/arrows/README.md` for the selected segmentation and known
   discrepancies. Treat provisional design decisions as unresolved.

## Audit

When asked to audit the maps, check every segment and report:

- Broken index, detail, HLD, LLD, EARS, test, and code references.
- EARS IDs that are missing, duplicated, absent from the arrow coverage table,
  or not cited by tests/code where applicable.
- Implementation or test changes that indicate drift from the mapped EARS.
- Relevant source/test/code artifacts not referenced by any arrow document.
- Normative requirements embedded in a design document instead of its
  sibling EARS file.
- Current `status`, `next`, and `drift` fields compared with the evidence.

The current maps are `MAPPED`; no code or test files yet contain `@spec`
citations. Report this as missing traceability, not as passing citation
coverage. Do not advance a segment to `AUDITED` or `OK` until its EARS have
actually been checked against implementation and tests.

Apply only mechanically clear corrections to links and derived coverage.
Refresh `audited` and `audited_sha` only after a real audit. Surface behavior
choices, mismatches, and uncertain segment assignments for owner direction;
do not silently rewrite intent or hide discrepancies.

## Segment lifecycle

For an authorized split, merge, rename, or re-parent, update the LLD/spec files,
arrow docs, `docs/arrows/index.yaml`, and every affected `@spec` citation
together. Renames and re-parenting change path-based EARS namespaces; make
those changes atomically and check for stale IDs repository-wide.

## Brownfield remapping

The upstream `/map-codebase` flow is a Claude Code plugin command. Copilot
does not invoke that command or reproduce its automatic stops. If asked to
remap or expand Portal's scope, follow the manual sweep, lens, granularity,
reconciliation, generation, and verification phases in
`docs/lid/workflow.md`. Inspect every in-scope file and record evidence before
proposing boundaries. Do not generate new map artifacts for changed boundaries
until the owner has authorized them.

Upstream methodology: <https://github.com/jszmajda/lid> (v1.4.7).
