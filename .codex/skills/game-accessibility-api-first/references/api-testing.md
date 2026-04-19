# API-First Testing

Use this file when building tests for an accessibility mod that runs through a supported mod or plugin API.

Read [../../game-accessibility-architecture/references/testing-strategy.md](../../game-accessibility-architecture/references/testing-strategy.md) first for the shared three-layer test model.

This file only adds API-first-specific emphasis.

## API-First Test Delta

Keep these especially strong in API-first projects:
- key-graph generation
- focus restoration by stable key
- search and filtering
- message building
- rich-text cleanup
- route selection
- cache behavior

Use in-engine runtime coverage for:
- tick-based behavior
- event order and priority
- entity creation and destruction
- menu open and close flows
- selection movement
- world scanning against real game state
- announcements that depend on the runtime's localized values

## API-First Success Checks

Good tests assert semantic outcomes:
- the same item stays focused after rerender
- entity ordering is stable
- opening a menu announces the correct starting item
- movement keys update the accessibility cursor as expected
- scanner output reflects real nearby world state

Avoid tests that only prove the harness can call the API.

## API-First Architecture Hint

If a feature is hard to unit test, move more of its logic into:
- graph builders
- routers
- message builders
- search helpers
- snapshot transformers

Leave the game API layer thin.
