# Ideas — things worth considering

A parking place for suggestions that came up in conversation but weren't asked for. Nothing
here is committed to. When one gets picked up, move it into [TODO.md](../TODO.md) and build it.

---

## Keyboard

Suggested after the 1.0.7 keyboard work, roughly best-first.

### Help → Keyboard shortcuts dialog
**Do this one first.** Bindings nobody can discover get used by nobody. Dvojnik now has a
dozen keys and the only record of them is the README. A small dialog off the Help menu costs
almost nothing and makes the rest of the list worth building.

### Ctrl+D — add the selection to this pane's shortcut bar
The strip is mouse-only today (drag, or right-click → add). A key would complete it.
See the "Keyboard access to the shortcut bar" item already in TODO.md.

### Ctrl+← / Ctrl+→ — copy the other pane's folder into this one
The classic dual-pane move: line both sides up on the same folder without retyping a path.
`Ctrl+←` means "bring the left pane's folder here", `Ctrl+→` the right's. Pairs naturally
with Sync View.

### Alt+F1 / Alt+F2 — drive picker per pane
Wait until the drive selector (already in TODO.md) exists; these are the Commander bindings
for it.

### F3 — filter the listing as you type
See "Search" below — this is the cheap half of that idea.

### The Commander convention: F5 copy, F6 move, F8 delete
Deliberate decision needed, not a free addition. F5 currently refreshes (Explorer's meaning)
and it cannot sensibly be both. Recommendation: keep the Explorer keys unless muscle memory
from Total Commander says otherwise — the rest of the app already follows Explorer.

---

## Search

Two quite different features often called the same thing. Worth being clear which is wanted,
because the cost differs by an order of magnitude.

### 1. Filter the current folder — cheap, fast, no surprises
A box above the list; typing narrows the visible rows to those matching. No disk access —
the listing is already in memory, so it is instant even in huge folders.

Notes on doing it properly:
- Filter `_realItems` into the displayed `Items`; keep `RealItems` as the unfiltered truth so
  Sync View comparison and alignment stay correct.
- In Sync View, a filter has to apply to both panes at once, exactly as sorting does — the
  aligned rows share one row order.
- Escape clears; the pane's existing type-ahead already covers "jump to a name", so this is
  for *narrowing*, not jumping.

### 2. Search a folder tree — genuinely bigger
Walk subfolders for name (and perhaps content) matches, showing results as a flat list.

Notes:
- Must run off the UI thread and stream results in, with a cancel. A synchronous walk of a
  deep tree freezes the window — the same problem already noted for large copies.
- Needs a results view that is *not* a normal folder listing: rows come from many folders, so
  the pane's "current path" no longer describes them. Decide whether a result opens its
  containing folder or launches the file.
- Permission errors are normal, not exceptional — skip and continue, as the pane already does
  when listing.
- Content search needs an extension allow-list, or it will try to grep 4 GB videos.

**Recommendation:** build (1) first. It is small, it is where most of the everyday value is,
and it is the natural companion to the sorting that already exists. Treat (2) as a separate
feature once (1) is in use.

---

## Other

### Progress + cancel for long transfers
Already in TODO.md, but worth restating: large copies currently block the UI thread. This is
the most likely thing to make the app feel broken in real use.

### Overwrite prompt
Drag & drop renames on a clash (` (2)`), paste just fails. Neither is what a user expects —
Overwrite / Skip / Rename / Apply to all.

### Turn the automation scripts into a real test project
The scratch UI Automation scripts caught two genuine bugs (the Sync toggle only reacting to
`Click`; Alt+←/→ being eaten by the ScrollViewer). They live in a temp folder and will be lost.
