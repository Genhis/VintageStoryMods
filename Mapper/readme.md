# Mapper
This mod adds map drawing mechanic - the world map no longer updates automatically and costs resources.

## Features
- **A handbook guide** to explain the mechanics.
- **Compass:** Required for showing position and direction overlay (Ctrl+V) in uncharted territory; accuracy depends on item type.
- **Map:**
  - A consumable item without durability; adds map chunks starting from player position and going in circle.
  - Higher scales can chart a larger area with limited resolution.
  - Waypoint placement requires a charted area, even the last death position and ore markers.
  - Shows player position in charted areas, no compass needed.
  - Maps don't refresh automatically, they need to be repainted.
- **Drawing kit:**
  - A consumable item with durability; paints an area based on the selected tool mode. Lower-resolution maps use less durability.
  - Apart from the charcoal kit, it must be held in your left hand with a paintbrush in your right hand.

## Technical details
- Mapper stores data both server-side and client-side. For each player, the server knows about all chunks where the map item was used, their resolution and their last-applied color. The client stores individual chunk pixels.
- Unlike Vintage Story map which uses a SQLite database, Mapper stores data in a compressed binary format and loads it all into memory. This could become a problem for large mapped areas, but the fact that chunks aren't charted automatically and doing so costs resources should mitigate it.
- The save format is backwards compatible. If the binary structure changes, new mod versions will be able to load old save files but old mod versions won't be able to read new save files.
- In the unlikely scenario if this data gets corrupted, an error is shown in-game. This error doesn't affect anything besides the mapping functionality. To restore the mod and overwrite corrupted data, type `.mapper restore` (client-side) or `/mapper restore` (server-side) depending on where the issue occured.

## Built-in mod compatibility
- [Auto Map Markers](https://mods.vintagestory.at/automapmarkers)
