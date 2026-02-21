# Mapper - Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- A **cartography table** for sharing mapped chunks with friends. Thank you [hutli](https://github.com/hutli) for the idea and its initial implementation. ([#6](https://github.com/Genhis/VintageStoryMods/pull/6))
- A **papyrus sheet** paper variant. Its recipe is temporary and will be changed in the future.
### Changed
- Rebalanced map properties and changed primitive map recipe to use papyrus sheet.
- Updated parchment texture to be lighter.
- Updated precise map texture and added light border.
### Fixed
- A variable could be concurrently accessed from multiple threads when sending chunk redraw updates to GiMap mod.
- Regular compass recipe wasn't grouped in the handbook.

## [0.3.0] - 2026-01-20
### Added
- Basic support for [GiMap](https://mods.vintagestory.at/gimap) mod:
  - Chunks are refreshed and stored periodically, but Mapper processes them and decides if they should be shown.
  - OreMapLayer is limited to processing one chunk per iteration, otherwise it would stall loading of other layers.

## [0.2.0] - 2026-01-09
### Added
- An alternative recipe for primitive compass which uses iron ore nuggets.
### Changed
- AutoMapMarkers error messages about unrevealed map were given 10 seconds cooldown.
- Renamed `mapper enable` command to `mapper restore`.
### Fixed
- AutoMapMarkers would spam error messages about unrevealed map for disabled object types.
- Mapper wouldn't load on non-Windows systems because of the release zip file structure. ([#3](https://github.com/Genhis/VintageStoryMods/issues/3))
- Mapper raised an exception when used in save files which had the world map disabled.
- Compass didn't point to true north.

## [0.1.0] - 2026-01-07
### Added
- **Main items:** Compass, map and drawing kit variations.
- **Intermediates:** Compass base, coloring pigments.
- **World map patch:** The map no longer updates automatically and costs resources.
  - Player position is only visible in charted areas or when holding a compass.
  - Waypoints can only be added to charted areas, even the last death position and ore markers.
  - Includes a handbook guide to explain the mechanics.
### Changed
- Increased refresh rate of coordinates HUD from 250 ms to 50 ms.

[0.1.0]: https://github.com/Genhis/VintageStoryMods/pull/2
[0.2.0]: https://github.com/Genhis/VintageStoryMods/pull/4
[0.3.0]: https://github.com/Genhis/VintageStoryMods/pull/5
