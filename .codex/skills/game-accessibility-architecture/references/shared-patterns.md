# Shared Patterns

These patterns are shared across different styles of game accessibility mods even though their implementation paths differ.

## Narrow Speech Pipeline

Use one narrow speech-output boundary:

- upstream systems decide what to announce
- the speech pipeline delivers final speech requests
- the backend speaks final text
- review storage remains separate

Do not bind screen logic to a specific backend such as Tolk, Prism, SAPI, or a launcher protocol. Backend choice is a runtime concern.

Typical upstream systems:

- focus manager
- event dispatcher
- tooltip capture
- help system
- scanner or world notifications

Read [speech-and-announcements.md](speech-and-announcements.md) for the full split.

## Semantic Items

Keep the rest of the mod working with semantic items rather than raw engine objects.

Use wrappers or proxies when needed to:
- validate raw controls or objects
- extract fields from engine-specific data
- convert raw objects into semantic items

Read [semantic-items.md](semantic-items.md) for the recommended structure for semantic items, wrappers, proxies, containers, and resolvers.

## Context and Focus

Track both:
- current item focus
- current container or screen context

Announce container context when it changes. Do not repeat it on every move if the container is unchanged.

Read [focus-and-context.md](focus-and-context.md) for the full pattern for context ownership, focus repair, stable focus identity, and container-path diffing.

## Help and Review

Every major screen or mode should expose:
- repeat current item
- contextual help
- review or history for dense or transient output

Use review buffers for:
- combat logs
- tooltip-heavy screens
- scanners
- dense tables
- event narration

## Structured Message Composition

Build messages from fields, not ad-hoc concatenation:

```csharp
var message = SpeechMessage.Create()
    .Add("screen", "Main menu")
    .Add("item", "Options")
    .Add("position", "2 of 5")
    .Add("state", "button");
```

This makes localization, suppression, repetition, and testing much easier.

## Logging

Log these classes of failure:
- lifecycle mismatch
- missing focus target
- patch or hook failure
- reflection failure
- null widget during capture
- duplicate announcement suppression
- stale buffer reuse

## Parts You Can Fake in Tests

Keep these seams replaceable in tests:
- time or frame source
- speech backend
- speech pipeline
- logging backend
- input source
- focus source
- engine object adapters

This lets you test accessibility logic without needing the full game runtime for every case.
