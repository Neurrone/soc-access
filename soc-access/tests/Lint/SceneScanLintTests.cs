using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// A scene scan costs milliseconds - the root walk that finds the unbound menu-scene objects is
    /// about 3.5 ms on the adventure scene. That is affordable once, when a source resolves a menu
    /// or an adapter finds its panel, and unaffordable on a path a <c>Build</c> runs every frame
    /// (AGENTS.md, Performance).
    ///
    /// So a scan belongs in a constructor or in a member whose name says it is the one-time lookup:
    /// <c>Find...</c>, <c>Resolve...</c>, <c>Probe...</c>, <c>Recover...</c>, <c>Scan...</c>.
    /// Anywhere else it is a per-frame walk of the scene, and the site says which.
    /// <c>soc-access/dev/</c> is exempt: the dump routes exist to walk the tree and are never on a
    /// build path.
    /// </summary>
    [TestClass]
    public class SceneScanLintTests
    {
        private const string Allowlist = "scene-scans.allow";

        private const string Rule =
            "A scene scan belongs in a constructor or in a Find/Resolve/Probe/Recover/Scan member - once per adapter, never on a path a Build runs every frame.\n"
            + "FindObjectsOfTypeAll, FindObjectOfType, GetComponentsInChildren, GetComponentInChildren and GetComponentsInParent all walk the scene or a subtree (AGENTS.md, Performance).";

        private static readonly Regex Scan = new Regex(
            @"\b(FindObjectsOfTypeAll|FindObjectOfType|GetComponentsInChildren|GetComponentInChildren|GetComponentsInParent)\s*<");

        /// <summary>The member names a one-time lookup is allowed to have.</summary>
        private static readonly Regex Lookup = new Regex(@"^(Find|Resolve|Probe|Recover|Scan)");

        [TestMethod]
        public void EverySceneScanIsOnTheAllowlist()
        {
            LintSources.AssertAllowed(Allowlist, Sites(), Rule);
        }

        [TestMethod]
        public void TheAllowlistFileExists()
        {
            LintSources.AssertArmed(Allowlist);
        }

        public static Dictionary<Site, int> Sites()
        {
            Dictionary<Site, int> found = new Dictionary<Site, int>();
            foreach (string file in LintSources.ModSources())
            {
                if (file.StartsWith("soc-access/dev/", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] lines = LintSources.Lines(file);
                Structure structure = LintSources.Read(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LintSources.IsComment(lines[i]) || !Scan.IsMatch(LintSources.Code(lines[i])))
                    {
                        continue;
                    }

                    // The enclosing member comes from the forward brace-depth walk in
                    // Structure - the nearest declaration whose block this line is still inside.
                    // A constructor is the member whose name is its type's.
                    string member = structure.Member[i];
                    if (member != null
                        && (Lookup.IsMatch(member)
                            || string.Equals(member, structure.Type[i], StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    LintSources.Add(found, file, lines[i]);
                }
            }

            return found;
        }
    }
}
