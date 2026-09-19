using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The poll-and-diff contract: which screens end up on the stack and in what order, which
    /// lifecycle calls the diff makes and in which direction, where focus lands when a screen has a
    /// child open, and whose cursor survives being taken off the stack.
    /// </summary>
    [TestClass]
    public class ScreenManagerTests
    {
        /// <summary>A screen that is whatever the test says it is, and writes down what it was told.
        /// </summary>
        private sealed class Fake : GraphScreen
        {
            private readonly string _key;
            private readonly int _layer;
            private readonly List<string> _log;

            public Fake(string key, int layer, List<string> log)
            {
                _key = key;
                _layer = layer;
                _log = log;
            }

            public bool Active { get; set; }

            public bool Keep { get; set; }

            public override string Key
            {
                get { return _key; }
            }

            public override int Layer
            {
                get { return _layer; }
            }

            public override bool KeepStateOnPop
            {
                get { return Keep; }
            }

            public override bool IsActive()
            {
                return Active;
            }

            public override void Build(GraphBuilder builder)
            {
            }

            public override void OnPush()
            {
                _log.Add("push " + _key);
            }

            public override void OnPop()
            {
                _log.Add("pop " + _key);
            }
        }

        private static List<string> Keys(IReadOnlyList<Screen> screens)
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < screens.Count; i++)
            {
                keys.Add(screens[i].Key);
            }

            return keys;
        }

        [TestMethod]
        public void ActiveScreensSortByLayerAndKeepRegistrationOrderWithinOne()
        {
            List<string> log = new List<string>();
            ScreenManager manager = new ScreenManager(null, null, null);
            Fake high = new Fake("high", 40, log) { Active = true };
            Fake first = new Fake("first", 10, log) { Active = true };
            Fake second = new Fake("second", 10, log) { Active = true };
            manager.Register(high);
            manager.Register(first);
            manager.Register(second);

            manager.Tick();

            CollectionAssert.AreEqual(
                new List<string> { "first", "second", "high" },
                Keys(manager.Stack));
            Assert.AreSame(high, manager.Current);
        }

        [TestMethod]
        public void NothingIsOnTheStackWhileTheGameBlocksItsUi()
        {
            List<string> log = new List<string>();
            bool blocked = false;
            ScreenManager manager = new ScreenManager(null, null, null, () => blocked);
            Fake battlefield = new Fake("battlefield", 10, log) { Active = true };
            Fake pause = new Fake("pause", 40, log) { Active = true };
            manager.Register(battlefield);
            manager.Register(pause);
            manager.Tick();
            log.Clear();

            // The pause menu closes behind the blocker: the battlefield must not become the top.
            blocked = true;
            pause.Active = false;
            manager.Tick();

            CollectionAssert.AreEqual(new List<string> { "pop pause", "pop battlefield" }, log);
            Assert.IsNull(manager.Current);

            blocked = false;
            manager.Tick();
            Assert.AreSame(battlefield, manager.Current);
        }

        [TestMethod]
        public void ClosuresRunTopDownAndOpeningsBottomUp()
        {
            List<string> log = new List<string>();
            ScreenManager manager = new ScreenManager(null, null, null);
            Fake low = new Fake("low", 10, log) { Active = true };
            Fake high = new Fake("high", 40, log) { Active = true };
            Fake newLow = new Fake("new-low", 20, log);
            Fake newHigh = new Fake("new-high", 30, log);
            manager.Register(low);
            manager.Register(high);
            manager.Register(newLow);
            manager.Register(newHigh);
            manager.Tick();
            log.Clear();

            low.Active = false;
            high.Active = false;
            newLow.Active = true;
            newHigh.Active = true;
            manager.Tick();

            CollectionAssert.AreEqual(
                new List<string> { "pop high", "pop low", "push new-low", "push new-high" },
                log);
        }

        [TestMethod]
        public void FocusLandsOnTheDeepestChildOfTheTopScreen()
        {
            List<string> log = new List<string>();
            ScreenManager manager = new ScreenManager(null, null, null);
            Fake page = new Fake("page", 10, log) { Active = true };
            Fake child = new Fake("child", 0, log);
            Fake grandchild = new Fake("grandchild", 0, log);
            manager.Register(page);
            manager.Tick();
            Assert.AreSame(page, manager.Current);

            page.PushChild(child);
            child.PushChild(grandchild);
            manager.Tick();
            Assert.AreSame(grandchild, manager.Current);

            // A child is not polled: it is reached through its parent, and never stands on the stack
            // in its own right.
            CollectionAssert.AreEqual(new List<string> { "page" }, Keys(manager.Stack));

            child.RemoveChild(grandchild);
            manager.Tick();
            Assert.AreSame(child, manager.Current);
        }

        [TestMethod]
        public void KeepStateOnPopDecidesWhetherTheCursorSurvivesLeavingTheStack()
        {
            List<string> log = new List<string>();
            GraphNavigator navigator = new GraphNavigator();
            ScreenManager manager = new ScreenManager(navigator, null, null);
            Fake kept = new Fake("kept", 10, log) { Active = true, Keep = true };
            Fake dropped = new Fake("dropped", 20, log) { Active = true };
            manager.Register(kept);
            manager.Register(dropped);

            // Focus visits each in turn, which is what gives it a cursor to keep.
            manager.Tick();
            dropped.Active = false;
            manager.Tick();
            Assert.IsTrue(navigator.HasState(kept), "the kept screen keeps its cursor while it is up");

            kept.Active = false;
            manager.Tick();

            Assert.IsTrue(navigator.HasState(kept), "KeepStateOnPop keeps the cursor across the gap");
            Assert.IsFalse(navigator.HasState(dropped), "an ordinary screen starts again at the top");
        }
    }
}
