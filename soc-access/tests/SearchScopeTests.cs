using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The search scope's merge: what a page declares now, plus what it WOULD declare with its
    /// branches open, with nothing offered twice.
    /// </summary>
    [TestClass]
    public class SearchScopeTests
    {
        // A page with one open group and one closed one, built twice: as the player left it, and with
        // everything forced open. That pair is exactly what the navigator hands Extend.
        private static GraphRender Build(bool expandAll)
        {
            GraphBuilder builder = new GraphBuilder(new HashSet<ControlId>());
            builder.ExpandAll = expandAll;
            builder.BeginStop("tree");
            builder.BeginGroup(new SyntheticNode(Graphs.Id("open"), Graphs.Vt("Serpens")), expanded: true);
            builder.AddItem(new SyntheticNode(Graphs.Id("open/star"), Graphs.Vt("Dusay")));
            builder.EndGroup();
            builder.BeginGroup(new SyntheticNode(Graphs.Id("shut"), Graphs.Vt("Osulo group")), expanded: false);
            builder.AddItem(new SyntheticNode(Graphs.Id("shut/star"), Graphs.Vt("Osulo")));
            builder.BeginGroup(new SyntheticNode(Graphs.Id("shut/deep"), Graphs.Vt("Osulo I")), expanded: false);
            builder.AddItem(new SyntheticNode(Graphs.Id("shut/deep/deposit"), Graphs.Vt("Antimatter")));
            builder.EndGroup();
            builder.EndGroup();
            return builder.Build();
        }

        [TestMethod]
        public void ACollapsedBranchDeclaresNothingUntilTheBuildIsForcedOpen()
        {
            Assert.IsNull(Build(false).NodeAt(Graphs.Id("shut/deep/deposit")));
            Assert.IsNotNull(Build(true).NodeAt(Graphs.Id("shut/deep/deposit")));
        }

        [TestMethod]
        public void TheMergedScopeOffersTheBuriedControlsAndNothingTwice()
        {
            GraphRender standing = Build(false);
            GraphRender deep = Build(true);
            SearchScope scope = SearchScope.Extend(
                SearchScope.OverStop(standing, "tree"),
                standing,
                deep,
                "tree",
                node => node.Id
            );

            List<string> offered = new List<string>();
            for (int i = 0; i < scope.Count; i++)
            {
                offered.Add(Convert.ToString(scope.IdOf(i).StructuralKey));
            }

            CollectionAssert.AreEqual(
                new[] { "open", "open/star", "shut", "shut/star", "shut/deep", "shut/deep/deposit" },
                offered
            );
            Assert.AreEqual("Antimatter", scope.TextOf(scope.Count - 1));
        }

        [TestMethod]
        public void LandingOnABuriedControlAsksTheHostToOpenItsBranches()
        {
            GraphRender standing = Build(false);
            GraphRender deep = Build(true);
            List<string> opened = new List<string>();
            SearchScope scope = SearchScope.Extend(
                SearchScope.OverStop(standing, "tree"),
                standing,
                deep,
                "tree",
                node =>
                {
                    for (GraphNode at = node.Parent; at != null; at = at.Parent)
                    {
                        if (at.Expandable)
                        {
                            opened.Add(Convert.ToString(at.Id.StructuralKey));
                        }
                    }

                    return node.Id;
                }
            );

            ControlId landed = scope.Land(scope.Count - 1);
            Assert.AreEqual("shut/deep/deposit", Convert.ToString(landed.StructuralKey));
            CollectionAssert.AreEqual(new[] { "shut/deep", "shut" }, opened);
        }

        [TestMethod]
        public void AScopeThatAlreadyOffersAControlKeepsItsOwnLanding()
        {
            GraphRender standing = Build(false);
            GraphRender deep = Build(true);
            int landings = 0;
            SearchScope declared = new SearchScope(
                1,
                index => "Osulo",
                index =>
                {
                    landings++;
                    return Graphs.Id("shut/star");
                },
                index => Graphs.Id("shut/star")
            );

            SearchScope scope = SearchScope.Extend(declared, standing, deep, "tree", node => node.Id);
            List<string> offered = new List<string>();
            for (int i = 0; i < scope.Count; i++)
            {
                offered.Add(Convert.ToString(scope.IdOf(i).StructuralKey));
            }

            Assert.AreEqual(1, offered.FindAll(key => key == "shut/star").Count);
            scope.Land(0);
            Assert.AreEqual(1, landings);
        }
    }
}
