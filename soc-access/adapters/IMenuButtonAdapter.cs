using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal interface IMenuButtonAdapter
    {
        UIButton Button { get; }

        string GetLabel();

        string GetStatus();

        bool IsVisible();

        bool IsEnabled();

        bool Activate();
    }
}
