# Todos

## Spells

Verify narration for

- Ice bolt: damage, -initiative and -movement
- Fireball: AOE with radius of 1
- Psychic spear: damage
- Repel: push
- Arcane storm
- Earth block
- Chain lightning
- Strenghten essence
- Destroy essence
- Aegis
- Burst of strength
- Ethereal scales
- Justice
- Lethergy
- Blind hatred
- Rupture
- Rejuvenation
- Rapid fire

Knights attacks 13 enemy Plague Rats at 6, 4
Knights deals 229 melee damage to 13 enemy Plague Rats at 6, 4, killing 13

- Merge the 3 scanner PRs
- Replace high pitch sounds with something less grating
- Dev server
- Investigate damage preview bugs
- Figure out why mission 4 text doesn't read
- instead of saying impassable the tile cursor just said what the obstical is.
- In the scanner, use pathfinding instead of as crows flies distance, so unreachable stuff is at the bottom. I'll probably make this a setting
- Rewrite the UI to reduce the number of tab stops
- Describe all battlefield layouts
- Consider re-enabling announcements of decorations
- Get Claude to go through all other screens to see if I can finish supporting everything relevant
- Top / bottom of buffer sound
- Check threatened tiles indication in combat

restoring decoratives to maps
resorting scanner to 1, stop saying things that are reachable just not this turn as unreachable, and 2, sort by pathable distance rather than as the crow flies. for number 2, an improvement should probably be made because often something is reachable you just have to fight something else first and it's really hard to figure out what, so it'd be nice if we could work backwards and figure it out, though idk how possible that is

the combat map descriptions plus restoring decoratives to explain what's causing blocking.

- Verify leap narration
- Documentation: add notes about objectives, add note about how to buy / sell
- Document shift+tab issue + disabling steam overlay
- Options keybinding support
- Mod keybind support
- Bug reporter
- troop slider is unintuitive when dragging from right to left
- shift+arrows adjust sliders by 10%
- `CustomMessageMenu.Show(...)`: modal choice screen.
- `MapMessagePopup.Show(...)`: modal hint/message popup, blocks map input until dismissed.
- `AdventureNewRoundPopup.Show(...)` when `requireConfirm == true`: modal new-turn popup requiring confirm/cancel.

## Code Health

- Centralize reading of localized strings from the game
- Audit uses of MenuButtonTextUtility.JoinParts()
- Check all uses of hard-coded strings and see if we can have constants instead

## Long-term

- Localization

## In-game localization

```
using System;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Adapters
{
    internal static class TutorialLocalizationUtility
    {
        public static string GetText(string key, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(key) && GlobalLocalizationVariables.LocalizationHandler != null)
            {
                try
                {
                    string localized = GlobalLocalizationVariables.LocalizationHandler.GetText(key);
                    localized = SpeechTextSanitizer.Normalize(localized);
                    if (!string.IsNullOrWhiteSpace(localized))
                    {
                        return localized;
                    }
                }
                catch (Exception ex)
                {
                    SocAccessPlugin.Instance?.LogWarning("Tutorial localization lookup failed for " + key + ": " + ex.Message);
                }
            }

            return fallback ?? string.Empty;
        }
    }
}
```
