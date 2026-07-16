# Note: "auto mode cannot determine the safety of Bash"

Reference note from a session on 2026-07-16, where command execution (Bash / PowerShell)
was blocked while file reads and writes kept working. Kept for later reference.

## The question

> "the tool that approves command execution has been down" — which tool is that?

Fair challenge — that phrasing was sloppy. It isn't a tool in the sense of the things
Claude calls. Here is the actual error rather than a paraphrase:

```
claude-sonnet-5[1m] is temporarily unavailable, so auto mode cannot determine the
safety of Bash right now.
```

## What is actually failing

The session runs in **auto permission mode**. Instead of prompting the user to approve
every command, the harness asks a **separate classifier model — Claude Sonnet 5** — to
judge whether a given command is safe to run unattended.

That classifier endpoint was returning *unavailable*. No classifier verdict means no
approval, so the Bash and PowerShell calls were rejected **before they ever executed**.

File reads and writes went through untouched, because they don't need that check.

Calling it "the tool that approves command execution" compressed that into something
wrong. **It's a model the permission system depends on, not a tool.**

## A corroborating detail

A bare `echo probe` *did* run. That suggests trivial commands take a different path
(allowlisted or cached), while anything substantive — invoking a script, `dotnet build`,
`git push` — needs a fresh verdict and was stuck.

## Practical upshot

- The failure is on Anthropic's side, not in the project.
- It is likely **transient** — retrying is often enough.
- If it persists, switching the session's permission mode so that **the user** approves
  commands rather than the classifier routes around it entirely. `/permissions` is where
  that lives.

## Symptom checklist

If this recurs, the signature is:

| Works | Blocked |
|---|---|
| Read, Write, Edit, Glob, Grep | Bash, PowerShell |
| Trivial allowlisted commands (`echo`) | Scripts, `dotnet build`, `git`, publishing |

The error text names a model (`claude-sonnet-5[1m]`) rather than a tool — that's the tell
that it is the permission classifier, not the tool itself, that is down.
