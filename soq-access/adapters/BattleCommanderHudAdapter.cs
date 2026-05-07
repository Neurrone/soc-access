using System;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.HUD;
using SongsOfConquest.Client.Battle.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class BattleCommanderHudAdapter
    {
        private static readonly FieldInfo WielderPortraitButtonField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_wielderPortraitButton");
        private static readonly FieldInfo WielderPortraitContainerField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_wielderPortraitContainer");
        private static readonly FieldInfo EssenceContainerField =
            AccessTools.Field(typeof(BattleCommanderHUD), "_essenceContainer");
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
            string sideLabel = side == CombatHudSide.Attacker ? "Attacker" : "Defender";
            if (commander == null || commander.GetIsEmpty())
            {
                return sideLabel + " portrait";
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
                name = "wielder";
            }

            return sideLabel + ", " + name + ", level " + commander.GetLevel();
        }

        public UIButton GetPortraitButton(CombatHudSide side)
        {
            return GetField<UIButton>(GetCommanderHud(side), WielderPortraitButtonField);
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
            if (_localization == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            string localized = _localization.GetText(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : SpeechTextSanitizer.Normalize(localized);
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

        private static bool IsGameObjectVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }
    }
}
