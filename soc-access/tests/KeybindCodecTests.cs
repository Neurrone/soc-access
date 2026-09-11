using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;
using UnityEngine.InputSystem;
using InputBinding = SongsOfConquestAccess.Input.InputBinding;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// How a mod gesture's override bindings ride in one config string
    /// (<see cref="KeybindCodec"/>): a stable English round-trip, and a tolerant reader that steps
    /// over anything it cannot parse rather than losing the whole entry.
    /// </summary>
    [TestClass]
    public class KeybindCodecTests
    {
        [TestMethod]
        public void RoundTripsAChordWithModifiers()
        {
            List<InputBinding> bindings = new List<InputBinding>
            {
                new KeyboardBinding(Key.G, ctrl: true, shift: false, alt: true),
            };
            List<KeyboardBinding> decoded = KeybindCodec.Decode(KeybindCodec.Encode(bindings));

            Assert.AreEqual(1, decoded.Count);
            Assert.AreEqual(Key.G, decoded[0].Key);
            Assert.IsTrue(decoded[0].Ctrl);
            Assert.IsFalse(decoded[0].Shift);
            Assert.IsTrue(decoded[0].Alt);
        }

        [TestMethod]
        public void RoundTripsSeveralBindingsInOrder()
        {
            List<InputBinding> bindings = new List<InputBinding>
            {
                new KeyboardBinding(Key.Home),
                new KeyboardBinding(Key.J),
            };
            List<KeyboardBinding> decoded = KeybindCodec.Decode(KeybindCodec.Encode(bindings));

            Assert.AreEqual(2, decoded.Count);
            Assert.AreEqual(Key.Home, decoded[0].Key);
            Assert.AreEqual(Key.J, decoded[1].Key);
        }

        [TestMethod]
        public void SkipsNonKeyboardBindingsWhenEncoding()
        {
            List<InputBinding> bindings = new List<InputBinding>
            {
                new KeyboardBinding(Key.Backslash),
                new KeyboardDisplayNameBinding("\\"),
            };
            List<KeyboardBinding> decoded = KeybindCodec.Decode(KeybindCodec.Encode(bindings));

            Assert.AreEqual(1, decoded.Count);
            Assert.AreEqual(Key.Backslash, decoded[0].Key);
        }

        [TestMethod]
        public void EmptyEncodesAndDecodesToNothing()
        {
            Assert.AreEqual(string.Empty, KeybindCodec.Encode(new List<InputBinding>()));
            Assert.AreEqual(0, KeybindCodec.Decode(string.Empty).Count);
            Assert.AreEqual(0, KeybindCodec.Decode(null).Count);
        }

        [TestMethod]
        public void SkipsABadTokenButKeepsTheGoodOnesAroundIt()
        {
            // An unknown key, a numeric key, and a token with the wrong field count all drop out; the
            // two valid tokens survive.
            string text = "A,1,0,0;NotAKey,0,0,0;5,0,0,0;G,0,1;M,0,0,1";
            List<KeyboardBinding> decoded = KeybindCodec.Decode(text);

            Assert.AreEqual(2, decoded.Count);
            Assert.AreEqual(Key.A, decoded[0].Key);
            Assert.IsTrue(decoded[0].Ctrl);
            Assert.AreEqual(Key.M, decoded[1].Key);
            Assert.IsTrue(decoded[1].Alt);
        }

        [TestMethod]
        public void ReadsTheTrueFalseFlagFormToo()
        {
            List<KeyboardBinding> decoded = KeybindCodec.Decode("R,true,false,false");
            Assert.AreEqual(1, decoded.Count);
            Assert.IsTrue(decoded[0].Ctrl);
        }

        [TestMethod]
        public void RoundTripsTheDisplayNameEvenWhenItIsASeparator()
        {
            List<InputBinding> bindings = new List<InputBinding>
            {
                new KeyboardBinding(Key.Comma, displayName: ","),
                new KeyboardBinding(Key.Semicolon, shift: true, displayName: ";"),
                new KeyboardBinding(Key.OEM1, displayName: "\\"),
            };
            List<KeyboardBinding> decoded = KeybindCodec.Decode(KeybindCodec.Encode(bindings));

            Assert.AreEqual(3, decoded.Count);
            Assert.AreEqual(",", decoded[0].DisplayName);
            Assert.AreEqual(";", decoded[1].DisplayName);
            Assert.IsTrue(decoded[1].Shift);
            Assert.AreEqual("\\", decoded[2].DisplayName);
            Assert.AreEqual(Key.OEM1, decoded[2].Key);
        }

        [TestMethod]
        public void OnlyPunctuationIsAPortableDisplayName()
        {
            Assert.IsTrue(ModSettings.IsPortableDisplayName("\\"));
            Assert.IsTrue(ModSettings.IsPortableDisplayName("/"));
            Assert.IsFalse(ModSettings.IsPortableDisplayName("a"));
            Assert.IsFalse(ModSettings.IsPortableDisplayName("7"));
            Assert.IsFalse(ModSettings.IsPortableDisplayName(" "));
            Assert.IsFalse(ModSettings.IsPortableDisplayName("Enter"));
            Assert.IsFalse(ModSettings.IsPortableDisplayName(string.Empty));
            Assert.IsFalse(ModSettings.IsPortableDisplayName(null));
        }
    }
}
