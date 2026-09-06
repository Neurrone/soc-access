using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// One USAGE HINT on a control: a short sentence saying what one of the mod's gesture chords does
    /// HERE - "Ctrl+Shift+Enter to queue it first".
    ///
    /// A hint names an ACTION, never a key. The chord in the sentence is rendered from the mod's own
    /// action table when the buffer is read, so re-binding a gesture re-words every hint that names
    /// it and no screen holds a key name of its own.
    ///
    /// <see cref="BindingIndex"/> is which of that action's chords the sentence means, and it is not
    /// always the first: the map's off-lane move is the SECOND binding of the very same Contextual
    /// action as the ordinary move (Ctrl+Backslash beside Backslash - <c>docs/interaction.md</c>),
    /// because the game runs one handler for both clicks and reads the physical modifier inside it.
    /// A hint that named the action alone could not tell those two apart.
    ///
    /// <see cref="When"/> is the possibility gate - a hint for something this selection cannot
    /// actually do is worse than no hint - and null means "always, wherever the control is
    /// declared".
    /// </summary>
    public sealed class NodeHint
    {
        public NodeHint(
            ModString template,
            string actionKey,
            int bindingIndex = 0,
            Func<bool> when = null,
            string secondActionKey = null,
            int secondBindingIndex = 0
        )
        {
            Template = template;
            ActionKey = actionKey;
            BindingIndex = bindingIndex;
            When = when;
            SecondActionKey = secondActionKey;
            SecondBindingIndex = secondBindingIndex;
        }

        /// <summary>The <see cref="ModStrings"/> key of the whole sentence, whose <c>{0}</c> the
        /// rendered chord fills.</summary>
        public readonly ModString Template;

        /// <summary>The action the chord belongs to, as the input manager knows it
        /// ("ui.alternate").</summary>
        public readonly string ActionKey;

        /// <summary>Which of the action's bindings the sentence means; 0 - the usual case - is the
        /// action's first chord.</summary>
        public readonly int BindingIndex;

        /// <summary>Whether the gesture is really available here, asked when the buffer is read.
        /// Null = always.</summary>
        public readonly Func<bool> When;

        /// <summary>A SECOND chord the sentence names, filling its <c>{1}</c> - null for the ordinary
        /// one-chord hint. Some gestures only exist as a pair and are useless said one at a time: an
        /// adjustable is worked with the key that lowers it and the key that raises it, and a sentence
        /// naming one of them describes half a control. Both are rendered from the live bindings, so
        /// re-binding either re-words the sentence.</summary>
        public readonly string SecondActionKey;

        /// <summary>Which of the second action's bindings the sentence means.</summary>
        public readonly int SecondBindingIndex;
    }

    /// <summary>
    /// The hint surface: how a declared <see cref="NodeHint"/> becomes the last thing said about a
    /// control - the last lines of its review buffer, and the last part of its focus readout
    /// (<c>GraphAnnouncer.EffectiveAnnouncements</c>, which composes the same lines into one part).
    ///
    /// <see cref="Chord"/> is the seam. <c>Core</c> is BCL-only and cannot see the input manager, so
    /// the host injects the renderer once - the <see cref="GraphAnnouncer"/> precedent, and static
    /// for the same reason: every node's buffer flows through it and threading it per call would
    /// touch every node factory. Process state, so <see cref="Reset"/> exists for mod teardown and
    /// for test isolation. Left null (tests, boot, a host with no keyboard) NO hint renders at all,
    /// which is the right answer: a sentence with no chord in it says nothing.
    /// </summary>
    public static class NodeHints
    {
        /// <summary>Render the chord at (action key, binding index) in the player's language, or null
        /// where the action or that binding does not exist. Installed by the host.</summary>
        public static Func<string, int, string> Chord;

        /// <summary>Drop the injected renderer - mod teardown, and test isolation.</summary>
        public static void Reset()
        {
            Chord = null;
        }

        /// <summary>Declare a hint on <paramref name="vtable"/>, creating its list on first use. The
        /// order hints are added in is the order they read.</summary>
        public static void Add(
            NodeVtable vtable,
            ModString template,
            string actionKey,
            int bindingIndex = 0,
            Func<bool> when = null,
            string secondActionKey = null,
            int secondBindingIndex = 0
        )
        {
            if (vtable == null || string.IsNullOrEmpty(template.Text) || string.IsNullOrEmpty(actionKey))
            {
                return;
            }

            if (vtable.Hints == null)
            {
                vtable.Hints = new List<NodeHint>();
            }

            vtable.Hints.Add(
                new NodeHint(
                    template,
                    actionKey,
                    bindingIndex,
                    when,
                    secondActionKey,
                    secondBindingIndex
                )
            );
        }

        /// <summary>Append the control's hint lines - one per line, in declared order - to
        /// <paramref name="into"/>. A hint whose gate says no, or whose chord cannot be rendered,
        /// contributes nothing.</summary>
        public static void Lines(List<string> into, NodeVtable vtable)
        {
            Func<string, int, string> render = Chord;
            IList<NodeHint> hints = vtable != null ? vtable.Hints : null;
            if (render == null || hints == null)
            {
                return;
            }

            for (int i = 0; i < hints.Count; i++)
            {
                NodeHint hint = hints[i];
                if (hint == null)
                {
                    continue;
                }

                try
                {
                    if (hint.When != null && !hint.When())
                    {
                        continue;
                    }

                    string chord = render(hint.ActionKey, hint.BindingIndex);
                    if (string.IsNullOrEmpty(chord))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(hint.SecondActionKey))
                    {
                        into.Add(ModText.Get(hint.Template, chord));
                        continue;
                    }

                    // A pair sentence needs BOTH halves: one chord in a sentence written for two
                    // leaves a hole where the other one goes.
                    string second = render(hint.SecondActionKey, hint.SecondBindingIndex);
                    if (string.IsNullOrEmpty(second))
                    {
                        continue;
                    }

                    into.Add(ModText.Get(hint.Template, chord, second));
                }
                catch (Exception)
                {
                    // A hint is the least important thing in a buffer: a gate that throws costs the
                    // player one sentence, never the content it was appended to.
                }
            }
        }
    }
}
