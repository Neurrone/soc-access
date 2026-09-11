using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// ONE SECTION'S ITEMS, or none where reading them threw: a part of the menu the game has stopped
    /// answering for costs its own rows and never the rest of the page.
    ///
    /// A build runs every frame, so a section that goes on throwing would write a line a frame into
    /// the log and bury everything else in it. Each section is therefore reported ONCE, the way an
    /// unrecognized tooltip instruction is (<c>AdventureMapAdapter.ClassifyMapInstruction</c>), and
    /// the exception is in that one line.
    ///
    /// A screen holds one as a readonly field: what has already been reported is mod-owned state that
    /// outlives any one menu instance, and there is no reset hook for it to want.
    /// </summary>
    public sealed class SectionItems
    {
        private readonly string _owner;

        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);

        public SectionItems(string owner)
        {
            _owner = owner;
        }

        /// <summary>The section's items, never null.</summary>
        public IReadOnlyList<T> Of<T>(string section, Func<IReadOnlyList<T>> getter)
        {
            try
            {
                IReadOnlyList<T> items = getter != null ? getter() : null;
                return items ?? new T[0];
            }
            catch (Exception exception)
            {
                if (_reported.Add(section))
                {
                    SocAccessMod.Instance?.LogWarning(
                        _owner + " section " + section + " failed to build: " + exception);
                }

                return new T[0];
            }
        }
    }
}
