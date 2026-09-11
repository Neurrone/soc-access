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
using SongsOfConquestAccess.UI;
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
        private static readonly FieldInfo PlayerNameContainerField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_playerNameContainer");
        private static readonly FieldInfo PlayerNameTextField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_playerNameText");

        private readonly BattleHUDStateHandler.Settings _settings;
        private readonly IClientBattleFacade _facade;
        private readonly ILocalizationHandler _localization;

        // One component lookup per side for the life of the battle, misses included: the combat
        // screen is a graph screen and asks these questions on every frame.
        private BattleCommanderHUD _attackerHud;
        private bool _attackerHudProbed;
        private BattleCommanderHUD _defenderHud;
        private bool _defenderHudProbed;

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
            if (!GameObjects.IsLive(hud))
            {
                return false;
            }

            GameObject portraitContainer = Reflect.Get<GameObject>(hud, WielderPortraitContainerField);
            ICommanderState commander = GetCommander(hud);
            return GameObjects.IsLive(portraitContainer)
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

            name = SpokenLines.Clean(name);
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
            return Reflect.Get<UIButton>(GetCommanderHud(side), WielderPortraitButtonField);
        }

        /// <summary>Whether the side's panel is drawing the player's name, which the game does only
        /// when both sides are played by people (<c>BattleCommanderHUD.SetPlayerName</c>).</summary>
        public bool IsPlayerNameVisible(CombatHudSide side)
        {
            BattleCommanderHUD hud = GetCommanderHud(side);
            return GameObjects.IsLive(Reflect.Get<GameObject>(hud, PlayerNameContainerField))
                && !string.IsNullOrWhiteSpace(GetPlayerName(side));
        }

        /// <summary>The player's name as the game wrote it on the side's panel.</summary>
        public string GetPlayerName(CombatHudSide side)
        {
            UITextMesh text = Reflect.Get<UITextMesh>(GetCommanderHud(side), PlayerNameTextField);
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text));
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
            string label = TooltipLines.First(GetAiControlButtonTooltip(side));
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
            Transform innerContainer = Reflect.Get<Transform>(container, BattleEssenceContainerField);
            return IsPortraitVisible(side)
                && (innerContainer == null || GameObjects.IsLive(innerContainer.gameObject));
        }

        public string GetEssenceLabel(CombatHudSide side, EssenceType essenceType)
        {
            return SpokenText.Get(_localization, "Units/Types/" + essenceType, FormatEnumName(essenceType.ToString()))
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
            bool attacker = side == CombatHudSide.Attacker;
            BattleCommanderHUD cached = attacker ? _attackerHud : _defenderHud;
            if (cached != null || (attacker ? _attackerHudProbed : _defenderHudProbed))
            {
                return cached;
            }

            GameObject container = null;
            if (_settings != null)
            {
                container = attacker
                    ? _settings.AttackingCommanderContainer
                    : _settings.DefendingCommanderContainer;
            }

            BattleCommanderHUD hud = container != null ? container.GetComponent<BattleCommanderHUD>() : null;
            if (attacker)
            {
                _attackerHudProbed = true;
                _attackerHud = hud;
            }
            else
            {
                _defenderHudProbed = true;
                _defenderHud = hud;
            }

            return hud;
        }

        private ICommanderState GetCommander(BattleCommanderHUD hud)
        {
            return Reflect.Get<ICommanderState>(hud, CommanderStateField);
        }

        private BattleEssenceContainer GetEssenceContainer(CombatHudSide side)
        {
            return Reflect.Get<BattleEssenceContainer>(GetCommanderHud(side), EssenceContainerField);
        }

        private UIButton GetAiControlButton(CombatHudSide side)
        {
            if (!IsAiControlSideActive(side))
            {
                return null;
            }

            UIButton[] buttons = Reflect.Get<UIButton[]>(GetCommanderHud(side), AiAutoBattleButtonsField);
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
            ICommanderState commander = Reflect.Get<ICommanderState>(GetEssenceContainer(side), BattleEssenceCommanderField);
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

            parts.Add(SpokenText.Get(_localization, "Units/Types/" + essenceType, FormatEnumName(essenceType.ToString())) + " " + amount);
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

            return Reflect.Get<Component>(container, field);
        }

        private static string FormatEnumName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        }

        private static bool IsButtonVisible(UIButton button)
        {
            return button != null && button.Active && GameObjects.IsLive(button as Component);
        }

        private static bool IsButtonInteractable(UIButton button)
        {
            return IsButtonVisible(button) && button.Interactable;
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
    }
}
