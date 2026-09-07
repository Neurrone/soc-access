using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The band the game draws across the top of every menu a wielder walked into - the artifact
    /// market, the town, the dwelling, the rally point, the hostile join offer and the world choice
    /// menu all hang the same <c>WielderInteractHeader</c> there. It holds the wielder's portrait,
    /// their army as a <c>TroopHUD</c>, and the close cross the menu is shut with.
    ///
    /// One adapter over that band, so the screens that draw it all read the same facts from the same
    /// place; how it is SAID is the screens' business (<c>ui/TroopHudRows.cs</c> composes it).
    /// </summary>
    public sealed class WielderInteract
    {
        private static readonly FieldInfo TroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");
        private static readonly FieldInfo PortraitField = AccessTools.Field(typeof(WielderInteractHeader), "_wielderPortrait");
        private static readonly FieldInfo CloseButtonField = AccessTools.Field(typeof(WielderInteractHeader), "_closeButton");

        private readonly WielderInteractHeader _header;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private TroopHudAdapter _troops;

        public WielderInteract(
            WielderInteractHeader header,
            IClientAdventureFacade facade,
            ILocalizationHandler localization)
        {
            _header = header;
            _facade = facade;
            _localization = localization;
        }

        /// <summary>Whether the band is drawn at all: a menu that shows no wielder has none.</summary>
        public bool IsPresent
        {
            get
            {
                return _header != null
                    && _header.gameObject != null
                    && _header.gameObject.activeInHierarchy
                    && GetTroopHud() != null;
            }
        }

        /// <summary>The wielder the band is about, in the game's own words.</summary>
        public string WielderName
        {
            get
            {
                TroopHudAdapter troops = Troops;
                return troops == null ? string.Empty : troops.OwnerName;
            }
        }

        /// <summary>The wielder's own portrait, whose details are the stats the game draws on hover.
        /// </summary>
        public Component Portrait
        {
            get { return GetPortrait() as Component; }
        }

        public Tooltip PortraitTooltip
        {
            get { return Tooltip.ForComponent(Portrait, _localization); }
        }

        /// <summary>The army the band draws, or null before the band has been set up.</summary>
        public TroopHudAdapter Troops
        {
            get
            {
                TroopHUD hud = GetTroopHud();
                if (hud == null)
                {
                    return null;
                }

                // Kept: the adapter wakes the game's drag ghost when it is made, and the rows are
                // rebuilt on every navigation operation.
                if (_troops == null || !ReferenceEquals(_troops.Hud, hud))
                {
                    _troops = new TroopHudAdapter(hud, _facade, _localization);
                }

                return _troops;
            }
        }

        /// <summary>The cross the band draws at its top right. The game only turns it on where the
        /// menu may be closed and the player is on mouse and keyboard.</summary>
        public Component CloseButton
        {
            get { return GetCloseButton() as Component; }
        }

        public bool IsCloseVisible
        {
            get
            {
                Component close = CloseButton;
                return close != null && close.gameObject != null && close.gameObject.activeInHierarchy;
            }
        }

        public bool ActivateClose()
        {
            return NativeSelectionUtility.Click(GetCloseButton());
        }

        public bool FocusPortrait()
        {
            return NativeSelectionUtility.Select(Portrait);
        }

        private TroopHUD GetTroopHud()
        {
            return _header != null && TroopHudField != null ? TroopHudField.GetValue(_header) as TroopHUD : null;
        }

        private IUIImage GetPortrait()
        {
            return _header != null && PortraitField != null ? PortraitField.GetValue(_header) as IUIImage : null;
        }

        private IUIButton GetCloseButton()
        {
            return _header != null && CloseButtonField != null ? CloseButtonField.GetValue(_header) as IUIButton : null;
        }
    }
}
