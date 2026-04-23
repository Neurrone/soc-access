using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal interface IMenuButtonAdapter
    {
        string Id { get; }

        UIButton Button { get; }

        string GetLabel();

        string GetStatus();

        bool IsVisible();

        void Focus();

        bool Activate();
    }
}
