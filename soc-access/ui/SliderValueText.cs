using System;
using System.Globalization;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// A slider's number, as it is SAID. The game draws some sliders as a percentage and the rest as
    /// a plain number, and which of the two it is is the slider's own fact; how the percentage reads
    /// is not - the sign sits after the number in English and before it in Turkish - so the wording
    /// lives here and the number is formatted invariantly, as a number the player reads rather than
    /// prose.
    /// </summary>
    public static class SliderValueText
    {
        public static string Of(float value, bool asPercent)
        {
            return asPercent
                ? ModText.Get(ModStrings.UI.Percent, Math.Round(value * 100f))
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
