# Restock

Restock refills your inventory from an open chest in a single key press: select an
item, press the hotkey, and every identical item found in the container is moved
into your inventory until your free slots or your maximum carry weight are used up.

# Usage

1. Open a container.
2. Select an item with a plain left click, either in the container or in your own
   inventory (the item stays on the cursor, exactly like in vanilla).
3. Press the hotkey (default: `Shift + R`). It accepts a single key (`B`) or a key
   combination (`Shift + R`, `Ctrl + B`, ...).

Only the items identical to the selected one are transferred, using the game's own
stacking rule (same name, quality and world level). The transfer stops as soon as
one of these limits is reached:

* existing partial stacks of that item are topped up first, then empty slots are
  filled, and the transfer stops when your inventory has no room left;
* the transfer never pushes you over your maximum carry weight.

Once the restock succeeded, the selection is put down automatically: the selected
item stays in its slot and the next key press needs a new selection (see
[Behaviour](#behaviour) to keep the selection instead).

Nothing else in the container is touched, and the container is saved normally (it
must be opened by you, which is what grants you the ownership Valheim requires).

# Configuration

The hotkey is defined in `BepInEx\config\orax.Restock.cfg`:

```ini
[Input]

## Key combination pressed while a container is open to pull the items matching the selected one into the player inventory. Write the keys joined with '+' in any order; the key that is not a modifier (Shift, Ctrl, Alt, Win) is the one to press. 'None' disables it. Examples: Shift + R, LeftControl + B, B.
# Setting type: System.String
# Default value: Shift + R
Hotkey = Shift + R
```

* Any order works: `Shift + R` and `R + Shift` both mean "hold Shift and press R",
  and the key that is not a modifier is the one that triggers the transfer.
* Modifiers may be written either as their `KeyCode` names (`LeftShift`,
  `LeftControl`) or as aliases (`Shift`, `Ctrl`, `Alt`, `Win`); key names are
  case-insensitive, so `SHIFT+R` and `Shift+r` are both valid.
* A single key works too (`B`), and `None` disables the hotkey entirely.
* Changes made through the in-game configuration manager apply immediately; changes
  made to the file are picked up on the next launch.

## Behaviour

```ini
[Behaviour]

## Put the selection down after a restock: the item stays where it was and the next key press needs a new selection. Set it to false to keep the selection and press the key again right away.
# Setting type: System.Boolean
# Default value: true
DeselectAfterRestock = true
```

When it is on, a successful restock puts the selected item down: nothing follows
the cursor any more and a new key press reports that no item is selected. A
transfer that moved nothing (no matching item, full inventory, weight reached)
keeps the selection, so the same key can be pressed again right after.

## Messages

The four messages shown in the middle of the screen are configurable as well. They
are read on every press, so the settings apply immediately:

```ini
[Messages]

## Shown when the hotkey is pressed without a selected item. Leave it empty to disable the message.
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
