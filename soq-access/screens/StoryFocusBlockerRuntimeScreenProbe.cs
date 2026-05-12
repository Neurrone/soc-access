using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class StoryFocusBlockerRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private readonly Func<bool> _isStorySequenceActive;

        public StoryFocusBlockerRuntimeScreenProbe(Func<bool> isStorySequenceActive)
        {
            _isStorySequenceActive = isStorySequenceActive;
        }

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null || _isStorySequenceActive == null || !_isStorySequenceActive())
            {
                return;
            }

            screens.Add(new StoryFocusBlockerScreen(_isStorySequenceActive));
        }
    }
}
