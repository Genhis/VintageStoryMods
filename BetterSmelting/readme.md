# Better Smelting

This mod improves firepit behavior when heating and smelting items.
It also makes mined coal burn longer in coal piles.

## Features
- **Coal pile patch:** Layer burn duration depends on item burn duration and burn temperature.
- **Firepit patch:**
  - Item stack heating is no longer slowed down when it reaches the melting temperature.
  - Contribution of item stack temperature above melting point towards smelting progress is no longer clamped to integer multiples of melting temperature (items smelt faster).
  - Cooking pot and crucible heating speed depends on items in the slots (configurable).
  - When adding items to cooking containers, the temperature is averaged between all slots instead of taking the lowest value.

## Burning time comparison (real-time minutes)

### Firepit (charcoal as fuel)

|        Item        | Vanilla (before 1.22) |      Vanilla       |  Better Smelting   |
| -----------------: | --------------------: | -----------------: | -----------------: |
|   1 flint          |                 01:03 |              01:03 |              01:01 |
|   4 flints         |                 02:28 |              02:23 |              02:17 |
|                    |    Heating / Smelting | Heating / Smelting | Heating / Smelting |
|  20 copper nuggets |         00:58 / 00:29 |      00:57 / 00:30 |      01:05 / 00:27 |
| 200 copper nuggets |         01:01 / 04:56 |      00:58 / 05:00 |      02:01 / 04:17 |
|   4 meat (pot)     |         00:13 / 00:40 |      00:10 / 00:40 |      00:16 / 00:30 |
|  24 meat (pot)     |         00:13 / 04:00 |      00:10 / 04:00 |      00:34 / 03:00 |

### Coal pile (full)

| Fuel type  | Vanilla | Better Smelting |
| ---------: | ------: | --------------: |
|   Charcoal |   08:00 |           08:00 |
|       Coke |   08:00 |           08:15 |
| Brown coal |   08:00 |           13:02 |
| Black coal |   08:00 |           15:30 |
| Anthracite |   08:00 |           36:12 |
