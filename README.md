# StackingElectricFurnace

Allows players to stack **electric furnaces** and **industrial electric furnaces** up to **2 total furnaces**.

## Features
- Stack **electric furnace** on **electric furnace**
- Stack **industrial electric furnace** on **industrial electric furnace**
- Stack **electric furnace** on **industrial electric furnace**
- Stack **industrial electric furnace** on **electric furnace**
- Maximum stack height is **2 total furnaces**
- A stacked top furnace **cannot** be used as a base for another stack
- Removing the **top furnace** works normally
- Removing the **bottom furnace** removes the stacked top furnace
- Stacked top furnaces are protected from normal ground-missing checks when supported by their tracked bottom furnace
- Optional building privilege requirement
- Persistent stack pair tracking across plugin reloads and server restarts

## Permissions
Required permission:
- `stackingelectricfurnace.use`

Grant to a user:
```bash
oxide.grant user <playername|steamid> stackingelectricfurnace.use
```

Grant to a group:
```bash
oxide.grant group <groupname> stackingelectricfurnace.use
```

## Commands
This plugin does not provide any chat or console commands.

## Installation
1. Place `StackingElectricFurnace.cs` in your server's `oxide/plugins` folder
2. Reload the plugin:
```bash
oxide.reload StackingElectricFurnace
```

## Configuration
Configuration file:
`oxide/config/StackingElectricFurnace.json`

Default config:
```json
{
  "Building privilege required": false,
  "Max use distance": 6.0,
  "Horizontal tolerance": 0.15,
  "Max vertical support distance": 2.0
}
```

### Config Options
- `Building privilege required`
  - `false` = players only need permission
  - `true` = players must also be building authorized

- `Max use distance`
  - Maximum distance allowed between player and target furnace when stacking

- `Horizontal tolerance`
  - Maximum allowed horizontal offset between the tracked top and bottom furnace before support is considered invalid

- `Max vertical support distance`
  - Maximum allowed vertical distance between the tracked top and bottom furnace for support validation

## Data File
Data file:
`oxide/data/StackingElectricFurnace.json`

This stores tracked top/bottom stack pairs so stacked furnaces continue to behave correctly after plugin reloads and server restarts.

## How to Use
1. Make sure the player has the permission `stackingelectricfurnace.use`
2. Hold one of these items:
   - `electric.furnace`
   - `industrial.electric.furnace`
3. Look at a placed electric or industrial electric furnace
4. Right-click the furnace
5. The held furnace will be stacked on top if the target is valid and does not already have a stacked furnace above it

## Rules
- Players must hold the exact furnace item they want to place
- Supported targets:
  - electric furnace
  - industrial electric furnace
- Supported placed item types:
  - electric furnace
  - industrial electric furnace
- Stacking is limited to exactly **2 total furnaces**
- A stacked top furnace cannot be used as a base for another stack
- If the bottom furnace is removed, the top furnace is also removed

## Notes
- This plugin supports mixed stacking between electric and industrial electric furnaces
- The plugin uses Rust's ground-missing handling to protect properly stacked top furnaces
- Stack relationships are tracked exactly as top-to-bottom pairs to avoid nearby stacks interfering with each other
- On unload, the plugin clears the runtime stacked marker flag from tracked furnaces loaded during that session

## Plugin Info
- **Name:** StackingElectricFurnace
- **Version:** 1.2.1
- **Permission:** `stackingelectricfurnace.use`
- **Config:** `oxide/config/StackingElectricFurnace.json`
- **Data:** `oxide/data/StackingElectricFurnace.json`
