using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// A detector handler delivers an event and nothing else. It may register a screen
    /// (<c>Reg&lt;T&gt;</c>), point its slot at what the game made ready (<c>Show</c>), let go of it
    /// (<c>Forget</c>), ask whether the slot is the object the game is talking about
    /// (<c>Matches</c>, <c>ReferenceEquals</c>) and build the adapter it hands over
    /// (<c>new ...Adapter(</c>). Anything else in a handler body is the detector writing state that
    /// a per-frame <c>Build</c>, an <c>IsActive</c> or a tooltip then depends on - the shape the
    /// 2026-09-09 audit found nine times, where first entry and hot-reload recovery are two code
    /// paths that can disagree (AGENTS.md, Screen Resolution).
    /// </summary>
    [TestClass]
    public class DetectorSideEffectLintTests
    {
        private const string Allowlist = "detector-side-effects.allow";

        private const string Detector = "soc-access/screens/ScreenDetector.cs";

        private const string Rule =
            "A detector handler delivers an event: Reg<T>, Show, Forget, Matches, ReferenceEquals, a new adapter, a return or a null check - nothing else.\n"
            + "Anything more is the detector writing state a Build, an IsActive, a ScreenName or a tooltip reads, and losing the call then costs more than one announcement (AGENTS.md, Screen Resolution).";

        /// <summary>A handler: the public event entry points the patches call into.</summary>
        private static readonly Regex Handler = new Regex(@"^\s*public\s+(?:void|bool)\s+(On\w*)\s*\(");

        /// <summary>Every <c>name(</c> on a line, member call or not - <c>screen.BeginHandover()</c>
        /// is exactly the kind of side effect this rule is about, so a leading dot does not
        /// exempt it. The optional generic argument list is what carries <c>Reg&lt;T&gt;()</c>.</summary>
        private static readonly Regex Call = new Regex(@"(\w+)\s*(?:<[^<>()]*>)?\s*\(");

        /// <summary>Preceded by <c>new</c>: a construction rather than a call.</summary>
        private static readonly Regex Constructed = new Regex(@"\bnew\s+(?:[\w\.]+\.)?(\w+)\s*(?:<[^<>()]*>)?\s*\(");

        private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Reg",
            "Show",
            "Forget",
            "Matches",
            "ReferenceEquals",
        };

        /// <summary>Control flow, not calls.</summary>
        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "if", "else", "while", "for", "foreach", "switch", "return", "catch", "lock", "using",
            "do", "fixed", "throw", "typeof", "nameof", "sizeof", "default",
        };

        [TestMethod]
        public void EveryDetectorHandlerSideEffectIsOnTheAllowlist()
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
            string[] lines = LintSources.Lines(Detector);
            Structure structure = LintSources.Read(Detector);
            for (int i = 0; i < lines.Length; i++)
            {
                if (LintSources.IsComment(lines[i])
                    || structure.Signature[i] == null
                    || !Handler.IsMatch(structure.Signature[i]))
                {
                    continue;
                }

                if (SideEffect(LintSources.Code(lines[i])))
                {
                    LintSources.Add(found, Detector, lines[i]);
                }
            }

            return found;
        }

        private static bool SideEffect(string code)
        {
            HashSet<string> constructed = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Constructed.Matches(code))
            {
                constructed.Add(match.Groups[1].Value);
            }

            foreach (Match match in Call.Matches(code))
            {
                string name = match.Groups[1].Value;
                if (Keywords.Contains(name) || Allowed.Contains(name))
                {
                    continue;
                }

                // `new SomethingAdapter(menu)` is the object the handler hands over, not a call it
                // makes; anything else built here is state the handler is creating.
                if (constructed.Contains(name)
                    && name.EndsWith("Adapter", StringComparison.Ordinal))
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
