using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// The machinery the source lints share: find the mod's sources on disk, read their block
    /// structure, and compare what they contain against a checked-in allowlist.
    ///
    /// A lint site is identified by its FILE plus the trimmed text of the line it sits on, never by
    /// a line number - a number drifts the moment anything above it is edited, and an allowlist that
    /// churns on every unrelated edit stops being read. Identical lines in one file are counted
    /// rather than listed twice, so the count is the thing that has to move when another appears.
    ///
    /// The gate runs BOTH WAYS. A site the allowlist does not cover fails, and so does an allowlist
    /// entry no site answers to any more - a stale entry is not a harmless leftover, it is a
    /// standing pre-authorisation, and the first line of code that happens to match its text again
    /// is admitted without anybody deciding. The count is part of the entry, so an entry allowing
    /// more occurrences than the tree contains is the same fossil in miniature and fails the same
    /// way.
    ///
    /// Mechanical half of the remedy, once the why-comment is written:
    /// <c>SOCACCESS_LINT_REGENERATE=patches dotnet test</c> rewrites THAT allowlist in place, and
    /// the entry then shows up in the diff, which is the whole point of the file. One list at a
    /// time, named: rewriting all seven at once means a run made for one rule silently re-blesses
    /// whatever the other six happen to see.
    /// </summary>
    public static class LintSources
    {
        /// <summary>Set this in the environment to the NAME of the one allowlist to rewrite from the
        /// current tree - <c>patches</c>, with or without the <c>.allow</c>. Any other value
        /// rewrites nothing, so a typo leaves every list alone rather than rewriting the wrong
        /// one.</summary>
        public const string RegenerateVariable = "SOCACCESS_LINT_REGENERATE";

        /// <summary>The one sentence every gate's message ends in, once, as its last line.</summary>
        public const string ExceptionSentence =
            "Making an exception means a why-comment at the site, an entry in `{0}.allow`, and telling the owner in the handover before it merges. No silent exceptions.";

        private static string _root;

        /// <summary>Where the repository is. The tests run out of <c>bin/</c>, so the sources have to
        /// be found by walking up rather than by being copied next to the test assembly. One
        /// sentinel: the live mod project, the thing that makes this tree the mod's tree.</summary>
        public static string RepoRoot()
        {
            if (_root != null)
            {
                return _root;
            }

            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "soc-access", "soc-access.csproj")))
                {
                    _root = directory.FullName;
                    return _root;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "no soc-access\\soc-access.csproj above " + AppContext.BaseDirectory);
        }

        /// <summary>Every <c>.cs</c> file under <c>soc-access/</c> that a person wrote, as
        /// repository-relative paths with forward slashes, sorted so a regenerated allowlist has a
        /// stable order.
        ///
        /// <c>tests/</c> is out because a lint reads the mod, not itself. <c>obj/</c> and
        /// <c>bin/</c> are out, and that is correctness rather than tidiness: the generated
        /// <c>obj/Debug/*.AssemblyInfo.cs</c> exists only once the plugin has been built, so leaving
        /// it in makes what the lints see depend on whether somebody ran a build - the one thing an
        /// allowlist gate must never do. <c>loader/</c> is out because it is a separate plugin that
        /// never reloads and holds none of the state these rules are about. <c>dev/</c> stays in
        /// here and is skipped by the individual rules that say so.</summary>
        public static IList<string> ModSources()
        {
            string root = Path.Combine(RepoRoot(), "soc-access");
            List<string> relative = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string path = "soc-access/"
                    + file.Substring(root.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                if (path.Contains("/obj/")
                    || path.Contains("/bin/")
                    || path.StartsWith("soc-access/tests/", StringComparison.Ordinal)
                    || path.StartsWith("soc-access/loader/", StringComparison.Ordinal))
                {
                    continue;
                }

                relative.Add(path);
            }

            relative.Sort(StringComparer.Ordinal);
            Assert.AreNotEqual(0, relative.Count, "no mod sources found under soc-access/");
            return relative;
        }

        /// <summary>The sources under one folder, <c>soc-access/patches/</c> and the like.</summary>
        public static IList<string> Under(string folder)
        {
            List<string> chosen = new List<string>();
            foreach (string file in ModSources())
            {
                if (file.StartsWith(folder, StringComparison.Ordinal))
                {
                    chosen.Add(file);
                }
            }

            return chosen;
        }

        // Every lint fact sweeps every source, so the whole tree would otherwise be read off disk
        // once per fact. The tree does not change while a test run is in flight.
        private static readonly Dictionary<string, string[]> LineCache =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        public static string[] Lines(string relativePath)
        {
            lock (LineCache)
            {
                string[] lines;
                if (!LineCache.TryGetValue(relativePath, out lines))
                {
                    lines = File.ReadAllLines(Path.Combine(
                        RepoRoot(),
                        relativePath.Replace('/', Path.DirectorySeparatorChar)));
                    LineCache[relativePath] = lines;
                }

                return lines;
            }
        }

        /// <summary>A line that carries no code: a <c>//</c> comment, a doc comment, or the
        /// continuation of a block comment. Prose about a static field is not one.</summary>
        public static bool IsComment(string line)
        {
            string text = line.Trim();
            return text.StartsWith("//", StringComparison.Ordinal)
                || text.StartsWith("*", StringComparison.Ordinal)
                || text.StartsWith("/*", StringComparison.Ordinal);
        }

        /// <summary>The line with its string and character literals blanked and its trailing
        /// <c>//</c> comment cut, so that a brace or a parenthesis inside a message does not move the
        /// structure. Literals are blanked rather than removed so column positions survive.</summary>
        public static string Code(string line)
        {
            StringBuilder code = new StringBuilder(line.Length);
            bool inString = false;
            bool inChar = false;
            bool verbatim = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inString && !inChar && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    break;
                }

                if (!inString && !inChar && c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    inString = true;
                    verbatim = true;
                    code.Append(' ');
                    code.Append(' ');
                    i++;
                    continue;
                }

                if (!inString && !inChar && c == '"')
                {
                    inString = true;
                    verbatim = false;
                    code.Append('"');
                    continue;
                }

                if (inString)
                {
                    if (!verbatim && c == '\\' && i + 1 < line.Length)
                    {
                        code.Append(' ');
                        code.Append(' ');
                        i++;
                        continue;
                    }

                    if (c == '"')
                    {
                        if (verbatim && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            code.Append(' ');
                            code.Append(' ');
                            i++;
                            continue;
                        }

                        inString = false;
                        code.Append('"');
                        continue;
                    }

                    code.Append(' ');
                    continue;
                }

                if (!inChar && c == '\'')
                {
                    inChar = true;
                    code.Append('\'');
                    continue;
                }

                if (inChar)
                {
                    if (c == '\\' && i + 1 < line.Length)
                    {
                        code.Append(' ');
                        code.Append(' ');
                        i++;
                        continue;
                    }

                    if (c == '\'')
                    {
                        inChar = false;
                        code.Append('\'');
                        continue;
                    }

                    code.Append(' ');
                    continue;
                }

                code.Append(c);
            }

            return code.ToString();
        }

        public static void Add(IDictionary<Site, int> found, string file, string line)
        {
            Site site = new Site(file, line.Trim());
            int count;
            found[site] = found.TryGetValue(site, out count) ? count + 1 : 1;
        }

        /// <summary>Reads an allowlist. Blank lines and <c>#</c> lines are ignored; every other line
        /// is <c>path | count | trimmed source line</c>, and the source text may itself contain
        /// <c>|</c>, so only the first two separators split. <paramref name="kinded"/> lists carry a
        /// trailing <c>| kind</c> as well, split off at the LAST separator.</summary>
        public static IList<AllowEntry> Entries(string allowlist, bool kinded)
        {
            List<AllowEntry> entries = new List<AllowEntry>();
            string path = AllowlistPath(allowlist);
            if (!File.Exists(path))
            {
                return entries;
            }

            foreach (string line in File.ReadAllLines(path))
            {
                string text = line.Trim();
                if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string kind = null;
                if (kinded)
                {
                    int last = text.LastIndexOf('|');
                    if (last > 0)
                    {
                        kind = text.Substring(last + 1).Trim();
                        text = text.Substring(0, last).Trim();
                    }
                }

                int file = text.IndexOf('|');
                Assert.IsTrue(file > 0, allowlist + ": malformed entry: " + text);
                int count = text.IndexOf('|', file + 1);
                Assert.IsTrue(count > file, allowlist + ": malformed entry: " + text);

                entries.Add(new AllowEntry(
                    new Site(text.Substring(0, file).Trim(), text.Substring(count + 1).Trim()),
                    int.Parse(text.Substring(file + 1, count - file - 1).Trim()),
                    kind));
            }

            return entries;
        }

        public static Dictionary<Site, int> Allowed(string allowlist)
        {
            return Allowed(allowlist, false);
        }

        public static Dictionary<Site, int> Allowed(string allowlist, bool kinded)
        {
            Dictionary<Site, int> counts = new Dictionary<Site, int>();
            foreach (AllowEntry entry in Entries(allowlist, kinded))
            {
                counts[entry.Site] = entry.Count;
            }

            return counts;
        }

        public static void AssertAllowed(string allowlist, IDictionary<Site, int> found, string rule)
        {
            AssertAllowed(allowlist, found, rule, false, null);
        }

        /// <summary>Rewrite this list now, if this run was asked to. A rule that reads its own list
        /// back - the inventory, which checks the kind on every entry - calls this first, so that the
        /// entries it then judges are the ones the regeneration just wrote rather than the ones it
        /// replaced.</summary>
        public static void Regenerate(
            string allowlist,
            IDictionary<Site, int> found,
            string rule,
            bool kinded)
        {
            if (Regenerating(allowlist))
            {
                Write(allowlist, found, rule, kinded ? Kinds(allowlist) : null);
            }
        }

        /// <summary>
        /// The one assertion every source lint ends in, and it is a set EQUALITY rather than a
        /// containment: every site found in the tree is covered by the allowlist at least as many
        /// times as it occurs, and every entry of the allowlist answers to a site that is still
        /// there.
        ///
        /// Unlisted sites, stale entries and whatever else the rule found wrong
        /// (<paramref name="extra"/>: the inventory's missing and refused kinds) go into ONE
        /// message, one line each, and the exception sentence closes it once. A hundred sites is a
        /// hundred lines and one sentence.
        /// </summary>
        public static void AssertAllowed(
            string allowlist,
            IDictionary<Site, int> found,
            string rule,
            bool kinded,
            IList<string> extra)
        {
            if (Regenerating(allowlist))
            {
                Write(allowlist, found, rule, kinded ? Kinds(allowlist) : null);
            }

            if (!File.Exists(AllowlistPath(allowlist)))
            {
                if (Armed.Contains(allowlist))
                {
                    Assert.Fail("soc-access/tests/Lint/" + allowlist + ".allow is armed and missing; restore it, it is the list this rule is read against.");
                }

                Assert.Inconclusive(allowlist + " is not armed yet: the rule lands with the step that empties its list (screen-resolution-plan.md). Regenerate to see the work list, then delete the list again.");
            }

            Dictionary<Site, int> allowed = Allowed(allowlist, kinded);
            List<string> unlisted = new List<string>();
            foreach (KeyValuePair<Site, int> entry in found)
            {
                int budget;
                if (!allowed.TryGetValue(entry.Key, out budget) || budget < entry.Value)
                {
                    unlisted.Add(entry.Key.File + ": " + entry.Key.Text
                        + (entry.Value > 1 ? "   (x" + entry.Value + ")" : string.Empty));
                }
            }

            unlisted.Sort(StringComparer.Ordinal);

            // The other direction. An entry standing over nothing is a pre-authorisation waiting for
            // the next line of code to match its text, and one allowing more occurrences than the
            // tree has is the same thing for the next copy of a line that is already there.
            List<string> stale = new List<string>();
            foreach (KeyValuePair<Site, int> entry in allowed)
            {
                int occurrences;
                if (!found.TryGetValue(entry.Key, out occurrences))
                {
                    stale.Add(entry.Key.File + ": " + entry.Key.Text);
                }
                else if (entry.Value > occurrences)
                {
                    stale.Add(entry.Key.File + ": " + entry.Key.Text
                        + "   (allows " + entry.Value + ", tree has " + occurrences + ")");
                }
            }

            stale.Sort(StringComparer.Ordinal);

            if (unlisted.Count == 0 && stale.Count == 0 && (extra == null || extra.Count == 0))
            {
                return;
            }

            string bare = Bare(allowlist);
            StringBuilder message = new StringBuilder();
            message.Append(rule);
            if (unlisted.Count > 0)
            {
                message.Append(Environment.NewLine + Environment.NewLine);
                message.Append("Not in soc-access/tests/Lint/" + allowlist + ":");
                foreach (string site in unlisted)
                {
                    message.Append(Environment.NewLine + "  " + site);
                }
            }

            if (stale.Count > 0)
            {
                message.Append(Environment.NewLine + Environment.NewLine);
                message.Append("Stale in soc-access/tests/Lint/" + allowlist
                    + " - an entry no source site answers to is a standing pre-authorisation; prune it:");
                foreach (string site in stale)
                {
                    message.Append(Environment.NewLine + "  " + site);
                }
            }

            if (extra != null && extra.Count > 0)
            {
                message.Append(Environment.NewLine + Environment.NewLine);
                foreach (string problem in extra)
                {
                    message.Append(problem + Environment.NewLine);
                }

                message.Length -= Environment.NewLine.Length;
            }

            message.Append(Environment.NewLine + Environment.NewLine);
            message.Append("Re-run with " + RegenerateVariable + "=" + bare
                + " to rewrite this list from the tree.");
            message.Append(Environment.NewLine);
            message.Append(string.Format(ExceptionSentence, bare));
            Assert.Fail(message.ToString());
        }

        /// <summary>Whether this run was asked to rewrite THIS list. The variable carries the list's
        /// name rather than a flag, so a regeneration is always a decision about one rule.</summary>
        private static bool Regenerating(string allowlist)
        {
            string wanted = Environment.GetEnvironmentVariable(RegenerateVariable);
            if (string.IsNullOrEmpty(wanted))
            {
                return false;
            }

            wanted = wanted.Trim();
            return string.Equals(wanted, allowlist, StringComparison.OrdinalIgnoreCase)
                || string.Equals(wanted, Bare(allowlist), StringComparison.OrdinalIgnoreCase);
        }

        private static string Bare(string allowlist)
        {
            return allowlist.EndsWith(".allow", StringComparison.OrdinalIgnoreCase)
                ? allowlist.Substring(0, allowlist.Length - ".allow".Length)
                : allowlist;
        }

        /// <summary>The kinds the list already carries, so a regeneration keeps the words somebody
        /// wrote and only the new entries arrive without one - which is what fails until a person
        /// decides what they are.</summary>
        private static Dictionary<Site, string> Kinds(string allowlist)
        {
            Dictionary<Site, string> kinds = new Dictionary<Site, string>();
            foreach (AllowEntry entry in Entries(allowlist, true))
            {
                kinds[entry.Site] = entry.Kind;
            }

            return kinds;
        }

        private static void Write(
            string allowlist,
            IDictionary<Site, int> found,
            string rule,
            IDictionary<Site, string> kinds)
        {
            List<Site> sites = new List<Site>(found.Keys);
            sites.Sort(delegate (Site left, Site right)
            {
                int file = StringComparer.Ordinal.Compare(left.File, right.File);
                return file != 0 ? file : StringComparer.Ordinal.Compare(left.Text, right.Text);
            });

            List<string> lines = new List<string>();
            foreach (string sentence in rule.Split('\n'))
            {
                lines.Add("# " + sentence.Trim());
            }

            lines.Add("#");
            lines.Add("# Generated by " + RegenerateVariable + "=" + Bare(allowlist)
                + "; format: path | count | source line"
                + (kinds == null ? "." : " | kind."));
            lines.Add("#");
            string previous = null;
            foreach (Site site in sites)
            {
                if (site.File != previous)
                {
                    lines.Add(string.Empty);
                    previous = site.File;
                }

                string kind = null;
                if (kinds != null)
                {
                    kinds.TryGetValue(site, out kind);
                }

                lines.Add(site.File + " | " + found[site] + " | " + site.Text
                    + (kinds == null ? string.Empty : " | " + (kind ?? string.Empty)));
            }

            File.WriteAllLines(AllowlistPath(allowlist), lines.ToArray());
        }

        /// <summary>The lists whose rules are in force. A rule is armed by the step that empties its
        /// list (screen-resolution-plan.md): its name goes here and its file is committed, so that a
        /// list deleted later fails rather than silently disarming the rule. Until then the rule
        /// reports inconclusive and its regenerated output is a work list.</summary>
        public static readonly HashSet<string> Armed = new HashSet<string>(StringComparer.Ordinal)
        {
        };

        /// <summary>An armed rule's list exists; an unarmed one's may not.</summary>
        public static void AssertArmed(string allowlist)
        {
            if (Armed.Contains(allowlist))
            {
                Assert.IsTrue(
                    File.Exists(AllowlistPath(allowlist)),
                    "soc-access/tests/Lint/" + allowlist + ".allow is the list this rule is read against; it has to exist even while it is empty.");
            }
        }

        public static string AllowlistPath(string allowlist)
        {
            return Path.Combine(RepoRoot(), "soc-access", "tests", "Lint", allowlist);
        }

        // ---- block structure ----

        private static readonly Dictionary<string, Structure> StructureCache =
            new Dictionary<string, Structure>(StringComparer.Ordinal);

        public static Structure Read(string file)
        {
            lock (StructureCache)
            {
                Structure structure;
                if (!StructureCache.TryGetValue(file, out structure))
                {
                    structure = Structure.Of(Lines(file));
                    StructureCache[file] = structure;
                }

                return structure;
            }
        }
    }

    /// <summary>A lint site: the file it is in, and the trimmed text of its line.</summary>
    public struct Site : IEquatable<Site>
    {
        public readonly string File;

        public readonly string Text;

        public Site(string file, string text)
        {
            File = file;
            Text = text;
        }

        public bool Equals(Site other)
        {
            return string.Equals(File, other.File, StringComparison.Ordinal)
                && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Site && Equals((Site)obj);
        }

        public override int GetHashCode()
        {
            return File.GetHashCode() ^ Text.GetHashCode();
        }
    }

    /// <summary>One allowlist line, parsed.</summary>
    public struct AllowEntry
    {
        public readonly Site Site;

        public readonly int Count;

        /// <summary>The trailing word on a kinded list (the patch inventory), else null.</summary>
        public readonly string Kind;

        public AllowEntry(Site site, int count, string kind)
        {
            Site = site;
            Count = count;
            Kind = kind;
        }
    }

    /// <summary>
    /// Which type and which member every line of a file sits in.
    ///
    /// THE HEURISTIC. C# is not parsed here; braces are counted. Walking the file top to bottom, a
    /// running stack of open blocks is kept. A line is first attributed to the stack as it stands
    /// BEFORE that line's braces - so a member's own signature line reads as type scope, which is
    /// what it is. Then, if the line looks like a type or member declaration, it is remembered as
    /// PENDING; the next <c>{</c> opens that block, and every other <c>{</c> opens an anonymous one.
    /// A <c>}</c> closes the innermost. Pending survives lines with no braces, which is what carries
    /// a signature wrapped over three lines to the brace on the fourth.
    ///
    /// String and character literals are blanked first (<see cref="LintSources.Code"/>) so a brace
    /// inside a message counts for nothing. What this cannot see: an expression-bodied member
    /// (<c>=&gt;</c>) opens no block, so its line reads as type scope and its body is not a member
    /// body; a verbatim string spanning lines would desync the count. Neither appears in the folders
    /// these rules read, and both would show up as a site in the wrong member rather than as a site
    /// silently lost.
    /// </summary>
    public sealed class Structure
    {
        /// <summary>The enclosing member's name per line, or null at type or file scope.</summary>
        public string[] Member;

        /// <summary>The enclosing member's declaration line, trimmed, or null.</summary>
        public string[] Signature;

        /// <summary>The enclosing type's name per line, or null at file scope.</summary>
        public string[] Type;

        /// <summary>Whether the line sits directly in a type body rather than in a member.</summary>
        public bool[] AtTypeScope;

        private static readonly Regex TypeDeclaration = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|sealed|abstract|partial|readonly|unsafe|new)\s+)*(?:class|struct|interface|enum)\s+(\w+)");

        // A member: modifiers, then whatever the return type is, then the name and its opening
        // parenthesis. Non-greedy up to the FIRST parenthesis, and nothing may cross an `=` or a
        // `;`, which is what keeps a field initialiser holding a call from reading as a member.
        private static readonly Regex MemberDeclaration = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal|static|virtual|override|abstract|sealed|async|extern|unsafe|new|partial)\s[^=;{}]*?(\w+)\s*(?:<[^<>()]*>)?\s*\(");

        public static Structure Of(string[] lines)
        {
            Structure structure = new Structure
            {
                Member = new string[lines.Length],
                Signature = new string[lines.Length],
                Type = new string[lines.Length],
                AtTypeScope = new bool[lines.Length],
            };

            List<Frame> stack = new List<Frame>();
            Frame pending = null;
            for (int i = 0; i < lines.Length; i++)
            {
                Frame member = Innermost(stack, "member");
                Frame type = Innermost(stack, "type");
                structure.Member[i] = member == null ? null : member.Name;
                structure.Signature[i] = member == null ? null : member.Signature;
                structure.Type[i] = type == null ? null : type.Name;
                structure.AtTypeScope[i] = type != null && member == null
                    && ReferenceEquals(Innermost(stack, null), type);

                string code = LintSources.Code(lines[i]);
                if (!LintSources.IsComment(lines[i]))
                {
                    Match declared = TypeDeclaration.Match(code);
                    if (declared.Success)
                    {
                        pending = new Frame("type", declared.Groups[1].Value, lines[i].Trim());
                    }
                    else
                    {
                        Match method = MemberDeclaration.Match(code);
                        if (method.Success)
                        {
                            pending = new Frame("member", method.Groups[1].Value, lines[i].Trim());
                        }
                    }
                }

                foreach (char c in code)
                {
                    if (c == '{')
                    {
                        stack.Add(pending ?? new Frame("other", null, null));
                        pending = null;
                    }
                    else if (c == '}' && stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }
                }
            }

            return structure;
        }

        private static Frame Innermost(List<Frame> stack, string kind)
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (kind == null || stack[i].Kind == kind)
                {
                    return stack[i];
                }
            }

            return null;
        }

        private sealed class Frame
        {
            public readonly string Kind;

            public readonly string Name;

            public readonly string Signature;

            public Frame(string kind, string name, string signature)
            {
                Kind = kind;
                Name = name;
                Signature = signature;
            }
        }
    }
}
