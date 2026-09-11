using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Lobby;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Adapters
{
    public static class LobbyMapPreviewText
    {
        private static readonly FieldInfo MapNameHeaderField =
            AccessTools.Field(typeof(LobbyMapPreview), "_mapNameHeader");
        private static readonly FieldInfo MapInfoField =
            AccessTools.Field(typeof(LobbyMapPreview), "_mpInfo");

        public static string GetTitle(LobbyMapPreview preview)
        {
            return SpokenLines.Clean(GetText(preview, MapNameHeaderField));
        }

        /// <summary>What the panel draws under the name, one drawn line at a time - the paragraphs
        /// the game wrote, kept apart. <see cref="SpokenLines.Clean"/> is the whole of it: it splits
        /// on the newlines and the &lt;br&gt;s first and strips each line's tags, and every caller
        /// runs the result through <see cref="SpokenLines.Of"/> again to make its buffer lines, so a
        /// blank line kept between paragraphs here would be dropped there anyway.</summary>
        public static string GetInfo(LobbyMapPreview preview)
        {
            return SpokenLines.Clean(GetText(preview, MapInfoField));
        }

        private static string GetText(LobbyMapPreview preview, FieldInfo field)
        {
            UITextMesh textMesh = preview != null && field != null ? field.GetValue(preview) as UITextMesh : null;
            return UITextMeshTextUtility.GetEffectiveText(textMesh);
        }
    }
}
