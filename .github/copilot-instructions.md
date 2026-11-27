# Project instructions

This repository uses Full Linked-Intent Development (LID). Follow `AGENTS.md` and `docs/lid/workflow.md` for code changes. The workflow is HLD → LLD → EARS specifications → tests → code; inspect each phase before proceeding, and treat design documents as the source of truth.

Use the project skill `linked-intent-dev` for code changes and `arrow-maintenance` to navigate or audit the maps. They are Copilot Agent Skills under `.github/skills/`; explicitly ask for either by name when it is not selected automatically. See `docs/lid/README.md` for setup and limitations.

For brownfield mapping, read all in-scope sources rather than sampling. Present alternative component lenses and slicing sizes, then pause for the owner's approval of the final breakdown before generating maps. Do not claim uninspected coverage. When maps exist, begin at `docs/arrows/index.yaml` and follow each segment's references.

Copilot does not run LID's Claude Code `/map-codebase` command. Use the documented manual phase gates instead; never imply that the plugin command or its automatic stops are available in Copilot.
