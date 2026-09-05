using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class StoryTextAdapter : IStoryTextAdapter
    {
        private static readonly AccessTools.FieldRef<StoryText, Async> LoreAsyncRef =
            AccessTools.FieldRefAccess<StoryText, Async>("_loreAsync");

        private static readonly AccessTools.FieldRef<StoryText, UITransform> ContainerRef =
            AccessTools.FieldRefAccess<StoryText, UITransform>("_container");

        private static readonly AccessTools.FieldRef<StoryText, UITextMesh> TitleTextRef =
            AccessTools.FieldRefAccess<StoryText, UITextMesh>("_titleText");

        private static readonly AccessTools.FieldRef<StoryText, CanvasGroup> HeaderCanvasGroupRef =
            AccessTools.FieldRefAccess<StoryText, CanvasGroup>("_headerCanvasGroup");

        private static readonly AccessTools.FieldRef<StoryText, UITextMesh> LoreTextRef =
            AccessTools.FieldRefAccess<StoryText, UITextMesh>("_loreText");

        private static readonly AccessTools.FieldRef<StoryText, Coroutine> TypeRoutineRef =
            AccessTools.FieldRefAccess<StoryText, Coroutine>("_typeRoutine");

        private static readonly MethodInfo AbortCurrentStateMethod =
            AccessTools.Method(typeof(StoryText), "AbortCurrentState");

        private readonly StoryText _storyText;

        public StoryTextAdapter(StoryText storyText)
        {
            _storyText = storyText;
        }

        public object SourceKey
        {
            get { return _storyText; }
        }

        public string Title
        {
            get { return IsTitleVisible() ? SpeechTextSanitizer.Normalize(GetText(TitleTextRef)) : string.Empty; }
        }

        public string Body
        {
            get { return SpeechTextSanitizer.Normalize(GetText(LoreTextRef)); }
        }

        public bool IsPresent()
        {
            if (_storyText == null || LoreAsyncRef(_storyText) == null)
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

        private bool IsTitleVisible()
        {
            if (_storyText == null || HeaderCanvasGroupRef == null)
            {
                return false;
            }

            CanvasGroup header = HeaderCanvasGroupRef(_storyText);
            Component component = header as Component;
            return header != null
                && header.alpha > 0.001f
                && component != null
                && component.gameObject != null
                && component.gameObject.activeInHierarchy;
        }

        private string GetText(AccessTools.FieldRef<StoryText, UITextMesh> textRef)
        {
            if (_storyText == null || textRef == null)
            {
                return string.Empty;
            }

            return UITextMeshTextUtility.GetEffectiveText(textRef(_storyText));
        }

    }
}
