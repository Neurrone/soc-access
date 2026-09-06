using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The type-ahead glue: what a search looks through on a screen (<see cref="SearchScope"/>), and
    /// what typing, stepping and straying do to it (<see cref="TypeAhead"/>). The matching itself is
    /// <see cref="TypeAheadSearchTests"/>; nothing here needs the game.
    /// </summary>
    [TestClass]
    public class TypeAheadTests
    {
        private static GraphRender Menu()
        {
            return Renderer(b =>
            {
                b.BeginStop("left");
                b.AddItem(new SyntheticNode(Id("load"), Vt("Load Game")));
                b.AddItem(new SyntheticNode(Id("license"), Vt("License")));
                b.AddItem(new SyntheticNode(Id("dlc"), Vt("DLC")));
                b.BeginStop("right");
                b.AddItem(new SyntheticNode(Id("lore"), Vt("Lore")));
            })();
        }

        // The host's half: focus lands where the search asks and stays there, and the announcements
        // are recorded rather than spoken.
        private sealed class Landings
        {
            public readonly TypeAhead Search = new TypeAhead();
            public readonly List<string> Landed = new List<string>();
            public readonly List<string> NoMatch = new List<string>();
            public ControlId Focus;

            public Landings()
            {
                Search.OnLand = id =>
                {
                    Focus = id;
                    Landed.Add((string)id.StructuralKey);
                    return id;
                };
                Search.OnNoMatch = text => NoMatch.Add(text);
            }

            public void Type(string text, SearchScope scope)
            {
                foreach (char c in text)
                {
                    Search.Type(c, scope);
                }
            }
        }

        // ---- what a search looks through ----

        [TestMethod]
        public void TheDefaultScopeIsTheFocusedStopsControls()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");

            Assert.AreEqual(3, scope.Count);
            Assert.AreEqual("Load Game", scope.TextOf(0));
            Assert.AreEqual("DLC", scope.TextOf(2));
            Assert.AreEqual(Id("license"), scope.Land(1));
        }

        [TestMethod]
        public void AControlCanDeclareItsOwnSearchTextAndOptOutAltogether()
        {
            GraphRender render = Renderer(b =>
            {
                NodeVtable cell = Vt("12");
                cell.SearchText = () => "Alpha";
                b.AddItem(new SyntheticNode(Id("cell"), cell));

                NodeVtable heading = Vt("Turn");
                heading.ExcludeFromSearch = true;
                b.AddItem(new SyntheticNode(Id("heading"), heading));
            })();

            SearchScope scope = SearchScope.OverStop(render, render.Order[0].StopKey);

            Assert.AreEqual(1, scope.Count);
            Assert.AreEqual("Alpha", scope.TextOf(0));
        }

        [TestMethod]
        public void ATabularRowOffersOneResultAtItsPrimaryCell()
        {
            GraphRender render = Renderer(b =>
            {
                GraphSheet sheet = new GraphSheet(b, "t:");
                sheet.Region("Fleets", new[] { "Name", "Ships", "Move" });
                sheet.Row(Vt("Alpha"), new object(), null, () => "3", () => "5");
                sheet.Row(Vt("Beta"), new object(), null, () => "2", () => "4");
                sheet.Finish();
            })();

            SearchScope scope = SearchScope.OverStop(render, render.Order[0].StopKey);

            Assert.AreEqual(6, render.Order.Count); // two rows of three cells
            Assert.AreEqual(2, scope.Count);
            Assert.AreEqual("Alpha", scope.TextOf(0));
            Assert.AreEqual("Beta", scope.TextOf(1));
        }

        // ---- typing ----

        [TestMethod]
        public void TypingLandsOnTheBestMatchAndNarrowingMovesOn()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();

            host.Type("l", scope);
            CollectionAssert.AreEqual(new[] { "load" }, host.Landed); // list order breaks the tier tie
            Assert.AreEqual(3, host.Search.ResultCount);

            host.Type("i", scope);
            Assert.AreEqual("license", host.Landed[host.Landed.Count - 1]);
            Assert.AreEqual(1, host.Search.ResultCount);
            Assert.AreEqual("li", host.Search.Buffer);
        }

        [TestMethod]
        public void RepeatingTheLetterStepsThroughItsMatches()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();

            host.Type("lll", scope);

            CollectionAssert.AreEqual(new[] { "load", "license", "dlc" }, host.Landed);
        }

        [TestMethod]
        public void TheResultsCycleAndTheEndsAreReachable()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();
            host.Type("l", scope);
            host.Landed.Clear();

            host.Search.Step(1);
            host.Search.Step(-1);
            host.Search.Step(-1); // wraps past the front
            host.Search.Last();
            host.Search.First();

            CollectionAssert.AreEqual(new[] { "license", "load", "dlc", "dlc", "load" }, host.Landed);
        }

        [TestMethod]
        public void NothingMatchedSaysSoAndMovesNobody()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();

            host.Type("zq", scope);

            CollectionAssert.AreEqual(new[] { "z", "zq" }, host.NoMatch);
            Assert.AreEqual(0, host.Landed.Count);
            Assert.IsTrue(host.Search.IsActive);
            Assert.AreEqual(0, host.Search.ResultCount);
            // Nothing landed, so nothing can go stale wherever focus happens to be.
            Assert.IsFalse(host.Search.Strayed(Id("lore")));
        }

        [TestMethod]
        public void ACharacterWithNothingToSearchIsDroppedRatherThanRemembered()
        {
            Landings host = new Landings();

            Assert.IsFalse(host.Search.Type('l', SearchScope.OverStop(Menu(), "no such stop")));
            Assert.IsFalse(host.Search.HasBuffer);
            Assert.IsFalse(host.Search.IsActive);
        }

        // ---- staleness ----

        [TestMethod]
        public void FocusMovingOffTheResultMakesTheSearchStale()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();
            host.Type("l", scope);

            Assert.IsFalse(host.Search.Strayed(host.Focus));
            Assert.IsTrue(host.Search.Strayed(Id("lore")));

            host.Search.Clear();
            Assert.IsFalse(host.Search.Strayed(Id("lore")));
            Assert.IsFalse(host.Search.IsActive);
        }

        [TestMethod]
        public void ALandingThatCouldNotBeReachedLeavesNothingToGoStale()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            TypeAhead search = new TypeAhead();
            search.OnLand = id => null; // the control vanished between the render and the landing

            search.Type('l', scope);

            Assert.IsFalse(search.Strayed(Id("load")));
        }

        // ---- a screen's own scope ----

        [TestMethod]
        public void AScreenSuppliedScopeReplacesTheDeclaredControls()
        {
            // What the technology screen will do: search items that are not declared (a collapsed
            // branch's contents), landing by opening the branch and answering with the control.
            List<string> opened = new List<string>();
            string[] items = { "Applied Casimir Effect", "Nanorobotics", "Casimir Actuators" };
            SearchScope scope = new SearchScope(
                items.Length,
                i => items[i],
                i =>
                {
                    opened.Add(items[i]);
                    return Id("tech/" + i);
                }
            );

            Landings host = new Landings();
            host.Type("cas", scope);

            // One landing per keystroke, each doing the screen's own work of reaching the item.
            Assert.AreEqual(3, opened.Count);
            Assert.AreEqual("Casimir Actuators", opened[opened.Count - 1]);
            Assert.AreEqual("tech/2", host.Landed[host.Landed.Count - 1]);
            Assert.AreEqual(2, host.Search.ResultCount); // the mid-string match is offered second
        }
    }
}
