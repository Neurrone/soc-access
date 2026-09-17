using System;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// The identity of a control (graph node) — a two-tier identity so focus can be followed across
    /// rebuilds even when the world shifts under us. Ported from Tanglebeep (with permission), which
    /// upgraded Factorio Access's plain string node key.
    ///
    /// <para><b>Subject</b> (optional) is the model object or widget this node is ABOUT - identity's
    /// anchor, compared by reference identity. <b>StructuralKey</b> (always present) is a
    /// value-equatable key — a string, or a composite such as a (region, row, col) key.</para>
    ///
    /// <para>Two controls are "the same" when their subjects are identical (tier 1 — a perfect match
    /// that follows an object that MOVED, its structural key changing) OR their structural keys are equal
    /// (tier 2 — follows a logical control whose backing object was rebuilt: new instance, same identity).</para>
    ///
    /// <para>Equality/hashing is defined on <see cref="StructuralKey"/> alone, so it is a stable
    /// dictionary key (the graph stores nodes and traversal order by it). The subject tier is metadata,
    /// applied explicitly during focus reconciliation via <see cref="SubjectMatches"/>.</para>
    ///
    /// <para>The subject says WHAT this node is about; it says nothing about whether the game is still
    /// drawing it. That second question is a node's NATURE (<see cref="NodeDeclaration"/>) and is never
    /// inferred from here: a subject that happens to be a widget is still only an identity.</para>
    /// </summary>
    public sealed class ControlId : IEquatable<ControlId>
    {
        /// <summary>The model object or widget this node is about; identity's anchor. Null where the
        /// node is keyed structurally alone. Matched by reference identity.</summary>
        public object Subject { get; private set; }

        /// <summary>The value-equatable structural identity. Never null.</summary>
        public object StructuralKey { get; private set; }

        // The structural key is a string most of the time, and hashing it is what every dictionary
        // the render keeps costs per lookup: a 430-node table wires 1500 edges, each looked up by both
        // ends, and rehashing a thirty-character key every time was the largest single cost of a
        // build (measured on the conquest map list 2026-09-18). Hashed once, here.
        private readonly int _hash;

        private ControlId(object subject, object structuralKey)
        {
            if (structuralKey == null) throw new ArgumentNullException("structuralKey");
            Subject = subject;
            StructuralKey = structuralKey;
            _hash = structuralKey.GetHashCode();
        }

        /// <summary>A control identified only by a structural key (no subject).</summary>
        public static ControlId Structural(object structuralKey)
        {
            return new ControlId(null, structuralKey);
        }

        /// <summary>A control with both tiers: the subject it is about, and a structural key.</summary>
        public static ControlId For(object subject, object structuralKey)
        {
            return new ControlId(subject, structuralKey);
        }

        /// <summary>A control identified by its subject only — the object doubles as the structural
        /// key (equality collapses to identity). For wrapping a raw widget with no better key.</summary>
        public static ControlId ForObject(object subject)
        {
            if (subject == null) throw new ArgumentNullException("subject");
            return new ControlId(subject, subject);
        }

        /// <summary>Tier-1 test: is <paramref name="obj"/> this control's subject?</summary>
        public bool SubjectMatches(object obj)
        {
            return Subject != null && ReferenceEquals(Subject, obj);
        }

        public bool Equals(ControlId other)
        {
            return other != null && _hash == other._hash && Equals(StructuralKey, other.StructuralKey);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ControlId);
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        public override string ToString()
        {
            // The wording is a documented surface (duplicate-id reports, dumps): left exactly as it was.
            return Subject == null
                ? "ControlId(" + StructuralKey + ")"
                : "ControlId(" + StructuralKey + ", ref=" + Subject + ")";
        }
    }
}
