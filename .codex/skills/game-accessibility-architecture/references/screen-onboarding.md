# Screen Onboarding

Use this when the task is to add accessibility for a new screen such as a main menu, inventory, shop, side panel, settings page, modal, or map layer.

## Triage Checklist

1. Find how the screen becomes active.
   Look for screen creation, show, activate, push, modal-open, route-change, or focus-default code.
2. Find how the screen stops being active.
   Look for hide, deactivate, pop, destroy, or modal-close code.
3. Find the first meaningful focus target.
   If the screen has native focus, capture it. If not, define an accessibility-side default target.
4. Decide whether the screen is readable through inspectable controls.
   If yes, use small accessibility wrappers around the controls.
   If no, decide whether you need draw-time capture or a custom accessibility-side model.
5. Decide the navigation unit.
   Button, row, tab, card, tile, category, slot, field, or custom item.
6. Decide what must be spoken immediately.
   Usually screen title, current container, current item, position, and available actions.
7. Add repeat and help.
   Provide a key or command to re-read the current item and another to explain controls.
8. Add failure logging.
   Log missing focus, lifecycle mismatch, unexpected nulls, and duplicate announcements.

## Presentation Rules

- Speak the semantic item, not the raw control name.
- Include container context when it changes.
- Include position when the user is in a list, grid, or table.
- Separate state from help.
- Use earcons for open, close, selection, warnings, and mode changes.

## When To Build a Parallel Accessibility Layer

Build your own accessibility-side screen model, handler, or navigation layer when:
- native focus is inconsistent
- the game UI is drawn without stable inspectable controls
- built-in input conflicts with accessibility navigation
- the same visual screen contains multiple submodes that need different controls
- transient UI needs buffering or summarization

## Regression Checks

- Opening the screen always announces it once.
- Returning to the screen restores or repairs focus predictably.
- Modals do not leak focus to the parent screen.
- Closing the screen cleans up handlers, buffers, and delayed speech.
- Repeated navigation does not duplicate old labels.
