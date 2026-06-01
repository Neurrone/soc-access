using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Adapters
{
    internal enum HostileJoinMenuStage
    {
        None,
        Choice,
        Join
    }

    internal sealed class HostileJoinMenuAdapter : IDisposable
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(HostileJoinMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(HostileJoinMenu), "_async");
        private static readonly FieldInfo StageField = AccessTools.Field(typeof(HostileJoinMenu), "_stage");
        private static readonly FieldInfo AttackingCommanderField = AccessTools.Field(typeof(HostileJoinMenu), "_attackingCommander");
        private static readonly FieldInfo JoiningCommanderField = AccessTools.Field(typeof(HostileJoinMenu), "_joiningCommander");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(HostileJoinMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(HostileJoinMenu), "_localizationHandler");
        private static readonly FieldInfo HeaderTroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");

        private readonly HostileJoinMenu _menu;
        private readonly HostileJoinMenu.Settings _settings;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
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

        public string Title
        {
            get { return GetText(_settings != null ? _settings.TitleText : null); }
        }

        public string Instructions
        {
            get { return GetText(_settings != null ? _settings.JoinText : null); }
        }

        public string ChoiceBody
        {
            get { return GetText(_settings != null ? _settings.InformationText : null); }
        }

        public string AcceptLabel
        {
            get
            {
                if (_settings == null)
                {
                    return string.Empty;
                }

                string goldAmount = _settings.YesButtonGoldAmount != null && _settings.YesButtonGoldAmount.Active
                    ? GetText(_settings.YesButtonGoldAmount)
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(goldAmount))
                {
                    return ModText.Get(ModStrings.Common.ResourceAmount, goldAmount, GetGoldName());
                }

                string label = GetButtonText(_settings.YesButton, string.Empty);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }

                return string.Empty;
            }
        }

        public string RejectLabel
        {
            get { return GetButtonText(_settings != null ? _settings.NoButton : null, string.Empty); }
        }

        public string DiscardLabel
        {
            get { return GetButtonText(_settings != null ? _settings.DoneButton : null, "Discard"); }
        }

        public string MassMoveLabel
        {
            get { return GetButtonText(_settings != null ? _settings.MassMoveButton : null, "Mass move"); }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && GetField<object>(_menu, AsyncField) != null
                && Stage != HostileJoinMenuStage.None;
        }

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

        public TroopHudAdapter WielderTroops
        {
            get { return new TroopHudAdapter(GetWielderTroopHud(), _facade, _localization); }
        }

        public TroopHudAdapter JoiningTroops
        {
            get { return new TroopHudAdapter(_settings != null ? _settings.TroopHUD : null, _facade, _localization); }
        }

        public string AttackingCommanderName
        {
            get
            {
                ICommanderState commander = GetField<ICommanderState>(_menu, AttackingCommanderField);
                string name = commander != null && _facade != null ? _facade.Commanders.GetName(commander.Id) : string.Empty;
                return SpeechTextSanitizer.Normalize(name);
            }
        }

        public bool ActivateDiscard()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.DoneButton : null);
        }

        public bool ActivateAccept()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.YesButton : null);
        }

        public bool IsAcceptEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.YesButton : null);
        }

        public void FocusAccept()
        {
            NativeSelectionUtility.Select(_settings != null ? _settings.YesButton : null);
        }

        public bool ActivateReject()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.NoButton : null);
        }

        public bool IsRejectEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.NoButton : null);
        }

        public void FocusReject()
        {
            NativeSelectionUtility.Select(_settings != null ? _settings.NoButton : null);
        }

        public bool IsDiscardEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.DoneButton : null);
        }

        public bool ActivateMassMove()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.MassMoveButton : null);
        }

        public bool IsMassMoveEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.MassMoveButton : null);
        }

        public void FocusMassMove()
        {
            NativeSelectionUtility.Select(_settings != null ? _settings.MassMoveButton : null);
        }

        public Tooltip MassMoveTooltip
        {
            get { return Tooltip.ForComponent(_settings != null ? _settings.MassMoveButton : null, _localization); }
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        private TroopHUD GetWielderTroopHud()
        {
            return _settings != null && _settings.WielderInteractHeader != null
                ? GetField<TroopHUD>(_settings.WielderInteractHeader, HeaderTroopHudField)
                : null;
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

        private string GetGoldName()
        {
            return GameText.Get(_localization, "Common/Resource/" + ResourceType.Gold, ResourceType.Gold.ToString());
        }

        private static string GetButtonText(UIButton button, string fallback)
        {
            string text = MenuButtonTextUtility.GetStandardButtonLabel(button);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
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
