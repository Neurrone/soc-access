# Game-Native Navigation in Patched Games

Use this file when implementing game-native navigation in a patched or decompiled game.

Read [../../game-accessibility-architecture/references/navigation-strategies.md](../../game-accessibility-architecture/references/navigation-strategies.md) first for the shared definition, decision checks, responsibilities, and failure modes.

Read [../../game-accessibility-architecture/references/semantic-items.md](../../game-accessibility-architecture/references/semantic-items.md) when deciding how wrappers, proxies, containers, or semantic items should be structured for the target screen.

This file is only about the patched-game part: how to discover native focus or selection state and keep wrappers in sync through hooks, visibility checks, and screen transitions.

## Patched-Game Concerns

- find the real focus, selection, or screen-change hooks in decompiled code
- verify that the focused control is still alive and visible before handing it to the central accessibility coordinator
- resolve the focused control into a stable accessibility-side wrapper or element
- handle controls that bypass the main focus hook through separate patches or signals
- handle replacement transitions so the old screen stops speaking before the new screen takes over
- keep reflection and hook fragility close to the wrapper or hook layer

## Typical Hook Flow

```text
wrapper_cache = Dictionary<Control, ControlWrapper>()

on_focus_or_selection_changed(control):
    if !is_really_present(control):
        return

    wrapper = wrapper_cache.get(control)
    if wrapper is null:
        wrapper = build_wrapper_for(control)
        wrapper_cache[control] = wrapper

    element = wrapper.to_accessible_element()
    ui_coordinator.set_focused_element(element)

on_screen_changed():
    drop_wrappers_from_old_screen()
    wrapper_cache.clear()
```

What the example is meant to show:
- `wrapper_cache` keeps one wrapper per live control so wrappers can preserve state and avoid being rebuilt every time focus changes
- `build_wrapper_for(control)` means "inspect the game control and create the right accessibility wrapper for it"
- `wrapper.to_accessible_element()` means "convert the wrapper into the accessibility-side element the rest of the mod understands"
- `ui_coordinator.set_focused_element(element)` means "hand the resolved element to the central focus and speech system"

The important point is that hooks feed native selection state into a central coordinator, and wrappers turn raw controls into accessibility elements without making speech calls themselves.

## Patched-Game Risks

- relying on generic focus hooks for controls that bypass them
- treating an object reference as valid after the control has been hidden or freed
- missing container context
- re-announcing unchanged state every frame because the hook fires too often
- replacement transitions leaving the old screen registered
- reflection code and speech policy becoming tangled inside wrappers
