using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Addons;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Campaign;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class TaleButtonAdapter : IMenuButtonAdapter
    {
        private static readonly AccessTools.FieldRef<TaleButton, AddonProfile> AddonProfileRef =
            AccessTools.FieldRefAccess<TaleButton, AddonProfile>("_addonProfile");
        private static readonly AccessTools.FieldRef<TaleButton, UIButton> MainButtonRef =
            AccessTools.FieldRefAccess<TaleButton, UIButton>("_mainButton");
        private static readonly AccessTools.FieldRef<TaleButton, string> CampaignIdentifierRef =
            AccessTools.FieldRefAccess<TaleButton, string>("_campaignIdentifier");
        private static readonly AccessTools.FieldRef<TaleButton, UITextMesh> CampaignTitleRef =
            AccessTools.FieldRefAccess<TaleButton, UITextMesh>("_campaignTitle");
        private static readonly AccessTools.FieldRef<TaleButton, UITextMesh> CampaignDescriptionRef =
            AccessTools.FieldRefAccess<TaleButton, UITextMesh>("_campaignDescription");
        private static readonly AccessTools.FieldRef<TaleButton, GameObject> OwnedContainerRef =
            AccessTools.FieldRefAccess<TaleButton, GameObject>("_ownedContainer");
        private static readonly AccessTools.FieldRef<TaleButton, GameObject> PurchaseContainerRef =
            AccessTools.FieldRefAccess<TaleButton, GameObject>("_purchaseContainer");
        private static readonly AccessTools.FieldRef<TaleButton, GameObject> ComingSoonContainerRef =
            AccessTools.FieldRefAccess<TaleButton, GameObject>("_comingSoonContainer");
        private static readonly AccessTools.FieldRef<TaleButton, GameObject> ProgressContainerRef =
            AccessTools.FieldRefAccess<TaleButton, GameObject>("_progressContainer");
        private static readonly AccessTools.FieldRef<TaleButton, UITextMesh> PartOfDLCLabelRef =
            AccessTools.FieldRefAccess<TaleButton, UITextMesh>("_partOfDLCLabel");
        private static readonly AccessTools.FieldRef<TaleButton, ICampaignDefinition> DefinitionRef =
            AccessTools.FieldRefAccess<TaleButton, ICampaignDefinition>("_definition");
        private static readonly AccessTools.FieldRef<TaleButton, CampaignState> CampaignStateRef =
            AccessTools.FieldRefAccess<TaleButton, CampaignState>("_campaignState");
        private static readonly AccessTools.FieldRef<TaleButton, IAddonManager> AddonManagerRef =
            AccessTools.FieldRefAccess<TaleButton, IAddonManager>("_addonManager");
        private static readonly MethodInfo HandleHoverEnterMethod =
            AccessTools.Method(typeof(TaleButton), "HandleHoverEnter");

        private readonly TaleButton _taleButton;

        public TaleButtonAdapter(TaleButton taleButton)
        {
            _taleButton = taleButton;
            Button = taleButton != null ? MainButtonRef(taleButton) : null;
        }

        public UIButton Button { get; private set; }

        public string GetLabel()
        {
            return GetText(GetCampaignTitle());
        }

        /// <summary>The paragraph the card draws under its name, apart from the name itself: it is
        /// always on the screen, so a screen can decide where in the readout it belongs. Empty while
        /// the card draws its purchase state instead, which is what the card says in place of the
        /// description.</summary>
        public string GetDescription()
        {
            if (IsActive(GetPurchaseContainer()))
            {
                return string.Empty;
            }

            return GetText(GetCampaignDescription());
        }

        public string GetStatus()
        {
            List<string> parts = new List<string>();
            string nativeState = BuildNativeStateText();
            if (!string.IsNullOrWhiteSpace(nativeState))
            {
                parts.Add(nativeState);
            }

            string progress = BuildProgressStatus();
            if (!string.IsNullOrWhiteSpace(progress))
            {
                parts.Add(progress);
            }

            return parts.Count == 0 ? string.Empty : string.Join("\n", parts.ToArray());
        }

        public bool IsVisible()
        {
            return _taleButton != null
                && IsLiveSceneObject(((Component)_taleButton).gameObject)
                && MenuButtonAdapterBase.IsButtonVisible(Button);
        }

        public bool IsEnabled()
        {
            return Button != null && Button.Interactable;
        }

        public bool Activate()
        {
            return NativeSelectionUtility.Click(Button);
        }

        public void FocusNative()
        {
            if (!IsVisible())
            {
                return;
            }

            NativeSelectionUtility.Select(Button);
            if (_taleButton != null && HandleHoverEnterMethod != null)
            {
                HandleHoverEnterMethod.Invoke(_taleButton, new object[] { Button });
            }
        }

        private string BuildNativeStateText()
        {
            if (IsActive(GetComingSoonContainer()))
            {
                string comingSoon = GetAllVisibleText(GetComingSoonContainer());
                return string.IsNullOrWhiteSpace(comingSoon)
                    ? CampaignProgress.GetLocalizedText("Common/ComingSoon", "coming soon")
                    : comingSoon;
            }

            if (IsActive(GetPurchaseContainer()))
            {
                return JoinLines(
                    GetText(GetPartOfDLCLabel()),
                    GetAllVisibleText(GetPurchaseContainer(), GetPartOfDLCLabel()));
            }

            if (IsActive(GetProgressContainer()))
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private string BuildProgressStatus()
        {
            if (!IsActive(GetProgressContainer()))
            {
                return string.Empty;
            }

            IAddonManager addonManager = _taleButton != null ? AddonManagerRef(_taleButton) : null;
            return CampaignProgress.BuildMissionStatus(
                _taleButton != null ? DefinitionRef(_taleButton) : null,
                _taleButton != null ? CampaignStateRef(_taleButton) : null,
                map => map != null && (addonManager == null || map.AddonProfile == null || map.AddonProfile.CanBeUsed(addonManager)));
        }

        private UITextMesh GetCampaignTitle()
        {
            return _taleButton != null ? CampaignTitleRef(_taleButton) : null;
        }

        private UITextMesh GetCampaignDescription()
        {
            return _taleButton != null ? CampaignDescriptionRef(_taleButton) : null;
        }

        private GameObject GetOwnedContainer()
        {
            return _taleButton != null ? OwnedContainerRef(_taleButton) : null;
        }

        private GameObject GetPurchaseContainer()
        {
            return _taleButton != null ? PurchaseContainerRef(_taleButton) : null;
        }

        private GameObject GetComingSoonContainer()
        {
            return _taleButton != null ? ComingSoonContainerRef(_taleButton) : null;
        }

        private GameObject GetProgressContainer()
        {
            return _taleButton != null ? ProgressContainerRef(_taleButton) : null;
        }

        private UITextMesh GetPartOfDLCLabel()
        {
            return _taleButton != null ? PartOfDLCLabelRef(_taleButton) : null;
        }

        private static string GetText(UITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetAllVisibleText(GameObject root, params UITextMesh[] ignoredTextMeshes)
        {
            if (root == null || !root.activeInHierarchy)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            UITextMesh[] textMeshes = root.GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null || IsIgnored(textMesh, ignoredTextMeshes))
                {
                    continue;
                }

                string candidate = GetText(textMesh);
                if (!string.IsNullOrWhiteSpace(candidate) && !parts.Contains(candidate))
                {
                    parts.Add(candidate);
                }
            }

            return parts.Count == 0 ? string.Empty : string.Join("\n", parts.ToArray());
        }

        private static string JoinLines(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> cleaned = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(part))
                {
                    cleaned.Add(part);
                }
            }

            return cleaned.Count == 0 ? string.Empty : string.Join("\n", cleaned.ToArray());
        }

        private static bool IsIgnored(UITextMesh textMesh, UITextMesh[] ignoredTextMeshes)
        {
            if (textMesh == null || ignoredTextMeshes == null)
            {
                return false;
            }

            for (int i = 0; i < ignoredTextMeshes.Length; i++)
            {
                if (ReferenceEquals(textMesh, ignoredTextMeshes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
