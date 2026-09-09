using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// The roster of hooks allowed to exist. Unlike the other six lists this one is not an exception
    /// list: every patch in the tree is on it, and every entry says which of the two things it is.
    ///
    /// <c>event</c> - it delivers something the game did: a notification, a chat line, a battle
    /// response, a story trigger. Losing the call costs one announcement.
    /// <c>interception</c> - it alters game behaviour: forces a tooltip, routes a close.
    ///
    /// There is no third kind, and <c>readiness</c> in particular is refused rather than listed: a
    /// screen learns it can be read by looking at the game, off the end state the menu's own
    /// coroutine leaves behind, never because a hook fired (AGENTS.md, Screen Resolution).
    /// </summary>
    [TestClass]
    public class PatchInventoryLintTests
    {
        private const string Allowlist = "patches.allow";

        private const string Rule =
            "Every Harmony patch is on this inventory, and every entry names its kind: event (it delivers something the game did) or interception (it alters game behaviour).\n"
            + "A hook is never the source of truth for anything a Build, IsActive, ScreenName or tooltip reads; losing one call may cost one announcement and nothing else (AGENTS.md, Screen Resolution).\n"
            + "Format: path | count | source line | kind.";

        private const string Refused =
            "a readiness hook is refused outright: readiness is read from the game (AGENTS.md, Screen Resolution)";

        /// <summary>Both attribute forms: <c>[HarmonyPatch(typeof(X), "M")]</c> and the overload
        /// list, <c>[HarmonyPatch(typeof(X), "M", new[] { ... })]</c>, whose argument array may run
        /// on to the following lines - the attribute's own line is the site either way.</summary>
        private static readonly Regex Patch = new Regex(
            @"\[HarmonyPatch\s*\(\s*typeof\s*\([^)]*\)\s*,\s*""");

        private static readonly HashSet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "event",
            "interception",
        };

        [TestMethod]
        public void EveryPatchIsOnTheInventoryWithItsKind()
        {
            List<string> problems = new List<string>();
            Dictionary<Site, int> found = Sites();
            LintSources.Regenerate(Allowlist, found, Rule, true);
            foreach (AllowEntry entry in LintSources.Entries(Allowlist, true))
            {
                if (string.IsNullOrEmpty(entry.Kind))
                {
                    problems.Add("No kind on " + entry.Site.File + ": " + entry.Site.Text
                        + " - every entry is event or interception.");
                }
                else if (!Kinds.Contains(entry.Kind))
                {
                    problems.Add("Kind \"" + entry.Kind + "\" on " + entry.Site.File + ": "
                        + entry.Site.Text + " - " + Refused + ".");
                }
            }

            problems.Sort(StringComparer.Ordinal);
            LintSources.AssertAllowed(Allowlist, found, Rule, true, problems);
        }

        [TestMethod]
        public void TheAllowlistFileExists()
        {
            LintSources.AssertArmed(Allowlist);
        }

        public static Dictionary<Site, int> Sites()
        {
            Dictionary<Site, int> found = new Dictionary<Site, int>();
            foreach (string file in LintSources.Under("soc-access/patches/"))
            {
                foreach (string line in LintSources.Lines(file))
                {
                    if (!LintSources.IsComment(line) && Patch.IsMatch(line))
                    {
                        LintSources.Add(found, file, line);
                    }
                }
            }

            return found;
        }
    }
}
