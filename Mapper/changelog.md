# Mapper - Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Fixed
- The client crashed when it tried to render the GUI while the player wasn't controlling any entity.

## [0.5.0] - 2026-04-06
### Added
- **New intermediate items:** Fresh/soaked papyrus strips, wet papyrus sheet.
- Basic support for [Geology Map](https://mods.vintagestory.at/geologymap) mod: Chunks are refreshed and stored periodically, but Mapper processes them and decides if they should be shown.
### Changed
- Made papyrus sheet crafting chain more complex by adding cooking and drying steps.
- Halved parchemnt cost of the precise map.
- Optimized downloading chunks from the cartography table - it unnecessarily refreshed minimap chunks outside of its view.
### Fixed
- GiMap didn't refresh chunks downloaded from the cartography table.

## [0.4.2] - 2026-04-03
### Fixed
- Cartography table download packet could become too large and the server would reject it.

## [0.4.1] - 2026-03-29
### Added
- Russian localization (contributed by Wafflz).
### Fixed
- Potential issues with accessing cartography table variables from multiple threads when trying to synchronize data.
- Cartography table upload packet could become too large and the server would reject it.

## [0.4.0] - 2026-02-22
### Added
- A **cartography table** for sharing mapped chunks with friends. Thank you [hutli](https://github.com/hutli) for the idea and its initial implementation. ([#6](https://github.com/Genhis/VintageStoryMods/pull/6))
- A **papyrus sheet** paper variant. Its recipe is temporary and will be changed in the future.
### Changed
- Rebalanced map properties and changed primitive map recipe to use papyrus sheet.
- Updated parchment texture to be lighter.
- Updated precise map texture and added light border.
### Fixed
- Potential issues with accessing chunk map layer variables from multiple threads.
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
[0.4.0]: https://github.com/Genhis/VintageStoryMods/pull/7
[0.4.1]: https://github.com/Genhis/VintageStoryMods/pull/10
[0.4.2]: https://github.com/Genhis/VintageStoryMods/commit/9f351204c07cd2c5dc2f806a046f4a6b1bb0056e
[0.5.0]: https://github.com/Genhis/VintageStoryMods/pull/11
