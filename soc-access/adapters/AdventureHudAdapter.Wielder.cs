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
    /// THE SELECTED WIELDER'S PANEL: the portrait, the experience bar and its level-up button, the
    /// five essences, the army slots, and the inventory, move-to-destination and spellbook buttons
    /// drawn beside them.
    ///
    /// Split out of AdventureHudAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureHudAdapter
    {
        public CommanderHudPortraitAdapter SelectedWielderPortrait
        {
            get
            {
                CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
                return new CommanderHudPortraitAdapter(
                    "adventure-selected-wielder",
                    GetSelectedCommanderName,
                    portrait,
                    LocalizationHandler,
                    IsSelectionHudVisible,
                    () => true);
            }
        }

        public bool IsSelectionHudVisible()
        {
            CommanderHUD.Settings settings = CommanderSettings;
            return settings != null
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.SelectionHUDContainer : null)
                && GameObjects.IsLive(settings.CommanderContainer as Component)
                && settings.Portrait != null
                && settings.Portrait.Commander != null;
        }

        public bool IsExperienceVisible()
        {
            return IsSelectionHudVisible() && GameObjects.IsLive(GetExperienceBar());
        }

        /// <summary>The game's own caption for experience.</summary>
        public string ExperienceCaption
        {
            get { return Localize("Commanders/Tooltip/Experience", "Experience"); }
        }

        /// <summary>The game's own caption for a wielder's level.</summary>
        public string LevelCaption
        {
            get { return Localize("Commanders/Tooltip/Level", "Level"); }
        }

        /// <summary>
        /// The level the selected wielder is on, the experience it has and the experience the next
        /// level asks for. False where no wielder is selected, which is the whole of "there is
        /// nothing to count".
        /// </summary>
        public bool TryGetExperience(out int level, out int current, out int nextLevelExperience)
        {
            level = 0;
            current = 0;
            nextLevelExperience = 0;
            CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
            ICommanderState commander = portrait != null ? portrait.Commander : null;
            if (commander == null || commander.Stats == null)
            {
                return false;
            }

            level = commander.GetLevel();
            // The bar's own level text wins where it says anything: it is what the player can see.
            string levelText = UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(GetExperienceBar(), ExperienceBarLevelTextField));
            int parsedLevel;
            if (!string.IsNullOrWhiteSpace(levelText) && int.TryParse(levelText, out parsedLevel))
            {
                level = parsedLevel;
            }

            current = commander.Stats.Experience;
            nextLevelExperience = CommanderLevelUtility.GetExperienceForLevel(level + 1);
            return true;
        }

        public void FocusExperience()
        {
            Component component = GetExperienceTooltipComponent();
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
            }
        }

        public Tooltip ExperienceTooltip
        {
            get { return Tooltip.ForComponent(GetExperienceTooltipComponent(), LocalizationHandler); }
        }

        public bool IsLevelUpButtonVisible()
        {
            return IsSelectionHudVisible() && MenuButtonAdapterBase.IsButtonDrawn(GetLevelUpButton());
        }

        public string LevelUpButtonLabel
        {
            get { return Localize("Adventure/HUD/LevelUpButtonTooltip", "Level up"); }
        }

        public void FocusLevelUpButton()
        {
            NativeSelectionUtility.Select(GetLevelUpButton());
        }

        public bool ClickLevelUpButton()
        {
            return NativeSelectionUtility.Click(GetLevelUpButton());
        }

        public bool IsLevelUpButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetLevelUpButton());
        }

        public bool IsEssenceMenuVisible()
        {
            return IsSelectionHudVisible() && GameObjects.IsLive(GetAdventureEssenceContainer() as Component);
        }

        public string GetEssenceLabel(EssenceType essenceType)
        {
            return GetEssenceName(essenceType) + " " + GetSelectedCommanderEssenceAmount(essenceType);
        }

        public void FocusEssence(EssenceType essenceType)
        {
            Component component = GetEssenceTooltipComponent(essenceType);
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
            }
        }

        public Tooltip GetEssenceTooltip(EssenceType essenceType)
        {
            return Tooltip.ForComponent(GetEssenceTooltipComponent(essenceType), LocalizationHandler);
        }

        public bool IsTroopMenuVisible()
        {
            TroopHUD troopHud = CommanderSettings != null ? CommanderSettings.TroopHUD : null;
            if (!IsSelectionHudVisible() || !GameObjects.IsLive(troopHud))
            {
                return false;
            }

            for (int i = 0; i < 9; i++)
            {
                if (IsTroopSlotVisible(i))
                {
                    return true;
                }
            }

            return false;
        }

        public TroopHudAdapter Troops
        {
            get
            {
                return new TroopHudAdapter(
                    CommanderSettings != null ? CommanderSettings.TroopHUD : null,
                    Facade,
                    LocalizationHandler);
            }
        }

        public bool IsTroopSlotVisible(int index)
        {
            TroopHUDEntry entry = GetTroopSlot(index);
            return entry != null && entry.IsUnlocked && GameObjects.IsLive(entry);
        }

        public bool IsInventoryButtonVisible()
        {
            return IsSelectionHudVisible() && MenuButtonAdapterBase.IsButtonDrawn(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public string InventoryButtonLabel
        {
            // The button's own tooltip is the same words with the hotkey ("Wielder Sheet (C)"); use
            // it as the label so the readout is not "Wielder Sheet, button, Wielder Sheet (C)" - the
            // tooltip's first line, now identical to the label, drops from the readout. Fall back to
            // the plain game label when the tooltip has not been drawn.
            get
            {
                string label = TooltipLines.First(InventoryButtonTooltip);
                return string.IsNullOrWhiteSpace(label)
                    ? Localize("Adventure/HUD/InventoryButton", "Inventory")
                    : label;
            }
        }

        public void FocusInventoryButton()
        {
            NativeSelectionUtility.Select(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public bool ClickInventoryButton()
        {
            return NativeSelectionUtility.Click(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public bool IsInventoryButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public Tooltip InventoryButtonTooltip
        {
            get { return Tooltip.ForComponent(CommanderSettings != null ? CommanderSettings.InventoryButton : null, LocalizationHandler); }
        }

        public bool IsMoveToDestinationButtonVisible()
        {
            return IsSelectionHudVisible() && MenuButtonAdapterBase.IsButtonDrawn(GetMoveToDestinationButton());
        }

        public bool IsMoveToDestinationButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetMoveToDestinationButton());
        }

        public string MoveToDestinationButtonLabel
        {
            get { return TooltipLines.First(MoveToDestinationButtonTooltip); }
        }

        public void FocusMoveToDestinationButton()
        {
            NativeSelectionUtility.Select(GetMoveToDestinationButton());
        }

        public bool ClickMoveToDestinationButton()
        {
            return NativeSelectionUtility.Click(GetMoveToDestinationButton());
        }

        public Tooltip MoveToDestinationButtonTooltip
        {
            get { return Tooltip.ForComponent(GetMoveToDestinationButton(), LocalizationHandler); }
        }

        public bool IsSpellbookButtonVisible()
        {
            return IsSelectionHudVisible() && MenuButtonAdapterBase.IsButtonDrawn(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public string SpellbookButtonLabel
        {
            // As with the wielder-sheet button: take the label from the tooltip so its hotkey-bearing
            // first line ("Spells (V)") is not read twice.
            get
            {
                string label = TooltipLines.First(SpellbookButtonTooltip);
                return string.IsNullOrWhiteSpace(label)
                    ? Localize("Common/HUD/SpellbookButton", "Spellbook")
                    : label;
            }
        }

        public void FocusSpellbookButton()
        {
            NativeSelectionUtility.Select(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public bool ClickSpellbookButton()
        {
            return NativeSelectionUtility.Click(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public bool IsSpellbookButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public Tooltip SpellbookButtonTooltip
        {
            get { return Tooltip.ForComponent(CommanderSettings != null ? CommanderSettings.SpellbookButton : null, LocalizationHandler); }
        }

        private string GetSelectedCommanderName()
        {
            ICommanderState commander = CommanderSettings != null && CommanderSettings.Portrait != null
                ? CommanderSettings.Portrait.Commander
                : SelectionHandler != null ? SelectionHandler.SelectedCommander : null;
            return AdventureMapEntityLabel.GetCommanderName(Facade, commander);
        }

        private AdventureEssenceContainer GetAdventureEssenceContainer()
        {
            CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
            return Reflect.Get<AdventureEssenceContainer>(portrait, CommanderHudPortraitEssenceContainerField);
        }

        private ExperienceBar GetExperienceBar()
        {
            CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
            return Reflect.Get<ExperienceBar>(portrait, CommanderHudPortraitExperienceBarField);
        }

        private Component GetExperienceTooltipComponent()
        {
            UIImage image = Reflect.Get<UIImage>(GetExperienceBar(), ExperienceBarTooltipImageField);
            return image as Component;
        }

        private UIButton GetLevelUpButton()
        {
            return Reflect.Get<UIButton>(GetExperienceBar(), ExperienceBarLevelUpButtonField);
        }

        private Component GetEssenceTooltipComponent(EssenceType essenceType)
        {
            AdventureEssenceContainer container = GetAdventureEssenceContainer();
            FieldInfo field = GetEssenceTooltipField(essenceType);
            Image image = Reflect.Get<Image>(container, field);
            if (image == null)
            {
                return null;
            }

            UIImage uiImage = ((Component)image).GetComponent<UIImage>();
            return uiImage != null ? (Component)uiImage : image;
        }

        private static FieldInfo GetEssenceTooltipField(EssenceType essenceType)
        {
            switch (essenceType)
            {
                case EssenceType.Order:
                    return EssenceOrderImageField;
                case EssenceType.Creation:
                    return EssenceCreationImageField;
                case EssenceType.Chaos:
                    return EssenceChaosImageField;
                case EssenceType.Arcana:
                    return EssenceArcanaImageField;
                case EssenceType.Destruction:
                    return EssenceDestructionImageField;
                default:
                    return null;
            }
        }

        private int GetSelectedCommanderEssenceAmount(EssenceType essenceType)
        {
            ICommanderState commander = CommanderSettings != null && CommanderSettings.Portrait != null
                ? CommanderSettings.Portrait.Commander
                : SelectionHandler != null ? SelectionHandler.SelectedCommander : null;
            if (commander == null || Facade == null || Facade.Commanders == null)
            {
                return 0;
            }

            try
            {
                return Facade.Commanders.GetTotalEssenceIncome(commander.Id, essenceType);
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a wielder's essence income", exception);
                return 0;
            }
        }

        private string GetEssenceName(EssenceType essenceType)
        {
            return EssenceText.Name(LocalizationHandler, essenceType);
        }

        private TroopHUDEntry GetTroopSlot(int index)
        {
            if (index < 0)
            {
                return null;
            }

            TroopHUD troopHud = CommanderSettings != null ? CommanderSettings.TroopHUD : null;
            List<TroopHUDEntry> entries = Reflect.Get<List<TroopHUDEntry>>(troopHud, TroopHudTroopsField);
            return entries != null && index < entries.Count ? entries[index] : null;
        }

        private UIButton GetMoveToDestinationButton()
        {
            MovementActionButton movementActionButton = CommanderSettings != null ? CommanderSettings.MovementActionButton : null;
            return Reflect.Get<UIButton>(movementActionButton, MovementActionButtonMoveButtonField);
        }
    }
}
