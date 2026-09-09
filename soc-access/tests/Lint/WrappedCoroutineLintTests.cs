using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// A patch that wraps the game's coroutine, or starts one of the mod's own to wait for a menu to
    /// finish appearing, is a readiness hook wearing a different hat: the screen learns it can be
    /// read because a wrapper ran, not because it looked at the game. The wrapper is also the one
    /// thing a hot reload cannot repeat - the coroutine that would have told the screen already ran,
    /// or is running inside an assembly that no longer exists. Readiness is read from the game
    /// (AGENTS.md, Screen Resolution).
    /// </summary>
    [TestClass]
    public class WrappedCoroutineLintTests
    {
        private const string Allowlist = "wrapped-coroutines.allow";

        private const string Rule =
            "A patch that wraps or starts a coroutine to wait for a menu is a readiness hook; readiness is read from the game, off the end state the menu's own coroutine leaves behind.\n"
            + "A hot reload cannot re-run the wrapper, which is why a screen that learns from one is a screen that is right only the first time (AGENTS.md, Screen Resolution).";

        private static readonly Regex Wrapping = new Regex(
            @"ref\s+IEnumerator\s+__result|ref\s+UniTask\s+__result|StartCoroutine\s*\(");

        /// <summary>A fire-and-forget await. <c>Forget()</c> is also what a screen does with its
        /// slot, so the line has to look like the async one: a UniTask, a Task, an await or an
        /// <c>...Async(</c> call on the same line.</summary>
        private static readonly Regex Forgotten = new Regex(@"\.Forget\s*\(\s*\)");

        private static readonly Regex Await = new Regex(@"\bawait\b|\bUniTask\b|\bTask\b|Async\s*\(");

        [TestMethod]
        public void EveryWrappedCoroutineIsOnTheAllowlist()
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
            foreach (string file in LintSources.Under("soc-access/patches/"))
            {
                foreach (string line in LintSources.Lines(file))
                {
                    if (LintSources.IsComment(line))
                    {
                        continue;
                    }

                    string code = LintSources.Code(line);
                    if (Wrapping.IsMatch(code)
                        || (Forgotten.IsMatch(code) && Await.IsMatch(code)))
                    {
                        LintSources.Add(found, file, line);
                    }
                }
            }

            return found;
        }
    }
}
