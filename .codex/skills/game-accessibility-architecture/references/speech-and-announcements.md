# Speech and Announcements

Use this file when designing the output side of an accessibility mod.

## Core Split

Keep these responsibilities separate:

- announcement-producing systems
- speech pipeline
- speech backend
- review storage
- cue or earcon playback

This is the preferred model because it keeps focus logic, event logic, and help logic from being tangled with backend delivery.

## Announcement-Producing Systems

These systems decide what should be spoken and when:

- focus manager
- event dispatcher
- tooltip capture
- help system
- scanner or world notifications

They may decide:

- the message to speak
- whether it should interrupt
- whether it should also be added to review history
- whether a cue should play

They should not:

- talk directly to a specific TTS backend
- perform raw screen reader calls
- hide transport details inside focus hooks or wrappers

## Speech Pipeline

The speech pipeline is the narrow speech-output boundary.

It should:

- accept a final speech request
- resolve or normalize the message text if needed
- drop empty output
- pass the text to the active backend
- stop speech when requested

It should not own:

- focus policy
- event policy
- review-buffer policy
- screen-specific announcement rules

## Speech Message

Use one shared message type for user-facing speech content.

This type exists so announcement-producing systems can return structured content without committing early to:

- one TTS backend
- one localization mechanism
- one final string format

Minimal shape:

```csharp
public sealed class SpeechMessage
{
    public static SpeechMessage Raw(string text);
    public static SpeechMessage Create();
    public SpeechMessage Add(string field, string value);
    public string Resolve();
}
```

At minimum, the type should support:

- raw game-provided text
- structured field-based message construction
- final resolution to plain text at the output boundary

If the project already has a stronger message type, use that instead. The important rule is that screens, handlers, wrappers, and event systems should pass around structured speech content until the final output boundary.

Minimal shape:

```csharp
public interface ISpeechPipeline
{
    void Output(SpeechRequest request);
    void Stop();
}

public sealed class SpeechRequest
{
    public SpeechMessage Message { get; init; }
    public bool Interrupt { get; init; }
}
```

## Speech Backend

The backend is the final transport.

Examples:

- Tolk
- Prism
- SAPI
- launcher protocol
- clipboard fallback

The backend should only know how to speak final text and stop speaking.

```csharp
public interface ISpeechBackend
{
    void Speak(string text, bool interrupt);
    void Stop();
}
```

Choose the backend at runtime. Do not bind screens, handlers, or wrappers to one specific backend.

## Review Storage

Review storage is separate from speech delivery.

Announcement-producing systems may append to review storage when they decide something should be reviewable later.

Examples:

- events
- tooltips
- scanner results
- dense screen details

```csharp
public interface IReviewStore
{
    void Append(string channel, SpeechMessage message);
}
```

## Cues and Earcons

Keep cues separate from the speech pipeline unless the project is so small that a separate audio boundary is unnecessary.

The same upstream system that decides to speak may also decide to play a cue, but cue playback should not be mixed into backend-specific TTS code.

## Typical Flow

```text
focus_manager:
    message = build_focus_message(element)
    speech_pipeline.output({ message, interrupt = true })

event_dispatcher:
    message = build_event_message(event)
    speech_pipeline.output({ message, interrupt = false })
    review_store.append("events", message)

tooltip_capture:
    message = build_tooltip_message(tooltip)
    speech_pipeline.output({ message, interrupt = false })
    review_store.append("tooltip", message)
```

What this is meant to show:

- upstream systems decide what to announce
- the speech pipeline only delivers the request
- review storage is explicit and separate
- one feature can speak now and also save fuller detail for later review

## Good Boundaries

Good:

- focus manager decides whether focus changes should interrupt
- event dispatcher decides whether an event should also be buffered
- speech pipeline sends final text to the chosen backend

Bad:

- wrappers making raw backend calls
- hooks deciding backend details
- the speech pipeline owning all review categories
- screen-specific business rules hidden inside backend code
