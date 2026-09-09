using System;
using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// One component sweep of a menu subtree, held for the length of ONE frame.
    ///
    /// The accessible tree is immediate-mode: every screen rebuilds itself from live state every
    /// frame, which is what makes a stale cursor impossible. The cost that buys is paid in scene
    /// walks - <c>GetComponentsInChildren</c> from a panel root is O(subtree) - and the same subtree
    /// is walked several times in one frame, because the question is asked once by the screen's own
    /// "is this page still mine", once by its build, and again by whatever a row's text resolves to.
    /// Nobody is walking it twice for a different answer: within one frame the game has not moved.
    ///
    /// So the sweep is remembered by (root, frame) and nothing else. Not for the life of the menu:
    /// most of these roots are POOLED containers that add and retire entries between frames, and a
    /// cache that outlived the frame would answer for entries that have gone. Keying on the frame
    /// number rather than invalidating on an event is what makes that safe with nothing to remember
    /// to clear - the first call of the next frame drops the whole table, and a hook that never
    /// arrives cannot leave it wrong (AGENTS.md, "Screen Resolution").
    ///
    /// The last frame's entries are held until then, which is the one reference this keeps to game
    /// objects. There is no teardown step for it and none is needed: the cache lives in the mod
    /// assembly beside the game types it describes, so it dies when the assembly is replaced.
    ///
    /// One instance per (component kind, subject) - the subject is what a failed walk is logged as.
    /// </summary>
    public sealed class FrameSweep<T>
        where T : Component
    {
        private static readonly T[] None = new T[0];

        private readonly string _subject;

        private readonly bool _inactiveToo;

        private readonly Dictionary<Component, T[]> _found = new Dictionary<Component, T[]>();

        private int _frame = -1;

        /// <param name="subject">What a walk that threw is logged as.</param>
        public FrameSweep(string subject)
            : this(subject, true) { }

        /// <param name="subject">What a walk that threw is logged as.</param>
        /// <param name="inactiveToo">The <c>includeInactive</c> the walk is made with. It is part of
        /// the ANSWER, not a detail: a caller that matched only the components the game has switched
        /// on would start matching switched-off ones if this were changed under it, so each sweep
        /// says which question it is asking and one sweep never serves both.</param>
        public FrameSweep(string subject, bool inactiveToo)
        {
            _subject = subject;
            _inactiveToo = inactiveToo;
        }

        /// <summary>Every <typeparamref name="T"/> under <paramref name="root"/> - the same answer
        /// <c>GetComponentsInChildren</c> gives for this sweep's <c>includeInactive</c>, walked at
        /// most once per root per frame. Never null: a missing root and a walk that threw both answer
        /// empty, so a caller reads the same "nothing there" it read before.</summary>
        public T[] Under(Component root)
        {
            try
            {
                int frame = Time.frameCount;
                if (_frame != frame)
                {
                    _found.Clear();
                    _frame = frame;
                }

                if (root == null)
                {
                    return None;
                }

                T[] hit;
                if (_found.TryGetValue(root, out hit))
                {
                    return hit;
                }

                hit = root.GetComponentsInChildren<T>(_inactiveToo);
                _found[root] = hit;
                return hit;
            }
            catch (Exception e)
            {
                SocAccessMod.Instance?.LogWarning(
                    _subject + ": sweeping for " + typeof(T).Name + " threw: " + e);
                return None;
            }
        }

        /// <summary>Forget this frame's answers. For the one thing a frame is not fine enough for:
        /// the mod driving the menu's OWN click, after which the game re-pools the subtree in the
        /// frame the build has already read it in, so a read after the click has to walk again.
        /// </summary>
        public void Invalidate()
        {
            _found.Clear();
        }
    }
}
