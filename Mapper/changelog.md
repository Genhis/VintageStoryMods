# Mapper - Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
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
