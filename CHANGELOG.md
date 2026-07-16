# Changelog — Dvojnik

All notable changes, newest first. Versions are bumped by `publish.ps1` on every publish.

Everything below was built in one session on **16. 07. 2026**, starting from a working but
bare dual-pane explorer (two panes, Sync View, context-menu file operations, English only).

---

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
