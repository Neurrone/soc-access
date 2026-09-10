using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;
using UnityEngine.InputSystem;
using InputAction = SongsOfConquestAccess.Input.InputAction;
using InputBinding = SongsOfConquestAccess.Input.InputBinding;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The override/default memory an <see cref="InputAction"/> grew for rebinding: the compiled-in
    /// defaults stay put while a player override replaces the effective set, and clearing or resetting
    /// brings the defaults back.
    /// </summary>
    [TestClass]
    public class InputActionOverrideTests
    {
        private static InputAction Action()
        {
            return new InputAction("test", "Test", InputClaimScope.Screen)
                .AddBinding(new KeyboardBinding(Key.A));
        }

        [TestMethod]
        public void StartsOnItsDefaultWithNoOverride()
        {
            InputAction action = Action();
            Assert.IsFalse(action.HasOverride);
            Assert.AreEqual(1, action.Bindings.Count);
            Assert.AreEqual(1, action.Defaults.Count);
            Assert.AreSame(action.Defaults[0], action.Bindings[0]);
        }

        [TestMethod]
        public void OverrideReplacesTheEffectiveSetButNotTheDefaults()
        {
            InputAction action = Action();
            KeyboardBinding chord = new KeyboardBinding(Key.G, ctrl: true);
            action.SetOverride(new InputBinding[] { chord });

            Assert.IsTrue(action.HasOverride);
            Assert.AreEqual(1, action.Bindings.Count);
            Assert.AreSame(chord, action.Bindings[0]);
            // The default is untouched, ready to restore.
            Assert.AreEqual(Key.A, ((KeyboardBinding)action.Defaults[0]).Key);
        }

        [TestMethod]
        public void OverrideIsCopiedSoTheCallerCannotMutateItAfterwards()
        {
            InputAction action = Action();
            List<InputBinding> list = new List<InputBinding> { new KeyboardBinding(Key.G) };
            action.SetOverride(list);
            list.Add(new KeyboardBinding(Key.H));

            Assert.AreEqual(1, action.Bindings.Count);
        }

        [TestMethod]
        public void ClearOverrideAndResetToDefaultBothRestoreTheDefault()
        {
            InputAction cleared = Action();
            cleared.SetOverride(new InputBinding[] { new KeyboardBinding(Key.G) });
            cleared.ClearOverride();
            Assert.IsFalse(cleared.HasOverride);
            Assert.AreEqual(Key.A, ((KeyboardBinding)cleared.Bindings[0]).Key);

            InputAction reset = Action();
            reset.SetOverride(new InputBinding[] { new KeyboardBinding(Key.G) });
            reset.ResetToDefault();
            Assert.IsFalse(reset.HasOverride);
            Assert.AreEqual(Key.A, ((KeyboardBinding)reset.Bindings[0]).Key);
        }

        [TestMethod]
        public void AnEmptyOverrideIsDistinctFromNoOverride()
        {
            InputAction action = Action();
            action.SetOverride(new InputBinding[0]);
            Assert.IsTrue(action.HasOverride);
            Assert.AreEqual(0, action.Bindings.Count);

            action.SetOverride(null);
            Assert.IsFalse(action.HasOverride);
            Assert.AreEqual(1, action.Bindings.Count);
        }
    }
}
