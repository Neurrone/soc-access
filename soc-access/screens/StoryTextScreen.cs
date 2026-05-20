using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class StoryTextScreen : Screen
    {
        private static readonly PropertyInfo DialogueInstallerContainerProperty =
            AccessTools.Property(typeof(DialogueMenuInstaller), "Container");

        private readonly IStoryTextAdapter _adapter;

        public StoryTextScreen(IStoryTextAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveLetterboxScreen()
        {
            LetterboxStoryTextAdapter adapter = FindActiveLetterboxStoryText();
            return adapter != null ? new StoryTextScreen(adapter) : null;
        }

        public static Screen TryBuildActiveScreen()
        {
            StoryTextAdapter adapter = FindActiveStoryText();
            return adapter != null ? new StoryTextScreen(adapter) : null;
        }

        public static Screen TryBuildActiveDialogueScreen()
        {
            DialogueMenuAdapter adapter = FindActiveDialogueMenu();
            return adapter != null ? new StoryTextScreen(adapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.AdvanceNow();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(IStoryTextAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("story-text", string.Empty);

            root.AddChild(new TextWidget(
                "story-text",
                () => BuildStoryText(adapter),
                null,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new ButtonWidget(
                "next",
                ModText.Get(ModStrings.Screens.Next),
                () => adapter != null && adapter.AdvanceNow(),
                null,
                () => adapter != null && adapter.IsPresent()));

            return root;
        }

        private static string BuildStoryText(IStoryTextAdapter adapter)
        {
            if (adapter == null)
            {
                return string.Empty;
            }

            string title = adapter.Title;
            string body = adapter.Body;
            if (string.IsNullOrWhiteSpace(title))
            {
                return body;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return title;
            }

            return title + "\n" + body;
        }

        private static LetterboxStoryTextAdapter FindActiveLetterboxStoryText()
        {
            LetterboxStoryText[] storyTexts = Resources.FindObjectsOfTypeAll<LetterboxStoryText>();
            for (int i = 0; i < storyTexts.Length; i++)
            {
                LetterboxStoryText storyText = storyTexts[i];
                if (!IsLiveSceneStoryText(storyText))
                {
                    continue;
                }

                LetterboxStoryTextAdapter adapter = new LetterboxStoryTextAdapter(storyText);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static StoryTextAdapter FindActiveStoryText()
        {
            StoryText[] storyTexts = Resources.FindObjectsOfTypeAll<StoryText>();
            for (int i = 0; i < storyTexts.Length; i++)
            {
                StoryText storyText = storyTexts[i];
                if (!IsLiveSceneStoryText(storyText))
                {
                    continue;
                }

                StoryTextAdapter adapter = new StoryTextAdapter(storyText);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static DialogueMenuAdapter FindActiveDialogueMenu()
        {
            DialogueMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<DialogueMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                DialogueMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                DiContainer container = GetContainer(installer);
                DialogueMenu dialogueMenu = TryResolve<DialogueMenu>(container);
                DialogueMenuAdapter adapter = new DialogueMenuAdapter(dialogueMenu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneStoryText(LetterboxStoryText storyText)
        {
            if (storyText == null)
            {
                return false;
            }

            GameObject gameObject = storyText.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveSceneStoryText(StoryText storyText)
        {
            if (storyText == null)
            {
                return false;
            }

            GameObject gameObject = storyText.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveSceneInstaller(DialogueMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static DiContainer GetContainer(DialogueMenuInstaller installer)
        {
            if (installer == null || DialogueInstallerContainerProperty == null)
            {
                return null;
            }

            return DialogueInstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }

        private static T TryResolve<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
