# Dvojnik — Dual Pane Explorer

A dual-pane file manager for Windows, in the spirit of Total Commander and Midnight
Commander. Two folder listings sit side by side so you can move things between them
without juggling windows, and a **Sync View** mode turns the pair into a folder-comparison
tool.

The Slovenian name is **Dvojnik** ("the double"); in English it is **Dual Pane Explorer**.

![version](https://img.shields.io/badge/version-1.0.1-blue)
![platform](https://img.shields.io/badge/platform-Windows%20x64-lightgrey)
![framework](https://img.shields.io/badge/.NET-10-purple)

---

## What it does

### Two panes, one window
Both panes are independent file browsers with their own address bar, history
(back/forward), up-one-level and refresh. **Swap Panes** exchanges the two sides in place.

### Sync View — navigate together, spot the differences
Switch **Sync View** on and the panes anchor to their current folders as roots. From then on:

- Navigating one pane walks the other to the *same relative path*. Open `src\utils` on the
  left and the right jumps to its own `src\utils`. If there's no matching folder on the
  other side, that pane stays put and the status bar says so.
- **The two listings are padded so they line up.** Where one pane has an entry the other
  lacks, the other pane gets a blank row in its place — so anything present on both sides
  always sits at the same height, and you read differences straight across:

  ```
  both        | both
  onlyA       |              <- blank: right side has no "onlyA"
              | onlyB
  diff.txt    | diff.txt
  same.txt    | same.txt
  ```

- The listings are compared by name, and rows are colour-coded:

  | Colour | Meaning |
  |---|---|
  | 🟩 Green | Exists only in this pane |
  | 🟧 Orange | Same name on both sides, but the size or modified date differs |
  | ⬜ Plain | Identical on both sides |

  This makes it a quick way to eyeball two copies of a folder tree — a backup against an
  original, say — without a dedicated diff tool.

### Drag and drop
- **Between the panes** — drag a selection from one side to the other.
  - **Plain drag copies.**
  - **Hold SHIFT while dragging to move.**
- **Onto a folder row** drops *into* that folder; anywhere else in the pane drops into the
  folder currently listed.
- **To and from Windows Explorer** — drags use the shell's own `FileDrop` format, so you can
  drag files in from a real Explorer window, and drag them out to one.
- Name clashes get ` (2)`, ` (3)` … appended rather than overwriting anything, and the app
  refuses to move a folder into its own subtree.

### Shortcut bar
Under each pane's address bar sits a strip of shortcuts. There are two ways to add one:

- **Drag any folder or file onto the strip** — from either pane or from Windows Explorer.
- **Right-click a folder or file → "Add to this pane's shortcuts"**, which adds every
  selected item to the strip in the pane you right-clicked.

Click a shortcut to jump straight to that folder (or open that file). Right-click a shortcut
to remove it, or right-click the empty strip to clear them all.

Each pane keeps its own list, remembered between sessions in
`%AppData%\Dvojnik\shortcuts-Left.txt` and `shortcuts-Right.txt` — plain text, one path per
line. Shortcuts whose target has disappeared are dropped quietly on the next start.

### File operations
Open, rename, delete (to the **Recycle Bin**, not permanently), copy, cut, paste and
create folders — from the right-click menu. The clipboard is shared across both panes.

### Bilingual
Slovenian (`sl-SI`) is the primary language; British English (`en-GB`) is available from the
**Jezik / Language** menu. Switching takes effect immediately — no restart — and your choice
is remembered between sessions in `%AppData%\Dvojnik\language.txt`.

Dates follow the language: `16. 07. 2026` in Slovenian, `16/07/2026` in English.

**Help → About** shows the version, the build time and the full path of the running `.exe`,
plus the path of the language setting file. Both paths are clickable and open Explorer at
the containing folder with the file selected.

---

## Running it

Grab `Dvojnik.exe` from a release and double-click it. **The .NET runtime is bundled inside
the executable**, so nothing needs to be installed on the target machine.

On first launch Windows SmartScreen may warn that the app is unrecognised — the executable
isn't code-signed. Choose *More info → Run anyway*.

---

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build                       # fast, framework-dependent build for development
dotnet run --project FileExplorerClone
```

### Publishing

```powershell
.\publish.ps1                      # bumps the version, emits publish\Dvojnik.exe
.\publish.ps1 -Runtime win-arm64   # for ARM64 Windows machines
.\publish.ps1 -NoBump              # republish without bumping the version
```

Every run of `publish.ps1` bumps the patch version (1.0.1 → 1.0.2 → …) in
[`Version.props`](Version.props), which is the single source of truth for the version.
The published output is a single ~62 MB self-contained `.exe`.

Plain `dotnet publish` also produces a self-contained single file (the csproj sets this up
under an `_IsPublishing` condition) but does **not** bump the version — use `publish.ps1`
for real releases.

---

## Project layout

| Path | Purpose |
|---|---|
| [`FileExplorerClone/MainWindow.xaml`](FileExplorerClone/MainWindow.xaml) | Shell: menu, toolbar, the two panes and the splitter |
| [`FileExplorerClone/ExplorerPane.xaml`](FileExplorerClone/ExplorerPane.xaml) | One pane: toolbar, file list, context menu, drag & drop |
| [`FileExplorerClone/AboutWindow.xaml`](FileExplorerClone/AboutWindow.xaml) | Help → About: version, build time, clickable location |
| [`FileExplorerClone/FileSystemItem.cs`](FileExplorerClone/FileSystemItem.cs) | A row in a pane, its compare state, and the blank spacer row |
| [`FileExplorerClone/ShortcutItem.cs`](FileExplorerClone/ShortcutItem.cs) | Shortcut bar entries and their on-disk list |
| [`FileExplorerClone/Localisation.cs`](FileExplorerClone/Localisation.cs) | String tables, the bindable `Loc` indexer, language switching |
| [`FileExplorerClone/AppInfo.cs`](FileExplorerClone/AppInfo.cs) | Version, build stamp, exe path, date patterns |
| [`FileExplorerClone/Resources/Strings.resx`](FileExplorerClone/Resources/Strings.resx) | Slovenian strings (the neutral/default table) |
| [`FileExplorerClone/Resources/Strings.en-GB.resx`](FileExplorerClone/Resources/Strings.en-GB.resx) | British English strings |
| [`Version.props`](Version.props) | The version number |
| [`publish.ps1`](publish.ps1) | Bump + publish |
| [`tools/make-icon.ps1`](tools/make-icon.ps1) | Regenerates the app icon (`.ico` + `.png`) |

### Why the icon ships twice

`Resources/Dvojnik.ico` (frames 16–256px) is used for the executable and window chrome,
where the Windows shell picks a sensible frame. `Resources/Dvojnik-256.png` exists because
WPF will not pick one: `IconBitmapDecoder` decodes the **smallest** frame and upscales it —
even when `DecodePixelWidth` explicitly asks for 256 — which looks blurry. Anywhere WPF
renders the logo at size (the About dialog) uses the PNG. Both come from
`tools/make-icon.ps1`; re-run it after changing the artwork.

### How localisation works

The **neutral** `.resx` holds Slovenian, so Slovenian is what you get when no satellite
matches — that's what makes it the app's primary language. English ships as an `en-GB`
satellite assembly embedded in the single-file exe.

XAML binds through an indexer on a singleton:

```xml
Header="{Binding [Menu_Help], Source={x:Static local:Loc.Instance}}"
```

`LanguageManager.Apply` sets the thread cultures and then raises `PropertyChanged` for
`Item[]`, which re-evaluates every one of those bindings at once. Text built in code-behind
(the status line, list labels) is refreshed via the `LanguageChanged` event.

---

## Licence

Not yet specified.
