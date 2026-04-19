---
name: game-accessibility-spatial-exploration
description: Use this skill when a game needs accessible navigation of a world, map, grid, battle board, factory, colony, or other spatial environment. Use it to design accessible cursors, scanners, overlay-aware reading, skip navigation, sonification, and coarse-to-fine spoken exploration.
---

# Game Accessibility Spatial Exploration

This skill is most useful for factory, colony, tactics, roguelike, map, and other spatial games.

## Workflow

1. Define the navigation unit.
   Tile, cell, room, node, chunk, entity, cluster, or path step.
2. Define the accessibility cursor.
   Decide how the user moves, anchors, bookmarks, and returns.
3. Split output into glance and detail.
   One command should summarize the current location; another should drill deeper.
4. Add skip navigation.
   Let the user jump to the next meaningful change, not just the next coordinate.
5. Define overlays or modes.
   Heat, power, oxygen, enemies, production, buildability, pathability, and other overlays often need different spoken summaries.
6. Add scanner or browse views for dense world state.
7. Add spatial earcons and orientation cues.

## Core Rules

- Never force the user to inspect the world one empty tile at a time.
- Give both coarse and fine navigation.
- Separate current-position reading from scan-result browsing.
- Treat overlays as semantic transforms, not just extra text.
- Preserve orientation with cues, headings, distances, and jump-back points.

## References

- Read [../game-accessibility-architecture/references/project-structure.md](../game-accessibility-architecture/references/project-structure.md) for the shared module boundaries that should exist in any accessibility mod.
- Read [../game-accessibility-architecture/references/navigation-strategies.md](../game-accessibility-architecture/references/navigation-strategies.md) when deciding whether a spatial screen, mode, or map view should own navigation directly or reuse any game-native cursor or selection model.
- Read [../game-accessibility-architecture/references/focus-and-context.md](../game-accessibility-architecture/references/focus-and-context.md) when the spatial layer needs explicit context ownership, cursor focus repair, or container context for scanner views and modal map layers.
- Read [references/spatial-project-structure.md](references/spatial-project-structure.md) when adding a world-navigation subtree to an existing mod or starting a world-heavy accessibility project.
- Read [references/spatial-patterns.md](references/spatial-patterns.md) for cursor, scanner, skip, and overlay patterns.
- Read [../game-accessibility-architecture/references/buffers-and-review.md](../game-accessibility-architecture/references/buffers-and-review.md) when scanner results, transient map feedback, or event streams need a reviewable history.
- Read [references/sonification-and-cues.md](references/sonification-and-cues.md) when designing non-speech spatial feedback.

## Minimal Skeleton

```csharp
public interface ISpatialCursor
{
    WorldPoint Position { get; }
    void Move(Direction direction);
    void JumpToNext(SpatialPredicate predicate);
    SpeechMessage DescribeHere();
}
```

## Completion Bar

A spatial accessibility feature should let the user:
- orient themselves
- move intentionally
- skip noise
- inspect details on demand
- switch overlays or modes
- recover from disorientation quickly
