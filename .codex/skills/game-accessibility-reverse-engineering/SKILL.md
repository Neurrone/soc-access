---
name: game-accessibility-reverse-engineering
description: Use this skill when a game has no sufficient official mod API and adding screen-reader accessibility requires decompiling code, finding hook points, patching methods, using reflection, or intercepting input and focus. Use it to make a new screen accessible and implement the chosen navigation strategy in a patched game.
---

# Game Accessibility Reverse Engineering

This skill implements the shared navigation strategies from the architecture skill in a patched or decompiled game.

## Shared Strategy Reminder

Read [../game-accessibility-architecture/references/navigation-strategies.md](../game-accessibility-architecture/references/navigation-strategies.md) first for the shared strategy definitions, decision checks, responsibilities, and failure modes.

This skill focuses on patched-game execution details: hooks, lifecycle mapping, visibility checks, replacement transitions, draw-time capture, and patch-safe wrappers.

## First Pass in Decompiled Code

Before patching anything, find these five things:
1. input bootstrap
2. focus funnel
3. screen lifecycle
4. modal ownership
5. draw-time text such as tooltips, hover text, or transient overlays

These usually tell you where the stable hooks are and whether the screen can be modeled from controls or only from draw-time output.

In this list:
- `input bootstrap` means the code path where keyboard, mouse, controller, or action handlers are registered
- `focus funnel` means the small set of methods where the game updates the selected control or focused item
- `screen lifecycle` means the open, show, hide, disable, destroy, and teardown paths for the screen
- `modal ownership` means how parent and child panels are layered and which one should currently own input
- `draw-time text` means text or UI that only exists during rendering and cannot be read back from stable objects

## Reverse-Engineering Workflow

1. Find the main methods that screens, focus, input, or tooltips actually pass through.
   These are often the best starting points for hooks or patches.
2. Build a lifecycle map for the target screen.
   Do not assume `Show` is enough. Check activation, enable, spawn, hide, teardown, and render paths.
3. Check presence and visibility rules before acting on the screen.
   Do not treat an object reference as proof that the screen or control is really present. Check whether it is active, visible, still in the tree, or still the current screen before announcing it or routing input to it.
4. Check replacement and transition paths.
   One screen may disappear and another may be shown in its place without sharing the same hook path. Make sure the old accessibility state is removed or unfocused before the new one takes over.
5. Choose the navigation strategy for the screen or mode.
6. Decide whether the screen is better represented by:
   a handler on a stack,
   an accessibility-side screen model made of meaningful items,
   a path that reconstructs text or state during rendering,
   or a hybrid.
7. Patch the stable methods that many screens or controls already pass through.
8. Patch controls separately when they bypass the main focus hook, manage input in their own way, or only appear during draw or render.
9. Centralize fragile reflection and raw engine access behind small wrappers around engine objects.
10. Add parts you can fake in tests, plus smoke checks, early.
   Keep as much logic as possible testable without the live game, and add startup checks for critical hooks, reflected fields, and patch registration.
11. Add patch-failure and version-drift logging early.

## Hook Selection Rules

- Prefer stable methods that many controls or screens already pass through.
- Prefer methods whose meaning is likely to stay stable across updates.
- Use screen-specific hooks for exceptions, not as the primary architecture.
- Avoid patching low-level rendering every frame unless the information only exists there.

## Lifecycle Hook Guide

- `Activate` or equivalent:
  good when screen logic is active even before the user can interact with it
- `Show` or equivalent:
  good when visibility and first announcement should happen together
- `OnShow` or post-show callback:
  good when the visual tree is not ready during `Show`
- enable callback such as `OnCmpEnable`:
  good for components that do not use a classic screen show path
- `OnSpawn`:
  good for screens that populate controls only once at creation time
- draw or render hook:
  use when the information is ephemeral and not retained in usable objects

## New Screen Workflow

For a prompt like "make the main menu accessible", start with the shared onboarding checklist in [../game-accessibility-architecture/references/screen-onboarding.md](../game-accessibility-architecture/references/screen-onboarding.md).

