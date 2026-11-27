---
name: linked-intent-dev
description: Use for code changes in Portal. Follow the repository's Full Linked-Intent Development workflow from design and EARS specifications through tests and implementation.
---

# Portal Linked-Intent Development

This project skill adapts Linked-Intent Development (LID) v1.4.7 for GitHub
Copilot. It complements the always-on instructions in `AGENTS.md`; read
`docs/lid/workflow.md` for the complete project workflow.

## Before changing code

1. Start at `docs/arrows/index.yaml`. Read the selected segment's arrow
   document, then follow its HLD, LLD, EARS, tests, and implementation
   references.
2. Check that the HLD, LLD, EARS specifications, and tests are coherent for the
   proposed change. Treat the design and specifications as the source of
   truth, and treat `[inferred]` decisions and open questions as unresolved.
3. If the behavior is not specified or its intent is ambiguous, present the
   evidence and alternatives to the owner. Do not settle an intent decision on
   the owner's behalf. For a change crossing segment boundaries, pause for the
   owner's direction before cascading into the other segment.

## Change sequence

1. Update or create the design for the intended behavior and present it for
   review.
2. Update or add testable EARS requirements in the owning segment's
   `*-specs.md` file. Use that LLD's declared `prefix:` and status markers:
   `[x]` observed working, `[ ]` specified but incomplete, `[D]` explicit
   non-want. Review the requirements before proceeding.
3. Add or update tests before implementation. Verify a new or changed test
   fails for the expected reason. Tests cite their EARS IDs with `@spec`.
4. Implement the approved behavior and put `@spec` citations at the behavior's
   owning entry point, not every helper.
5. Run the existing relevant tests/builds. Verify the documents, tests, and
   implementation agree. Update the segment arrow and `index.yaml` in the same
   change when references, coverage, or status change.

For bugs, locate where behavior diverges from intent and cascade from that
point. Do not short-circuit directly to a code-only fix.

## Brownfield boundaries

Portal's maps describe observed behavior, not an assertion that every current
behavior is desired. Preserve evidence and distinguish implementation from
planned work. Do not mark a segment `AUDITED` or `OK` merely because its map
exists. Copilot does not run LID's upstream Claude Code `/map-codebase`
command or enforce its automatic review stops; follow the manual review gates
in `docs/lid/workflow.md`.

Upstream methodology: <https://github.com/jszmajda/lid> (v1.4.7).
