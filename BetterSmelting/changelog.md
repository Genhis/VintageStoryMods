# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-04-06
### Changed
- Updated patches for Vintage Story 1.22.
### Removed
- Forge patch - the forge was significantly reworked and I think it doesn't need any changes to its fuel mechanics.

## [0.2.1] - 2025-07-23
### Fixed
- Liquids with small portion size took too long to cook.

## [0.2.0] - 2025-07-13
### Added
- A setting to change minimum fuel temperature which the forge accepts.
### Changed
- Renamed the config file to "BetterSmelting.json" to match the mod name.

## [0.1.1] - 2025-07-12
### Fixed
- [VS 1.21] Forge entity wasn't filling up properly when adding fuel.
- Putting anthracite in the forge could overfill it.

## [0.1.0] - 2025-06-21
### Added
- **Coal pile patch:** Layer burn duration depends on item burn duration and burn temperature.
- **Firepit patch:**
  - Item stacks keep their temperature when one item is smelted, but heating time scales with stack size.
  - Contribution of item stack temperature above melting point towards smelting progress is no longer clamped to integer multiples of melting temperature (items smelt faster).
  - Cooking pot and crucible heating speed depends on items in the slots (configurable).
  - When adding items to cooking containers, the temperature is averaged between all slots instead of taking the lowest value.
- **Forge patch:** The amount of fuel added depends on item burn duration and burn temperature.

[0.1.0]: https://github.com/Genhis/VintageStoryMods/commit/0a96ca2f734c39b26539c1a7022007488a7f97e4
[0.1.1]: https://github.com/Genhis/VintageStoryMods/commit/07d5bb56e3ae7195dcd336e715299f2f3cf95b06
[0.2.0]: https://github.com/Genhis/VintageStoryMods/commit/36b12b36a9baec48d7617f0ef4a2b2578deb4366
[0.2.1]: https://github.com/Genhis/VintageStoryMods/commit/3c3484d11778eaf8e34c8219a0c29374e71288a2
