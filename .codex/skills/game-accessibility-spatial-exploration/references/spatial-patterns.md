# Spatial Patterns

These patterns are for making world navigation usable without sight.

## Accessible Cursor

The cursor should support:
- directional movement
- jump-back or previous position
- bookmarks
- coarse and fine movement
- a stable description of the current location

## Glance vs Detail

Use at least two output levels:
- glance: short summary of where the user is and what matters most
- detail: expanded description, nearby entities, state, hazards, or overlay values

## Skip Navigation

Implement commands such as:
- next interesting tile
- next building
- next hazard
- next entity of category X
- next change in overlay value

This is often more important than raw directional movement.

## Scanner Views

When the world is too dense for direct stepping, expose scanner or browser views:
- by category
- by subcategory
- by distance
- by importance

Allow jump from scanner result back into the cursor view.

## Overlay-Aware Reading

Overlays should change:
- what is considered interesting
- what the glance summary says
- what skip navigation targets
- what audio cues are used

