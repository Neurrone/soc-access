---
name: game-accessibility-api-first
description: Use this skill when a game exposes an official mod API, plugin API, scripting API, or other supported extension point, and the goal is to build or extend screen-reader accessibility without reverse engineering the executable. Use it to make menus, lists, scanners, overlays, and spoken UI flows accessible through supported hooks first.
---

# Game Accessibility API-First

This skill emphasizes these API-first patterns:
- event-driven UI routing
- key-graph navigation
- centralized announcement logic with a narrow speech boundary
- external protocol boundaries for speech, audio, and clipboard
- incremental world scanning and data export from build-time to runtime

## Workflow

1. Inventory the supported API.
   Find events, input hooks, GUI primitives, localization APIs, world queries, and build-time data hooks.
2. Choose the navigation strategy for this screen or mode.
   Use [../game-accessibility-architecture/references/navigation-strategies.md](../game-accessibility-architecture/references/navigation-strategies.md) to decide whether this part of the game should own navigation or reuse game-native navigation state.
3. Decide what stays inside the game and what crosses a process boundary.
   Keep interaction logic in the game. Move richer TTS, audio, clipboard, or launcher integration out only when the API cannot provide it cleanly.
4. Build a deterministic navigation model.
   Prefer an explicit graph, ordered menu, or query model over ad-hoc widget traversal.
5. Rebuild or refresh the model after interaction.
   Official APIs often expose state snapshots, not stable focus identities.
6. Cache expensive queries and localizations.
7. Use incremental work for large-world scanning.
8. Export build-time data into runtime if the runtime API cannot see it directly.
9. Test at two levels.
   Keep graph, search, formatting, and routing logic testable outside the game, then add in-engine tests for ticks, events, world state, and UI transitions.

## Architecture Rules

- Treat the API as the primary source of truth, not the rendered pixels.
- Prefer explicit navigation graphs over implicit focus repair.
- Make search, rerender, and bind invalidation part of the router.
- Keep launcher or service protocols narrow and explicit.
- When the API is missing data at runtime, export it during the build or data stage.

## API Screen Checks

For a prompt like "make the main menu accessible" in an API-first game, start with the shared onboarding checklist in [../game-accessibility-architecture/references/screen-onboarding.md](../game-accessibility-architecture/references/screen-onboarding.md).

Then add these API-specific checks:
1. Collect the screen state from supported API objects.
2. Convert that state into an ordered semantic model.
3. Build a deterministic navigation graph from that model.
4. Rebuild or refresh the graph after each action from current API state.
5. Restore focus by stable key, not by previous index, when possible.
6. Keep search, help, and repeat in the router rather than scattering them through callbacks.

Centralize:
- current menu or router state
- focus key
- search state
- help text
- final message building
- announcement decisions such as interrupt versus queue
- launcher or TTS protocol messages

Keep screen-specific:
- how the semantic items are derived
- what actions each item exposes
- what secondary details matter for the current screen

Watch for these API-specific failure modes:
- focus key becomes invalid after rerender
- localized strings arrive late or are expensive to resolve repeatedly
- world scans block the UI loop
- API events fire before supporting state is ready
- copied labels are correct but semantic grouping is wrong

## When To Read References

- Read [../game-accessibility-architecture/references/project-structure.md](../game-accessibility-architecture/references/project-structure.md) for the shared module boundaries that should exist in any accessibility mod.
- Read [../game-accessibility-architecture/references/navigation-strategies.md](../game-accessibility-architecture/references/navigation-strategies.md) when deciding whether an API-driven screen or mode should own navigation or observe game-native navigation.
- Read [../game-accessibility-architecture/references/focus-and-context.md](../game-accessibility-architecture/references/focus-and-context.md) when deciding how the router or screen should store focus, repair it after rerender, and avoid repeating unchanged container context.
- Read [../game-accessibility-architecture/references/semantic-items.md](../game-accessibility-architecture/references/semantic-items.md) when deciding whether an API-first feature should build semantic items directly from game state or still use wrappers around raw API objects.
- Read [../game-accessibility-architecture/references/speech-and-announcements.md](../game-accessibility-architecture/references/speech-and-announcements.md) when deciding what should produce speech requests, what belongs in the speech boundary, and what should stay in review storage.
- Read [references/api-first-patterns.md](references/api-first-patterns.md) for reusable API-first patterns.
- Read [../game-accessibility-architecture/references/buffers-and-review.md](../game-accessibility-architecture/references/buffers-and-review.md) when the feature needs review history for menus, scanners, notifications, or other transient output.
- Read [references/api-testing.md](references/api-testing.md) when deciding what should be unit tested versus run inside the game runtime.

## Generic Skeleton

```lua
local graph = {}

function graph:build(model)
  return {
    { key = "play", label = "Play", activate = model.play },
    { key = "options", label = "Options", activate = model.options },
  }
end

function graph:refresh(model, focusKey)
  local items = self:build(model)
  for _, item in ipairs(items) do
    if item.key == focusKey then
      return items, item.key
    end
  end

  return items, items[1] and items[1].key or nil
end
```

What this example is meant to show:
- rebuild the menu model from current API state after an action
- restore focus by a stable semantic key, not by old index
- fall back to the first item when the old key no longer exists

## Escalation Rule

Do not jump to reverse engineering just because the first API attempt feels awkward. First check whether the API can support:
- a custom navigation model
- exported static data
- background scanning
- out-of-process speech or audio
- a fully custom accessible menu built from game state

If those are still insufficient, move to `game-accessibility-reverse-engineering`.

## Testing Rule

For API-first games, do not rely only on pure unit tests. If the feature depends on event order, ticks, entity state, or the game's real UI loop, add in-engine integration tests around that behavior.
