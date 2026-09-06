using System.Runtime.InteropServices;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// The physical state of a key, asked of the operating system rather than of Unity's input
    /// system.
    ///
    /// The mod claims a key by marking its input-system event handled, and a handled event is
    /// skipped by the input system without updating the keyboard's state: after the mod has
    /// claimed an Enter press, <c>Keyboard.current.enterKey.isPressed</c> reads false for as long
    /// as the finger stays down. The one thing that must wait for the RELEASE of that Enter is the
    /// text-field handover (a field given the keyboard while Enter is still down acts on it), and
    /// it cannot read the release from a state the mod itself kept from being written. Windows
    /// knows; the game runs only there.
    /// </summary>
    public static class OsKeys
    {
        private const int VkReturn = 0x0D;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        /// <summary>Whether either Enter key is physically down right now.</summary>
        public static bool EnterIsDown()
        {
            try
            {
                return (GetAsyncKeyState(VkReturn) & 0x8000) != 0;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
