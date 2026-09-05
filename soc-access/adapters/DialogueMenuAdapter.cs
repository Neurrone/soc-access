using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Dialogue;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class DialogueMenuAdapter : IStoryTextAdapter
    {
        private static readonly AccessTools.FieldRef<DialogueMenu, DialogueMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<DialogueMenu, DialogueMenu.Settings>("_settings");

        private static readonly FieldInfo StateMachineField =
            AccessTools.Field(typeof(DialogueMenu), "_stateMachine");

        private static readonly FieldInfo CurrentDialogueField =
            AccessTools.Field(typeof(DialogueMenu), "_currentDialogue");

        private static readonly FieldInfo CurrentEntryIndexField =
            AccessTools.Field(typeof(DialogueMenu), "_currentEntryIndex");

        private static readonly FieldInfo ActiveConversantsField =
            AccessTools.Field(typeof(DialogueMenu), "_activeConversants");

        private static readonly PropertyInfo CurrentStateTypeProperty =
            AccessTools.Property(StateMachineField.FieldType, "CurrentStateType");

        private static readonly MethodInfo GetHeaderTextMethod =
            AccessTools.Method(typeof(DialogueMenu), "GetHeaderText");

        private static readonly MethodInfo HandlePrimaryClickedMethod =
            AccessTools.Method(typeof(DialogueMenu), "HandlePrimaryClicked");

        private readonly DialogueMenu _dialogueMenu;

        public DialogueMenuAdapter(DialogueMenu dialogueMenu)
        {
            _dialogueMenu = dialogueMenu;
        }

        public object SourceKey
        {
            get { return _dialogueMenu; }
        }

        public string Title
        {
            get
            {
                string nativeHeader;
                if (TryGetNativeHeaderText(out nativeHeader))
                {
                    return SpeechTextSanitizer.Normalize(nativeHeader);
                }

                return SpeechTextSanitizer.Normalize(GetVisibleNameText(GetSettings()));
            }
        }

        public string Body
        {
            get
            {
                DialogueMenu.Settings settings = GetSettings();
                return SpeechTextSanitizer.Normalize(settings != null
                    ? UITextMeshTextUtility.GetEffectiveText(settings.DialogueText)
                    : string.Empty);
            }
        }

        public bool IsPresent()
        {
            if (_dialogueMenu == null)
            {
                return false;
            }

            DialogueMenu.Settings settings = GetSettings();
            if (settings == null || !IsContainerActiveInLoadedScene(settings.Container))
            {
                return false;
            }

            if (IsWaitingForInput())
            {
                return HasVisibleText(settings);
            }

            return IsTypingText() && HasVisibleText(settings) && GetMaxVisibleCharacters(settings.DialogueText) > 0;
        }

        public bool AdvanceNow()
        {
            if (!IsPresent() || HandlePrimaryClickedMethod == null)
            {
                return false;
            }

            bool wasTyping = IsTypingText();
            HandlePrimaryClickedMethod.Invoke(_dialogueMenu, null);
            if (wasTyping && IsWaitingForInput())
            {
                HandlePrimaryClickedMethod.Invoke(_dialogueMenu, null);
            }

            return true;
        }

        private DialogueMenu.Settings GetSettings()
        {
            return _dialogueMenu != null ? SettingsRef(_dialogueMenu) : null;
        }

        private bool IsTypingText()
        {
            return IsState("TypingText");
        }

        private bool IsWaitingForInput()
        {
            return IsState("WaitingForInput");
        }

        private bool IsState(string stateName)
        {
            if (_dialogueMenu == null || StateMachineField == null || CurrentStateTypeProperty == null)
            {
                return false;
            }

            object stateMachine = StateMachineField.GetValue(_dialogueMenu);
            object currentState = stateMachine != null ? CurrentStateTypeProperty.GetValue(stateMachine, null) : null;
            return string.Equals(currentState != null ? currentState.ToString() : string.Empty, stateName, StringComparison.Ordinal);
        }

        private bool HasVisibleText(DialogueMenu.Settings settings)
        {
            string nativeHeader;
            return settings != null
                && ((!TryGetNativeHeaderText(out nativeHeader) && !string.IsNullOrWhiteSpace(GetVisibleNameText(settings)))
                    || !string.IsNullOrWhiteSpace(nativeHeader)
                    || !string.IsNullOrWhiteSpace(UITextMeshTextUtility.GetEffectiveText(settings.DialogueText)));
        }

        private static int GetMaxVisibleCharacters(UITextMesh textMesh)
        {
            TMP_Text tmpText = textMesh as TMP_Text;
            return tmpText != null ? tmpText.maxVisibleCharacters : int.MaxValue;
        }

        private bool TryGetNativeHeaderText(out string header)
        {
            header = string.Empty;
            if (_dialogueMenu == null
                || CurrentDialogueField == null
                || CurrentEntryIndexField == null
                || ActiveConversantsField == null
                || GetHeaderTextMethod == null)
            {
                return false;
            }

            try
            {
                DialogueDefinition dialogue = CurrentDialogueField.GetValue(_dialogueMenu) as DialogueDefinition;
                if (dialogue == null || dialogue.Entries == null)
                {
                    return false;
                }

                int entryIndex = (int)CurrentEntryIndexField.GetValue(_dialogueMenu);
                if (entryIndex < 0 || entryIndex >= dialogue.Entries.Count)
                {
                    return false;
                }

                DialogueDefinitionEntry entry = dialogue.Entries[entryIndex];
                Dictionary<string, DialogueMenu.PersonaInformation> conversants =
                    ActiveConversantsField.GetValue(_dialogueMenu) as Dictionary<string, DialogueMenu.PersonaInformation>;
                DialogueMenu.PersonaInformation conversant;
                if (entry == null
                    || conversants == null
                    || !conversants.TryGetValue(entry.UniqueIdentifier, out conversant))
                {
                    return false;
                }

                header = GetHeaderTextMethod.Invoke(_dialogueMenu, new object[] { entry, conversant }) as string ?? string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to resolve dialogue header text: " + exception.Message);
                return false;
            }
        }

        private static string GetVisibleNameText(DialogueMenu.Settings settings)
        {
            return settings != null && IsNameVisible(settings)
                ? UITextMeshTextUtility.GetEffectiveText(settings.NameText)
                : string.Empty;
        }

        private static bool IsNameVisible(DialogueMenu.Settings settings)
        {
            if (settings == null || settings.NameText == null)
            {
                return false;
            }

            Component component = settings.NameText as Component;
            return component != null
                && component.gameObject != null
                && component.gameObject.activeInHierarchy
                && (settings.NameCanvasGroup == null || settings.NameCanvasGroup.alpha > 0.001f);
        }

        private static bool IsContainerActiveInLoadedScene(UITransform container)
        {
            Component component = container as Component;
            GameObject gameObject = component != null ? component.gameObject : null;
            return gameObject != null
                && gameObject.activeInHierarchy
                && gameObject.scene.IsValid()
                && gameObject.scene.isLoaded;
        }
    }
}
