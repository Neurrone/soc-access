using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The story text the game shows between scenes - the panel, the letterbox and the dialogue line
    /// - made navigable as a graph. One stop holding ONE BUTTON (owner ruling 2026-09-07): the
    /// speaker or heading and the first paragraph as "{speaker}: {text}", the further paragraphs as
    /// parts, so the review buffer holds one line per paragraph. Every part is live: the game writes
    /// the next line of a dialogue in place under a cursor that never moves, and the live watch is
    /// what reads it, speaker first, then the text. The headerless variants read the body alone.
    ///
    /// The paragraphs survive because the adapters keep the game's own line breaks
    /// (<c>IStoryTextAdapter.BodyLines</c>).
    ///
    /// ENTER advances, through the game's own handler that a click would reach
    /// (<c>AbortCurrentState</c> on the two story texts, <c>HandlePrimaryClicked</c> on the dialogue
    /// menu). ESCAPE is the game's on all three sources: each binds it to advancing, and every key
    /// the navigator does not claim reaches the game's press-anything handling unchanged.
    /// </summary>
    public sealed class StoryTextScreen : GraphScreen
    {
        private const string StoryStop = "story-text";

        private static readonly PropertyInfo DialogueInstallerContainerProperty =
            AccessTools.Property(typeof(DialogueMenuInstaller), "Container");

        private readonly IStoryTextAdapter _adapter;

        // A subject of its own for the node: the sources draw the text in meshes the adapters do not
        // hand out.
        private readonly object _bodyKey = new object();

        public StoryTextScreen(IStoryTextAdapter adapter)
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

        public override string Key
        {
            get { return "story-text"; }
        }

        /// <summary>None: the one node says the speaker or heading itself.</summary>
        public override string ScreenName
        {
            get { return null; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(StoryStop);

            // ONE BUTTON: "{speaker}: {first paragraph}" then the further paragraphs as parts, every
            // part live, so the next line the game writes in place is read by the live watch under a
            // cursor that never moved. Enter advances (owner ruling 2026-09-07).
            if (!string.IsNullOrWhiteSpace(_adapter.Body))
            {
                ControlId bodyId = ControlId.For(_bodyKey, "story-text:body");
                NodeVtable body = GraphNodes.Paragraphs(Lines, live: true);
                body.ControlType = ControlTypes.Button;
                body.OnActivate = Advance;
                builder.AddItem(new SyntheticNode(bodyId, body));
                builder.SetStart(bodyId);
            }
        }

        /// <summary>The body's paragraphs, the first carrying the speaker or heading where the source
        /// draws one ("Cecilia Stoutheart: Peradine, I came as quickly as I could.").</summary>
        private IList<string> Lines()
        {
            IList<string> lines = _adapter != null ? _adapter.BodyLines : null;
            string title = _adapter != null ? _adapter.Title : null;
            if (lines == null || lines.Count == 0 || string.IsNullOrWhiteSpace(title))
            {
                return lines;
            }

            List<string> named = new List<string>(lines);
            named[0] = ModText.Get(ModStrings.UI.LabelValue, title, lines[0]);
            return named;
        }

        private void Advance()
        {
            if (_adapter != null)
            {
                _adapter.AdvanceNow();
            }
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
