using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The story text the game shows between scenes - the panel, the letterbox and the dialogue line
    /// - made navigable as a graph. One stop: the heading, which the game draws in capitals and which
    /// is also the screen name, and the body as the start node, whose sections are one line per
    /// paragraph. The headerless variants declare no heading node and have no screen name: the body
    /// is all there is to say.
    ///
    /// The paragraphs survive because the adapters keep the game's own line breaks
    /// (<c>IStoryTextAdapter.BodyLines</c>): the label is the whole body read as one line and the
    /// sections are its paragraphs, so the review buffer holds the text the way it is written.
    ///
    /// ENTER on either node advances, through the game's own handler that a click would reach
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

        // A subject of its own for each node: the sources draw the heading and the body in text
        // meshes the adapters do not hand out, and two nodes sharing one subject would collapse onto
        // whichever was declared first.
        private readonly object _headingKey = new object();
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

        /// <summary>The heading the source draws, or null where it draws none.</summary>
        public override string ScreenName
        {
            get
            {
                string title = _adapter != null ? _adapter.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
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

            if (!string.IsNullOrWhiteSpace(_adapter.Title))
            {
                NodeVtable heading = GraphNodes.Text(() => _adapter.Title);
                heading.OnActivate = Advance;
                builder.AddItem(new SyntheticNode(ControlId.For(_headingKey, "story-text:heading"), heading));
            }

            if (!string.IsNullOrWhiteSpace(_adapter.Body))
            {
                ControlId bodyId = ControlId.For(_bodyKey, "story-text:body");
                NodeVtable body = GraphNodes.Text(() => _adapter.Body, BodyLines);
                body.OnActivate = Advance;
                builder.AddItem(new SyntheticNode(bodyId, body));
                // Focus starts on the body, so arrival says the heading once as the screen name and
                // then the story itself.
                builder.SetStart(bodyId);
            }
        }

        private void Advance()
        {
            if (_adapter != null)
            {
                _adapter.AdvanceNow();
            }
        }

        /// <summary>The body's paragraphs as a buffer section, and only where there is more than one
        /// of them: the announcement part is a buffer line already, so a section repeating a
        /// single-paragraph body would put it in the buffer twice.</summary>
        private IList<string> BodyLines()
        {
            IList<string> lines = _adapter != null ? _adapter.BodyLines : null;
            return lines != null && lines.Count > 1 ? lines : null;
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
