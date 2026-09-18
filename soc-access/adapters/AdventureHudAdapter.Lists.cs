using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Levels;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Objectives;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE FOUR LISTS the HUD draws down its sides: the notifications, the town list, the wielder
    /// list with its "wielders n of m" count, and the turn order.
    ///
    /// Split out of AdventureHudAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureHudAdapter
    {
        public bool IsNotificationsMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.NotificationHUDContainer : null)
                && GetNotificationEntryCount() > 0;
        }

        public bool IsNotificationVisible(int index)
        {
            return GetNotificationEntry(index) != null;
        }

        /// <summary>The registry's information object behind the drawn notification - what the game
        /// registered when the notification happened, and what it keeps until it is dismissed
        /// (<c>NotificationHUDRegistry</c>). The drawn ENTRY is pooled and says nothing about which
        /// notification it is showing; this is not, so it is the notification's own identity. Null
        /// where nothing is drawn at that place.</summary>
        public object GetNotificationInformation(int index)
        {
            NotificationHUDEntry entry = GetNotificationEntry(index);
            return entry != null ? entry.Information : null;
        }

        public string GetNotificationLabel(int index)
        {
            NotificationHUDEntry entry = GetNotificationEntry(index);
            if (entry == null || entry.Information == null)
            {
                return string.Empty;
            }

            return SpokenLines.Clean(entry.Information.Text);
        }

        public void FocusNotification(int index)
        {
            UIButton button = GetNotificationButton(index);
            NativeSelectionUtility.Select(button);
            if (button != null)
            {
                NativeSelectionUtility.PointerEnter(button);
            }
        }

        public bool ClickNotification(int index)
        {
            return NativeSelectionUtility.Click(GetNotificationButton(index));
        }

        public Tooltip GetNotificationTooltip(int index)
        {
            UIButton button = GetNotificationButton(index);
            Tooltip tooltip = Tooltip.ForComponent(button, LocalizationHandler);
            if (tooltip == null)
            {
                return new Tooltip(
                    () => new[] { GetNotificationLabel(index) },
                    null);
            }

            return new Tooltip(
                () => tooltip.TextLines,
                tooltip.VisualMetadata,
                isLong: () => tooltip.IsLong);
        }

        public bool DismissNotification(int index)
        {
            UIButton button = GetNotificationButton(index);
            return button != null && NativeSelectionUtility.PointerRightClick(button);
        }

        public bool IsTownListMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.TownListContainer : null)
                && GetTownListEntryCount() > 0;
        }

        public bool IsTownListEntryVisible(int index)
        {
            return GetTownListEntry(index) != null;
        }

        /// <summary>The map entity the drawn town row is about, by the id the game gives it
        /// (<c>IMapEntity.Id</c>), or -1 where nothing is drawn at that place. The list respawns
        /// every row whenever a town changes hands (<c>TownListUI.CreateNewTownList</c>), so the
        /// place a row sits in is not what it is about.</summary>
        public int GetTownListEntryId(int index)
        {
            ITownListHUDEntry entry = GetTownListEntry(index);
            return entry != null && entry.Town != null ? entry.Town.Id : -1;
        }

        public string GetTownListEntryLabel(int index)
        {
            ITownListHUDEntry entry = GetTownListEntry(index);
            return entry != null && entry.Town != null ? GetMapEntityName(entry.Town) : string.Empty;
        }

        public void FocusTownListEntry(int index)
        {
            ITownListHUDEntry entry = GetTownListEntry(index);
            if (entry != null)
            {
                NativeSelectionUtility.Select(entry.GetSelectable());
            }
        }

        /// <summary>The entry's single click, which the game answers by centring the camera on the
        /// town (<c>TownListUI.OnEntryUIEvent</c>, <c>Clicked</c>). The entry hands out the Unity
        /// <c>Button</c>, which is a click handler of its own with nothing listening to it; the
        /// game's handlers hang off the <c>UIButton</c> beside it, so that is what is clicked.</summary>
        public bool ClickTownListEntry(int index)
        {
            Selectable selectable = GetTownListEntry(index)?.GetSelectable();
            UIButton button = selectable != null ? selectable.GetComponent<UIButton>() : null;
            return button != null && NativeSelectionUtility.Click(button);
        }

        public Tooltip GetTownListEntryTooltip(int index)
        {
            Selectable selectable = GetTownListEntry(index)?.GetSelectable();
            return Tooltip.ForComponent(selectable, LocalizationHandler);
        }

        public bool IsWielderListMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.WielderlistContainer : null)
                && GetWielderListEntryCount() > 0;
        }

        /// <summary>
        /// Whether the list draws its "Wielders n of m" count. The game only turns the container on
        /// where the team has a wielder cap and more than one wielder it could buy
        /// (<c>WielderListHUD.UpdateOwnedWieldersText</c>), so this is the container's own answer.
        /// </summary>
        public bool IsWielderAmountVisible()
        {
            return IsWielderListMenuVisible()
                && HudGroupVisible(Reflect.Get<GameObject>(WielderList, WielderAmountContainerField));
        }

        /// <summary>The count the list writes over itself, in the game's own words
        /// ("Adventure/CommanderListHUD/WielderAmount").</summary>
        public string WielderAmountLabel
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(WielderList, WielderAmountTextField)); }
        }

        /// <summary>The wielder-limit explanation the game hangs on the count's hover area.</summary>
        public Tooltip WielderAmountTooltip
        {
            get
            {
                return Tooltip.ForComponent(
                    Reflect.Get<UITransform>(WielderList, WielderLimitTooltipAreaField),
                    LocalizationHandler);
            }
        }

        public bool IsWielderListEntryVisible(int index)
        {
            return GetWielderListEntry(index) != null;
        }

        /// <summary>The commander the drawn wielder row is about, by the id the game gives them
        /// (<c>ICommanderState.Id</c>), or -1 where nothing is drawn at that place. The list drops
        /// the row of the commander it has just selected, spawns one for the deselected commander and
        /// re-sorts the rest by that same id (<c>WielderListHUD.HandleCommanderChanged</c>), so the
        /// place a row sits in is not what it is about.</summary>
        public int GetWielderListEntryCommanderId(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            return entry != null && entry.Commander != null ? entry.Commander.Id : -1;
        }

        public string GetWielderListEntryLabel(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            return entry != null && entry.Commander != null ? AdventureMapEntityLabel.GetCommanderName(Facade, entry.Commander) : string.Empty;
        }

        public void FocusWielderListEntry(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            if (entry != null)
            {
                RefreshWielderListEntryTooltip(entry);
                NativeSelectionUtility.Select(GetWielderListSelectable(entry));
            }
        }

        public bool ClickWielderListEntry(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            UIButton button = GetWielderListButton(entry);
            return button != null && NativeSelectionUtility.Click(button);
        }

        public Tooltip GetWielderListEntryTooltip(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            Selectable selectable = GetWielderListSelectable(entry);
            if (selectable == null)
            {
                return null;
            }

            return new Tooltip(
                () =>
                {
                    RefreshWielderListEntryTooltip(entry);
                    return NativeTooltipUtility.GetTooltipLinesForComponent(selectable, LocalizationHandler);
                },
                VisualTooltipMetadata.ForComponent(selectable),
                isLong: () => NativeTooltipUtility.IsLongForComponent(selectable, () => RefreshWielderListEntryTooltip(entry)));
        }

        public bool IsTeamQueueMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.TeamQueueContainer : null)
                && GetTeamQueueEntryCount() > 0;
        }

        public bool IsTeamQueueEntryVisible(int index)
        {
            return GetTeamQueueEntry(index) != null;
        }

        public string GetTeamQueueEntryLabel(int index)
        {
            TeamQueueEntryBehaviour entry = GetTeamQueueEntry(index);
            UITextMesh text = Reflect.Get<UITextMesh>(entry, TeamQueueEntryNameTextField);
            return SpokenLines.Clean(UITextMeshTextUtility.Spoken(text));
        }

        public void FocusTeamQueueEntry(int index)
        {
            UIButton button = Reflect.Get<UIButton>(GetTeamQueueEntry(index), TeamQueueEntryTooltipButtonField);
            NativeSelectionUtility.Select(button);
        }

        public Tooltip GetTeamQueueEntryTooltip(int index)
        {
            UIButton button = Reflect.Get<UIButton>(GetTeamQueueEntry(index), TeamQueueEntryTooltipButtonField);
            return Tooltip.ForComponent(button, LocalizationHandler);
        }

        private int GetNotificationEntryCount()
        {
            List<INotificationHUDEntry> entries = Reflect.Get<List<INotificationHUDEntry>>(NotificationHud, NotificationHudActiveEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private NotificationHUDEntry GetNotificationEntry(int index)
        {
            List<INotificationHUDEntry> entries = Reflect.Get<List<INotificationHUDEntry>>(NotificationHud, NotificationHudActiveEntriesField);
            return entries != null && index >= 0 && index < entries.Count ? entries[index] as NotificationHUDEntry : null;
        }

        private UIButton GetNotificationButton(int index)
        {
            NotificationHUDEntry.Settings settings = Reflect.Get<NotificationHUDEntry.Settings>(GetNotificationEntry(index), NotificationHudEntrySettingsField);
            return settings == null
                ? null
                : FirstActiveButton(
                    settings.InformationButton,
                    settings.PositiveButton,
                    settings.NegativeButton,
                    settings.ImportantButton,
                    settings.BeaconButton,
                    settings.BeaconOpponentButton);
        }

        private int GetTownListEntryCount()
        {
            List<ITownListHUDEntry> entries = Reflect.Get<List<ITownListHUDEntry>>(TownList, TownListEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private ITownListHUDEntry GetTownListEntry(int index)
        {
            List<ITownListHUDEntry> entries = Reflect.Get<List<ITownListHUDEntry>>(TownList, TownListEntriesField);
            return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
        }

        private int GetWielderListEntryCount()
        {
            List<WielderListHUDEntry> entries = GetWielderListEntries();
            return entries != null ? entries.Count : 0;
        }

        private WielderListHUDEntry GetWielderListEntry(int index)
        {
            List<WielderListHUDEntry> entries = GetWielderListEntries();
            return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
        }

        /// <summary>The wielder list's drawn entries, read at most once a frame: the map's build asks
        /// each of thirty-two slots whether it is drawn and then for its tooltip, and the pool's
        /// ActiveEntries property was looked up by name on every one of those calls.</summary>
        private List<WielderListHUDEntry> GetWielderListEntries()
        {
            int frame = Time.frameCount;
            if (_wielderListEntriesFrame == frame)
            {
                return _wielderListEntries;
            }

            _wielderListEntriesFrame = frame;
            _wielderListEntries = ReadWielderListEntries();
            return _wielderListEntries;
        }

        private List<WielderListHUDEntry> ReadWielderListEntries()
        {
            object pool = HudStateSettings != null && HudStateSettings.WielderList != null
                ? WielderListEntryPoolField.GetValue(HudStateSettings.WielderList)
                : null;
            if (pool == null)
            {
                return null;
            }

            if (!_wielderListActiveEntriesProbed)
            {
                _wielderListActiveEntriesProbed = true;
                _wielderListActiveEntriesProperty = AccessTools.Property(pool.GetType(), "ActiveEntries");
            }

            return _wielderListActiveEntriesProperty != null
                ? _wielderListActiveEntriesProperty.GetValue(pool, null) as List<WielderListHUDEntry>
                : null;
        }

        private Selectable GetWielderListSelectable(WielderListHUDEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            try
            {
                return entry.Commander != null && entry.Commander.IsAlive
                    ? entry.GetAliveSelectable()
                    : entry.GetDeadSelectable();
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a wielder row's selectable", exception);
                return null;
            }
        }

        private static UIButton GetWielderListButton(WielderListHUDEntry entry)
        {
            return entry != null && WielderListEntryButtonField != null
                ? WielderListEntryButtonField.GetValue(entry) as UIButton
                : null;
        }

        private void RefreshWielderListEntryTooltip(WielderListHUDEntry entry)
        {
            if (entry == null || entry.Commander == null || WielderListEntryRefreshTooltipMethod == null)
            {
                return;
            }

            try
            {
                WielderListEntryRefreshTooltipMethod.Invoke(entry, null);
            }
            catch (Exception exception)
            {
                LogFailureOnce("refreshing a wielder row's tooltip", exception);
            }
        }

        private int GetTeamQueueEntryCount()
        {
            List<TeamQueueEntryBehaviour> entries = Reflect.Get<List<TeamQueueEntryBehaviour>>(TeamQueueHud, TeamQueueEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private TeamQueueEntryBehaviour GetTeamQueueEntry(int index)
        {
            List<TeamQueueEntryBehaviour> entries = Reflect.Get<List<TeamQueueEntryBehaviour>>(TeamQueueHud, TeamQueueEntriesField);
            return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
        }
    }
}
