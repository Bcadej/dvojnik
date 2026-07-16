# TODO — Dvojnik

Ideas and known gaps, roughly in priority order. Tick items off as they land.

## Outstanding right now (as of 1.0.3, 16. 07. 2026)

Carried over — do these first.

- [ ] **Connect the repo to git and push** — `git init`, commit, add the remote
      `https://github.com/Bcadej/dvojnik.git` and push. Nothing is under version control yet;
      the push will need GitHub credentials.
- [ ] **Hands-on check of drag & drop between the panes** — dropping onto the *shortcut bar*
      is now verified by an automated drag, but pane-to-pane transfers are not. Verify:
  - [ ] Plain drag between panes copies
  - [ ] SHIFT+drag between panes moves
  - [ ] Dropping onto a folder row goes *into* that folder
  - [ ] Dragging in from a real Explorer window works
  - [ ] Dragging out to a real Explorer window works
  - [ ] Name clash appends ` (2)` instead of overwriting
  - [ ] Dragging a folder into its own subtree is refused
  - [ ] Dropping onto the shortcut bar from *Explorer* (the in-app drag is verified)
- [ ] **Hands-on check of Help → About** — version reads 1.0.1, build time is sensible, and
      the location hyperlink opens Explorer with the .exe selected.
- [ ] **Decide whether `docs/auto-mode-classifier-outage.md` belongs in the repo** — it is a
      note about tooling, not about Dvojnik. Keep, move out, or add to `.gitignore`.

### Verified so far

- Both languages load from the published single-file exe: forcing
  `%AppData%\Dvojnik\language.txt` to `en-GB` gives the window title *Dual Pane Explorer*,
  `sl-SI` gives *Dvojnik*. The app builds clean and launches.
- Sync View alignment was driven end-to-end through UI Automation against two purpose-built
  folders, and the panes padded correctly (7 rows each, blanks opposite the entries the
  other side lacks). The test script lives in the session scratchpad — worth turning into a
  proper checked-in test one day (see below).

## Done in 1.0.3

- [x] **Fixed: dropping on the shortcut bar did nothing.** A pane started its drag allowing
      only Copy|Move, so the bar's Link answer was downgraded to None and the drop refused.
      Drags from Explorer worked (Explorer allows Link), which is why it slipped through.
      Verified with an automated mouse drag.
- [x] Right-click → **"Add to this pane's shortcuts"** as an alternative to dragging
- [x] Shortcut buttons expose their name to screen readers

## Done in 1.0.2

- [x] Executable renamed to **Dvojnik.exe**
- [x] Sync View pads both panes with blank rows so matching entries line up
- [x] Per-pane shortcut bar under the address bar, built by dragging folders/files onto it,
      persisted to `%AppData%\Dvojnik\shortcuts-{Left,Right}.txt`
- [x] Help → About links to the language setting file (`language.txt`)
- [x] Sync toggle now reacts to Checked/Unchecked rather than Click, so keyboard and
      accessibility tools drive it correctly
- [x] Rows expose their file name to screen readers instead of the .NET type name

## Done in 1.0.1

- [x] Slovenian (`sl-SI`) as the primary language, British English (`en-GB`) switchable at runtime
- [x] All GUI and message text translated; language choice remembered between sessions
- [x] Slovenian date format `dd. MM. yyyy`, English `dd/MM/yyyy`
- [x] Larger base font (15pt)
- [x] Version number with automatic patch bump on every publish
- [x] Help → About with version, build time and clickable exe location
- [x] Drag & drop between panes — plain drag copies, SHIFT moves
- [x] Drag & drop to and from Windows Explorer
- [x] Application icon
- [x] Self-contained single-file publish (no .NET needed on target machines)

## Next up

- [ ] **Keyboard shortcuts** — F5 copy, F6 move, F7 new folder, F8/Del delete, Tab to switch
      pane, Alt+Left/Right for history. The classic Commander bindings.
- [ ] **Progress dialogue for long transfers** — big copies currently block the UI thread with
      no feedback and no way to cancel. Move transfers onto a background task.
- [ ] **Overwrite prompt** — drag & drop silently renames on a name clash (` (2)`) and paste
      just fails. Offer Overwrite / Skip / Rename / Apply to all.
- [ ] **Column sorting** — click a header to sort by name, size, type or date.
- [ ] **Drive selector** — a drive dropdown per pane; currently you must type the path.
- [ ] **Persist window layout** — remember size, splitter position and both panes' folders.
- [ ] **Check in the UI Automation test** — the script that drove Sync View and read the
      padded rows back caught a real bug (the toggle only reacting to Click). It currently
      lives only in a scratch folder; make it a proper test project.
- [ ] **Keyboard access to the shortcut bar** — the strip is mouse-only today.

## Later

- [ ] **Tabs per pane**
- [ ] **Filter / search box** to narrow the current listing
- [ ] **Real file-type icons** from the shell instead of the two emoji glyphs
- [ ] **Compare by content** (hash) rather than just size + modified date
- [ ] **Watch folders** with `FileSystemWatcher` so listings update themselves
- [ ] **Bookmarks / favourite folders**
- [ ] **Dark theme**
- [ ] **ARM64 release** alongside x64 (`publish.ps1 -Runtime win-arm64` works today, but it
      isn't part of the normal release)
- [ ] **Code signing** so SmartScreen stops warning on first launch

## Known issues

- [ ] Sync View compares only the *current* folder listing, not recursively.
- [ ] Delete goes to the Recycle Bin via `Microsoft.VisualBasic.FileIO`; the NU1510 build
      warning about that package reference is expected and harmless.
- [ ] Very large folders are loaded synchronously and can hitch the UI.
- [ ] No unit tests yet.
