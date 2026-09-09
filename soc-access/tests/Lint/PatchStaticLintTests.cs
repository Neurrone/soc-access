using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// A patch class is reloaded with the mod but its statics are not: the mod is loaded from bytes
    /// into a fresh assembly, and whatever the previous load left in a static field is either gone
    /// or, worse, a live reference to a menu the game has since destroyed. So a patch class holds no
    /// static state without a <c>Reset</c> that <c>SocAccessMod.Stop</c> calls (AGENTS.md, Screen
    /// Resolution).
    ///
    /// A <c>readonly</c> collection counts as state: the field cannot be reassigned but the
    /// dictionary it points at fills up all the same, which is where the six instance sets the
    /// 2026-09-09 audit found were living.
    /// </summary>
    [TestClass]
    public class PatchStaticLintTests
    {
        private const string Allowlist = "patch-statics.allow";

        private const string Mod = "soc-access/SocAccessMod.cs";

        private const string Rule =
            "A static field in patches/ needs a Reset on its class that SocAccessMod.Stop calls - the mod reloads from bytes and the field does not.\n"
            + "A readonly collection counts: the field cannot be reassigned, but what it points at fills up with menus the game has destroyed (AGENTS.md, Screen Resolution).";

        /// <summary>A field at class scope, with or without an initialiser running on to the next
        /// line. No parenthesis and no brace: that is what separates a field from a method and from a
        /// property.</summary>
        private static readonly Regex Field = new Regex(
            @"^\s*(?:private|public|protected|internal)\s+[^()=;{}]*?(\w+)\s*(?:;|=(?!>))");

        private static readonly Regex Static = new Regex(@"\bstatic\b");

        private static readonly Regex Settled = new Regex(@"\b(readonly|const)\b");

        /// <summary>A readonly collection is still mutable state; these are the shapes patches/ uses
        /// to hold on to menus between calls.</summary>
        private static readonly Regex Collection = new Regex(
            @"\b(HashSet|Dictionary|List|ConditionalWeakTable)\s*<");

        private static readonly Regex Declares = new Regex(@"\bvoid\s+Reset\s*\(");

        [TestMethod]
        public void EveryUnresetPatchStaticIsOnTheAllowlist()
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
            string stop = string.Join("\n", LintSources.Lines(Mod));
            foreach (string file in LintSources.Under("soc-access/patches/"))
            {
                string[] lines = LintSources.Lines(file);
                Structure structure = LintSources.Read(file);
                HashSet<string> reset = ResetTypes(lines, structure, stop);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LintSources.IsComment(lines[i]) || !structure.AtTypeScope[i])
                    {
                        continue;
                    }

                    string code = LintSources.Code(lines[i]);
                    if (!Field.IsMatch(code) || !Static.IsMatch(code))
                    {
                        continue;
                    }

                    if (Settled.IsMatch(code) && !Collection.IsMatch(code))
                    {
                        continue;
                    }

                    if (structure.Type[i] != null && reset.Contains(structure.Type[i]))
                    {
                        continue;
                    }

                    LintSources.Add(found, file, lines[i]);
                }
            }

            return found;
        }

        /// <summary>The types in this file that declare a <c>Reset</c> AND are named with it from
        /// <c>SocAccessMod.cs</c>. A Reset nobody calls is not a reset.</summary>
        private static HashSet<string> ResetTypes(string[] lines, Structure structure, string stop)
        {
            HashSet<string> reset = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < lines.Length; i++)
            {
                if (LintSources.IsComment(lines[i]) || structure.Type[i] == null)
                {
                    continue;
                }

                if (!Declares.IsMatch(LintSources.Code(lines[i])))
                {
                    continue;
                }

                // A call or a method group handed to Step(): both name the type's Reset.
                if (Regex.IsMatch(stop, @"\b" + Regex.Escape(structure.Type[i]) + @"\.Reset\b"))
                {
                    reset.Add(structure.Type[i]);
                }
            }

            return reset;
        }
    }
}
