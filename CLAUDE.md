# Claude Code Project Entry

@AGENTS.md

## Claude Code Startup

Before making substantive edits, Claude Code must:

1. Read `docs/codex/INDEX.md` for navigation.
2. Read `docs/codex/STATUS.md` as the mandatory single AI-facing source of truth. Do not rely on `INDEX.md` alone.
3. If this is a Git repository:
   a. Run `git pull` to sync with the remote before making changes.
   b. Run `git status` before editing.
4. Identify whether Claude Code is taking over work last handled by Codex, continuing its own previous work, or resuming on a different device, directory, or virtual environment.
5. Check local environment details only when the task requires execution or `STATUS.md` says recheck is required.
6. For non-trivial tasks, draft a phased plan, confirm with the user unless the user already gave clear implementation direction, then execute step by step.

## Claude Code Closeout

Before ending substantial work, Claude Code must:

1. Update the current snapshot in `docs/codex/STATUS.md`.
2. Append a `YYYY-MM-DD HH:MM` session log entry at the end of `docs/codex/STATUS.md`.
3. Append a timestamped Chinese progress entry at the end of `version/工作进度.md`.
4. Update `version/版本迭代记录.md` only when the project version or release changed.

Record changed files, commands run, verification results, TODO changes, decisions, blockers, remaining issues, next recommended steps, and Git status in `STATUS.md`.

The status update is mandatory. If it cannot be completed, explain why before claiming the task is finished.

After each completed execution step, remind the user to consider a git commit before continuing.

When replying to the user, explain in Chinese. Keep commands, paths, errors, code, package names, and file names in their original form.

## Claude Code Writing Tasks

For papers, teaching designs, coursework, literature reviews, and curriculum-standard analysis, keep writing state inside `docs/codex/STATUS.md` before drafting. Use Simplified Chinese by default, keep the tone natural and suitable for graduate-level or teacher-training-student writing, and avoid stiff AI-style prose, repetitive phrasing, excessive bulleting, and hollow academic wording.

Do not invent references, policy documents, curriculum standards, journal details, data, DOI, URL, author names, titles, years, volumes, issues, or pages. If a detail cannot be verified, mark it as `待人工复核` or state the uncertainty clearly. Verify online-checkable and time-sensitive information when available before making factual claims.
