using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// Whether a control's USAGE HINTS reach the focus readout, installed as the engine's
    /// <see cref="GraphAnnouncer.PartFilter"/> by <see cref="GraphNavigator.InstallWiring"/>.
    ///
    /// The filter is asked about every part of every control, and everything that is not a hint
    /// passes: the announcer runs its automatic position part through here too, so a filter that
    /// answered only about hints would silence positions everywhere. Hints stay in the review buffer
    /// whatever this says - the buffer composes them from <see cref="NodeHints.Lines"/> directly,
    /// which never goes near a part filter - so turning them off takes them out of the readout and
    /// leaves them a keystroke away.
    /// </summary>
    public static class UsageHints
    {
        public static bool Speaks(ControlType type, NodeAnnouncement part)
        {
            return part == null
                || part.Kind != AnnouncementKinds.Hint
                || ModSettings.ReadUsageHints != UsageHintReading.Never;
        }
    }
}
