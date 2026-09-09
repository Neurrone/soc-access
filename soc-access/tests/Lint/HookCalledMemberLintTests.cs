using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// A screen or adapter member that only a hook ever calls is a member the screen cannot reach
    /// for itself, and that is the shape of hook-written state: the detector or a patch pushes
    /// something in, a per-frame <c>Build</c> reads it back out, and a hot reload or a missed call
    /// leaves the two disagreeing. A member a hook calls is fine as long as the screen's own
    /// resolution path - its constructor, its <c>Find...</c>, <c>Resolve...</c>, <c>Source...</c> or
    /// <c>Recover</c> - names it too, because then the screen can arrive at the same answer from the
    /// game (AGENTS.md, Screen Resolution).
    ///
    /// The slot's own vocabulary is exempt: <c>Show</c>, <c>Forget</c>, <c>IsPresent</c>,
    /// <c>Matches</c>, <c>Live</c> and <c>SourceKey</c> are how a handler hands a menu over and how a
    /// screen answers whether it is the one, and a constructor is not called by name.
    ///
    /// The site is the line in the detector or the patch that makes the call - that is where the
    /// decision to reach in was taken.
    /// </summary>
    [TestClass]
    public class HookCalledMemberLintTests
    {
        private const string Allowlist = "hook-called-members.allow";

        private const string Detector = "soc-access/screens/ScreenDetector.cs";

        private const string Rule =
            "A screen or adapter member a hook calls has to be reachable from the screen's own resolution path too - its constructor, Find, Resolve, Source or Recover.\n"
            + "A member only a hook ever calls is state pushed in from outside, which a hot reload or a missed call then loses (AGENTS.md, Screen Resolution).";

        /// <summary>How a handler hands a menu over, and how a screen says whether it is the one.
        /// None of these is state pushed in.</summary>
        private static readonly HashSet<string> Exempt = new HashSet<string>(StringComparer.Ordinal)
        {
            "Show",
            "Forget",
            "IsPresent",
            "Matches",
            "Live",
            "SourceKey",
        };

        /// <summary>The members that resolve a screen's menu for itself.</summary>
        private static readonly Regex Resolution = new Regex(@"^(Find|Resolve|Source)|^Recover$");

        private static readonly Regex Reference = new Regex(@"\.(\w+)");

        /// <summary>A namespace on a using or namespace line is not a call into a screen.</summary>
        private static readonly Regex Directive = new Regex(@"^\s*(using|namespace)\s");

        private static readonly Regex Word = new Regex(@"\w+");

        private static readonly Regex Method = new Regex(@"^\s*public\s[^=;{}]*?(\w+)\s*(?:<[^<>()]*>)?\s*\(");

        private static readonly Regex Declaration = new Regex(@"^\s*public\s");

        private static readonly Regex TypeDeclaration = new Regex(@"\b(class|struct|interface|enum|delegate)\s+\w+");

        [TestMethod]
        public void EveryHookCalledMemberIsOnTheAllowlist()
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
            Dictionary<string, List<string>> declared = Declared();
            Dictionary<string, HashSet<string>> resolution = Resolutions();

            Dictionary<Site, int> found = new Dictionary<Site, int>();
            foreach (string file in Callers())
            {
                foreach (string line in LintSources.Lines(file))
                {
                    if (LintSources.IsComment(line))
                    {
                        continue;
                    }

                    string code = LintSources.Code(line);
                    if (Directive.IsMatch(code))
                    {
                        continue;
                    }

                    bool reaches = false;
                    foreach (Match match in Reference.Matches(code))
                    {
                        string name = match.Groups[1].Value;
                        List<string> homes;
                        if (Exempt.Contains(name) || !declared.TryGetValue(name, out homes))
                        {
                            continue;
                        }

                        if (!Reachable(resolution, homes, name))
                        {
                            reaches = true;
                            break;
                        }
                    }

                    if (reaches)
                    {
                        LintSources.Add(found, file, line);
                    }
                }
            }

            return found;
        }

        /// <summary>Whether any file declaring this member also names it from that type's own
        /// resolution path.</summary>
        private static bool Reachable(
            Dictionary<string, HashSet<string>> resolution,
            List<string> homes,
            string name)
        {
            foreach (string home in homes)
            {
                HashSet<string> named;
                if (resolution.TryGetValue(home, out named) && named.Contains(name))
                {
                    return true;
                }
            }

            return false;
        }

        private static IList<string> Callers()
        {
            List<string> callers = new List<string> { Detector };
            foreach (string file in LintSources.Under("soc-access/patches/"))
            {
                callers.Add(file);
            }

            return callers;
        }

        /// <summary>Every public member declared under screens/ or adapters/, by name, with the files
        /// that declare it. Constructors are left out: nothing calls one by name.</summary>
        private static Dictionary<string, List<string>> Declared()
        {
            Dictionary<string, List<string>> declared =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string file in Homes())
            {
                string[] lines = LintSources.Lines(file);
                Structure structure = LintSources.Read(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LintSources.IsComment(lines[i]) || !structure.AtTypeScope[i])
                    {
                        continue;
                    }

                    string name = Name(LintSources.Code(lines[i]));
                    if (name == null
                        || Exempt.Contains(name)
                        || string.Equals(name, structure.Type[i], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    List<string> homes;
                    if (!declared.TryGetValue(name, out homes))
                    {
                        homes = new List<string>();
                        declared[name] = homes;
                    }

                    if (!homes.Contains(file))
                    {
                        homes.Add(file);
                    }
                }
            }

            return declared;
        }

        /// <summary>The name a public declaration line introduces: a method's, or the last
        /// identifier before the property's brace, the field's initialiser or its
        /// semicolon.</summary>
        private static string Name(string code)
        {
            if (!Declaration.IsMatch(code) || TypeDeclaration.IsMatch(code))
            {
                return null;
            }

            Match method = Method.Match(code);
            if (method.Success)
            {
                return method.Groups[1].Value;
            }

            int end = code.Length;
            foreach (char stop in new[] { '{', '=', ';' })
            {
                int at = code.IndexOf(stop);
                if (at >= 0 && at < end)
                {
                    end = at;
                }
            }

            string head = code.Substring(0, end);
            if (head.IndexOf('(') >= 0)
            {
                return null;
            }

            string last = null;
            foreach (Match word in Word.Matches(head))
            {
                last = word.Value;
            }

            return string.Equals(last, "public", StringComparison.Ordinal) ? null : last;
        }

        /// <summary>Per screen or adapter file, the names its own resolution path mentions - what a
        /// constructor, a <c>Find...</c>, a <c>Resolve...</c>, a <c>Source...</c> or a
        /// <c>Recover</c> reads or writes.</summary>
        private static Dictionary<string, HashSet<string>> Resolutions()
        {
            Dictionary<string, HashSet<string>> resolution =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string file in Homes())
            {
                HashSet<string> named = new HashSet<string>(StringComparer.Ordinal);
                string[] lines = LintSources.Lines(file);
                Structure structure = LintSources.Read(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string member = structure.Member[i];
                    if (LintSources.IsComment(lines[i]) || member == null)
                    {
                        continue;
                    }

                    if (!Resolution.IsMatch(member)
                        && !string.Equals(member, structure.Type[i], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (Match word in Word.Matches(LintSources.Code(lines[i])))
                    {
                        named.Add(word.Value);
                    }
                }

                resolution[file] = named;
            }

            return resolution;
        }

        /// <summary>Where a hook-called member may be declared. The detector itself is not one of
        /// them although it sits under screens/: its <c>On...</c> handlers are the event entry points
        /// the patches are supposed to call, which is the one thing this rule is not about - it is
        /// the caller's side of the same rule.</summary>
        private static IList<string> Homes()
        {
            List<string> homes = new List<string>();
            foreach (string file in LintSources.Under("soc-access/screens/"))
            {
                if (string.Equals(file, Detector, StringComparison.Ordinal))
                {
                    continue;
                }

                homes.Add(file);
            }

            foreach (string file in LintSources.Under("soc-access/adapters/"))
            {
                homes.Add(file);
            }

            return homes;
        }
    }
}
