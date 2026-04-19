# Testing Strategy

Use this file when deciding how to test an accessibility mod feature or subsystem.

## Test Layers

Split tests into three layers.

### 1. Pure Logic Tests

Use plain unit tests for code that should not depend on a live game runtime:
- search ranking
- graph navigation
- message composition
- localization cleanup
- deduplication
- taxonomy and sorting
- tooltip text cleanup
- scanner snapshots
- help aggregation

This should be the largest layer because it is the cheapest and most stable.

### 2. Engine-Linked Offline Tests

Use these when code needs game types, assemblies, or generated data, but not a running game process.

Good uses:
- handler-stack contracts
- speech pipeline delivery rules
- announcement-policy code that can run without a live game
- reflection adapters
- patch registration smoke checks
- decompiled-type integration that can run in a test executable

These tests are especially useful for reverse-engineered games.

### 3. In-Engine Integration Tests

Run these inside the real game runtime when behavior depends on:
- ticks or frame updates
- entity creation and destruction
- live focus changes
- UI rerender timing
- event delivery order
- real world scanning
- actual menu or screen transitions

These are the highest-value tests for API-first interaction flows and for fragile end-to-end accessibility behavior.

## What To Test

Prefer testing what the player experiences over just checking whether internal methods ran.

Good assertions:
- focus restores to the same semantic item after rerender
- container context only announces when it changes
- duplicate speech is suppressed correctly
- search picks the best semantic match
- entity or item ordering is stable
- scanner results prune or regroup correctly
- settings and event metadata round-trip correctly

Weaker assertions:
- a callback fired
- a field was non-null
- a method returned any string at all

## Design For Testability

Keep accessibility logic outside the engine boundary when possible.

Make these swappable:
- speech sink
- speech backend
- time/frame source
- log sink
- input source
- focus source
- localization resolver

If the only way to test a subsystem is by launching the whole game, the architecture is usually too coupled.

## Coverage Guidance

Aim for:
- pure logic tests for most ranking, formatting, filtering, and semantic transforms
- engine-linked offline tests for contracts that rely on game assemblies
- in-engine tests for lifecycle, focus, input, and world-state behavior

Do not stop at utility tests. Accessibility regressions often happen in focus, lifecycle, and timing.

Use the API-first and reverse-engineering testing references only for path-specific deltas on top of this shared model.
