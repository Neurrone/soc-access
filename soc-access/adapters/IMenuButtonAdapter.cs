using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    public interface IMenuButtonAdapter
    {
        UIButton Button { get; }

        string GetLabel();

        string GetStatus();

        bool IsVisible();

        bool IsEnabled();

        bool Activate();
    }
}
