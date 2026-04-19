# Project Structure

## Table of Contents

- [Core Rule](#core-rule)
- [Suggested Baseline Layout](#suggested-baseline-layout)
- [Folder Responsibilities](#folder-responsibilities)
- [Mapping Rules](#mapping-rules)
- [Design Checks](#design-checks)
- [Starting Rule](#starting-rule)

Use this file when starting a new accessibility mod or when a current project has grown without clear module boundaries.

This file defines the responsibility map that should exist in almost any serious accessibility mod, even if the exact folder names change by engine or language.

## Core Rule

Organize by responsibility, not by game screen or by raw engine type.

Good folders isolate:
- input capture
- semantic modeling
- speech delivery
- review storage
- cue playback
- review buffers
- help and commands
- diagnostics
- tests

Avoid mixing patch glue, raw engine access, and announcement logic in the same folder.

## Suggested Baseline Layout

```text
AccessibilityMod/
  Input/
  Screens/
  Handlers/
  Adapters/
  Speech/
  Buffers/
  Help/
  Events/
  Localization/
  Diagnostics/
  Tests/
```

## Folder Responsibilities

### `Input/`

Owns:
- input intents
- input routing
- keybinding or action-map integration
- mode-sensitive command dispatch

Should not own:
- message formatting
- raw speech calls

### `Screens/`

Owns:
- accessible screens
- screen-specific state
- screen-specific focus models
- accessibility-side screen trees

Use `Screens/` for parts of the game that behave like menus, dialogs, panels, lists, inventories, or other bounded screen-like interfaces.

### `Handlers/`

Owns:
- handlers
- context-specific command handling
- accessibility-owned navigation layers
- mode-specific state machines

Use `Handlers/` for parts of the game where the mod owns navigation and input behavior more directly, such as world navigation, tool modes, layered contexts, or full input interception flows.

Some projects will use mostly `Screens/`.
Some will use mostly `Handlers/`.
Mixed projects may use both.

Keep the distinction simple:
- `Screens/` model bounded UI areas
- `Handlers/` own active interaction behavior and command routing

If you are not sure which parts of the project belong under `Screens/` versus `Handlers/`, read [navigation-strategies.md](navigation-strategies.md). That file explains when the mod should own navigation directly and when it should follow the game's own navigation state.

### `Adapters/`

Owns:
- wrappers around engine objects
- building meaningful accessibility wrappers for game controls
- raw widget traversal
- reflection helpers

This is where engine-specific knowledge should concentrate.

Read [semantic-items.md](semantic-items.md) when deciding whether the project should split wrappers, proxies, elements, containers, or resolvers more explicitly.

### `Speech/`

Owns:
- speech pipeline
- speech backend selection
- final text output
- speech-specific normalization or filtering

Should not own:
- focus policy
- event policy
- help policy
- review-buffer categories or lifecycle

If the mod needs a separate home for cue playback, use `Audio/` or `Cues/` instead of folding cues into the speech boundary by default.

### `Buffers/`

Owns:
- review history
- event buffers
- tooltip replay
- scanner result history

See [buffers-and-review.md](buffers-and-review.md) for the behavior model.

### `Help/`

Owns:
- contextual help content
- command summaries
- help screen or help overlays

### `Events/`

Owns:
- game-to-accessibility event translation
- event narration rules
- event registration and filtering

This is a common place for an event dispatcher that decides what should be spoken and what should be added to review history.

### `Localization/`

Owns:
- localization resolvers
- tokenized or structured message definitions
- text normalization helpers

### `Diagnostics/`

Owns:
- logging
- patch diagnostics
- runtime assertions
- developer debug views or trace dumps

### `Tests/`

Split by test type when the project grows:

```text
Tests/
  Unit/
  Offline/
  Runtime/
```

See [testing-strategy.md](testing-strategy.md) for how these layers differ.

## Mapping Rules

Map the baseline layout to the implementation style:
- handler-stack-heavy systems often put more behavior under `Handlers/`
- screen-heavy systems often put more behavior under `Screens/`
- systems that follow the game's own navigation state often split `Adapters/` into:
  - `Elements/` for the accessibility-side items the mod wants to speak and navigate
  - `Proxies/` for the wrappers that read game controls and translate them into those items
- API-first mods often add `Router/`, `Storage/`, or `Protocol/`
- mods with richer audio may add `Audio/` or `Cues/`
- world-heavy mods often add `World/` as a top-level subtree

The names may change. The responsibilities should not.

## Design Checks

If a folder contains both:
- raw engine hooks and speech text, or
- patch registration and semantic business logic

the boundaries are probably wrong.

## Starting Rule

When scaffolding a new project, start with the baseline layout, then specialize it with the execution-path skill:
- supported API: read [../../game-accessibility-api-first/SKILL.md](../../game-accessibility-api-first/SKILL.md)
- patched or decompiled game: read [../../game-accessibility-reverse-engineering/references/reverse-project-structure.md](../../game-accessibility-reverse-engineering/references/reverse-project-structure.md)
- world-heavy navigation: read [../../game-accessibility-spatial-exploration/references/spatial-project-structure.md](../../game-accessibility-spatial-exploration/references/spatial-project-structure.md)
