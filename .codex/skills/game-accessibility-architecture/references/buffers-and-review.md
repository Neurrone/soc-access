# Buffers and Review

Use this file when a skill or feature mentions a `buffer`, `review`, `history`, `event log`, or `re-read` path.

## What a Buffer Is

A buffer is a reviewable history of accessibility output or captured transient UI state.

Its purpose is to let the user revisit information that:

- was spoken too quickly
- was too dense to absorb in one pass
- only existed briefly on screen
- was produced by background systems such as scanners or event narration

A buffer is not the main focus model. It is a secondary review tool.

## Common Buffer Types

- current-screen review:
  recent announcements for the current screen or modal
- event buffer:
  combat logs, status changes, notifications, or story events
- tooltip buffer:
  the latest tooltip or hover text, especially when rendered transiently
- scanner buffer:
  results collected from world scanning or category browsing

## What Goes Into a Buffer

Buffer entries should usually contain:

- resolved or resolvable message text
- source channel or category
- timestamp or frame/tick ordering
- optional semantic metadata such as screen id, item id, entity id, or world position

Keep buffer entries semantic. Do not store raw widget pointers unless there is a specific reason.

## Core Operations

Most buffer systems should support:

- append a new entry
- read latest entry
- move backward and forward through entries
- repeat current entry
- clear or reset when the owning context ends
- optionally expire stale entries

## Review Controls

At minimum, define intents for:

- open review or re-read the current buffer
- move to previous entry
- move to next entry
- move to previous buffer category or channel
- move to next buffer category or channel
- jump to newest entry

Typical example bindings many games can support:

- `Ctrl+Up` and `Ctrl+Down`:
  cycle through entries in the current buffer category
- `Ctrl+Left` and `Ctrl+Right`:
  switch buffer categories or channels such as tooltips, events, scanner, or screen history
- `Ctrl+Home` or equivalent:
  jump to the newest entry in the current category
- `Ctrl+End` or equivalent:
  jump to the oldest entry if backward review matters
- `Ctrl+R` or another dedicated review command:
  re-read the current or latest buffer entry

These are examples, not mandatory bindings. The important rule is consistency:

- use one predictable modifier family for review navigation
- keep the same review actions across buffer categories
- avoid colliding with the game's highest-priority controls

If the game already has a strong navigation convention, adapt the bindings to that convention while preserving the same review intents.

## When To Speak Immediately vs Buffer

Speak immediately when:

- the information is the current focus target
- the information is needed for immediate control or safety
- the user just triggered the action directly

Buffer when:

- the information is supplementary
- multiple updates can arrive quickly
- the UI text is transient or render-time only
- the user may reasonably want to review older entries

Many events should do both:

- speak a short summary now
- push the fuller detail into a buffer

## Scope

Choose the scope explicitly:

- per-screen buffer
- per-modal buffer
- per-system buffer such as events or scanner results
- global history buffer

Do not mix unrelated streams into one undifferentiated history unless the user explicitly wants that.

## Lifecycle Rules

- clear or detach screen-local buffers when the screen closes
- keep global event buffers across screen changes when useful
- avoid replaying stale entries after focus moves to a different context
- log stale buffer reuse, missing current entry, and buffer overflow behavior

## Minimal Skeleton

```csharp
public interface IReviewBuffer
{
    void Append(BufferEntry entry);
    BufferEntry? Latest();
    BufferEntry? Previous();
    BufferEntry? Next();
    void Clear();
}
```

## Design Rule

If a user can miss important information because it is dense, fast, or transient, the feature probably needs a buffer or review path.
