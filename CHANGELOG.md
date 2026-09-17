# Changelog

## 0.2.0 - 2026-09-17

- Replaced the previous Core/Host/View layers with `ScreenStackElement` and `StackableScreenElement`.
- Added retained Active/Covered transitions and per-screen focus restoration.
- Added lifecycle callbacks for activation, coverage and removal.
- Removed Back, modal, gameplay-input, command, action-bar, Input System and global-context APIs.
- Moved screen IDs, asset catalogs and instance lifetime policy to consuming projects.

This is a breaking redesign. There is no compatibility layer for the 0.1 API.

## 0.1.0

- Added screen registration and optional stacks with project-owned identifiers.
- Added UI Toolkit views, activation, modal input boundaries and focus restoration.
- Added shared commands, native button bindings and action hints.
- Added Input System integration for additional commands and device-aware hints.
- Added a runnable sample with enum identifiers and package usage documentation.

This is the first development release. See README.md for the supported scope and validation results.
