using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using TMPro;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The words the community-maps browser draws, read the same way by every panel of it:
    /// mod.io labels its own controls through one translation table and writes the browser's heading
    /// and its two counts into the nav bar the panels share.</summary>
    public static class CommunityMapsText
    {
        /// <summary>What a browser label is drawing, empty when the label is not drawn.</summary>
        public static string Of(TMP_Text text)
        {
            return text != null && text.gameObject.activeInHierarchy
                ? SpokenLines.Clean(text.text)
                : string.Empty;
        }

        /// <summary>mod.io's own word for one of its keys, and the key itself where the browser has
        /// no table loaded, which is what the browser falls back to as well.</summary>
        public static string Translate(string key)
        {
            TranslationManager manager = CommunityMapsSources.Translations;
            if (manager == null || string.IsNullOrWhiteSpace(key))
            {
                return key ?? string.Empty;
            }

            return SpokenLines.Clean(manager.Get(key));
        }

        /// <summary>The nav bar label drawn under one named parent - the browser's title, the
        /// subscription count, the storage figure - empty where the bar is not drawing it.</summary>
        public static string FindTopBar(string transformName)
        {
            if (string.IsNullOrWhiteSpace(transformName))
            {
                return string.Empty;
            }

            NavBar navBar = CommunityMapsSources.NavBar;
            TMP_Text[] texts = navBar != null ? navBar.GetComponentsInChildren<TMP_Text>(false) : null;
            if (texts == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || text.transform.parent == null || text.transform.parent.name != transformName)
                {
                    continue;
                }

                string value = Of(text);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }
}
