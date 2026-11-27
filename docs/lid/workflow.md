# Linked-Intent Development workflow

This project follows Linked-Intent Development (LID) v1.4.7. The upstream methodology and plugin skills are maintained at [jszmajda/lid](https://github.com/jszmajda/lid); this project uses the upstream-supported instruction-file path for agents that do not load its plugins.

## Change workflow

Intent flows downstream: **HLD → LLD → EARS → tests → code**.

1. Before implementation, check the project HLD, relevant component design, EARS specifications, and tests for coherence. If an upstream document is incomplete or inconsistent, resolve that first.
2. Draft or update the design for the intended behavior. Review it before proceeding.
3. Write testable EARS requirements with stable IDs and review them. For an existing codebase, mark observed working behavior `[x]`, specified but incomplete behavior `[ ]`, and explicit non-wants `[D]`. Mark inferred decisions as `[inferred]` and record unexplained behavior as an open question.
4. Check for coverage gaps, contradictions, ambiguous scope, and interactions across component boundaries. Ask the owner to resolve choices that the evidence does not settle.
5. Add or update tests before implementation. Tests should cite the applicable EARS IDs with `@spec`.
6. Implement the approved behavior and add `@spec` annotations at the entry point that owns each specified behavior. Verify the tests and that design, specifications, tests, and implementation agree.

For a bug, locate where behavior diverges from intent and cascade the correction from that level. Do not silently change code to conform to an ambiguous specification. Within one component, cascade through its design, specifications, tests, and code; pause before changing intent owned by another component.

## Brownfield mapping

LID's `/arrow-maintenance:map-codebase` command is currently a Claude Code plugin workflow. It is not directly invocable in GitHub Copilot. When mapping through Copilot or another instruction-file agent, manually follow the same phases:

1. **Sweep:** inspect every file in the established full-project scope and capture file-level purpose, interfaces, dependencies, data, side effects, role, and notable behavior with path/line evidence. Do not segment during the sweep. If the full sweep exceeds the available capacity, make that limitation explicit and use separate documented batches; never silently sample.
2. **Choose a lens:** present 3–5 materially different clusterings, such as user capability, data flow, domain concept, or behavioral boundary. Avoid splitting only by frontend/backend, deployment unit, team, or generic utilities. Stop for the owner's choice.
3. **Choose granularity:** offer coarse (3–4), medium (6–8), and fine (10+) segment options, nesting related leaves under a grouping node only when that clarifies the design tree. Stop for the owner's choice.
4. **Reconcile:** present the resulting segment boundaries, evidence, and conflicts. The owner may approve, modify, combine, split, or reject them. Do not create map artifacts until the owner approves the final breakdown.
5. **Generate, with review after each artifact:** for each approved leaf, create its arrow document, skeleton LLD, EARS specifications, and `index.yaml` entry in that order. Use tree-mirrored paths under `docs/arrows/` and `docs/intent/`; grouping nodes are design nodes, not arrow segments. Mark current observed behavior separately from planned work. Then create `docs/high-level-design.md` if absent. Stop for review at each artifact stage.
6. **Reconcile and hand off:** verify every index and document link, segment reference coverage, and status against the inspected files. Clean up temporary sweep notes, configure the LID navigation, and ask the owner to flesh out each mapped skeleton through the change workflow above.

An arrow document is a navigation page, not a second design document. The leaf LLD owns its EARS specifications; spec IDs use the component path as a stable prefix. An arrow index records the design-tree relationships and map status. Start future work from `docs/arrows/index.yaml` and load only the relevant segment and its references.

Describe observed implementation separately from planned work. If documenting planned Digital Fortress work, label it as planned and do not infer that it is implemented; the current repository sweep found no Digital Fortress references.

## Inspection and status

Use the upstream arrow-maintenance meanings: `UNMAPPED`, `MAPPED`, `AUDITED`, `OK`, `PARTIAL`, `BROKEN`, `STALE`, `OBSOLETE`, and `MERGED`. A freshly reconstructed map is `MAPPED`, not `AUDITED` or `OK`, until its specifications have been checked against implementation. Keep index status authoritative; regenerate derived references and coverage from source scans rather than hand-editing them to disagree.

During reconciliation, check for broken links, uncovered specifications, stale code references, missing or unresolved `@spec` IDs, unreferenced artifacts, and requirements incorrectly embedded in design documents. Resolve only mechanically clear issues. Surface interpretation-dependent choices to the owner.

## Upstream and agent limitations

- LID upstream: [jszmajda/lid](https://github.com/jszmajda/lid), release `v1.4.7` (MIT-licensed).
- GitHub Copilot: root `AGENTS.md` supplies the cross-tool workflow; `.github/copilot-instructions.md` covers Copilot instruction surfaces. Project skills in `.github/skills/linked-intent-dev/SKILL.md` and `.github/skills/arrow-maintenance/SKILL.md` provide focused workflows on Copilot surfaces that support Agent Skills. Ask Copilot to use the `linked-intent-dev` skill for a code change or the `arrow-maintenance` skill to navigate/audit maps; skills are loaded when relevant, and explicit natural-language invocation is available. These are Portal-specific adapters, not copies of the upstream plugins. Copilot does not provide the upstream plugin slash commands or their enforced phase stops, so agents must follow the review gates described here.
- Claude Code: `CLAUDE.md` imports `AGENTS.md`. Install the upstream `linked-intent-dev` and `arrow-maintenance` plugins to use their skills, including `/arrow-maintenance:map-codebase`.
- Cursor can read `AGENTS.md` and supports the upstream LID plugins. opencode can load the upstream plugin skills using the absolute plugin paths in its `skills.paths` configuration. Other upstream-listed `AGENTS.md` readers (including Codex CLI, Amp, Jules, Pi, Zed, Cline, Junie, and Windsurf) can use the same project instructions; tool-specific plugin/skill installation is optional and is not included as a repository dependency.
- The upstream `lid-experimental` skills are not required for this project.
