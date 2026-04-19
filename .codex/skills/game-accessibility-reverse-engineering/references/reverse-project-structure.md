# Reverse-Engineering Project Structure

Use this file only after reading the shared structure reference in [../../game-accessibility-architecture/references/project-structure.md](../../game-accessibility-architecture/references/project-structure.md).

That shared file is the canonical structure guide. This file only describes what to add or split differently in patched or decompiled games.

## Reverse-Engineering Additions

Add these folders only when the reverse-engineering path needs them:

- `Patches/` or `Hooks/`
  capture engine events, lifecycle transitions, focus changes, or draw-time state
- `Context/`
  detect active screen, mode, overlay, or modal ownership when patched lifecycle is inconsistent
- `Focus/`
  centralize focus snapshots, suppression, and context changes when the game already exposes native selection state
- `Elements/` and `Proxies/`
  split `Adapters/` when wrappers and accessibility-side element models need to stay separate
- `Tests/Offline/`
  add executable tests that link against game assemblies without running the full game

## Structure Deltas

For projects that lean toward accessibility-owned navigation:
- keep more behavior under `Handlers/`
- add `Patches/` and often `Context/`
- keep raw engine access near `Patches/`, `Context/`, and `Adapters/`

For projects that lean toward game-native navigation:
- keep more behavior under `Screens/`
- add `Hooks/`, `Focus/`, `Elements/`, and `Proxies/`
- keep raw engine access near `Hooks/` and `Proxies/`

Mixed projects may use both sets of additions.

## Reverse-Engineering Test Delta

Compared with the shared structure, the main extra folder is `Tests/Offline/` for assembly-linked executable tests.

For the shared test layers, read [../../game-accessibility-architecture/references/testing-strategy.md](../../game-accessibility-architecture/references/testing-strategy.md). For reverse-specific testing priorities, read [../SKILL.md](../SKILL.md) and use the `Reverse-Engineering Test Focus` section.

## Reverse-Engineering Design Checks

- If `Patches/` or `Hooks/` contain most of the behavior, move logic back into `Handlers/`, `Screens/`, `Adapters/`, `Elements/`, or `Proxies/`.
- If buffers or speech logic live inside patch classes, move them back into the shared `Buffers/` and `Speech/` modules.
- If screen logic cannot run without reflected objects, move more translation into `Adapters/` or `Proxies/`.
