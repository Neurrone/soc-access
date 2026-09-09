using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// THE ROSTER OF WHAT A SCREEN IS ALLOWED TO REMEMBER. Like the patch inventory and unlike the
    /// other lists, this one is not an exception list: every mutable instance field of every
    /// <c>LiveScreen</c> subclass is on it, and every entry says which of two things it is.
    ///
    /// <c>cursor</c> - a cursor, a cursor intent, or a memo keyed on that cursor: the tile the player
    /// is standing on, the grid built over the adapter it walks, the page the screen means to focus
    /// after the game redraws it, the tooltip composed for the tile the cursor is on.
    /// <c>baseline</c> - what was last said, so that only a CHANGE is spoken: an instruction line, a
    /// heading, the code echoed back as it is typed.
    ///
    /// Both outlive the menu, which is the whole test: a screen object lives for the whole mod load
    /// and an adapter lives exactly as long as the menu instance it wraps, so anything that is
    /// "about this menu" is an adapter field and needs no reset (AGENTS.md, Screen Resolution).
    /// <c>subscription</c> and <c>cache</c> are therefore REFUSED rather than listed: a handler the
    /// screen handed the game is released in the adapter's <c>Dispose</c>, and a cache of what the
    /// menu said is the adapter's too. There is no reset hook to remember and none is wanted.
    ///
    /// Each field carries a comment at its declaration saying which it is - the sentence AGENTS.md
    /// already asks for, in the place a reader is standing when the question comes up.
    /// </summary>
    [TestClass]
    public class ScreenStateLintTests
    {
        private const string Allowlist = "screen-state.allow";

        private const string Rule =
            "Every mutable instance field of a LiveScreen is on this roster, and every entry names its kind: cursor (a cursor, a cursor intent, or a memo keyed on it) or baseline (what was last said, so only a change is spoken).\n"
            + "Both outlive the menu. Per-menu state - a subscription, a cache of what the menu said - lives on the adapter, which lives exactly as long as the menu instance and releases in Dispose whatever it attached to the game (AGENTS.md, Screen Resolution).\n"
            + "Format: path | count | source line | kind.";

        private const string Refused =
            "a subscription or a cache belongs on the adapter, which lives exactly as long as the menu and releases in Dispose what it attached to the game (AGENTS.md, Screen Resolution)";

        private static readonly Regex Derives = new Regex(@"\bclass\s+\w+\s*(?:<[^<>]*>)?\s*:\s*[^{]*\bLiveScreen\s*<");

        /// <summary>A field at class scope: modifiers, a type, a name, then either the end of the
        /// declaration or the start of an initialiser that may run on to the next line. No parenthesis
        /// and no brace, which is what separates a field from a method and from a property - the rule
        /// is about fields, so an auto-property with a setter is out of its reach.</summary>
        private static readonly Regex Field = new Regex(
            @"^\s*(?:private|public|protected|internal)\s+[^()=;{}]*?(\w+)\s*(?:;|=(?!>))");

        private static readonly Regex Immutable = new Regex(@"\b(readonly|const|static|event|delegate)\b");

        private static readonly HashSet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "cursor",
            "baseline",
        };

        [TestMethod]
        public void EveryScreenFieldIsOnTheRosterWithItsKind()
        {
            List<string> problems = new List<string>();
            Dictionary<Site, int> found = Sites();
            LintSources.Regenerate(Allowlist, found, Rule, true);
            foreach (AllowEntry entry in LintSources.Entries(Allowlist, true))
            {
                if (string.IsNullOrEmpty(entry.Kind))
                {
                    problems.Add("No kind on " + entry.Site.File + ": " + entry.Site.Text
                        + " - every entry is cursor or baseline.");
                }
                else if (!Kinds.Contains(entry.Kind))
                {
                    problems.Add("Kind \"" + entry.Kind + "\" on " + entry.Site.File + ": "
                        + entry.Site.Text + " - " + Refused + ".");
                }
            }

            foreach (Site site in Uncommented())
            {
                problems.Add("No comment above " + site.File + ": " + site.Text
                    + " - a field a screen keeps says at its declaration which it is.");
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
            Walk((file, lines, i) => LintSources.Add(found, file, lines[i]));
            return found;
        }

        /// <summary>The fields with no comment above them. A field the screen keeps says why at its
        /// declaration, which is where the next reader is standing. The walk up steps over blank
        /// lines and over the other fields of the same run, so the memo whose seven fields are one
        /// idea is explained once rather than seven times.</summary>
        private static IList<Site> Uncommented()
        {
            List<Site> bare = new List<Site>();
            Walk((file, lines, i) =>
            {
                if (!Explained(lines, i))
                {
                    bare.Add(new Site(file, lines[i].Trim()));
                }
            });

            bare.Sort(delegate (Site left, Site right)
            {
                int file = StringComparer.Ordinal.Compare(left.File, right.File);
                return file != 0 ? file : StringComparer.Ordinal.Compare(left.Text, right.Text);
            });
            return bare;
        }

        private static bool Explained(string[] lines, int at)
        {
            for (int i = at - 1; i >= 0; i--)
            {
                if (LintSources.IsComment(lines[i]))
                {
                    return true;
                }

                string code = LintSources.Code(lines[i]);
                if (code.Trim().Length == 0 || Field.IsMatch(code))
                {
                    continue;
                }

                return false;
            }

            return false;
        }

        private static void Walk(Action<string, string[], int> onField)
        {
            foreach (string file in LintSources.Under("soc-access/screens/"))
            {
                string[] lines = LintSources.Lines(file);
                bool derives = false;
                foreach (string line in lines)
                {
                    if (!LintSources.IsComment(line))
                    {
                        derives = derives || Derives.IsMatch(LintSources.Code(line));
                    }
                }

                if (!derives)
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
                        onField(file, lines, i);
                    }
                }
            }
        }
    }
}
