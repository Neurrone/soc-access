using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// One run of nodes and the list they were built from - what
    /// <see cref="ArtifactSlotNodes.Column"/> is for a wielder's slots, for any band whose adapter
    /// already answers with the same list while the game has not moved.
    ///
    /// A node is a vtable and a handful of closures that read the game when they are READ, so
    /// rebuilding a band whose rows have not changed buys nothing but the allocation. The adapter's
    /// list identity is the key, because the adapter is the one that knows when the game moved; a
    /// new adapter (a new menu instance, a hot reload) hands back a list this has never seen and
    /// misses.
    ///
    /// It is held by the SCREEN, as a readonly field, because adapters may hold no graph concepts.
    /// </summary>
    public sealed class NodeMemo
    {
        private object _source;

        private List<NodeDeclaration> _nodes;

        /// <summary>The nodes built for exactly this list, or null where it has changed since.
        /// </summary>
        public List<NodeDeclaration> For(object source)
        {
            return _nodes != null && ReferenceEquals(_source, source) ? _nodes : null;
        }

        /// <summary>Hold these nodes for that list, and answer with them.</summary>
        public List<NodeDeclaration> Keep(object source, List<NodeDeclaration> nodes)
        {
            _source = source;
            _nodes = nodes;
            return nodes;
        }
    }
}
