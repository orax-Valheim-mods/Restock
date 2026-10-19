# Restock

Restock refills your inventory from an open chest in a single key press: point at
an item (or pick it with a click), press the hotkey, and every identical item found
in the container is moved into your inventory until your free slots or your maximum
carry weight are used up.

# Usage

1. Open a container.
2. Pick the item to use as a template: simply point the mouse cursor at it, or
   select it with a plain left click, either in the container or in your own
   inventory. Both work by default, see [Selection mode](#selection-mode). A click
   leaves the item on the cursor, exactly like in vanilla.
3. Press the hotkey (default: Shift + R, written `R + LeftShift` in the
   configuration). It accepts a single key (`B`) or a key combination.

Only the items identical to that template are transferred, using the game's own
stacking rule (same name, quality and world level). The transfer stops as soon as
one of these limits is reached:

* existing partial stacks of that item are topped up first, then empty slots are
  filled, and the transfer stops when your inventory has no room left;
* the transfer never pushes you over your maximum carry weight.

Once the restock succeeded, the selection is put down automatically: nothing stays
on the cursor and the next key press needs a new template (see
[Behaviour](#behaviour) to keep the selection instead).

Nothing else in the container is touched, and the container is saved normally (it
must be opened by you, which is what grants you the ownership Valheim requires).

# Configuration

The hotkey is a
[BepInEx `KeyboardShortcut`](https://docs_new.bepinex.dev/api/BepInEx.Configuration.KeyboardShortcut.html),
defined in `BepInEx\config\orax.Restock.cfg`:

```ini
[Input]

## Key combination pressed while a container is open to pull the items matching the selected one into the player inventory. The main key is written first and the modifiers after it: R + LeftShift is the combination Shift + R. A single key (B) works too, and None disables the hotkey.
# Setting type: BepInEx.Configuration.KeyboardShortcut
# Default value: R + LeftShift
Hotkey = R + LeftShift
```

* The **main key comes first**, then the modifiers: `R + LeftShift` is "hold Shift
  and press R", `LeftControl + LeftShift + B` is "hold Shift and Ctrl and press B".
  A single key (`B`) is valid too, and `None` disables the hotkey entirely.
* Key names are the `KeyCode` names (`LeftShift`, `LeftControl`, `LeftAlt`,
  `Alpha1`, `F5`, ...). BepInEx rejects anything else, disables the hotkey and
  reports the problem in the log.
* Like every BepInEx shortcut, the combination only fires when **no other key is
  held**, so release the movement keys before pressing it.
* Changes made through the in-game configuration manager apply immediately; changes
  made to the file are picked up on the next launch.

## Behaviour

```ini
[Behaviour]

## Put the selection down after a restock: the item stays where it was and the next key press needs a new selection. Set it to false to keep the selection and press the key again right away.
# Setting type: System.Boolean
# Default value: true
DeselectAfterRestock = true

## How the item to restock is picked. Click: the item selected with a plain left click. Hover: the item under the mouse cursor when the key is pressed, no click needed. Both: the hovered item when there is one, otherwise the clicked one.
# Setting type: Restock.SelectionMode
# Default value: Both
SelectionMode = Both
```

`DeselectAfterRestock` puts the selected item down after a successful restock:
nothing follows the cursor any more and a new key press reports that no item is
selected. A transfer that moved nothing (no matching item, full inventory, weight
reached) keeps the selection, so the same key can be pressed again right after.

### Selection mode

`SelectionMode` decides where the template item comes from:

* **`Hover`** — the simple workflow: point the cursor at an item in the container
  or in your own inventory and press the hotkey. No click is needed. Nothing is
  kept between two presses, so the cursor position at the moment of the key press
  is what counts.
* **`Click`** — only a plain left click picks the template, the cursor position is
  ignored. This is the previous behaviour, unchanged.
* **`Both`** (default) — the item under the cursor is used when there is one, and
  the last item selected with a click is used otherwise, for instance when the
  cursor sits over an empty slot. In other words, hovering wins, clicking is the
  fallback.

With `Hover`, you may want to reword the `NothingSelected` message (see
[Messages](#messages)) to match the workflow, for example
`Restock: point at an item first`.

## Messages

The four messages shown in the middle of the screen are configurable as well. They
are read on every press, so the settings apply immediately:

```ini
[Messages]

## Shown when the hotkey is pressed without a hovered or selected item. Leave it empty to disable the message.
# Setting type: System.String
# Default value: Restock: select an item first
NothingSelected = Restock: select an item first

## Shown when the container holds no item identical to the selected one. Leave it empty to disable the message.
# Setting type: System.String
# Default value: Restock: no matching items in the container
NoMatchingItems = Restock: no matching items in the container

## Shown when the transfer stops because your inventory has no room left. Leave it empty to disable the message.
# Setting type: System.String
# Default value: Restock: not enough room in your inventory
NoSpace = Restock: not enough room in your inventory

## Shown when the transfer stops because it would exceed your maximum carry weight. Leave it empty to disable the message.
# Setting type: System.String
# Default value: Restock: that would make you overburdened
TooHeavy = Restock: that would make you overburdened
```

An empty value keeps the message off entirely. This section is also the way to
translate the mod: write the messages in your own language. Keep them plain text,
a leading `$` would be looked up as a game localization token instead.

# Installation

This mod is client-side only. You do not need to install it on the server.

## Using a mod manager (recommended)

You can install the mod using Vortex, r2modman or Gale.

## Manual

Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
and [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), then copy
`Restock.dll` to `Steam\steamapps\common\Valheim\BepInEx\plugins\Restock\`.

# Development

* `BUILD.bat` or `dotnet build Restock.sln -c Release` builds, packages (Thunderstore
  zip under `.RELEASE\`) and deploys the mod.
* Debug builds are deployed without bumping the version or creating a release.

# Misc.

Source code: [GitHub repository](https://github.com/orax-Valheim-mods/)
