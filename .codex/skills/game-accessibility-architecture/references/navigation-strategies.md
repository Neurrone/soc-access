# Navigation Strategies

## Table of Contents

- [Core Distinction](#core-distinction)
- [Accessibility-Owned Navigation](#accessibility-owned-navigation)
- [Game-Native Navigation](#game-native-navigation)
- [Mixed Use](#mixed-use)
- [Decision Checks](#decision-checks)

Use this file when deciding how a screen, mode, or other part of the game should obtain and present navigation state.

These strategies are shared concepts. They can appear in both API-first mods and reverse-engineered mods.

## Core Distinction

The key question is:

For this screen or mode, does the game already know what is selected, or does the mod need to decide that itself?

Two common strategies appear across game accessibility mods:

- accessibility-owned navigation
- game-native navigation

One project can use both. Choose for each screen or mode, not once for the whole game.

## Accessibility-Owned Navigation

Use this when the accessibility layer must invent or own the navigation model.

The mod decides:
- what "next", "previous", "activate", and "back" mean
- which item is currently active
- how focus is restored
- how modal or contextual layers take over input

This often fits:
- world exploration
- tactical grids
- custom spoken menus
- hover-driven or mouse-first UI
- parts of the game without a stable internal focus model
- screens where native input conflicts with accessibility navigation
- screens where the mod must create its own navigation order

Quick rule:

"I must invent the accessible navigation model."

Illustrative example:

```csharp
public sealed class BuildMenuHandler
{
    private int _index;
    private readonly IReadOnlyList<MenuItem> _items;

    public bool CanHandle(GameState state)
    {
        return state.CurrentMode == GameMode.BuildMenu;
    }

    public void OnEnter()
    {
        _index = 0;
    }

    public bool OnInput(AccessIntent intent)
    {
        if (intent == AccessIntent.NextItem)
        {
            _index = Math.Min(_index + 1, _items.Count - 1);
            return true;
        }

        if (intent == AccessIntent.PreviousItem)
        {
            _index = Math.Max(_index - 1, 0);
            return true;
        }

        if (intent == AccessIntent.Activate)
        {
            _items[_index].Activate();
            return true;
        }

        return false;
    }

    public SpeechMessage DescribeCurrentItem()
    {
        var item = _items[_index];
        return SpeechMessage.Raw($"{item.Label}. {_index + 1} of {_items.Count}.");
    }
}

public void OnAccessibilityIntent(BuildMenuHandler handler, AccessIntent intent)
{
    if (!handler.OnInput(intent))
        return;

    speechPipeline.Speak(handler.DescribeCurrentItem());
}
```

What this is meant to show:
- the mod decides whether this navigation layer is active
- the mod receives accessibility intents directly and decides what they do
- the mod tracks the current item itself
- the handler describes the current item from its own state instead of asking the game for a native focused control
- a separate speech layer decides when to announce that description

In practice, this usually becomes a handler, controller, or screen model that owns navigation for a map, mode, menu, or modal layer.

Core responsibilities:
- decide when this layer is active
- decide what commands it consumes
- track the current focus, cursor, or selection
- describe the current state in a way the speech system can announce
- restore focus and clean up correctly when the layer exits

Common failure modes:
- hidden layers continue to consume input
- push and pop behavior gets out of sync
- lifecycle overlap causes duplicate announcements
- too much engine-specific logic leaks into the navigation layer instead of staying in wrappers or adapters

## Game-Native Navigation

Use this when the game already has an internal navigation or focus model and the accessibility layer can observe and translate it.

The game decides:
- which control, row, card, tab, or item is selected
- how navigation moves between items

The accessibility layer decides:
- how to wrap the selected item semantically
- how to add container context
- how to suppress duplicates
- how to build useful announcements

This often fits:
- controller-driven menus
- list and table screens with real selected rows
- card or inventory UIs with stable internal focus
- settings screens and other conventional control trees
- event-heavy UIs where the main need is better narration
- screens where the game already exposes a stable selected item and the main need is semantic wrapping

Quick rule:

"The game already has a navigation model; I must observe, wrap, and reconcile it."

Illustrative example:

```csharp
public sealed class ShopButtonWrapper
{
    private readonly GameButton _button;

    public ShopButtonWrapper(GameButton button)
    {
        _button = button;
    }

    public bool IsAvailable()
    {
        return _button != null && _button.Visible && _button.Enabled;
    }

    public AccessibleElement ToElement()
    {
        return new AccessibleElement(
            role: "button",
            label: _button.Text,
            status: _button.PriceText,
            help: _button.TooltipText);
    }
}

public void OnGameFocusChanged(GameButton focusedButton)
{
    var wrapper = new ShopButtonWrapper(focusedButton);
    if (!wrapper.IsAvailable())
        return;

    var element = wrapper.ToElement();
    uiCoordinator.SetFocusedElement(element);
}
```

What this is meant to show:
- the game already has a selected control, row, card, or other item
- the mod checks whether that game object is still present and usable
- the mod wraps that object and turns it into an accessibility-side element
- the accessibility element can then provide label, state, help, tooltip, and buffer content in a consistent way
- a separate coordinator or speech layer decides whether and how to announce the element

In practice, this usually becomes a small wrapper around a game control, plus an accessibility-side element model that the speech and buffer systems can understand.

Read [semantic-items.md](semantic-items.md) when the screen needs wrappers, proxies, containers, or a clearer semantic item model.

Core responsibilities:
- confirm that the wrapped control is still available
- identify what role the control represents
- build the primary message for focus
- expose secondary information such as help, tooltip, status, or buffer content
- expose container and position information when that changes what should be announced

Common failure modes:
- relying on one generic focus hook for controls that bypass it
- missing container context
- announcing unchanged state too often
- letting wrappers absorb too much global policy instead of keeping that in a central manager

## Mixed Use

One project can mix strategies freely:
- world or map navigation may use accessibility-owned navigation
- conventional menus may use game-native navigation
- one architecture may still dominate the codebase overall

The important rule is local coherence:
- keep one screen or mode internally consistent
- do not mix ownership of navigation carelessly inside the same interaction model

## Decision Checks

Choose accessibility-owned navigation when the answer to this is yes:

"If I removed the mod's own navigation layer, would this screen or mode still expose a stable selected item that accessibility could follow?"

If the answer is no, accessibility-owned navigation is usually the right fit.

Choose game-native navigation when the answer to this is yes:

"Does the game already expose a stable selected item or focus state that I can observe, even if players cannot access it properly today?"

If the answer is yes, game-native navigation is usually the right fit.
