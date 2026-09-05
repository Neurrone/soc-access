using Lavapotion.Sound;

namespace SongsOfConquestAccess.Adapters
{
    public static class NativeSoundUtility
    {
        public static void PostEvent(string eventKey)
        {
            if (string.IsNullOrWhiteSpace(eventKey))
            {
                return;
            }

            try
            {
                ISoundManager soundManager = SoundManager.Instance;
                if (soundManager == null || !soundManager.IsInitialized)
                {
                    return;
                }

                soundManager.PostEvent(eventKey);
            }
            catch (System.Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to post sound event " + eventKey + ": " + exception.Message);
            }
        }
    }
}
