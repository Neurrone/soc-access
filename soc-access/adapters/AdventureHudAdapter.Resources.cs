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
    /// THE TREASURY STRIP: the six resources the HUD draws across the top, their drawn amounts and
    /// incomes, and the tooltip each one carries.
    ///
    /// Split out of AdventureHudAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureHudAdapter
    {
        public bool IsResourcesMenuVisible()
        {
            ResourceHUD.Settings settings = ResourceSettings;
            return settings != null
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.ResourceContainer : null)
                && settings.Container != null
                && settings.Container.activeInHierarchy;
        }

        /// <summary>The amount the strip draws beside a resource, empty where it draws none.</summary>
        public string GetResourceAmountText(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            return UITextMeshTextUtility.Spoken(entry != null ? entry.AmountText : null);
        }

        /// <summary>The income the strip draws under a resource, empty where it draws none.</summary>
        public string GetResourceIncomeText(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            return entry != null && GameObjects.IsLive(entry.IncomeText)
                ? UITextMeshTextUtility.Spoken(entry.IncomeText)
                : string.Empty;
        }

        public int GetResourceAmount(ResourceType resourceType)
        {
            ITeamState team = Facade != null && Facade.Teams != null ? Facade.Teams.LocalTeamInControl : null;
            Resource resource = team != null && team.Resources != null ? team.Resources.GetResource(resourceType) : null;
            return resource != null ? resource.Amount : 0;
        }

        public string GetResourceName(ResourceType resourceType)
        {
            return ResourceCosts.Name(LocalizationHandler, resourceType);
        }

        public void FocusResource(ResourceType resourceType)
        {
            Component component = GetResourceTooltipComponent(resourceType);
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
            }
        }

        public Tooltip GetResourceTooltip(ResourceType resourceType)
        {
            Component component = GetResourceTooltipComponent(resourceType);
            RectTransform anchor = GetResourceTooltipAnchor(resourceType);
            return anchor != null
                ? Tooltip.ForComponent(component, anchor, ResourceTooltipAnchors, LocalizationHandler)
                : Tooltip.ForComponent(component, LocalizationHandler);
        }

        private ResourceHUD.ResourceEntry GetResourceEntry(ResourceType resourceType)
        {
            ResourceHUD.Settings settings = ResourceSettings;
            ResourceHUD.ResourceEntry[] entries = settings != null ? settings.Resources : null;
            if (entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                ResourceHUD.ResourceEntry entry = entries[i];
                if (entry != null && entry.Type == resourceType)
                {
                    return entry;
                }
            }

            return null;
        }

        private Component GetResourceTooltipComponent(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            return entry != null ? entry.TooltipImage as Component : null;
        }

        private RectTransform GetResourceTooltipAnchor(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            RectTransform incomeGlow = entry != null && entry.IncomeGlow != null ? entry.IncomeGlow.rectTransform : null;
            if (incomeGlow != null)
            {
                return incomeGlow;
            }

            Component amountText = entry != null ? entry.AmountText as Component : null;
            if (amountText != null)
            {
                return amountText.GetComponent<RectTransform>();
            }

            Component tooltipImage = entry != null ? entry.TooltipImage as Component : null;
            return tooltipImage != null ? tooltipImage.GetComponent<RectTransform>() : null;
        }
    }
}
