using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// Keys pressed the way a hand presses them: real OS key events, posted with <c>SendInput</c>.
    ///
    /// WHY THIS EXISTS. <c>POST /input</c> runs an ACTION - it enters the mod's dispatch below the
    /// keyboard, so no key is ever physically down while it runs. Everything that branches on a key
    /// being down is therefore invisible to it: the mod's own raw <c>InputSystem.onEvent</c>
    /// subscription and its release debounce, the game's input manager reading the same key,
    /// <c>Keyboard.current[key].isPressed</c>, and the "was this Return still down when the focus
    /// left" question a text commit is decided by. Those are exactly the seams a text box lives at,
    /// and until this route they could only be tested by hand - which is how, on Endless Space 2, a
    /// rename box that committed on the FIRST Enter shipped twice.
    ///
    /// SAFETY. <c>SendInput</c> posts to whatever window has the foreground, so a route that fired
    /// blind would type into the owner's own desktop. Nothing is sent until the foreground window is
    /// PROVED to belong to this process: the game is asked to raise itself, given a moment, and asked
    /// again - and a refusal is an error, never a silent no-op.
    ///
    /// Keyboard only. A mouse would need the pointer moved off whatever the player left it on, and
    /// nothing in this mod is driven by clicks.
    /// </summary>
    internal static class RawKeyboard
    {
        /// <summary>How long a tapped key stays down, and how long the next one waits - a few frames
        /// each at 60fps, which is what makes a press look like a press to code that counts frames.
        /// </summary>
        public const int DefaultHoldMilliseconds = 60;

        public const int DefaultGapMilliseconds = 60;

        /// <summary>One step of a sequence: a key, and whether the step presses it, releases it, or
        /// both.</summary>
        private struct Step
        {
            public KeyCode Key;
            public bool Down;
            public bool Up;
            public string Text;
        }

        /// <summary>What the route reports back: the steps that were sent, in the words they were
        /// asked for.</summary>
        public sealed class Result
        {
            public bool Ok;
            public string Error;

            /// <summary>True when the refusal is about WHO HAS THE FOREGROUND rather than about what
            /// was asked for - the caller can fix that one by clicking the game window.</summary>
            public bool Refused;
            public readonly List<string> Sent = new List<string>();
        }

        /// <summary>
        /// Send a whitespace-separated sequence of key steps.
        ///
        /// A bare name (<c>Return</c>, <c>A</c>, <c>Escape</c>) is a tap. <c>+Name</c> presses and
        /// HOLDS; <c>-Name</c> releases. Modifiers are written as part of the step
        /// (<c>Ctrl+I</c>, <c>Shift+Tab</c>), and are held down around the key and released after it,
        /// which is what the game's own combination matcher reads.
        /// </summary>
        public static Result Send(string body, int holdMilliseconds, int gapMilliseconds)
        {
            Result result = new Result();
            List<Step> steps = new List<Step>();
            string parsed = Parse(body, steps);
            if (parsed != null)
            {
                result.Error = parsed;
                return result;
            }

            if (steps.Count == 0)
            {
                result.Error = "the body names no key";
                return result;
            }

            string foreground = TakeTheForeground();
            if (foreground != null)
            {
                result.Error = foreground;
                result.Refused = true;
                return result;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps[i];
                // A step that neither presses nor releases sends nothing, so it is not reported as
                // sent: a hold-only token (+Ctrl+I) and a release-only one (-Ctrl+I) each give their
                // modifier one such step, and reporting them had the answer claiming key events the
                // game never saw.
                if (!step.Down && !step.Up)
                {
                    continue;
                }

                // Re-checked before every step, not once for the batch: a modal the game raises
                // mid-sequence could take the foreground with it, and the rest of the sequence would
                // land somewhere else entirely.
                string still = ForegroundIsOurs();
                if (still != null)
                {
                    result.Error = still;
                    result.Refused = true;
                    return result;
                }

                Press(step, holdMilliseconds);
                result.Sent.Add(step.Text);
                if (i + 1 < steps.Count)
                {
                    Thread.Sleep(gapMilliseconds);
                }
            }

            result.Ok = true;
            return result;
        }

        /// <summary>Type characters as the keyboard would produce them - each one resolved to the key
        /// and shift state that makes it on the current layout, so the game's own text field receives
        /// them through its ordinary path.</summary>
        public static Result Type(string text, int gapMilliseconds)
        {
            Result result = new Result();
            if (string.IsNullOrEmpty(text))
            {
                result.Error = "the body is the characters to type";
                return result;
            }

            string foreground = TakeTheForeground();
            if (foreground != null)
            {
                result.Error = foreground;
                result.Refused = true;
                return result;
            }

            for (int i = 0; i < text.Length; i++)
            {
                string still = ForegroundIsOurs();
                if (still != null)
                {
                    result.Error = still;
                    result.Refused = true;
                    return result;
                }

                short scan = VkKeyScan(text[i]);
                if (scan == -1)
                {
                    result.Error = "this keyboard layout has no key for '" + text[i] + "'";
                    return result;
                }

                ushort vk = (ushort)(scan & 0xFF);
                bool shift = (scan & 0x100) != 0;
                if (shift)
                {
                    Key(VkShift, true, false);
                }

                Key(vk, true, false);
                Thread.Sleep(gapMilliseconds);
                Key(vk, false, false);
                if (shift)
                {
                    Key(VkShift, false, false);
                }

                result.Sent.Add(text[i].ToString());
                Thread.Sleep(gapMilliseconds);
            }

            result.Ok = true;
            return result;
        }

        /// <summary>Every key name this route understands, for the error that rejects one.</summary>
        public static string KnownKeys()
        {
            List<string> names = new List<string>(Map().Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", names.ToArray());
        }

        // ---- the sequence ----

        private static string Parse(string body, List<Step> steps)
        {
            string[] tokens = (body ?? string.Empty).Split(
                new[] { ' ', '\t', '\r', '\n', ',' },
                StringSplitOptions.RemoveEmptyEntries
            );
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                Step step = new Step { Down = true, Up = true, Text = token };
                if (token.Length > 1 && token[0] == '+')
                {
                    step.Up = false;
                    token = token.Substring(1);
                }
                else if (token.Length > 1 && token[0] == '-')
                {
                    step.Down = false;
                    token = token.Substring(1);
                }

                string[] parts = token.Split('+');
                KeyCode key;
                if (!Named(parts[parts.Length - 1], out key))
                {
                    return "no key named '"
                        + parts[parts.Length - 1]
                        + "'; /key understands: "
                        + KnownKeys();
                }

                for (int j = 0; j < parts.Length - 1; j++)
                {
                    KeyCode modifier;
                    if (!Named(parts[j], out modifier) || !IsModifier(modifier))
                    {
                        return "'" + parts[j] + "' is not a modifier (Ctrl, Shift, Alt)";
                    }

                    steps.Add(
                        new Step
                        {
                            Key = modifier,
                            Down = step.Down,
                            Up = false,
                            Text = parts[j] + " down",
                        }
                    );
                }

                step.Key = key;
                steps.Add(step);
                for (int j = parts.Length - 2; j >= 0; j--)
                {
                    KeyCode modifier;
                    Named(parts[j], out modifier);
                    steps.Add(
                        new Step
                        {
                            Key = modifier,
                            Down = false,
                            Up = step.Up,
                            Text = parts[j] + " up",
                        }
                    );
                }
            }

            return null;
        }

        private static bool IsModifier(KeyCode key)
        {
            return key == KeyCode.LeftControl
                || key == KeyCode.RightControl
                || key == KeyCode.LeftShift
                || key == KeyCode.RightShift
                || key == KeyCode.LeftAlt
                || key == KeyCode.RightAlt;
        }

        private static void Press(Step step, int holdMilliseconds)
        {
            ushort vk;
            bool extended;
            Virtual(step.Key, out vk, out extended);
            if (step.Down)
            {
                Key(vk, true, extended);
            }

            if (step.Down && step.Up)
            {
                Thread.Sleep(holdMilliseconds);
            }

            if (step.Up)
            {
                Key(vk, false, extended);
            }
        }

        // ---- who has the foreground ----

        /// <summary>Ask the game's window to come forward, then prove that it did. Null when the
        /// foreground is ours and a sentence saying why not when it is not.</summary>
        private static string TakeTheForeground()
        {
            if (ForegroundIsOurs() == null)
            {
                return null;
            }

            try
            {
                IntPtr window = OurWindow();
                if (window != IntPtr.Zero)
                {
                    Raise(window);
                }
            }
            catch (Exception e)
            {
                return "the game's window could not be raised: " + e.Message;
            }

            // Windows takes its time about a foreground change, and refuses it outright when another
            // process owns the foreground lock. Either way the answer is the same question asked
            // again a moment later.
            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(50);
                if (ForegroundIsOurs() == null)
                {
                    return null;
                }
            }

            return "the game does not have the foreground, so no key was sent - click the game "
                + "window and try again";
        }

        /// <summary>
        /// Come forward.
        ///
        /// Windows refuses a bare <c>SetForegroundWindow</c> from a process that does not already own
        /// the foreground - it flashes the taskbar button instead, which is not a keyboard. The
        /// documented way round it is to borrow the foreground thread's input queue for the length of
        /// the call, so that Windows sees the request as coming from the window that already has it.
        /// </summary>
        private static void Raise(IntPtr window)
        {
            uint ignored;
            uint ours = GetWindowThreadProcessId(window, out ignored);
            IntPtr foreground = GetForegroundWindow();
            uint theirs =
                foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out ignored);

            // Restore first: a minimized window cannot take the foreground, and the game is left
            // minimized by an owner who alt-tabbed away.
            ShowWindow(window, SwRestore);
            if (theirs != 0 && theirs != ours)
            {
                AttachThreadInput(theirs, ours, true);
            }

            try
            {
                BringWindowToTop(window);
                SetForegroundWindow(window);
            }
            finally
            {
                if (theirs != 0 && theirs != ours)
                {
                    AttachThreadInput(theirs, ours, false);
                }
            }
        }

        private const int SwRestore = 9;

        private static string ForegroundIsOurs()
        {
            try
            {
                IntPtr foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero)
                {
                    return "nothing has the foreground, so no key was sent";
                }

                uint pid;
                GetWindowThreadProcessId(foreground, out pid);
                int ours = Process.GetCurrentProcess().Id;
                return pid == (uint)ours
                    ? null
                    : "the foreground window belongs to process " + pid + ", not the game ("
                        + ours + "), so no key was sent";
            }
            catch (Exception e)
            {
                return "who has the foreground could not be established (" + e.Message
                    + "), so no key was sent";
            }
        }

        private static IntPtr OurWindow()
        {
            try
            {
                IntPtr main = Process.GetCurrentProcess().MainWindowHandle;
                if (main != IntPtr.Zero)
                {
                    return main;
                }
            }
            catch (Exception)
            {
                // Fall through to the enumeration, which does not depend on the process record.
            }

            // The search keeps nothing between requests: /key is served on HTTP threads, and two
            // concurrent searches sharing a static answered each other's window.
            IntPtr found = IntPtr.Zero;
            try
            {
                uint ours = (uint)Process.GetCurrentProcess().Id;
                EnumWindows(
                    (window, unused) =>
                    {
                        uint pid;
                        GetWindowThreadProcessId(window, out pid);
                        if (pid != ours || !IsWindowVisible(window))
                        {
                            return true;
                        }

                        found = window;
                        return false;
                    },
                    IntPtr.Zero
                );
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }

            return found;
        }

        // ---- the keys themselves ----

        private static bool Named(string name, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return Map().TryGetValue(name, out key);
        }

        private static Dictionary<string, KeyCode> _map;

        /// <summary>The names a caller may write, keyed case-insensitively. Unity's own
        /// <c>KeyCode</c> names throughout, so the vocabulary is the one every other probe in this mod
        /// speaks (<c>DevProbe.Claims("Escape,Minus")</c>), plus the three modifier words a chord is
        /// ordinarily written with.</summary>
        private static Dictionary<string, KeyCode> Map()
        {
            if (_map != null)
            {
                return _map;
            }

            Dictionary<string, KeyCode> map = new Dictionary<string, KeyCode>(
                StringComparer.OrdinalIgnoreCase
            );
            string[] names = Enum.GetNames(typeof(KeyCode));
            for (int i = 0; i < names.Length; i++)
            {
                KeyCode key = (KeyCode)Enum.Parse(typeof(KeyCode), names[i]);
                ushort vk;
                bool extended;
                if (Virtual(key, out vk, out extended) && !map.ContainsKey(names[i]))
                {
                    map.Add(names[i], key);
                }
            }

            map["Ctrl"] = KeyCode.LeftControl;
            map["Control"] = KeyCode.LeftControl;
            map["Shift"] = KeyCode.LeftShift;
            map["Alt"] = KeyCode.LeftAlt;
            map["Enter"] = KeyCode.Return;
            _map = map;
            return map;
        }

        /// <summary>A Unity key as Windows names it, and whether it is one of the keys that needs the
        /// extended flag (the grey block and the keypad's Enter and slash - without it the arrows
        /// arrive as their keypad twins).</summary>
        private static bool Virtual(KeyCode key, out ushort vk, out bool extended)
        {
            extended = false;
            vk = 0;
            if (key >= KeyCode.A && key <= KeyCode.Z)
            {
                vk = (ushort)('A' + (key - KeyCode.A));
                return true;
            }

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                vk = (ushort)('0' + (key - KeyCode.Alpha0));
                return true;
            }

            if (key >= KeyCode.F1 && key <= KeyCode.F12)
            {
                vk = (ushort)(0x70 + (key - KeyCode.F1));
                return true;
            }

            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                vk = (ushort)(0x60 + (key - KeyCode.Keypad0));
                return true;
            }

            switch (key)
            {
                case KeyCode.Backspace:
                    vk = 0x08;
                    return true;
                case KeyCode.Tab:
                    vk = 0x09;
                    return true;
                case KeyCode.Return:
                    vk = 0x0D;
                    return true;
                case KeyCode.KeypadEnter:
                    vk = 0x0D;
                    extended = true;
                    return true;
                case KeyCode.Escape:
                    vk = 0x1B;
                    return true;
                case KeyCode.Space:
                    vk = 0x20;
                    return true;
                case KeyCode.PageUp:
                    vk = 0x21;
                    extended = true;
                    return true;
                case KeyCode.PageDown:
                    vk = 0x22;
                    extended = true;
                    return true;
                case KeyCode.End:
                    vk = 0x23;
                    extended = true;
                    return true;
                case KeyCode.Home:
                    vk = 0x24;
                    extended = true;
                    return true;
                case KeyCode.LeftArrow:
                    vk = 0x25;
                    extended = true;
                    return true;
                case KeyCode.UpArrow:
                    vk = 0x26;
                    extended = true;
                    return true;
                case KeyCode.RightArrow:
                    vk = 0x27;
                    extended = true;
                    return true;
                case KeyCode.DownArrow:
                    vk = 0x28;
                    extended = true;
                    return true;
                case KeyCode.Insert:
                    vk = 0x2D;
                    extended = true;
                    return true;
                case KeyCode.Delete:
                    vk = 0x2E;
                    extended = true;
                    return true;
                case KeyCode.LeftShift:
                    vk = 0xA0;
                    return true;
                case KeyCode.RightShift:
                    vk = 0xA1;
                    return true;
                case KeyCode.LeftControl:
                    vk = 0xA2;
                    return true;
                case KeyCode.RightControl:
                    vk = 0xA3;
                    extended = true;
                    return true;
                case KeyCode.LeftAlt:
                    vk = 0xA4;
                    return true;
                case KeyCode.RightAlt:
                    vk = 0xA5;
                    extended = true;
                    return true;
                case KeyCode.KeypadMultiply:
                    vk = 0x6A;
                    return true;
                case KeyCode.KeypadPlus:
                    vk = 0x6B;
                    return true;
                case KeyCode.KeypadMinus:
                    vk = 0x6D;
                    return true;
                case KeyCode.KeypadPeriod:
                    vk = 0x6E;
                    return true;
                case KeyCode.KeypadDivide:
                    vk = 0x6F;
                    extended = true;
                    return true;
                case KeyCode.Semicolon:
                    vk = 0xBA;
                    return true;
                case KeyCode.Equals:
                    vk = 0xBB;
                    return true;
                case KeyCode.Comma:
                    vk = 0xBC;
                    return true;
                case KeyCode.Minus:
                    vk = 0xBD;
                    return true;
                case KeyCode.Period:
                    vk = 0xBE;
                    return true;
                case KeyCode.Slash:
                    vk = 0xBF;
                    return true;
                case KeyCode.BackQuote:
                    vk = 0xC0;
                    return true;
                case KeyCode.LeftBracket:
                    vk = 0xDB;
                    return true;
                case KeyCode.Backslash:
                    vk = 0xDC;
                    return true;
                case KeyCode.RightBracket:
                    vk = 0xDD;
                    return true;
                case KeyCode.Quote:
                    vk = 0xDE;
                    return true;
                default:
                    return false;
            }
        }

        private const ushort VkShift = 0xA0;

        private static void Key(ushort vk, bool down, bool extended)
        {
            INPUT[] one = new INPUT[1];
            one[0].Type = 1;
            one[0].Union.Keyboard.Vk = vk;
            one[0].Union.Keyboard.Scan = (ushort)MapVirtualKey(vk, 0);
            uint flags = 0;
            if (extended)
            {
                flags |= 0x0001;
            }

            if (!down)
            {
                flags |= 0x0002;
            }

            one[0].Union.Keyboard.Flags = flags;
            SendInput(1, one, Marshal.SizeOf(typeof(INPUT)));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public InputUnion Union;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;

            [FieldOffset(0)]
            public KEYBDINPUT Keyboard;

            [FieldOffset(0)]
            public HARDWAREINPUT Hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint Data;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint Msg;
            public ushort ParamL;
            public ushort ParamH;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr unused);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr unused);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern short VkKeyScan(char character);
    }
}
