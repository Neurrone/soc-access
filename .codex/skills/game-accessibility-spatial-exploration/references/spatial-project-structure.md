# Spatial Project Structure

Use this file when a project needs an explicit world-navigation subtree for map, grid, colony, tactics, or other spatial interaction.

This is usually an addition to another project structure, not a complete project layout by itself.

## Suggested World Subtree

```text
World/
  Cursor/
  Scanner/
  Overlays/
  Navigation/
  Bookmarks/
  Sonification/
```

## Folder Roles

### `World/Cursor/`

Owns:
- current world position
- movement commands
- glance and detail descriptions
- jump-back behavior

### `World/Scanner/`

Owns:
- scanner passes
- category and subcategory browsing
- result snapshots
- next-interesting-item navigation

### `World/Overlays/`

Owns:
- overlay profiles
- overlay-specific reading rules
- overlay-specific skip logic

### `World/Navigation/`

Owns:
- pathing helpers
- coarse and fine stepping
- orientation utilities
- directional search

### `World/Bookmarks/`

Owns:
- saved positions
- named anchors
- return and restore behavior

### `World/Sonification/`

Owns:
- directional cues
- hazard cues
- target-acquired cues
- overlay-specific sound families

## Integration Rule

Keep the world subtree separate from menu or screen modules. World navigation should feed semantic output and buffers, but it should not be buried inside unrelated UI folders.

## Useful Cross-Links

- for reviewable scanner history, see [../../game-accessibility-architecture/references/buffers-and-review.md](../../game-accessibility-architecture/references/buffers-and-review.md)
- for shared project boundaries, see [../../game-accessibility-architecture/references/project-structure.md](../../game-accessibility-architecture/references/project-structure.md)

