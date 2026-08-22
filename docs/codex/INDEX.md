# AI Status Index

This directory stores the single AI-facing operational status record for Codex and Claude Code continuity.

## Mandatory Status Entry

Before planning, editing, running commands beyond orientation, or answering project-status questions, every AI agent must read:

```text
docs/codex/STATUS.md
```

`STATUS.md` is the only AI-facing operational source of truth. This `INDEX.md` is only a navigation file; it does not replace reading `STATUS.md`.

## New Session Reading Order

1. `AGENTS.md`
2. `CLAUDE.md` when using Claude Code
3. `docs/codex/INDEX.md`
4. `docs/codex/STATUS.md`
5. Git state with `git pull` and `git status` when this is a Git repository

Do not use separate default `HANDOFF.md`, `TODO.md`, `DECISIONS.md`, `ENVIRONMENT.md`, `PROGRESS.zh-CN.md`, `PYTHON.md`, or `PAPER.md` records. Their concerns belong in `STATUS.md`.

## Before Editing

- Read the current snapshot in `STATUS.md`.
- Read the latest entries at the end of the `Session Log`.
- Check active TODOs, blockers, decisions, verification, and Git sync sections.
- If the task involves running code, changing environments, Python dependencies, local paths, or a different device, check the environment notes section and verify locally before relying on old results.
- Check `git status` before editing.

## Required Closeout

At the end of substantial work, always update:

- `STATUS.md`: current snapshot and a new timestamped session log entry appended at the end.
- `version/工作进度.md`: Chinese user-facing progress entry appended at the end.

Update `version/版本迭代记录.md` only when the project version or release changed.

The final reply should tell the user whether `git status` is clean and whether a commit is recommended before switching agents or devices.

## Old Record Migration

If this project contains old HandShake files such as `HANDOFF.md`, `TODO.md`, `DECISIONS.md`, `ENVIRONMENT.md`, `PROGRESS.zh-CN.md`, `PYTHON.md`, or `PAPER.md`, treat them as migration sources only.

Move useful content into `STATUS.md`:

- Handoff details -> latest session log entry.
- TODO lists -> `Active TODO`.
- Decisions -> `Decisions`.
- Environment and Python notes -> `Environment Notes`.
- Paper or writing notes -> relevant snapshot sections and TODOs.
- Chinese progress summary -> `version/工作进度.md`.

## Current State Files

- [Status](STATUS.md)
- [中文工作进度](../../version/工作进度.md)
- [中文版本迭代记录](../../version/版本迭代记录.md)

## Can Work Continue?

Work can continue when:

- `STATUS.md` has a clear current snapshot.
- The latest `Session Log` entry is understandable.
- Active TODOs, blockers, verification state, and next action are visible.
- Git state is known, or the absence of Git is explicitly noted.
- Any required environment recheck is stated.

If any of these are missing, inspect the repository directly, update `STATUS.md` with what can be verified, and tell the user what remains uncertain.
