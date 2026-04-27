using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class LetterboxStoryTextAdapter : IStoryTextAdapter
    {
        private static readonly AccessTools.FieldRef<LetterboxStoryText, UITransform> ContainerRef =
            AccessTools.FieldRefAccess<LetterboxStoryText, UITransform>("_container");

        private static readonly AccessTools.FieldRef<LetterboxStoryText, UITextMesh> TitleTextRef =
            AccessTools.FieldRefAccess<LetterboxStoryText, UITextMesh>("_titleText");

        private static readonly AccessTools.FieldRef<LetterboxStoryText, UITextMesh> LoreTextRef =
            AccessTools.FieldRefAccess<LetterboxStoryText, UITextMesh>("_loreText");

        private static readonly AccessTools.FieldRef<LetterboxStoryText, Coroutine> TypeRoutineRef =
            AccessTools.FieldRefAccess<LetterboxStoryText, Coroutine>("_typeRoutine");

        private static readonly MethodInfo AbortCurrentStateMethod =
            AccessTools.Method(typeof(LetterboxStoryText), "AbortCurrentState");

        private readonly LetterboxStoryText _storyText;

        public LetterboxStoryTextAdapter(LetterboxStoryText storyText)
        {
            _storyText = storyText;
        }

        public object SourceKey
        {
            get { return _storyText; }
        }

        public string Title
        {
            get { return SpeechTextSanitizer.Normalize(GetText(TitleTextRef)); }
        }

        public string Body
        {
            get { return SpeechTextSanitizer.Normalize(GetText(LoreTextRef)); }
        }

        public bool IsPresent()
        {
            if (_storyText == null)
            {
                return false;
            }

            UITransform container = ContainerRef(_storyText);
            if (container == null)
            {
                return false;
            }

            Component component = container as Component;
            return component != null
                && component.gameObject != null
                && component.gameObject.activeInHierarchy
                && component.gameObject.scene.IsValid()
                && component.gameObject.scene.isLoaded
                && HasTextStarted();
        }

        public bool AdvanceNow()
        {
            if (!IsPresent() || AbortCurrentStateMethod == null)
            {
                return false;
            }

            bool wasTyping = IsTyping();
            AbortCurrentStateMethod.Invoke(_storyText, null);
            if (wasTyping && IsPresent())
            {
                AbortCurrentStateMethod.Invoke(_storyText, null);
            }

            return true;
        }

        private bool IsTyping()
        {
            return _storyText != null && TypeRoutineRef(_storyText) != null;
        }

        private bool HasTextStarted()
        {
            UITextMesh loreText = _storyText != null ? LoreTextRef(_storyText) : null;
            TMP_Text tmpText = loreText as TMP_Text;
            return !IsTyping()
                || tmpText == null
                || tmpText.maxVisibleCharacters > 0;
        }

        private string GetText(AccessTools.FieldRef<LetterboxStoryText, UITextMesh> textRef)
        {
            if (_storyText == null || textRef == null)
            {
                return string.Empty;
            }

            return UITextMeshTextUtility.GetEffectiveText(textRef(_storyText));
        }

    }
}
