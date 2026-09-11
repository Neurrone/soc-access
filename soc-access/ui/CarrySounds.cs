using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The sounds the GAME plays around a drag, played around the keyboard's carry.
    ///
    /// The keyboard's carry IS the game's mouse drag, so it makes the game's own noises and no
    /// others: nothing here invents a cue the pointer does not get, and nothing here knows a sound
    /// id. A screen that has measured what its drag sounds like registers the two calls for its own
    /// cargo kind (<see cref="CarryItem.Kind"/>) when it is built; cargo nobody registered for is
    /// carried in silence, which is what a drag the game itself makes no noise for should sound like.
    ///
    /// Hung on <see cref="CarryState.Started"/> and <see cref="CarryState.Ended"/> by the navigator's
    /// constructor, and emptied by <c>GraphNavigator.ResetWiring</c> on Stop: the registrations are
    /// delegates over this assembly and a screen's game objects, so they must not outlive the load.
    /// </summary>
    public static class CarrySounds
    {
        private sealed class Cues
        {
            public Action Started;
            public Action Ended;
        }

        private static readonly Dictionary<string, Cues> ByKind = new Dictionary<string, Cues>();

        /// <summary>Declare what a carry of <paramref name="kind"/> sounds like. Either half may be
        /// null where the game plays nothing at that point; registering again replaces what was
        /// there, so a screen may do this every time it is built.</summary>
        public static void Register(string kind, Action started, Action ended)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return;
            }

            if (started == null && ended == null)
            {
                ByKind.Remove(kind);
                return;
            }

            ByKind[kind] = new Cues { Started = started, Ended = ended };
        }

        /// <summary>Whether a carry of <paramref name="kind"/> already has its cues. A screen asks
        /// before registering, so the two delegates and the entry are made once per load instead of
        /// once per build, and a <see cref="Reset"/> is still healed by the next build that asks.
        /// </summary>
        public static bool Has(string kind)
        {
            return !string.IsNullOrEmpty(kind) && ByKind.ContainsKey(kind);
        }

        /// <summary>Forget every registration - mod teardown, and test isolation.</summary>
        public static void Reset()
        {
            ByKind.Clear();
        }

        /// <summary>Something has just been picked up.</summary>
        public static void Started(CarryItem item)
        {
            Cues cues = Find(item);
            Play(cues == null ? null : cues.Started);
        }

        /// <summary>A carry has just ended: dropped, refused, or given up. All three are one drag
        /// ending, which is the game's own reading of them.</summary>
        public static void Ended(CarryItem item)
        {
            Cues cues = Find(item);
            Play(cues == null ? null : cues.Ended);
        }

        private static Cues Find(CarryItem item)
        {
            Cues cues;
            return item != null && item.Kind != null && ByKind.TryGetValue(item.Kind, out cues)
                ? cues
                : null;
        }

        private static void Play(Action cue)
        {
            if (cue == null)
            {
                return;
            }

            try
            {
                cue();
            }
            catch (Exception e)
            {
                SocAccessMod.Instance?.LogWarning("carry: playing the game's drag sound threw: " + e);
            }
        }
    }
}
