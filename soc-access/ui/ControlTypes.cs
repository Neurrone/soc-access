using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The mod's registry of control types for the graph engine. A type is a value, not a class: a
    /// node factory points at one, and the type supplies the localized role word every control of
    /// that kind speaks plus the order its announcement parts read in.
    ///
    /// One order serves every type - label, then role, then value, selection and enabled state,
    /// then what the tooltip has to say, and the list position last - because that is the order a
    /// screen reader user expects to hear a control in, whatever the control is. Kinds no type
    /// orders (the drag words, the usage hints) trail behind the position.
    ///
    /// Only the types a ported screen needs so far are registered; each later phase adds its own
    /// beside them, with its role word in the same localization batch.
    /// </summary>
    public static class ControlTypes
    {
        private static readonly string[] StandardOrder =
        {
            AnnouncementKinds.Label,
            AnnouncementKinds.Role,
            AnnouncementKinds.Value,
            AnnouncementKinds.Selected,
            AnnouncementKinds.Enabled,
            AnnouncementKinds.Tooltip,
            AnnouncementKinds.Position,
        };

        /// <summary>Anything the player activates to make something happen.</summary>
        public static readonly ControlType Button = new ControlType
        {
            Key = "button",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.UI.RoleButton),
        };

        /// <summary>A container the player opens and closes. The announcer appends its expanded or
        /// collapsed state itself.</summary>
        public static readonly ControlType Group = new ControlType
        {
            Key = "group",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.UI.RoleGroup),
        };

        /// <summary>A line the player reads but cannot work: the one type with no role word. What
        /// the type is for is the reading ORDER.</summary>
        public static readonly ControlType Text = new ControlType
        {
            Key = "text",
            Order = StandardOrder,
        };

        /// <summary>Free text the player types into. The editing itself is the game's own field:
        /// activating the control is what hands the keyboard over to it.</summary>
        public static readonly ControlType EditField = new ControlType
        {
            Key = "edit-field",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.UI.RoleEditable),
        };

        private static IList<NodeAnnouncement> RoleWord(ModString word)
        {
            return new[]
            {
                new NodeAnnouncement(() => ModText.Get(word), kind: AnnouncementKinds.Role),
            };
        }
    }
}
