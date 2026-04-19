# Focus and Context

Use this file when the mod needs a clear model for:

- what currently owns input
- what item is currently focused
- how focus survives rerender, replacement, or screen transitions
- how container context is announced without repetition

This pattern is shared across API-first and reverse-engineered mods.

## Table of Contents

- [Core Split](#core-split)
- [Terms](#terms)
- [Recommended Pattern](#recommended-pattern)
- [Flow](#flow)
- [Context Ownership](#context-ownership)
- [Focus Repair](#focus-repair)
- [Container Context](#container-context)
- [Rules](#rules)
- [Minimal Shapes](#minimal-shapes)
- [Example Flows](#example-flows)

## Core Split

Keep these concerns separate:

- `ContextStack`
  owns which screen, modal, handler, overlay, or mode currently owns input
- `FocusTarget`
  stores the minimal identity of what should be focused
- `FocusManager`
  resolves the focus target, repairs it if needed, deduplicates changes, and produces the final focus announcement
- `PathTracker`
  remembers container path so the mod only re-announces container context when it actually changes

Do not let raw hooks, widgets, or handlers speak directly when focus changes. They should update focus state. A central focus manager should decide whether there is a real focus change and what final message to send upstream to the speech system.

## Terms

### Context

A context is a screen, dialog, panel, overlay, handler, world mode, or other layer that can own input and focus.

Examples:

- main menu
- build menu
- map overlay
- tooltip-heavy modal dialog
- world cursor mode

### FocusTarget

A `FocusTarget` is the minimal identity the mod stores for "what should be focused right now."

It is not:

- the raw engine control
- the final speech message
- the whole accessibility-side item model

It is only the reference used to resolve the current focused item.

Use:

- `StableKey` when the mod owns navigation
- `NativeHandle` when the game owns navigation
- both when a context can resolve either form safely

### AccessibleElement

For this pattern, an `AccessibleElement` only means:

- the resolved accessibility-side item that focus is currently on
- the object that can answer:
  - is it visible
  - what is its main focus message
  - what container path is it in
  - what position message, if any, should be added

Read [semantic-items.md](semantic-items.md) for the fuller guidance on semantic items, wrappers, proxies, containers, and resolvers.

## Recommended Pattern

Use:

1. a `ContextStack` to decide which context is active
2. a `FocusManager` to reconcile focus changes
3. a `PathTracker` to avoid repeating unchanged container context

## Flow

1. Detect that focus should change.
2. Update the current `FocusTarget`.
3. The `FocusManager` runs.
4. The `FocusManager` checks the active context from the `ContextStack`.
5. The active context resolves the `FocusTarget` into an `AccessibleElement`.
6. If the target is stale or invalid, the active context repairs focus.
7. The `PathTracker` builds the focus announcement using:
   - changed container path
   - current item focus message
   - optional position message
8. If the result is meaningfully different, the `FocusManager` passes it to the announcement-producing system.
9. Buffer updates happen after focus has been committed.

## Context Ownership

Every focus change must be interpreted relative to the active context.

The active context is responsible for:

- saying whether it is still present
- resolving a focus target into an accessible element
- repairing focus when the old target disappears
- cleaning up when the context is removed or replaced

Examples:

- in a handler-stack mod, the top handler is usually the active context
- in a screen-stack mod, the top visible screen is usually the active context

## Focus Repair

Focus repair is mandatory.

If the previously focused item disappears because of:

- rerender
- tab switch
- filtering
- item removal
- screen replacement
- hidden control state

then the context must choose a replacement target before the mod announces focus again.

Good repair sources:

- stable logical key
- nearest surviving key in ordered traversal
- first visible child
- context-specific default item

Bad repair sources:

- stale array index with no validation
- destroyed native handle
- hidden control accepted as still focused

## Container Context

Container context should be tracked separately from current item focus.

Announce container context when it changes, such as:

- entering a new screen
- moving into a new group, tab, panel, or list
- moving out of a modal into its parent

Do not repeat unchanged container context on every move inside the same container.

The `PathTracker` should:

- store the last announced container path
- compare it with the new path
- only emit the changed portion

## Rules

- Check whether the current context is still present before using it.
- Check whether the resolved item is visible before treating it as focused.
- Prefer stable logical keys for rerender-heavy UI.
- Keep raw focus hooks dumb. They should capture focus change, not build announcements.
- Reset path tracking when the active context changes completely.
- Update review buffers after focus is committed, not before.
- Remove or unfocus old contexts when a replacement screen takes over.
- Keep one focus owner per screen or mode. Do not let multiple layers claim focus at once unless the stack model is explicit.

## Minimal Shapes

```csharp
public sealed class FocusTarget
{
    // Stable logical identity such as a menu key, row id, tab id, card id, or tile id.
    public string? StableKey { get; init; }

    // Native engine object when the game already exposes a selected control or item.
    public object? NativeHandle { get; init; }
}
```

```csharp
public interface IAccessibilityContext
{
    string ContextId { get; }
    bool IsPresent();
    bool BlocksUnderlyingInput { get; }

    FocusTarget? GetCurrentFocus();
    AccessibleElement? ResolveFocus(FocusTarget target);
    FocusTarget? RepairFocus(FocusTarget? previousTarget);

    void OnActivated();
    void OnDeactivated();
}
```

```csharp
public abstract class AccessibleElement
{
    public abstract string ElementId { get; }
    public abstract bool IsVisible { get; }
    public abstract SpeechMessage BuildFocusMessage();

    // Optional container path from outermost to innermost container.
    public virtual IReadOnlyList<SpeechMessage> BuildContainerPath() => [];

    // Optional "3 of 10", row/column, or similar position detail.
    public virtual SpeechMessage? BuildPositionMessage() => null;
}
```

```csharp
public sealed class ContextStack
{
    public void Push(IAccessibilityContext context);
    public void Pop(string contextId);
    public void Replace(string oldContextId, IAccessibilityContext nextContext);
    public void RemoveStaleContexts();
    public IAccessibilityContext? Current();
}
```

```csharp
public sealed class FocusManager
{
    public void SetNativeFocus(string contextId, object nativeHandle);
    public void SetLogicalFocus(string contextId, string stableKey);
    public void ClearFocus(string contextId);

    public SpeechMessage? ReconcileCurrentFocus();
}
```

```csharp
public sealed class PathTracker
{
    public SpeechMessage? BuildFocusAnnouncement(AccessibleElement element);
    public void Reset();
}
```

## Example Flows

### Game-Native Focus Flow

```csharp
public void OnGameFocusChanged(Control control)
{
    focusManager.SetNativeFocus("shop-screen", control);
}

public void OnFrame()
{
    var message = focusManager.ReconcileCurrentFocus();
    if (message != null)
    {
        // Hand the final focus message to the part of the mod
        // that produces focus announcements.
    }
}
```

What this shows:

- the focus hook only records the new native focus target
- focus resolution and announcement happen later in one place

### Accessibility-Owned Focus Flow

```csharp
public void OnMoveToNextItem()
{
    _currentKey = NextVisibleKey(_currentKey);
    focusManager.SetLogicalFocus("build-menu", _currentKey);
}

public void OnUpdate()
{
    var message = focusManager.ReconcileCurrentFocus();
    if (message != null)
    {
        // Hand the final focus message to the part of the mod
        // that produces focus announcements.
    }
}
```

What this shows:

- the mod owns the focus key
- the context later resolves that key into the current accessibility-side item
- the announcement still comes from the central focus manager path, not directly from input handling
