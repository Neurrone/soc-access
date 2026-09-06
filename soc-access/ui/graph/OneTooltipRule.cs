using System.Collections.Generic;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// How many DIFFERENT hover surfaces a node declared - the count behind the builder's refusal.
    ///
    /// A game draws one tooltip at a time (this one keeps a single tooltip window and a single
    /// controller slot), so a node can only ever RAISE the one tooltip it aims at. Words declared off
    /// any other tooltip on the same node are a promise nothing can keep: the buffer says the player
    /// may read them and no gesture the node offers will ever make the game draw them. The standing
    /// ruling is that a second hover surface becomes a CHILD ENTRY of its own - a node the player
    /// steps onto, which aims at that surface - or it is nothing at all.
    ///
    /// So the count is of SOURCES, not of sections. One tooltip may legitimately split into several
    /// sections: a hint-blocked button's tooltip speaks its description and buffers the mouse
    /// instruction it ends in, which is two sections and one hover surface. Two sections off two
    /// tooltips is the shape that cannot be kept.
    ///
    /// A section with no named source is never counted, so the rule under-reports rather than
    /// refusing an honest node - the direction a build-time refusal has to err in.
    /// </summary>
    public static class OneTooltipRule
    {
        /// <summary>True when these sections name more than one tooltip.</summary>
        public static bool Breached(IList<NodeSection> sections)
        {
            return Sources(sections) > 1;
        }

        /// <summary>How many distinct tooltips these sections were derived from. Identity is reference
        /// identity: two sections built from the same tooltip object are one surface, and anything this
        /// layer cannot tell apart it counts apart.</summary>
        public static int Sources(IList<NodeSection> sections)
        {
            int count = 0;
            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                object source = sections[i] == null ? null : sections[i].Source;
                if (source == null || Seen(sections, i, source))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        // Quadratic on purpose: a node has a handful of sections, and a set would allocate on every
        // declared node of every frame.
        private static bool Seen(IList<NodeSection> sections, int upTo, object source)
        {
            for (int i = 0; i < upTo; i++)
            {
                if (sections[i] != null && ReferenceEquals(sections[i].Source, source))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
