using SongsOfConquest.Common.Map;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The name one battlefield layout goes by, "&lt;LevelType&gt;/&lt;PathName&gt;" - the key the
    /// authored descriptions are written under and the key the built table holds
    /// (<c>build-battlefields.ps1</c>, <see cref="Battlefields.BattlefieldDescriptions"/>).
    ///
    /// <c>Metadata.PathName</c>, never <c>Metadata.Name</c>: the name is unreliable, and the path is
    /// what the level provider loaded the map by.
    /// </summary>
    public static class BattlefieldKeys
    {
        public static string For(MapFormat map)
        {
            return map == null || string.IsNullOrEmpty(map.Metadata.PathName)
                ? null
                : map.Metadata.Type + "/" + map.Metadata.PathName;
        }
    }
}
