# Tooltip Support

## Goals

- Speak tooltip text as part of accessibility focus.
- Show the game's native visual tooltip at the accessibility focus location.
- Preserve native localized tooltip contents where possible.
- Support structured tooltip actions through an accessibility-only actions menu.
- Keep unknown tooltip instruction rows spoken by default so unsupported actions are discoverable.

## Native Tooltip Model

The game does not expose most tooltip contents as plain strings. Tooltip content is usually represented as `IDetails`, and an `IDetails` object reveals its content by drawing into an `IDetailsDrawer`:

```csharp
details.Draw(drawer, localization);
```

Inside `Draw`, native details code builds the tooltip procedurally by calling methods on the drawer, such as:

- `drawer.AddHeader(...)`
- `drawer.AddText(...)`
- `drawer.AddTextWithHeader(...)`
- `drawer.AddEntry(...)`
- `drawer.AddLabelWithImage(...)`
- `drawer.AddLabelsWithInputTypes(...)`
- `drawer.AddImage(...)`

For example, a native details class may look conceptually like this:

```csharp
public void Draw(IDetailsDrawer drawer, ILocalizationHandler loc)
{
    drawer.AddHeader(loc.GetText("Artifact/Dagger/Name"));
    drawer.AddText(loc.GetText("Artifact/Dagger/Description"));
    drawer.AddTextWithHeader(loc.GetText("Artifact/Slot"), loc.GetText("Any Hand"));
    drawer.AddLabelWithImage(
        loc.GetText("Adventure/TooltipInstruction/Drop"),
        InputType.NoInput);
}
```

There is no generic `details.Text` property to read. To extract tooltip text generically, the mod implements `IDetailsDrawer` and lets the native `Draw(...)` method run. The visual game tooltip passes a real drawer that creates UI; the mod passes `DetailsTextUtility`, a fake drawer that captures text and ignores visual-only calls.

## DetailsTextUtility

`DetailsTextUtility` is the text-capturing fake `IDetailsDrawer`.

Text-producing methods capture speech. Visual-only methods such as `AddImage`, `AddSpace`, and frame methods are safe no-ops. Methods that return UI elements return `NullDetailsElement` because native tooltip code may mutate the returned object after adding it, for example:

```csharp
drawer.AddText(text).FontColor = FontColor.SoftRed;
```

Returning a no-op element lets native tooltip builders run without null-reference failures.

`AddLabelWithImage(...)` and `AddLabelsWithInputTypes(...)` capture instruction rows as metadata. They are not treated as accessibility actions by the generic extractor because the same native drawing mechanism is used for both real actions and status/help text.

Tooltip extraction preserves raw localized strings. Speech sanitization happens later, just before speaking.

## Tooltip Object Contract

`Widget.GetTooltip()` returns a `Tooltip` object or `null`.

- `TextLines` contains raw localized lines suitable for speech and future buffer review. Empty means there is no tooltip text.
- `VisualMetadata` describes how to show the game's native visual tooltip. Null means there is no visual tooltip to show.
- `Actions` contains structured accessibility actions when an adapter has verified that native instruction rows represent real invokable actions.

Unknown tooltip rows are preserved in `TextLines`.

## Tooltip Text And Instruction Rows

Native details can contain both ordinary text rows and instruction-style rows.

Ordinary text rows are descriptive content, stats, headers, or status details. Instruction rows are usually produced by native methods such as `AddLabelWithImage(...)` or `AddLabelsWithInputTypes(...)`, which render text beside an input icon.

Instruction rows are ambiguous. Some describe real invokable actions, while others are status/help text.

Example equipment tooltip:

```text
Dagger
To cut a slice of bread, a strip of meat or the throat of your opponent.
Any Hand
+1 Offence
CTRL + Destroy
CTRL + Drop
Auto Arrange
```

In this tooltip, the native instruction rows represent real actions. The equipment adapter can remove those rows from tooltip text and replace them with structured tooltip actions:

```text
Available actions: Destroy, Drop, Auto Arrange.
```

Example troop tooltip:

```text
Rangers
Troop Size: 5
Max Troop Size: 20
Damage: 2-3
Status: Human, Ranged
Disband Troop
```

Here `Disband Troop` is also a real action, but only when the troop state allows disbanding.

Example non-action instruction row:

```text
Cannot attack
```

This may be drawn through the same instruction-row mechanism, but it is status/help text, not an action the mod can invoke.

Because the generic extractor cannot safely distinguish these cases, it preserves all instruction rows as spoken text by default. Specific adapters may remove known action rows only after they verify the native source and callback path.

## Visual Tooltip Display

`UIManager.Update()` speaks focus and then asks `NativeTooltipUtility` to show visual tooltip metadata.

`NativeTooltipUtility` handles component-backed and map-backed tooltips. `TooltipPatches` captures the live `UITooltipManager` instance from `UITooltipManager.Tick`, then uses the native `ITooltipManager.ForceDisplayTooltip(...)` path to show the tooltip.

For UI objects, visual display is anchored to a `Component` or explicit `RectTransform`. For adventure map tiles, visual display uses the native `ITooltipable`, screen point, and `IDetails`.

## Tooltip Actions

Native instruction rows are not automatically accessibility actions. The generic extractor records them but does not interpret them.

Adapters that understand a specific tooltip source may remove known instruction rows from `TextLines` and add structured `TooltipAction` entries. If an adapter does not support actions for a tooltip, it must leave the raw instruction lines spoken.

Action labels and line matching should use localized native strings, not English-only literals.

## Tooltip Actions Menu

`TooltipActionsMenuScreen` is accessibility-only. It opens from the global `TooltipActionsMenu` action when the focused widget has a tooltip with at least one structured action.

The menu contains one item per structured action plus Cancel. Pressing Enter invokes the selected action. Escape or Cancel closes the menu.

The screen has no native runtime probe. `IsPresent()` returns true while the screen is on the accessibility screen stack.

## Current Supported Action Sources

Equipment and artifact tooltips support actions such as Equip, Unequip, Destroy, Drop, and Auto Arrange. They use explicit localized instruction matching because some real artifact actions are rendered with `InputType.NoInput`, so input metadata alone is not reliable.

Troop tooltips support Disband Troop when the native troop state allows it. This uses explicit native troop state and callback handling.

Adventure map tooltips use captured instruction row input metadata for primary and secondary actions:

- primary instruction rows map to the native primary map action;
- secondary instruction rows map to the native secondary map action;
- unsupported or ambiguous rows remain spoken as normal tooltip text.

Gamepad-only instruction rows are currently ignored for structured actions. They remain spoken as normal tooltip text unless an adapter explicitly supports them later.

## Localization Rules

Tooltip extraction should preserve native localized strings. Matching instruction rows for supported actions should use localized strings. Do not match English literals such as `"Drop"` unless they came from localization.

Speech sanitization should happen at speech output time, not during extraction.

## Extension Guidance

To add tooltip support for a new widget:

- return a `Tooltip` from `Widget.GetTooltip()`;
- use `NativeTooltipUtility.TryGetUiDetails(...)` for standard UI `IDetails`;
- provide visual metadata if native visual tooltip display is available;
- leave raw text untouched unless actions are explicitly supported.

To add tooltip actions:

- inspect the native tooltip source and native callback path;
- identify localized instruction rows;
- remove only verified action instruction rows;
- add `TooltipAction` entries with localized labels and native callbacks;
- preserve unknown instruction rows.
