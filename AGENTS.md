# Project Instructions for AI Coding Agents

This project uses HandShake records so Codex and Claude Code can alternately take over the same repository without relying on previous chat history.

## Required Startup

Before substantial work, the active AI agent must:

1. Read this file.
2. If running in Claude Code, read `CLAUDE.md` if present.
3. Read `docs/codex/INDEX.md` for navigation.
4. Read `docs/codex/STATUS.md` as the mandatory single AI-facing source of truth. Do not rely on `INDEX.md` alone.
5. If this is a Git repository:
   a. Run `git pull` to synchronize with the remote before making any changes.
   b. Run `git status` to see branch, commit, staged, unstaged, and untracked files.
6. Identify whether this is a new session, Codex taking over from Claude Code, Claude Code taking over from Codex, same-tool continuation, or cross-device/cross-environment continuation.
7. Check local environment details only when the task involves execution, Python, dependencies, changed paths, changed devices, changed virtual environments, or `STATUS.md` says recheck is required.
8. Report the orientation files used and current Git/workflow risk before editing.
9. For non-trivial tasks, draft a phased plan and confirm with the user unless the user already gave clear implementation direction. Execute step by step.

## Required Status Logging

`docs/codex/STATUS.md` is the only AI-facing operational record.

The active agent must keep it current:

- Update the current snapshot near the top when goals, state, risks, TODOs, decisions, verification, or Git sync changes.
- Append every substantial session entry at the end of the `Session Log` section.
- Use `YYYY-MM-DD HH:MM` timestamps so same-day operations remain ordered.
- Record changed files, commands run, verification results, TODO changes, decisions, blockers, remaining issues, next steps, and Git status.
- Record environment notes inside `STATUS.md` only when a changed device, path, interpreter, virtual environment, dependency, command, or local setup issue matters.

Do not create or rely on separate default `HANDOFF.md`, `TODO.md`, `DECISIONS.md`, `ENVIRONMENT.md`, `PROGRESS.zh-CN.md`, `PYTHON.md`, or `PAPER.md` files. If old split files exist, migrate their useful information into `STATUS.md`.

## Required Closeout

Before ending substantial work, the active AI agent must:

1. Update `docs/codex/STATUS.md`.
2. Append a timestamped session log entry at the end of `STATUS.md`.
3. Append a timestamped Chinese user-facing entry at the end of `version/工作进度.md`.
4. Update `version/版本迭代记录.md` only when the project version or release changed.
5. Tell the user current Git status, whether the worktree is clean, and whether a commit is recommended before switching tools or devices.

The status update is mandatory. If the agent cannot update `STATUS.md`, it must say why before claiming the task is complete.

After each completed execution step, remind the user to consider a git commit before continuing.

## Workflow Priority

Use this priority order:

1. Current user request.
2. System and developer instructions.
3. This `AGENTS.md`.
4. Specialized project or domain skill.
5. `docs/codex/STATUS.md`.
6. General HandShake skill defaults.

If a specialized workflow has its own management records, use that workflow but keep `docs/codex/STATUS.md` updated or clearly cross-linked.

Do not treat `version/工作进度.md` or `version/版本迭代记录.md` as authoritative project state. They are derived summaries for Chinese-speaking users.

## Safety

- Do not overwrite user changes.
- Do not run destructive Git commands unless explicitly requested.
- Do not store secrets in repository files.
- Record local-only paths as local notes, not portable setup instructions.
- Do not reuse local environment assumptions from another device until they have been verified on the current device.

## File Handling

- 所有文件读写使用 UTF-8 编码，修改文件时不要改变原有编码。
- 在 PowerShell 中读取含中文的文件前先执行 `chcp 65001`，设置 UTF-8 输出，并使用 `Get-Content -Encoding UTF8`。
- 不要用 `sed` 或 `awk` 处理含中文的文件；需要机械处理时使用 Python 或 Node.js。
- 代码注释使用中文。

## Academic Writing Tasks

For papers, teaching designs, coursework, literature reviews, curriculum-standard analysis, and similar formal writing tasks:

- Keep writing state, source state, citation checks, unsupported claims, and next writing tasks in `docs/codex/STATUS.md`.
- Communicate with the user in Simplified Chinese unless asked otherwise.
- Write naturally and clearly at a graduate-student or teacher-training-student level; avoid stiff AI-style prose, excessive frameworks, repetitive phrasing, and inflated abstractions.
- Use bullets only when they help readability. Do not break ordinary prose into unnecessary lists.
- Never invent references, policy documents, curriculum standards, journal details, data, DOI, URL, author names, titles, years, volumes, issues, or pages.
- Mark unverifiable bibliographic details or claims as `待人工复核`.
- Verify online-checkable and time-sensitive information when available before making factual claims.

## Versioning

Management workflow skills and templates used by this project must be released by explicit version number and follow version control principles.