Then add the patched-game-specific checks:
1. Find the lifecycle hooks for open, close, hide, disable, and replacement.
2. Check which visibility or active-state tests must pass before the mod treats the screen or control as present.
3. Determine whether the screen can be read through controls, needs small wrappers around controls, or only exists during draw or render.
4. Use the shared navigation-strategy reference to decide whether the mod should own navigation or reuse the game's native navigation state for this screen.
5. Identify any controls that bypass the main focus or lifecycle path and handle those separately.

## Reverse-Engineering Test Focus

Start with the shared testing strategy in [../game-accessibility-architecture/references/testing-strategy.md](../game-accessibility-architecture/references/testing-strategy.md). In patched games, give extra attention to:

- handler-stack behavior and cleanup
- focus synchronization
- reflection adapters and small wrappers around engine objects
- tooltip or text capture helpers
- speech queueing, deduplication, and review-buffer logic
- how scanner results are grouped, filtered, or reorganized
- patch registration smoke checks

For offline or startup smoke coverage, verify:

- critical patches applied
- reflected targets still resolve
- core accessibility managers still initialize
- the main screen path still produces a meaningful accessible item

For runtime smoke coverage, verify:

- screen open, close, hide, and replacement transitions
- default focus behavior after a screen appears
- tooltip or hover capture that depends on render timing
- special-case controls that bypass the main focus or lifecycle path

If too much logic can only be tested by launching the full game, move more behavior out of the patch layer and into wrappers, handlers, focus managers, formatters, or snapshot builders. The patch layer should capture state and hand it to the rest of the mod, not own most of the behavior.

## References

- Read [../game-accessibility-architecture/references/project-structure.md](../game-accessibility-architecture/references/project-structure.md) for the shared module boundaries that should exist in any accessibility mod.
- Read [../game-accessibility-architecture/references/navigation-strategies.md](../game-accessibility-architecture/references/navigation-strategies.md) when deciding which navigation strategy a screen or mode should use.
- Read [../game-accessibility-architecture/references/focus-and-context.md](../game-accessibility-architecture/references/focus-and-context.md) when designing active context ownership, focus repair, container-path diffing, or transition-safe focus updates in a patched game.
- Read [../game-accessibility-architecture/references/semantic-items.md](../game-accessibility-architecture/references/semantic-items.md) when patched hooks need wrappers, proxies, containers, or a clearer semantic item model for raw engine objects.
- Read [../game-accessibility-architecture/references/speech-and-announcements.md](../game-accessibility-architecture/references/speech-and-announcements.md) when deciding what should produce speech requests and what should remain a narrow speech-output boundary.
- Read [../game-accessibility-architecture/references/screen-onboarding.md](../game-accessibility-architecture/references/screen-onboarding.md) for the shared checklist for adding accessibility to a new screen.
- Read [references/reverse-project-structure.md](references/reverse-project-structure.md) when scaffolding or reorganizing a patch-based accessibility mod, especially to decide the dominant architecture while still allowing variation between screens and modes.
- Read [references/accessibility-owned-navigation-in-patched-games.md](references/accessibility-owned-navigation-in-patched-games.md) when a patched screen or mode needs hook-driven handler activation, input interception, or stack-based context ownership.
- Read [references/game-native-navigation-in-patched-games.md](references/game-native-navigation-in-patched-games.md) when a patched screen or mode needs focus hooks, wrapper resolution, visibility guards, or transition-safe synchronization with the game's own selection state.
- Read [../game-accessibility-architecture/references/buffers-and-review.md](../game-accessibility-architecture/references/buffers-and-review.md) when capturing transient tooltips, event narration, scanner output, or other reviewable history.

## Rules

- Patch the smallest stable method that gives you the needed behavior.
- Keep engine-specific code near the hook or patch layer, not spread through the rest of the mod.
- Check visibility and active state before treating a screen or control as present.
- Handle close, hide, disable, and replacement transitions explicitly.
- Treat draw-time capture as a valid tactic, not a failure.
- Log every patch assumption that could break across versions.
- Log when a target method, field, or property is missing.
- Keep patch registration explicit enough that you can audit what should have been hooked.
- Keep each game area internally coherent. Mixing strategies is fine; mixing them carelessly inside one screen or mode is not.
- Test pure logic separately from patch glue whenever possible.
