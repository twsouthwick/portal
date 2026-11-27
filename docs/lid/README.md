# LID in Portal

Portal uses **Full LID**: the whole repository is in scope, including the .NET application, Aspire and shared defaults, native Tauri client, tests, CI, and deployment configuration.

## Version and setup

The project instructions follow [Linked-Intent Development](https://github.com/jszmajda/lid) v1.4.7. The upstream project is MIT-licensed; Portal does not vendor plugin code or add runtime/build dependencies. `workflow.md` is a concise project adaptation, not a copy of the full generated upstream workflow document.

- GitHub Copilot and other agents that support `AGENTS.md` read the root `AGENTS.md`.
- Copilot instruction surfaces that do not read `AGENTS.md` also receive `.github/copilot-instructions.md`.
- Copilot Agent Skills are available as project skills in `.github/skills/`. Ask Copilot to use `linked-intent-dev` for a code change or `arrow-maintenance` to orient/audit the maps. The skills are local adapters to the workflow here, not the upstream plugin package.
- `CLAUDE.md` imports the same `AGENTS.md`, avoiding duplicate instructions.
- In Claude Code, install both upstream plugins and invoke the setup skill:

  ```text
  /plugin marketplace add jszmajda/lid
  /plugin install linked-intent-dev@jszmajda-lid
  /plugin install arrow-maintenance@jszmajda-lid
  /linked-intent-dev
  ```

- Cursor supports the same plugins; follow the upstream [Cursor setup](https://github.com/jszmajda/lid/blob/v1.4.7/docs/setup.md#cursor). The upstream setup guide documents other agent integrations: [docs/setup.md](https://github.com/jszmajda/lid/blob/v1.4.7/docs/setup.md).
- For opencode, load those upstream plugin directories through its `skills.paths` setting as described in the setup guide. Other agents that natively read `AGENTS.md` need no additional adapter.
- Copilot has no direct `/map-codebase` command. Follow the manual, approval-gated mapping phases in [`workflow.md`](workflow.md). Do not claim automatic plugin behavior in Copilot.

## Map status

The delegated brownfield sweep has been followed by an agent-selected
behavior-oriented medium slicing with seven leaf segments. The owner authorized
the agent to make the best choices and document assumptions; the exact
boundaries and inferred design decisions are therefore recorded as provisional
for later reconciliation. The maps are `MAPPED`, not `AUDITED` or `OK`.

Start at [`docs/arrows/index.yaml`](../arrows/index.yaml), then follow the
segment documents under `docs/arrows/` to their designs and EARS specifications
under `docs/intent/`. [`docs/arrows/README.md`](../arrows/README.md) records
the selected lens/granularity and alternatives. Existing discrepancies are
listed there and in the relevant LLDs for later resolution.

## Maintaining maps for a future change

For a representative change to Kubernetes-backed site content:

1. Start at `docs/arrows/index.yaml` and follow the segment reference for the behavior. Read the linked LLD, EARS file, tests, and implementation.
2. Check that the intended change is consistent with the HLD and the segment's current design. Update the design and EARS requirement first, then inspect and approve them.
3. Add a test that cites the requirement ID with `@spec` and verify the test fails for the expected reason before changing implementation.
4. Implement the behavior with a matching `@spec` annotation at its owning entry point. Run the existing targeted tests and the repository's applicable build checks.
5. Update the segment's arrow references, spec coverage, and index status. Check that every link resolves, every cited ID exists, and no new behavior is left without a specification or test. If the change crosses a segment boundary, stop for the owner's decision before cascading into that segment.

These are maintenance instructions, not a claim that Portal already has a Kubernetes content segment or coverage map; the approved breakdown will establish those names and relationships.
