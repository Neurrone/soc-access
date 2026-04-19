# Semantic Items

Use this file when defining:

- accessibility-side items for a screen or mode
- wrappers or proxies around raw engine objects
- containers for hierarchy and position context
- the boundary between raw object access and semantic accessibility behavior

This file gives one recommended structure. It is guidance, not a law. Adjust it where the game or engine makes a lighter or flatter structure more practical.

This file uses the shared `SpeechMessage` type from [speech-and-announcements.md](speech-and-announcements.md). Read that file first if you need the message type definition or the speech-output boundary.

## Table of Contents

- [Core Rule](#core-rule)
- [Terms](#terms)
- [Recommended Structure](#recommended-structure)
- [Responsibilities](#responsibilities)
- [Minimal Shapes](#minimal-shapes)
- [Typical Flow](#typical-flow)
- [When to Use a Simpler Variant](#when-to-use-a-simpler-variant)
- [Rules](#rules)

## Core Rule

The rest of the accessibility layer should consume semantic items, not raw engine controls.

That means:

- focus management should resolve focus into semantic items
- announcement-producing systems should read semantic items
- review buffers should store messages derived from semantic items
- raw engine access should stay close to wrappers, proxies, walkers, or resolvers

## Terms

### AccessibleElement

An `AccessibleElement` is the semantic accessibility-side item that the rest of the mod works with.

It should answer:

- what role it represents
- whether it is visible and available
- what label, status, tooltip, and help it exposes
- what actions it supports
- what container it belongs to

### Wrapper or Proxy

A wrapper or proxy is an engine-facing adapter around a raw object.

Its job is to:

- hold the raw engine object
- check validity and visibility
- extract semantic fields
- translate engine-specific activation or adjustment into semantic behavior

### Container

A container is the accessibility-side parent of one or more elements.

Its job is to:

- hold child elements
- expose container label when needed
- expose position information for children when needed
- support hierarchy for focus context and path tracking

### Resolver

A resolver is the code that decides which wrapper or element type to build for a raw engine object.

## Recommended Structure

The recommended default is:

1. raw engine object
2. wrapper or proxy
3. `AccessibleElement`
4. optional `ElementContainer` hierarchy

This is the clearest structure when:

- the game already has focusable controls or inspectable UI objects
- different raw control types need different semantic handling
- container context matters for focus announcements
- generic fallback support and specialized support both exist

## Responsibilities

### `AccessibleElement`

Owns:

- semantic role
- label, status, tooltip, and help
- activation or adjustment behavior
- stable item identity
- container relationship

Should not own:

- raw backend calls
- global speech policy
- global review-buffer policy
- screen-stack ownership

### Wrapper or Proxy

Owns:

- raw engine object access
- validity checks
- visibility checks
- extraction of fields from raw controls or objects
- conversion into semantic elements

Should not own:

- final focus announcement policy
- queue versus interrupt policy
- global deduplication
- review category decisions

### Container

Owns:

- child elements
- container label
- child position information
- parent and child relationships used by focus context

Should not own:

- raw engine traversal
- global focus ownership

## Minimal Shapes

The shapes below use `SpeechMessage` from [speech-and-announcements.md](speech-and-announcements.md).

```csharp
public abstract class AccessibleElement
{
    public abstract string ElementId { get; }
    public abstract string Role { get; }

    public abstract bool IsVisible { get; }
    public abstract bool IsAvailable { get; }

    public virtual ElementContainer? Parent { get; set; }

    public abstract SpeechMessage? BuildLabel();
    public virtual SpeechMessage? BuildStatus() => null;
    public virtual SpeechMessage? BuildTooltip() => null;
    public virtual SpeechMessage? BuildHelp() => null;

    public virtual bool Activate() => false;
    public virtual bool Adjust(int direction, int stepLevel) => false;

    public virtual SpeechMessage BuildFocusMessage()
    {
        var message = SpeechMessage.Create();

        var label = BuildLabel();
        if (label != null)
            message = message.Add("label", label.Resolve());

        var status = BuildStatus();
        if (status != null)
            message = message.Add("status", status.Resolve());

        var tooltip = BuildTooltip();
        if (tooltip != null)
            message = message.Add("tooltip", tooltip.Resolve());

        return message;
    }
}
```

```csharp
public abstract class ElementContainer : AccessibleElement
{
    public abstract IReadOnlyList<AccessibleElement> Children { get; }

    public abstract SpeechMessage? BuildContainerLabel();
    public abstract SpeechMessage? BuildPositionMessage(AccessibleElement child);
}
```

```csharp
public interface IElementWrapper
{
    bool IsValid();
    bool IsVisible();
    AccessibleElement ToElement();
}
```

```csharp
public interface IElementResolver
{
    AccessibleElement? Resolve(object rawObject);
}
```

## Typical Flow

```text
raw focus or selection change
    -> resolver chooses wrapper or direct element
    -> wrapper validates the raw object
    -> wrapper produces an AccessibleElement
    -> focus manager uses the element
    -> announcement-producing system builds messages from the element
```

The important boundary is:

- wrappers read raw objects
- elements expose semantic behavior
- focus and speech systems consume elements

## When to Use a Simpler Variant

The recommended structure above is the clearest default, but some games want a flatter model.

Examples:

- A widget walker that already emits normalized items with label, validity, activation, and adjustment may let those items implement `AccessibleElement` directly and skip a separate wrapper class.
- An API-first menu system that builds semantic nodes directly from game state and stable keys may create `AccessibleElement` instances directly and skip wrappers entirely.

If a flatter structure is easier for the game, use it. The important rule is still the same:

- keep raw engine access at the edge
- make the rest of the mod consume semantic items

## Rules

- Prefer stable semantic ids over raw object identity where rerender is common.
- Keep one clear place that resolves raw objects into semantic items.
- Add specialized wrappers before adding policy to generic fallback wrappers.
- Do not let wrappers make raw speech calls.
- Do not hide screen-specific business rules in a generic resolver if they belong in a specialized screen model.
- Keep container hierarchy explicit when container context changes what should be announced.
- If wrappers and elements collapse into one object for a given game, keep the same responsibility boundary in mind even if the class count is smaller.
