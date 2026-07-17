# Changelog — Dvojnik

All notable changes, newest first. Versions are bumped by `publish.ps1` on every publish.

Versions **1.0.1–1.0.8** were built in one session on **16. 07. 2026**, starting from a working
but bare dual-pane explorer (two panes, Sync View, context-menu file operations, English only).
Versions **1.0.9–1.0.16** followed in a second session on **17. 07. 2026**, focused on
drag-and-drop correctness, a WebDAV delete bug, and the logging that eventually cracked it.

---

## 1.0.16 / 1.0.14 — Rebuilds, no functional change

- Clean rebuilds with no code changes, each carrying the preceding version's fixes (1.0.15 and
  1.0.13 respectively) with an updated version stamp.

## 1.0.15 — WebDAV delete finally fixed: the trailing NUL

- **Root cause found.** The `diagnostika…@SSL@2078\DavWWWRoot` WebDAV share returns *every*
  enumerated entry with a stray trailing `U+0000` null character — the file really named
  `same1.txt` comes back as `same1.txt\0`. That invisible null showed as a "trailing space" in
  the error dialog and made every file API fail with **"Null character in path"**. This was the
  actual blocker behind 1.0.10 and 1.0.12 — both earlier fixes were treating symptoms.
- **Fix:** `FileSystemItem` now strips trailing NULs from names and paths the moment they are
  read (`StripNul`), so delete, rename, copy and navigation all use the true name. Verified
  end-to-end against the live share.
- Also filter out the `.` and `..` pseudo-entries that this server lists, so they no longer
  appear as junk folder rows.

> **How it was found:** the 1.0.11 logging turned a blank "Could not delete" into the exact
> `ArgumentException (0x80070057): Null character in path`, and dumping the filename's code
> points (`U+0061 U+0061 U+0061 U+0000` for `aaa`) revealed the trailing null.

## 1.0.13 — Trailing space/dot name detection

- **Flags files whose name ends in a space or a dot** with an amber ⚠ marker and an explanatory
  tooltip, and logs a `WARN` on navigation. Windows silently trims a trailing space/dot during
  path normalisation, so such files resist delete/rename; the flag tells the user to rename
  them. (Independent of the WebDAV NUL issue, which is a different, invisible character.)

## 1.0.12 — WebDAV delete attempt #2 (superseded by 1.0.15)

- Permanent deletes bypass the `Microsoft.VisualBasic` shell helper and use plain
  `File.Delete` / `Directory.Delete`. Correct in general (those map straight onto the Win32
  calls), but did **not** fix the share on its own — the real cause was the trailing NUL.

## 1.0.11 — File-operation logging

- **Always-on logging** to `%APPDATA%\Dvojnik\logs\dvojnik-YYYYMMDD.log`, kept 7 days. Records
  every copy/move/delete/rename/paste with source, target and outcome; full exceptions **with
  their HRESULT** on failure; drag decisions (`effect`, `samePane`); navigation; sort; and a
  global handler that captures any unhandled crash with a stack trace. Never logs file
  contents, never throws.
- The **About window gained a "Logs" link** that opens the log folder.
- This is what made the WebDAV bug diagnosable — see 1.0.15.

## 1.0.10 — WebDAV delete attempt #1 (superseded by 1.0.15)

- Detects locations without a Recycle Bin (UNC / network / WebDAV) and falls back to a
  permanent delete there, matching Explorer, with a prompt that warns the delete is permanent.
  A sound behaviour to keep, but not the actual cause of the failure.

## 1.0.9 — Drag-and-drop move vs. copy

- **A drag within one pane now moves; a drag from the other pane (or from Explorer) copies.**
  Previously a plain drag always copied, so dragging a file inside the same pane duplicated it.
  Dropping onto a subfolder moves into it; dropping back into the same folder is a no-op and the
  cursor shows "no drop". **SHIFT** still forces a move. Verified by driving the real app with
  synthetic mouse input across all three cases.

## 1.0.8 — Rebuild, no functional change

- **No code changes.** A clean rebuild of the 1.0.7 feature set.
- Fixes the build stamp: 1.0.7 was published *before* its own keyboard-browsing commit was
  made, so the exe embedded the previous commit hash (`1.0.7+03356c1`) and pointed at the
  wrong source. Publishing after committing means 1.0.8 carries the commit it was actually
  built from.
