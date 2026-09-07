using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public enum HostileJoinMenuStage
    {
        None,
        Choice,
        Join
    }

    /// <summary>
    /// The offer a hostile army makes when a wielder walks into it. The menu has TWO SHAPES over the
    /// same window, and the game's own <c>_stage</c> field says which is drawn: the CHOICE, where the
    /// army is shown behind a lock and the player answers Yes or No, and the JOIN, where the lock is
    /// gone and the troops are moved across before the window is done with.
    ///
    /// The attacking wielder's band is the same <c>WielderInteractHeader</c> every menu a wielder walks
    /// into draws, here with no close cross of its own (<c>showCloseButton: false</c>): there is no
    /// close cross anywhere on this menu, and Escape is the game's - No in the choice stage, Done in
    /// the join stage (<c>HostileJoinMenu.ReregisterInput</c>).
    /// </summary>
    public sealed class HostileJoinMenuAdapter : IDisposable
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(HostileJoinMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(HostileJoinMenu), "_async");
        private static readonly FieldInfo StageField = AccessTools.Field(typeof(HostileJoinMenu), "_stage");
        private static readonly FieldInfo AttackingCommanderField = AccessTools.Field(typeof(HostileJoinMenu), "_attackingCommander");
        private static readonly FieldInfo JoiningCommanderField = AccessTools.Field(typeof(HostileJoinMenu), "_joiningCommander");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(HostileJoinMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(HostileJoinMenu), "_localizationHandler");

        private readonly HostileJoinMenu _menu;
        private readonly HostileJoinMenu.Settings _settings;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private WielderInteract _wielder;
        private TroopHudAdapter _joiningTroops;
        private bool _disposed;

        public HostileJoinMenuAdapter(HostileJoinMenu menu)
        {
            _menu = menu;
            _settings = GetField<HostileJoinMenu.Settings>(menu, SettingsField);
            _facade = GetField<IClientAdventureFacade>(menu, AdventureFacadeField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public int AttackingCommanderId
        {
            get
            {
                ICommanderState commander = GetField<ICommanderState>(_menu, AttackingCommanderField);
                return commander != null ? commander.Id : -1;
            }
        }

        public int JoiningCommanderId
        {
            get
            {
                ICommanderState commander = GetField<ICommanderState>(_menu, JoiningCommanderField);
                return commander != null ? commander.Id : -1;
            }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && GetField<object>(_menu, AsyncField) != null
                && Stage != HostileJoinMenuStage.None;
        }

        /// <summary>Which of the menu's two shapes is drawn, read off the game's own stage and the
        /// container it turned on for it.</summary>
        public HostileJoinMenuStage Stage
        {
            get
            {
                if (_menu == null || _settings == null)
                {
                    return HostileJoinMenuStage.None;
                }

                if (IsNativeStage("Choice")
                    && _settings.ChoiceStageContainer != null
                    && _settings.ChoiceStageContainer.activeInHierarchy)
                {
                    return HostileJoinMenuStage.Choice;
                }

                if (IsNativeStage("Join")
                    && _settings.JoinStageContainer != null
                    && _settings.JoinStageContainer.activeInHierarchy)
                {
                    return HostileJoinMenuStage.Join;
                }

                return HostileJoinMenuStage.None;
            }
        }

        /// <summary>The title the menu writes over the window, the same in both stages.</summary>
        public string Title
        {
            get { return GetText(_settings != null ? _settings.TitleText : null); }
        }

        /// <summary>What the menu says about the offer, in the choice stage.</summary>
        public string OfferText
        {
            get { return GetText(_settings != null ? _settings.InformationText : null); }
        }

        /// <summary>What the menu says about moving the troops, in the join stage.</summary>
        public string JoinText
        {
            get { return GetText(_settings != null ? _settings.JoinText : null); }
        }

        /// <summary>The attacking wielder's band across the top of the window.</summary>
        public WielderInteract Wielder
        {
            get
            {
                WielderInteractHeader header = _settings != null ? _settings.WielderInteractHeader : null;
                if (header == null)
                {
                    return null;
                }

                if (_wielder == null || !ReferenceEquals(_wielder.Header, header))
                {
                    _wielder = new WielderInteract(header, _facade, _localization);
                }

                return _wielder;
            }
        }

        /// <summary>The army being offered. Kept: the adapter wakes the game's drag ghost when it is
        /// made.</summary>
        public TroopHudAdapter JoiningTroops
        {
            get
            {
                TroopHUD hud = _settings != null ? _settings.TroopHUD : null;
                if (hud == null)
                {
                    return null;
                }

                if (_joiningTroops == null || !ReferenceEquals(_joiningTroops.Hud, hud))
                {
                    _joiningTroops = new TroopHudAdapter(hud, _facade, _localization);
                }

                return _joiningTroops;
            }
        }

        /// <summary>Whether the game is still drawing the lock over the offered army, which is what it
        /// takes off once the offer is accepted (<c>HostileJoinMenu.HandleYesButtonClicked</c>).
        /// </summary>
        public bool IsOfferLocked
        {
            get
            {
                GameObject overlay = _settings != null ? _settings.TroopHUDOverlay : null;
                return overlay != null && overlay.activeInHierarchy;
            }
        }

        // ---- the three buttons ----

        public Component AcceptButton
        {
            get { return _settings != null ? _settings.YesButton : null; }
        }

        /// <summary>The game's own text on the accept button, which it draws only where the offer costs
        /// nothing; a paid offer draws the price INSTEAD of a word, and the price is
        /// <see cref="AcceptGoldAmount"/> rather than the button's name. A paid offer's button is still
        /// named by the game's own word for it (<c>Adventure/FightOrFlight/Join/YesButton</c>, the text
        /// it draws on the free variant), so the node does not read as a bare price.</summary>
        public string AcceptText
        {
            get
            {
                UITextMesh text = _settings != null ? _settings.YesButtonText : null;
                if (text != null && text.Active)
                {
                    return GetText(text);
                }

                if (string.IsNullOrWhiteSpace(AcceptGoldAmount))
                {
                    return GetButtonText(_settings != null ? _settings.YesButton : null);
                }

                return GameText.Get(_localization, "Adventure/FightOrFlight/Join/YesButton", string.Empty);
            }
        }

        /// <summary>The gold the offer costs, as the button draws it, or nothing where it draws no
        /// price.</summary>
        public string AcceptGoldAmount
        {
            get
            {
                UITextMesh amount = _settings != null ? _settings.YesButtonGoldAmount : null;
                return amount != null && amount.Active ? GetText(amount) : string.Empty;
            }
        }

        /// <summary>The game's own name for gold.</summary>
        public string GoldName
        {
            get { return GameText.Get(_localization, "Common/Resource/" + ResourceType.Gold, ResourceType.Gold.ToString()); }
        }

        /// <summary>Whether the game will take the click: it turns the button off when the local team
        /// cannot afford the price.</summary>
        public bool IsAcceptEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.YesButton : null);
        }

        public bool ActivateAccept()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.YesButton : null);
        }

        public bool FocusAccept()
        {
            return NativeSelectionUtility.Select(_settings != null ? _settings.YesButton : null);
        }

        public Component RejectButton
        {
            get { return _settings != null ? _settings.NoButton : null; }
        }

        public string RejectText
        {
            get { return GetButtonText(_settings != null ? _settings.NoButton : null); }
        }

        public bool IsRejectEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.NoButton : null);
        }

        public bool ActivateReject()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.NoButton : null);
        }

        public bool FocusReject()
        {
            return NativeSelectionUtility.Select(_settings != null ? _settings.NoButton : null);
        }

        public Component DoneButton
        {
            get { return _settings != null ? _settings.DoneButton : null; }
        }

        /// <summary>The game rewrites this button as the army empties: Discard while troops are still
        /// on offer, Close once none are (<c>HostileJoinMenu.HandleTroopsupdated</c>).</summary>
        public string DoneText
        {
            get { return GetButtonText(_settings != null ? _settings.DoneButton : null); }
        }

        public bool IsDoneEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.DoneButton : null);
        }

        public bool ActivateDone()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.DoneButton : null);
        }

        public bool FocusDone()
        {
            return NativeSelectionUtility.Select(_settings != null ? _settings.DoneButton : null);
        }

        public Component MassMoveButton
        {
            get { return _settings != null ? _settings.MassMoveButton : null; }
        }

        /// <summary>The game's own name for it: the button draws no text, only the details the menu
        /// hangs on it in the join stage ("Common/MoveTroops/MoveMax").</summary>
        public string MassMoveText
        {
            get
            {
                string label = GetButtonText(_settings != null ? _settings.MassMoveButton : null);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }

                Tooltip tooltip = Tooltip.ForComponent(MassMoveButton, _localization);
                System.Collections.Generic.IReadOnlyList<string> lines = tooltip != null ? tooltip.TextLines : null;
                return lines != null && lines.Count > 0 ? SpeechTextSanitizer.Normalize(lines[0]) : string.Empty;
            }
        }

        /// <summary>Whether the game will take the click: it turns the button off once the offered army
        /// is empty.</summary>
        public bool IsMassMoveEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.MassMoveButton : null);
        }

        public bool ActivateMassMove()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.MassMoveButton : null);
        }

        public bool FocusMassMove()
        {
            return NativeSelectionUtility.Select(_settings != null ? _settings.MassMoveButton : null);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        private bool IsNativeStage(string stageName)
        {
            object value = StageField != null ? StageField.GetValue(_menu) : null;
            return value != null && value.ToString() == stageName;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonText(UIButton button)
        {
            return MenuButtonTextUtility.GetStandardButtonLabel(button);
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }
    }
}
