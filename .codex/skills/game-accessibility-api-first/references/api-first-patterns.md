# API-First Patterns

These patterns are generalized for any game with a supported API.

## Router Owns the Spoken UI

Keep one router that owns:
- the active menu or screen
- pushes and pops of child views
- search state
- bind invalidation
- the final decision about what message to announce after interaction

This is stronger than scattering speech calls through button callbacks.

The router should send final speech requests to the speech pipeline. It should not make raw backend calls.

## Key Graphs

Use stable keys for each semantic item. Rebuild the key graph after actions and restore focus by key when possible.

This works especially well for:
- custom menus
- nested lists
- data-driven dialogs
- drill-down flows

## Explicit External Protocols

When you need functionality beyond the API, use explicit narrow protocol messages for:
- speech output
- non-speech audio
- clipboard

Avoid leaking transport details into menu code.

## Incremental Scanners

For large maps or worlds:
- scan incrementally
- sort work by player relevance or proximity
- store results in persistent state
- expose scanner results through a higher-level browsing model

## Build-Time to Runtime Export

If the runtime API hides needed metadata, export it earlier when the game still exposes it, then read the compact exported form at runtime.
