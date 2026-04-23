using System.Collections.Generic;

namespace SongsOfConquestAccess.Screens
{
    internal interface IRuntimeScreenProbe
    {
        void AddActiveScreens(List<Screen> screens);
    }
}