- Documentation added in this round: `CHANGELOG.md` (this file) and `docs/ideas.md`, which
  parks the keyboard extras and the two meanings of "search" (filter the current folder vs.
  walk a tree). Neither search variant is built — both were declined for now.

## 1.0.7 — Keyboard browsing

- **Enter** opens the selected item, **Backspace** and **Alt+↑** go up a level, **Alt+←/→**
  walk history, **Tab** switches pane.
- **F5** refresh, **F2** rename, **Delete**, **Ctrl+C/X/V**, **F7** / **Ctrl+Shift+N** new
  folder — bound to the same methods the context menu calls.
- Going up **selects the folder you just left**, so walking a tree keeps your place.
- Sync View spacer rows are non-focusable, so ↑/↓ step over them.
- The left pane takes focus at startup.

> **Bug found by testing:** Alt+←/→ did nothing. The ListView's inner ScrollViewer consumes
> Left/Right for horizontal scrolling and marks them handled before they bubble up. Fixed by
> tunnelling via `PreviewKeyDown` instead of `KeyDown`.

## 1.0.6 — Slovenian settings files, sorting, shortcut rename

- Settings files renamed to Slovenian: **`jezik.txt`**, **`bližnjice-levo.txt`**,
  **`bližnjice-desno.txt`**. Files from earlier versions migrate on first run.
- **Sortable columns**: click Name/Size/Type/Modified, click again to reverse; an arrow marks
  the active column; folders stay above files. In Sync View the sort applies to both panes,
  since aligned rows must share one order.
- **Rename a shortcut** from its right-click menu; stored as `path|custom name`.
- About icon reduced 96 → 64px.
- Column headers and shortcut buttons now expose accessible names.

## 1.0.5 — Icon fixed properly

- **Fixed the blurry About icon** — the real cause. WPF's `IconBitmapDecoder` decodes the
  *smallest* `.ico` frame and upscales it, and `DecodePixelWidth` does **not** override that,
  so 1.0.4 achieved nothing. The dialog now uses a dedicated 256px PNG.
- Icon generator moved into the repo at `tools/make-icon.ps1`, emitting both artefacts.
- The **language setting link opens the containing folder** (file selected), matching the
  executable link; both share one `RevealInExplorer` helper.

## 1.0.4 — About icon enlarged

- Enlarged the About icon. This did **not** fix the blur — see 1.0.5.

## 1.0.3 — Shortcut bar fixes, git

- **Fixed: dropping on the shortcut bar did nothing.** A pane started its drag allowing only
  `Copy|Move`, so the bar's `Link` answer was downgraded to `None` and the drop silently
  refused. Drags from Explorer worked (Explorer allows `Link`), which is why it slipped
  through.
- Right-click → **"Add to this pane's shortcuts"** as an alternative to dragging.
- Repo connected to git and pushed to https://github.com/Bcadej/dvojnik.git.

## 1.0.2 — Alignment, shortcut bar, rename to Dvojnik.exe

- **Sync View pads both panes with blank rows** so matching entries sit level and differences
  read straight across.
- **Per-pane shortcut bar** under the address bar, built by dragging items onto it.
- Executable renamed to **Dvojnik.exe**.
- Help → About links to the language setting file.
- Rows expose their file name to screen readers instead of the .NET type name.

> **Bug found by testing:** the Sync toggle only reacted to `Click`, so keyboard and
> accessibility tools could flip its state without the panes ever re-comparing. Now bound to
> `Checked`/`Unchecked`.

## 1.0.1 — Slovenian, versioning, drag & drop, About

- **Slovenian (`sl-SI`) as the primary language**, British English (`en-GB`) switchable at
  runtime from the menu. The neutral resource table *is* Slovenian; English ships as a
  satellite assembly. The choice persists between sessions.
- Dates follow the language: `16. 07. 2026` vs `16/07/2026`.
- **Drag & drop between panes** — plain drag copies, **SHIFT** moves. Uses the shell's
  `FileDrop` format, so drags to and from Windows Explorer work. Name clashes get ` (2)`
  appended rather than overwriting; moving a folder into its own subtree is refused.
- **Help → About** with version, build time, and a clickable executable path.
- Application icon (16–256px).
- Base font size raised to 15pt.
- **Versioning**: `Version.props` is the single source of truth; `publish.ps1` bumps the patch
  on every publish.
- **Self-contained single-file publish** — one ~58 MB `Dvojnik.exe` that needs no .NET
  installed on the target machine.
