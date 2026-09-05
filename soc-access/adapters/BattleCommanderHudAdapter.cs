using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using SongsOfConquest;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.HUD;
using SongsOfConquest.Client.Battle.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Ai;
using SongsOfConquest.Common.Game;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class BattleCommanderHudAdapter
    {
        private static readonly FieldInfo WielderPortraitButtonField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_wielderPortraitButton");
        private static readonly FieldInfo WielderPortraitContainerField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_wielderPortraitContainer");
        private static readonly FieldInfo EssenceContainerField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_essenceContainer");
        private static readonly FieldInfo AiAutoBattleButtonsField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_aiAutoBattleButtons");
        private static readonly FieldInfo CommanderStateField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_commanderState");
        private static readonly FieldInfo BattleEssenceCommanderField =
            AccessTools.Field(typeof(BattleEssenceContainer), "_commander");
        private static readonly FieldInfo BattleEssenceContainerField =
            AccessTools.Field(typeof(BattleEssenceContainer), "_container");
        private static readonly FieldInfo BattleEssenceOrderImageNonActiveField =
            AccessTools.Field(typeof(BattleEssenceContainer), "_orderImageNonActive");
        private static readonly FieldInfo BattleEssenceCreationImageNonActiveField =
            AccessTools.Field(typeof(BattleEssenceContainer), "_creationImageNonActive");
        private static readonly FieldInfo BattleEssenceChaosImageNonActiveField =
            AccessTools.Field(typeof(BattleEssenceContainer), "_chaosImageNonActive");
        private static readonly FieldInfo BattleEssenceArcanaImageNonActiveField =
            AccessTools.Field(typeof(BattleEssenceContainer), "_arcanaImageNonActive");
        private static readonly FieldInfo BattleEssenceDestructionImageNonActiveField =
            AccessTools.Field(typeof(BattleEssenceContainer), "_destructionImageNonActive");

        private readonly BattleHUDStateHandler.Settings _settings;
        private readonly IClientBattleFacade _facade;
        private readonly ILocalizationHandler _localization;

        public BattleCommanderHudAdapter(
            BattleHUDStateHandler.Settings settings,
            IClientBattleFacade facade,
            ILocalizationHandler localization)
        {
            _settings = settings;
            _facade = facade;
            _localization = localization;
        }

        public bool IsPortraitVisible(CombatHudSide side)
        {
            BattleCommanderHUD hud = GetCommanderHud(side);
            if (!IsComponentVisible(hud))
            {
                return false;
            }

            GameObject portraitContainer = GetField<GameObject>(hud, WielderPortraitContainerField);
            ICommanderState commander = GetCommander(hud);
            return IsGameObjectVisible(portraitContainer)
                && commander != null
                && !commander.GetIsEmpty();
        }

        public ILocalizationHandler Localization
        {
            get { return _localization; }
        }

        public string GetPortraitLabel(CombatHudSide side)
        {
            BattleCommanderHUD hud = GetCommanderHud(side);
            ICommanderState commander = GetCommander(hud);
            if (commander == null || commander.GetIsEmpty())
            {
                return side == CombatHudSide.Attacker
                    ? ModText.Get(ModStrings.Screens.AttackerPortrait)
                    : ModText.Get(ModStrings.Screens.DefenderPortrait);
            }

            string name = string.Empty;
            try
            {
                name = _facade != null && _facade.Commanders != null
                    ? _facade.Commanders.GetName(commander.Id)
                    : string.Empty;
            }
            catch
            {
                name = string.Empty;
            }

            name = SpeechTextSanitizer.Normalize(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = ModText.Get(ModStrings.Combat.Wielder);
            }

            return ModText.Get(
                ModStrings.Screens.CombatPortraitDetail,
                side == CombatHudSide.Attacker
                    ? ModText.Get(ModStrings.Screens.Attacker)
                    : ModText.Get(ModStrings.Screens.Defender),
                name,
                commander.GetLevel());
        }

        public UIButton GetPortraitButton(CombatHudSide side)
        {
            return GetField<UIButton>(GetCommanderHud(side), WielderPortraitButtonField);
        }

        public bool IsAiControlButtonVisible(CombatHudSide side)
        {
            return IsButtonVisible(GetAiControlButton(side));
        }

        public bool IsAiControlButtonEnabled(CombatHudSide side)
        {
            return IsButtonInteractable(GetAiControlButton(side));
        }

        public string GetAiControlButtonLabel(CombatHudSide side)
        {
            string label = GetFirstTooltipLine(GetAiControlButtonTooltip(side));
            return string.IsNullOrWhiteSpace(label) ? ModText.Get(ModStrings.Screens.AiControl) : label;
        }

        public void FocusAiControlButton(CombatHudSide side)
        {
            NativeSelectionUtility.Select(GetAiControlButton(side));
        }

        public bool ClickAiControlButton(CombatHudSide side)
        {
            return NativeSelectionUtility.Click(GetAiControlButton(side));
        }

        public Tooltip GetAiControlButtonTooltip(CombatHudSide side)
        {
            return Tooltip.ForComponent(GetAiControlButton(side), _localization);
        }

        public bool IsEssenceMenuVisible(CombatHudSide side)
        {
            BattleEssenceContainer container = GetEssenceContainer(side);
            Transform innerContainer = GetField<Transform>(container, BattleEssenceContainerField);
            return IsPortraitVisible(side)
                && (innerContainer == null || IsGameObjectVisible(innerContainer.gameObject));
        }

        public string GetEssenceLabel(CombatHudSide side, EssenceType essenceType)
        {
            return Localize("Units/Types/" + essenceType, FormatEnumName(essenceType.ToString()))
                + ", "
                + GetEssenceAmount(side, essenceType);
        }

        public string BuildEssenceSummary(CombatHudSide side, bool requireVisible)
        {
            if (requireVisible && !IsEssenceMenuVisible(side))
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddEssenceSummaryPart(parts, side, EssenceType.Order);
            AddEssenceSummaryPart(parts, side, EssenceType.Creation);
            AddEssenceSummaryPart(parts, side, EssenceType.Chaos);
            AddEssenceSummaryPart(parts, side, EssenceType.Arcana);
            AddEssenceSummaryPart(parts, side, EssenceType.Destruction);
            return string.Join(", ", parts.ToArray());
        }

        public int GetCommanderTeamId(CombatHudSide side)
        {
            ICommanderState commander = GetCommander(GetCommanderHud(side));
            return commander != null && !commander.GetIsEmpty() ? commander.TeamId : -1;
        }

        public void FocusEssence(CombatHudSide side, EssenceType essenceType)
        {
            NativeSelectionUtility.Select(GetEssenceTooltipComponent(side, essenceType));
        }

        public Tooltip GetEssenceTooltip(CombatHudSide side, EssenceType essenceType)
        {
            return Tooltip.ForComponent(GetEssenceTooltipComponent(side, essenceType), _localization);
        }

        private BattleCommanderHUD GetCommanderHud(CombatHudSide side)
        {
            GameObject container = null;
            if (_settings != null)
            {
                container = side == CombatHudSide.Attacker
                    ? _settings.AttackingCommanderContainer
                    : _settings.DefendingCommanderContainer;
            }

            return container != null ? container.GetComponent<BattleCommanderHUD>() : null;
        }

        private ICommanderState GetCommander(BattleCommanderHUD hud)
        {
            return GetField<ICommanderState>(hud, CommanderStateField);
        }

        private BattleEssenceContainer GetEssenceContainer(CombatHudSide side)
        {
            return GetField<BattleEssenceContainer>(GetCommanderHud(side), EssenceContainerField);
        }

        private UIButton GetAiControlButton(CombatHudSide side)
        {
            if (!IsAiControlSideActive(side))
            {
                return null;
            }

            UIButton[] buttons = GetField<UIButton[]>(GetCommanderHud(side), AiAutoBattleButtonsField);
            if (buttons == null)
            {
                return null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (IsButtonVisible(buttons[i]))
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private bool IsAiControlSideActive(CombatHudSide side)
        {
            if (!HudGroupVisible(GetAiAutoBattleContainer(side))
                || _facade == null
                || _facade.Teams == null)
            {
                return false;
            }

            try
            {
                ITeamState current = _facade.Teams.Current;
                if (current == null
                    || _facade.GameMode != GameMode.Adventure
                    || !_facade.Teams.GetIsLocal(current.Id)
                    || current.AiMode != AiMode.Off)
                {
                    return false;
                }

                ITeamState sideTeam = side == CombatHudSide.Attacker
                    ? _facade.Teams.AttackingTeam
                    : _facade.Teams.DefendingTeam;
                return sideTeam != null && current.Id == sideTeam.Id;
            }
            catch
            {
                return false;
            }
        }

        private GameObject GetAiAutoBattleContainer(CombatHudSide side)
        {
            if (_settings == null)
            {
                return null;
            }

            return side == CombatHudSide.Attacker
                ? _settings.AttackerAIAutoBattleContainer
                : _settings.DefenderAIAutoBattleContainer;
        }

        private int GetEssenceAmount(CombatHudSide side, EssenceType essenceType)
        {
            ICommanderState commander = GetField<ICommanderState>(GetEssenceContainer(side), BattleEssenceCommanderField);
            if (commander == null || commander.GetIsEmpty() || commander.EssenceWallet == null)
            {
                return 0;
            }

            try
            {
                return commander.EssenceWallet.Amount(essenceType);
            }
            catch
            {
                return 0;
            }
        }

        private void AddEssenceSummaryPart(List<string> parts, CombatHudSide side, EssenceType essenceType)
        {
            int amount = GetEssenceAmount(side, essenceType);
            if (amount <= 0)
            {
                return;
            }

            parts.Add(Localize("Units/Types/" + essenceType, FormatEnumName(essenceType.ToString())) + " " + amount);
        }

        private Component GetEssenceTooltipComponent(CombatHudSide side, EssenceType essenceType)
        {
            BattleEssenceContainer container = GetEssenceContainer(side);
            if (container == null)
            {
                return null;
            }

            FieldInfo field = null;
            switch (essenceType)
            {
                case EssenceType.Order:
                    field = BattleEssenceOrderImageNonActiveField;
                    break;
                case EssenceType.Creation:
                    field = BattleEssenceCreationImageNonActiveField;
                    break;
                case EssenceType.Chaos:
                    field = BattleEssenceChaosImageNonActiveField;
                    break;
                case EssenceType.Arcana:
                    field = BattleEssenceArcanaImageNonActiveField;
                    break;
                case EssenceType.Destruction:
                    field = BattleEssenceDestructionImageNonActiveField;
                    break;
            }

            return GetField<Component>(container, field);
        }

        private string Localize(string key, string fallback)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback));
        }

        private static string FormatEnumName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            if (owner == null || field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(owner) as T;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsComponentVisible(Component component)
        {
            return component != null && IsGameObjectVisible(component.gameObject);
        }

        private static bool IsButtonVisible(UIButton button)
        {
            return button != null && button.Active && IsGameObjectVisible(button as Component);
        }

        private static bool IsButtonInteractable(UIButton button)
        {
            return IsButtonVisible(button) && button.Interactable;
        }

        private static bool IsGameObjectVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsGameObjectVisible(Component component)
        {
            return component != null && IsGameObjectVisible(component.gameObject);
        }

        private static bool HudGroupVisible(GameObject gameObject)
        {
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>();
            return canvasGroup == null || canvasGroup.alpha > 0.01f;
        }

        private static string GetFirstTooltipLine(Tooltip tooltip)
        {
            if (tooltip == null || tooltip.TextLines == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < tooltip.TextLines.Count; i++)
            {
                string line = SpeechTextSanitizer.Normalize(tooltip.TextLines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return string.Empty;
        }
    }
}
