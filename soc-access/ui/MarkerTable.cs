using System.Collections.Generic;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The per-owner objects a node id is minted from. <c>ControlId.For(marker, key)</c> wants a
    /// reference that is the same one next frame and a different one for every other node, and a
    /// bare string will not do: two screens using the key "close" would mint the same id.
    ///
    /// The table is held by ONE owner - a screen, or a node group instance - as a readonly field, so
    /// the object handed back for a key is that owner's own and lives exactly as long as it does.
    /// </summary>
    public sealed class MarkerTable
    {
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        /// <summary>This owner's marker for that key, minted on first ask.</summary>
        public object For(string key)
        {
            object marker;
            if (!_markers.TryGetValue(key, out marker))
            {
                marker = new object();
                _markers.Add(key, marker);
            }

            return marker;
        }
    }
}
