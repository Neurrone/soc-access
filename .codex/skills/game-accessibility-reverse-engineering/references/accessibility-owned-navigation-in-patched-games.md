# Accessibility-Owned Navigation in Patched Games

Use this file when implementing accessibility-owned navigation in a patched or decompiled game.

Read [../../game-accessibility-architecture/references/navigation-strategies.md](../../game-accessibility-architecture/references/navigation-strategies.md) first for the shared definition, decision checks, responsibilities, and failure modes.

This file is only about the patched-game part: how to wire that strategy into hooks, lifecycle events, visibility checks, and input interception.

## Patched-Game Concerns

- find the hooks that tell you when the layer should enter and exit
- verify that the screen, mode, or tool is really active before pushing a handler
- remove or deactivate handlers when the screen hides, disables, or is replaced
- intercept input at a point where the mod can consume it before the game does something inaccessible with it
- decide where barriers belong so lower handlers stop receiving input at the right time
- use draw-time capture when the game does not expose usable controls or state objects

## Typical Hook Flow

```text
current_handler = null

on_screen_or_mode_enter(context):
    if !is_really_present(context):
        return

    current_handler = BuildMenuHandler(context)
    ui_coordinator.set_current_message(current_handler.describe_current_item())

on_screen_or_mode_exit(context):
    if current_handler != null and current_handler.owns(context):
        current_handler = null

on_accessibility_input(intent):
    if current_handler == null:
        return

    handled = current_handler.handle_intent(intent)
    if handled:
        ui_coordinator.set_current_message(current_handler.describe_current_item())
```

What the example is meant to show:
- `current_handler` is the accessibility-owned navigation layer that is currently active
- `BuildMenuHandler(context)` means "create the right handler for the screen or mode that just became active"
- `current_handler.owns(context)` means "check whether the closing or replacement event belongs to this handler"
- `current_handler.handle_intent(intent)` means "let the handler decide what next, previous, activate, or back should do"
- `ui_coordinator.set_current_message(...)` means "hand the handler's current description to the central focus and speech system"

The important point is that patched hooks decide when the handler becomes active, the handler decides what navigation means, and a separate coordinator decides when to announce the handler's current state.

## Patched-Game Risks

- hidden handlers leaking input
- mismatched push and pop
- duplicate announcements when multiple lifecycle hooks fire for one visible transition
- stale handlers surviving after a replacement or temporary hide
- input interception that blocks too much or too little
- too much patch and reflection code leaking into handlers instead of staying near the hook layer
