using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// The BUFFER half of a control's declared content - the other half of
    /// <see cref="TooltipParts"/>, projected from the same <see cref="NodeVtable.Sections"/>.
    ///
    /// Three parts, in this order:
    ///
    /// - a HEAD, read off the control's own readout unless the control declared one
    ///   (<see cref="NodeVtable.BufferHead"/>, for a readout that leaves out a word the buffer needs -
    ///   a table cell, whose caption is the crossed edge): its FIRST declared part, then the state
    ///   words the readout appends ("unavailable", "checked", "expanded"). The role word and the
    ///   auto-stamped position are left out - they describe the control, and the buffer is for what the
    ///   control has to say - and so is the tooltip part, whether it announces the text or only says
    ///   there is one, because the tooltip's own lines follow below. The head is why a control that
    ///   declares NO sections still reviews correctly: a lore paragraph declared as nothing but a label
    ///   is reviewable as that paragraph, for free. The part the head was read off is not read again,
    ///   whatever KIND it is: a table cell leads with its value rather than with a label, and testing
    ///   the kind alone had such a cell open its buffer with the same line twice.
    /// - the sections, in declared order, which is drawn order: a row's heading tooltip before its
    ///   value's dossier, a card's drawn output rows before the panel behind it.
    /// - the USAGE HINTS (<see cref="NodeVtable.Hints"/>), one sentence per line, last: what the
    ///   mod's gesture chords do on this control. They are about the keyboard rather than about the
    ///   thing, so they come after everything the control itself has to say.
    ///
    /// Nothing here asks what MODE a section is in. Every section is reviewable - that is what makes
    /// "indicate and review" and "announce and review" the same promise to the player, and it is why
    /// the two surfaces are derived from one declaration rather than wired twice.
    /// </summary>
    public static class NodeBuffer
    {
        public static List<string> Lines(GraphNode node)
        {
            List<string> lines = new List<string>();
            if (node == null || node.Vtable == null)
            {
                return lines;
            }

            string label = Head(node);
            Add(lines, label);

            IList<NodeAnnouncement> declared = node.Vtable.Announcements;
            NodeAnnouncement head =
                declared != null && declared.Count > 0 ? declared[0] : null;
            List<NodeAnnouncement> parts = GraphAnnouncer.EffectiveAnnouncements(node);
            for (int i = 0; i < parts.Count; i++)
            {
                NodeAnnouncement part = parts[i];
                // Only the HEAD part is left out for being the name - not every part of the label's
                // kind. A control that wants something said right after its name gives that part
                // the label's kind (the cost, a card's markings), and skipping the whole kind here
                // was how such words were spoken and yet nowhere to be reviewed (owner ruling
                // 2026-09-03: what is spoken beside the name is a buffer line by construction).
                if (
                    part == null
                    || ReferenceEquals(part, head)
                    || part.Kind == AnnouncementKinds.Role
                    || part.Kind == AnnouncementKinds.Tooltip
                    // The usage hints are a readout part too now, but the buffer's copy of them is
                    // written below, after the sections - where they have always been.
                    || part.Kind == AnnouncementKinds.Hint
                )
                {
                    continue;
                }

                Add(lines, Resolve(part.Text));
            }

            if (
                node.Expandable
                && !node.Vtable.SpeaksOwnExpansion
                && GraphAnnouncer.ExpandedStateText != null
            )
            {
                Add(lines, GraphAnnouncer.ExpandedStateText(node.Expanded));
            }

            IList<NodeSection> sections = node.Vtable.Sections;
            bool first = true;
            for (int s = 0; sections != null && s < sections.Count; s++)
            {
                IList<string> details = Resolve(sections[s]);
                for (int i = 0; i < details.Count; i++)
                {
                    // A tooltip whose first line is just the control's name again: the buffer already
                    // opened with it. Only the FIRST line of the whole list is tested, and only an
                    // exact repeat is dropped, so a heading that adds anything still reads.
                    bool duplicate = first && SpokenText.SameLine(label, details[i]);
                    first = false;
                    if (!duplicate)
                    {
                        Add(lines, details[i]);
                    }
                }
            }

            // The USAGE HINTS, last of all: what the mod's gesture chords do on this control
            // (<see cref="NodeHints"/>). After everything the control has to say, because they are
            // about the KEYBOARD rather than about the thing - a player reviewing the content should
            // reach the content first, and a player who wants the gestures knows they are at the end.
            NodeHints.Lines(lines, node.Vtable);

            // And the CARRY's own two hints, derived from the vtable rather than declared by any
            // screen (<see cref="CarryState.HintLines"/>): what this control would hand over while
            // nothing is held, and where what IS held can be put down. After the hand-picked hints
            // because those are the sentence somebody chose for this one control, and these are the
            // same two sentences every draggable surface in the mod gets for free.
            if (GraphAnnouncer.Carry != null)
            {
                GraphAnnouncer.Carry.HintLines(lines, node.Vtable);
            }

            return lines;
        }

        /// <summary>The line the buffer opens with: what the control declared
        /// (<see cref="NodeVtable.BufferHead"/>), or its readout where it declared nothing. Only the
        /// head LINE is the control's to choose - the state words and the sections that follow are
        /// composed the same way either way, and the part the readout's own head was read off is
        /// still not read twice.</summary>
        private static string Head(GraphNode node)
        {
            string declared = Resolve(node.Vtable.BufferHead);
            return string.IsNullOrEmpty(declared)
                ? GraphAnnouncer.FirstPartText(node)
                : declared;
        }

        private static readonly List<string> None = new List<string>();

        private static IList<string> Resolve(NodeSection section)
        {
            if (section == null || section.Lines == null)
            {
                return None;
            }

            try
            {
                return section.Lines() ?? None;
            }
            catch (Exception)
            {
                return None;
            }
        }

        private static string Resolve(Func<string> text)
        {
            try
            {
                return text == null ? null : text();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
            }
        }
    }
}
