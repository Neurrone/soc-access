---
name: game-accessibility-architecture
description: Define the shared architecture for a screen-reader accessibility mod before choosing the implementation path or building a new accessible screen, input flow, speech system, semantic UI layer, or review/help subsystem. Use this as the base layer for both official-mod-API games and reverse-engineered games, then follow with the API-first, reverse-engineering, or spatial-exploration skill as needed.
---

# Game Accessibility Architecture

This skill is the base layer for the rest of the game accessibility skills:
- Use `game-accessibility-api-first` when the game has a supported mod or plugin API.
- Use `game-accessibility-reverse-engineering` when the game must be decompiled or patched.
- Use `game-accessibility-spatial-exploration` when the game needs accessible world, map, or grid navigation.

## Core Rule

Keep accessibility work split into three layers:
1. Capture: obtain raw game state, focus state, widget state, lifecycle, events, and world data.
2. Semantics: convert raw engine objects into stable accessibility concepts such as screen, button, tab, row, card, creature, tile, overlay, or scanner result.
3. Output: build announcements, deliver speech through a narrow pipeline, store review history, and play cues without leaking engine details into announcement code.

Do not let engine widgets or patched hooks speak directly unless there is no alternative. Centralize output decisions.

## Workflow

1. Choose the execution path.
   Use the official API if possible. If not, use the reverse-engineering path for games that must be patched or decompiled.
2. Define the accessibility owner for the feature.
   Decide what object owns the current screen, modal layer, handler, or focus context.
3. Define the meaningful things the player should navigate.
   Write down things like buttons, rows, tabs, cards, creatures, tiles, or scanner results, not raw engine classes.
4. Define the announcement sources.
   Decide which systems produce speech requests, such as focus management, event dispatch, tooltip capture, help, or scanner notifications.
5. Define the speech boundary.
   Keep the speech pipeline narrow. It should take a final speech request and send it to the active backend. It should not own focus policy, event policy, or review-buffer policy.
6. Define help and review early.
   Add contextual help, repeat-last, and review buffers as part of the feature, not as cleanup work.
7. Define the parts you can swap or fake in tests.
   Decide what logic can be tested without the game, what needs engine-linked offline tests, and what must run in the real runtime loop.
8. Define failure logging.
   Log lifecycle mismatches, null controls, reflection failures, patch misses, focus repair, and suppressed speech.

## Shared Design Rules

- Route all speech through one central speech layer.
- Choose the speech backend at runtime, not inside screen logic.
- Keep announcement-producing systems separate from the speech pipeline.
- Preserve stable focus identity across rerenders when possible.
- Prefer small wrappers around game controls instead of reading raw UI objects everywhere.
- Treat contextual help as runtime UI.
- Add review buffers for dense, transient, or fast-changing information.
- Keep localization and message composition structured.
- Use earcons and speech together; do not force speech to carry every state change.
- Keep review storage separate from speech delivery.
- Keep game-specific engine knowledge at the edges, not in the speech rules.

## New Screen Onboarding

When scaffolding a new mod or cleaning up a messy codebase, start with [references/project-structure.md](references/project-structure.md).

When the task is "make the main menu accessible" or "add accessibility to screen X", start by reading [references/screen-onboarding.md](references/screen-onboarding.md).

For reusable architecture rules and generic skeletons, read [references/shared-patterns.md](references/shared-patterns.md).

For the shared vocabulary around accessibility-owned navigation versus game-native navigation, read [references/navigation-strategies.md](references/navigation-strategies.md).

For the shared pattern for context ownership, focus repair, stable focus identity, and container-path diffing, read [references/focus-and-context.md](references/focus-and-context.md).

For the shared structure for semantic items, wrappers, proxies, containers, and resolvers, read [references/semantic-items.md](references/semantic-items.md).

For the shared split between announcement-producing systems, the speech pipeline, backends, cues, and review storage, read [references/speech-and-announcements.md](references/speech-and-announcements.md).

For definitions and design rules around review history, event logs, tooltip replay, and other buffers, read [references/buffers-and-review.md](references/buffers-and-review.md).

For cross-game testing strategy, read [references/testing-strategy.md](references/testing-strategy.md).

## Minimal Skeletons

```csharp
public interface IAccessibleView
{
    string Id { get; }
    string Kind { get; }
    string? GetFocusedItemId();
    IReadOnlyList<string> GetActionIds();
    SpeechMessage BuildInitialAnnouncement();
}
```

```csharp
public interface ISpeechPipeline
{
    void Output(SpeechRequest request);
    void Stop();
}

public sealed class SpeechRequest
{
    public SpeechMessage Message { get; init; }
    public bool Interrupt { get; init; }
}
```

```csharp
public interface IReviewStore
{
    void Append(string channel, SpeechMessage message);
}
```

## Output Quality Bar

An accessible feature is not complete when it merely reads labels. It should give the user:
- a stable way to enter the feature
- a stable navigation model
- enough context to know where they are
- enough help to know what to do next
- a repeat or review path when information is dense
- a test setup that covers what the player experiences, not just the plumbing
