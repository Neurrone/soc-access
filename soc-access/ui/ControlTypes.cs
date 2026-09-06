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

        /// <summary>A setting the player turns on and off, reading its state every time it is
        /// touched.</summary>
        public static readonly ControlType Checkbox = new ControlType
        {
            Key = "checkbox",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.UI.RoleCheckbox),
        };

        /// <summary>One of a set the game lets the player choose exactly one of, drawn in place on
        /// the page rather than in a list something opened. It is not a checkbox: there is no
        /// unticking it, and a player told "checkbox, not checked" would go looking for one.</summary>
        public static readonly ControlType RadioButton = new ControlType
        {
            Key = "radio-button",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.UI.RoleRadioButton),
        };

        /// <summary>A value along a range. Left and Right move it rather than moving the cursor, so
        /// its role word is also the warning that the arrows mean something else here.</summary>
        public static readonly ControlType Slider = new ControlType
        {
            Key = "slider",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.UI.RoleSlider),
        };

        /// <summary>A setting chosen from a list the control opens.</summary>
        public static readonly ControlType ComboBox = new ControlType
        {
            Key = "combo-box",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.UI.RoleComboBox),
        };

        /// <summary>One page of a screen, reached from a bar of its peers. What matters about a tab
        /// is whether it is the page currently showing, which it says as its selection state.</summary>
        public static readonly ControlType Tab = new ControlType
        {
            Key = "tab",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.UI.RoleTab),
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
