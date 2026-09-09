using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// A PATCH MAY WRITE ONLY ANNOUNCEMENT-SIDE MEMBERS.
    ///
    /// A hook delivers an event or alters game behaviour. It is never the source of truth for
    /// anything a <c>Build</c>, an <c>IsActive</c>, a <c>ScreenName</c> or a tooltip reads, and
    /// losing one call may cost one announcement and nothing else (AGENTS.md, Screen Resolution).
    /// The shape that breaks that is a hook writing into a screen or an adapter - a counter a build
    /// keys a cache on, a stage an <c>IsActive</c> reads - because a hot reload or a missed call then
    /// leaves the two disagreeing.
    ///
    /// So: a line in <c>soc-access/patches/</c> that CALLS a member of a type declared under
    /// <c>screens/</c> or <c>adapters/</c>, or assigns to one, is a site unless that member's
    /// declaration carries <see cref="HookWritableAttribute"/>. Reading a property or a field is
    /// free - a read cannot be state pushed in.
    ///
    /// THE RECEIVER IS RESOLVED, NOT GUESSED. Only <c>X.Member</c> where <c>X</c> is a type declared
    /// under those two folders, or a local whose declared type is one, counts. Matching the bare
    /// member name instead - the rule's first shape - made 28 of its 52 sites collisions:
    /// <c>GameText.Get</c> flagged because an adapter happens to declare a <c>Get</c>,
    /// <c>seen.Add</c> because a narrator declares an <c>Add</c>. A rule whose output is mostly noise
    /// is a rule nobody reads.
    ///
    /// The allowlist is therefore expected to be EMPTY: the permitted surface is the set of
    /// <c>[HookWritable]</c> declarations, which is greppable and survives a rename, and an entry
    /// here is a write somebody deliberately left unmarked.
    /// </summary>
    [TestClass]
    public class HookCalledMemberLintTests
    {
        private const string Allowlist = "hook-called-members.allow";

        private const string Rule =
            "A patch may write only announcement-side members: a call to - or an assignment to - a member of a type declared under screens/ or adapters/ needs [HookWritable] on that member's declaration.\n"
            + "Reads are free. A hook is never the source of truth for anything a Build, IsActive, ScreenName or tooltip reads (AGENTS.md, Screen Resolution).";

        /// <summary>The slot's own vocabulary, and how a screen says whether it is the one. None of
        /// these is state pushed in, and none is worth a mark on every screen that has it.</summary>
        private static readonly HashSet<string> Exempt = new HashSet<string>(StringComparer.Ordinal)
        {
            "IsPresent",
            "Matches",
            "Live",
            "SourceKey",
            "Dispose",
        };

        /// <summary>A namespace on a using or namespace line is not a call into a screen.</summary>
        private static readonly Regex Directive = new Regex(@"^\s*(using|namespace)\s");

        /// <summary><c>X.Member</c>, with the null-conditional and any whitespace between them, and
        /// whatever follows on the line - which is what says whether this is a call, a write or a
        /// read.</summary>
        private static readonly Regex Access = new Regex(@"\b(\w+)\s*\??\s*\.\s*(\w+)");

        /// <summary>What may follow the member and still be a read: anything that is not a call's
        /// parenthesis, an assignment, or an increment.</summary>
        private static readonly Regex Call = new Regex(@"^\s*(?:<[^<>()]*>)?\s*\(");

        private static readonly Regex Write = new Regex(@"^\s*(?:\+\+|--|(?:[+\-*/|&^]|\?\?|<<|>>)?=(?!=))");

        /// <summary>A local whose declared type is a mod screen or adapter:
        /// <c>ChatAdapter adapter = ...</c>, <c>ChatScreen screen;</c>. The declared type is what the
        /// receiver resolves to; <c>var</c> is deliberately not followed, so a receiver the rule
        /// cannot name is a receiver it does not judge.</summary>
        private static readonly Regex Local = new Regex(@"\b([A-Z]\w*)\s+(\w+)\s*(?:=(?!=)|;|\))");

        /// <summary>A type declared in a file: the shapes <c>Structure</c> reads as a type.</summary>
        private static readonly Regex TypeDeclaration = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|sealed|abstract|partial|readonly|unsafe|new)\s+)*(?:class|struct|interface|enum)\s+(\w+)");

        private static readonly Regex Marked = new Regex(@"\[\s*HookWritable\s*\]");

        /// <summary>The name a declaration line introduces, method or property or field.</summary>
        private static readonly Regex Method = new Regex(@"^\s*(?:public|internal|protected)\s[^=;{}]*?(\w+)\s*(?:<[^<>()]*>)?\s*\(");

        private static readonly Regex Word = new Regex(@"\w+");

        [TestMethod]
        public void EveryHookWriteIsMarkedOrOnTheAllowlist()
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
            HashSet<string> modTypes = ModTypes();
            HashSet<string> marked = Marks();

            Dictionary<Site, int> found = new Dictionary<Site, int>();
            foreach (string file in LintSources.Under("soc-access/patches/"))
            {
                string[] lines = LintSources.Lines(file);
                Dictionary<string, string> locals = Locals(lines, modTypes);
                foreach (string line in lines)
                {
                    if (LintSources.IsComment(line))
                    {
                        continue;
                    }

                    string code = LintSources.Code(line);
                    if (Directive.IsMatch(code) || !Reaches(code, modTypes, locals, marked))
                    {
                        continue;
                    }

                    LintSources.Add(found, file, line);
                }
            }

            return found;
        }

        /// <summary>Whether this line calls or assigns an unmarked member of a mod screen or
        /// adapter.</summary>
        private static bool Reaches(
            string code,
            HashSet<string> modTypes,
            Dictionary<string, string> locals,
            HashSet<string> marked)
        {
            foreach (Match match in Access.Matches(code))
            {
                string receiver = match.Groups[1].Value;
                string member = match.Groups[2].Value;
                string type;
                if (modTypes.Contains(receiver))
                {
                    type = receiver;
                }
                else if (!locals.TryGetValue(receiver, out type))
                {
                    continue;
                }

                if (Exempt.Contains(member) || marked.Contains(type + "." + member))
                {
                    continue;
                }

                string rest = code.Substring(match.Index + match.Length);
                if (Call.IsMatch(rest) || Write.IsMatch(rest))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Every type declared under <c>screens/</c> or <c>adapters/</c>, by name.</summary>
        private static HashSet<string> ModTypes()
        {
            HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Homes())
            {
                foreach (string line in LintSources.Lines(file))
                {
                    if (LintSources.IsComment(line))
                    {
                        continue;
                    }

                    Match declared = TypeDeclaration.Match(LintSources.Code(line));
                    if (declared.Success)
                    {
                        types.Add(declared.Groups[1].Value);
                    }
                }
            }

            return types;
        }

        /// <summary>Every <c>Type.Member</c> whose declaration under <c>screens/</c> or
        /// <c>adapters/</c> carries <c>[HookWritable]</c> - the announcement side, as marked.</summary>
        private static HashSet<string> Marks()
        {
            HashSet<string> marks = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Homes())
            {
                string[] lines = LintSources.Lines(file);
                Structure structure = LintSources.Read(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LintSources.IsComment(lines[i]) || !Marked.IsMatch(lines[i]))
                    {
                        continue;
                    }

                    // The attribute sits on its own line above the declaration, or ahead of it on the
                    // same one. Either way the declaration is the next line that names something.
                    for (int j = i; j < lines.Length && j <= i + 4; j++)
                    {
                        string name = Name(LintSources.Code(lines[j]));
                        if (name == null)
                        {
                            continue;
                        }

                        string type = structure.Type[j];
                        if (type != null)
                        {
                            marks.Add(type + "." + name);
                        }

                        break;
                    }
                }
            }

            return marks;
        }

        /// <summary>The name a declaration line introduces: a method's, or the last identifier before
        /// a property's brace, a field's initialiser or its semicolon.</summary>
        private static string Name(string code)
        {
            if (Marked.IsMatch(code))
            {
                code = Marked.Replace(code, string.Empty);
            }

            if (code.Trim().Length == 0)
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
            if (head.IndexOf('(') >= 0 || head.Trim().Length == 0)
            {
                return null;
            }

            string last = null;
            foreach (Match word in Word.Matches(head))
            {
                last = word.Value;
            }

            return last;
        }

        /// <summary>Per patch file, the locals whose declared type is a mod screen or adapter.</summary>
        private static Dictionary<string, string> Locals(string[] lines, HashSet<string> modTypes)
        {
            Dictionary<string, string> locals = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                if (LintSources.IsComment(line))
                {
                    continue;
                }

                foreach (Match match in Local.Matches(LintSources.Code(line)))
                {
                    string type = match.Groups[1].Value;
                    if (modTypes.Contains(type))
                    {
                        locals[match.Groups[2].Value] = type;
                    }
                }
            }

            return locals;
        }

        /// <summary>Where an announcement-side member may be declared.</summary>
        private static IList<string> Homes()
        {
            List<string> homes = new List<string>(LintSources.Under("soc-access/screens/"));
            foreach (string file in LintSources.Under("soc-access/adapters/"))
            {
                homes.Add(file);
            }

            return homes;
        }
    }
}
