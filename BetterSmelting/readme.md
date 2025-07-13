# Better Smelting

This mod improves firepit behavior when heating and smelting items.
It also makes mined coal burn longer in the forge and coal piles.

## Features
- **Coal pile patch:** Layer burn duration depends on item burn duration and burn temperature.
- **Firepit patch:**
  - Item stacks keep their temperature when one item is smelted, but heating time scales with stack size.
  - Contribution of item stack temperature above melting point towards smelting progress is no longer clamped to integer multiples of melting temperature (items smelt faster).
  - Cooking pot and crucible heating speed depends on items in the slots (configurable).
  - When adding items to cooking containers, the temperature is averaged between all slots instead of taking the lowest value.
- **Forge patch:**
  - The amount of fuel added depends on item burn duration and burn temperature.
  - Configurable minimum fuel temperature - the default value is unchanged (1000).

## Burning time comparison (real-time minutes)

### Firepit (charcoal as fuel)

|      Item      |      Vanilla       |  Better Smelting   |
| -------------: | -----------------: | -----------------: |
|   1 flint      |              01:03 |              01:02 |
|   4 flints     |              02:28 |              02:17 |
|                | Heating / Smelting | Heating / Smelting |
|  20 nuggets    |      00:58 / 00:29 |      01:06 / 00:27 |
| 200 nuggets    |      01:01 / 04:56 |      02:19 / 03:17 |
|   4 meat (pot) |      00:13 / 00:40 |      00:22 / 00:30 |
|  24 meat (pot) |      00:13 / 04:00 |      00:51 / 03:01 |

### Coal pile (full)

| Fuel type  | Vanilla | Better Smelting |
| ---------: | ------: | --------------: |
|   Charcoal |   08:00 |           08:00 |
|       Coke |   08:00 |           08:15 |
| Brown coal |   08:00 |           13:02 |
| Black coal |   08:00 |           15:30 |
| Anthracite |   08:00 |           36:12 |

### Forge (one item)

| Fuel type  | Vanilla | Better Smelting |
| ---------: | ------: | --------------: |
|   Charcoal |   01:12 |           01:12 |
|       Coke |   01:12 |           01:14 |
| Brown coal |   01:12 |           01:58 |
| Black coal |   01:12 |           02:20 |
| Anthracite |   01:12 |           05:26 |
