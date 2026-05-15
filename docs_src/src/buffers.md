# Buffers

To assist with reading long tooltips and to replay events, the mod supports review buffers. The UI buffer is always available.

When focus changes, the UI buffer is updated with the focused label, status, tooltip lines, and any available tooltip actions. Other screens can expose extra buffers, such as notifications on the adventure map or combat events.

## Buffer Controls

- Ctrl+Left: previous buffer
- Ctrl+Right: next buffer
- Ctrl+Up: previous line in the current buffer
- Ctrl+Down: next line in the current buffer
- Ctrl+Home: first line in the current buffer
- Ctrl+End: last line in the current buffer
