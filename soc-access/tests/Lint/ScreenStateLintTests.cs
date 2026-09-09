using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// A <c>LiveScreen</c>'s slot changes when the game hands it a different menu - a new battle, a
    /// second settlement, the same page after a hot reload. Anything the screen remembered about the
    /// previous one is then wrong, and the only place it can be dropped is
    /// <c>OnLiveChanged</c>, which the slot's setter calls for exactly that. A mutable instance field
    /// on a screen with no such override is state that outlives what it describes (AGENTS.md,
    /// Screen Resolution).
    /// </summary>
    [TestClass]
    public class ScreenStateLintTests
    {
        private const string Allowlist = "screen-state.allow";

        private const string Rule =
            "A LiveScreen with a mutable instance field needs an OnLiveChanged override that clears it - the slot changes on a new menu, a new battle and a hot reload alike.\n"
            + "State that survives a change of Live describes a menu that is gone (AGENTS.md, Screen Resolution).";

        private static readonly Regex Derives = new Regex(@"\bclass\s+\w+\s*(?:<[^<>]*>)?\s*:\s*[^{]*\bLiveScreen\s*<");

        private static readonly Regex Reset = new Regex(@"\boverride\s+void\s+OnLiveChanged\s*\(");

        /// <summary>A field at class scope: modifiers, a type, a name, then either the end of the
        /// declaration or the start of an initialiser that may run on to the next line. No parenthesis
        /// and no brace, which is what separates a field from a method and from a property - the rule
        /// is about fields, so an auto-property with a setter is out of its reach.</summary>
        private static readonly Regex Field = new Regex(
            @"^\s*(?:private|public|protected|internal)\s+[^()=;{}]*?(\w+)\s*(?:;|=(?!>))");

        private static readonly Regex Immutable = new Regex(@"\b(readonly|const|static|event|delegate)\b");

        [TestMethod]
        public void EveryUnresetScreenFieldIsOnTheAllowlist()
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
            foreach (string file in LintSources.Under("soc-access/screens/"))
            {
                string[] lines = LintSources.Lines(file);
                bool derives = false;
                bool resets = false;
                foreach (string line in lines)
                {
                    if (LintSources.IsComment(line))
                    {
                        continue;
                    }

                    string code = LintSources.Code(line);
                    derives = derives || Derives.IsMatch(code);
                    resets = resets || Reset.IsMatch(code);
                }

                if (!derives || resets)
                {
                    continue;
                }

                Structure structure = LintSources.Read(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LintSources.IsComment(lines[i]) || !structure.AtTypeScope[i])
                    {
                        continue;
                    }

                    string code = LintSources.Code(lines[i]);
                    if (Field.IsMatch(code) && !Immutable.IsMatch(code))
                    {
                        LintSources.Add(found, file, lines[i]);
                    }
                }
            }

            return found;
        }
    }
}
