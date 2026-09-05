using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Mono.CSharp;

namespace SongsOfConquestAccess.Loader.Dev
{
    /// <summary>
    /// A boolean expression compiled once by <see cref="CSharpEvaluator"/> and then invoked as
    /// many times as POST /wait needs it - once per frame, for as long as the wait lasts.
    /// Compiling per frame would cost more than the game does.
    ///
    /// A failed compile is still a <see cref="CompiledPredicate"/>, with <see cref="Error"/> set
    /// and nothing to invoke, so the caller has one thing to inspect either way.
    /// </summary>
    internal sealed class CompiledPredicate
    {
        private readonly CompiledMethod _method;

        internal CompiledPredicate(CompiledMethod method, string error)
        {
            _method = method;
            Error = error;
        }

        /// <summary>Why the expression did not compile, or null when it did.</summary>
        public string Error { get; private set; }

        /// <summary>Main thread only, like everything else the evaluator produces.</summary>
        public bool Invoke()
        {
            object value = null;
            _method(ref value);
            if (!(value is bool))
            {
                throw new InvalidOperationException(
                    "the expression produced no boolean value; it is a statement, not a condition"
                );
            }

            return (bool)value;
        }
    }

    /// <summary>
    /// A C# REPL over the running game, behind POST /eval. One evaluator lives for one mod load,
    /// so variables and usings declared by one request are still there for the next, and a hot
    /// reload replaces it wholesale - see DevServer.RebindModAssembly for why nothing less will
    /// do, and note that REPL state does not survive a reload.
    ///
    /// Compiling is Mono.CSharp (vendor\mcs\mcs.dll, the sinai-dev/mcs-unity net35 build that
    /// UnityExplorer uses). Its diagnostics do not come back from the call: they are printed to a
    /// <see cref="ReportPrinter"/>, so this holds its own printer over a StringWriter and reads
    /// the text back out after each attempt.
    ///
    /// Main thread only - evaluated code runs inline, and reaching game state is the whole point.
    /// </summary>
    internal sealed class CSharpEvaluator
    {
        internal sealed class Result
        {
            public bool Ok;

            /// <summary>ToString of what the source evaluated to; null when it was a statement or
            /// a void call, which produce no value.</summary>
            public string Value;

            public string Error;

            public static Result Failed(string error)
            {
                return new Result { Error = error };
            }
        }

        // What evaluated code can name beyond the compiler's own defaults. Everything here is
        // already in the process; the REPL is for driving the live game, not for compiling
        // against things it has never loaded.
        //
        // The BCL is deliberately absent. Mono.CSharp imports mscorlib, System and System.Core
        // itself on the first compile, and importing an assembly it has already taken registers
        // every type in it a second time. Duplicate types go unnoticed - the namespace keeps the
        // first - but duplicate *extension* methods all stay in scope, so every LINQ call came
        // back as CS0121, ambiguous between two identical System.Linq.Enumerable overloads.
        private static readonly string[] ReferencedAssemblies =
        {
            "UnityEngine",
            "UnityEngine.CoreModule",
            "UnityEngine.ImageConversionModule",
            "UnityEngine.UIModule",
            "Assembly-CSharp",
            "Lavapotion.SongsOfConquest.GameLogicLayer.Runtime",
            "Lavapotion.SongsOfConquest.ProjectInstaller.Runtime",
            "Lavapotion.SongsOfConquest.UILayer.Runtime",
            "Lavapotion.SongsOfConquest.UtilitiesLayer.Runtime",
            "Lavapotion.Utilities",
            "Lavapotion.Networking",
            "Lavapotion.Pathfinding",
            "Zenject",
            "Unity.TextMeshPro",
            "Unity.InputSystem",
            "Newtonsoft.Json",
        };

        private static readonly string[] InitialUsings =
        {
            "using System;",
            "using System.Collections.Generic;",
            "using System.Linq;",
            "using UnityEngine;",
        };

        private readonly StringWriter _messages = new StringWriter(CultureInfo.InvariantCulture);
        private readonly StreamReportPrinter _printer;
        private readonly Evaluator _evaluator;

        public CSharpEvaluator()
        {
            _printer = new StreamReportPrinter(_messages);
            CompilerSettings settings = new CompilerSettings
            {
                Version = LanguageVersion.Experimental,
                Target = Target.Library,
                TargetExt = ".dll",
                GenerateDebugInfo = false,
                WarningLevel = 0,
                EnhancedWarnings = false,
            };
            _evaluator = new Evaluator(new CompilerContext(settings, _printer));

            foreach (Assembly assembly in Loaded())
            {
                Reference(assembly);
            }

            Reference(typeof(CSharpEvaluator).Assembly);

            // The first compile is also what loads the compiler's own default references, so any
            // complaint about those would otherwise come back as the first request's error text.
            foreach (string directive in InitialUsings)
            {
                _evaluator.Run(directive);
            }

            Warm();
            Clear();
        }

        public void Reference(Assembly assembly)
        {
            try
            {
                _evaluator.ReferenceAssembly(assembly);
            }
            catch (Exception e)
            {
                LoaderLog.Warn(
                    "eval: could not reference " + assembly.GetName().Name + ": " + e.Message
                );
            }
        }

        public Result Evaluate(string source)
        {
            Clear();

            object value;
            bool valueSet;
            string incomplete;
            try
            {
                incomplete = _evaluator.Evaluate(source, out value, out valueSet);
            }
            catch (Exception e)
            {
                return Result.Failed(e.ToString());
            }

            if (incomplete != null)
            {
                return Result.Failed(
                    "incomplete input: this is not a whole statement or expression"
                );
            }

            if (_printer.ErrorsCount > 0)
            {
                return Result.Failed(Messages("the source did not compile"));
            }

            return new Result { Ok = true, Value = valueSet ? Describe(value) : null };
        }

        /// <summary>
        /// Compile <paramref name="expression"/> into something POST /wait can ask every frame.
        /// The cast pins the result to a bool at compile time, so a caller who sends a statement
        /// or a non-boolean expression is told now rather than one frame at a time.
        /// </summary>
        public CompiledPredicate CompilePredicate(string expression)
        {
            Clear();

            CompiledMethod method;
            string incomplete;
            try
            {
                incomplete = _evaluator.Compile(
                    "(bool)(" + expression.Trim().TrimEnd(';') + ")",
                    out method
                );
            }
            catch (Exception e)
            {
                return new CompiledPredicate(null, e.ToString());
            }

            if (incomplete != null)
            {
                return new CompiledPredicate(
                    null,
                    "incomplete input: this is not a whole boolean expression"
                );
            }

            if (method == null || _printer.ErrorsCount > 0)
            {
                return new CompiledPredicate(null, Messages("the expression did not compile"));
            }

            return new CompiledPredicate(method, null);
        }

        /// <summary>
        /// Take both routes' compile paths for a walk before anyone is waiting on them. Emitting a
        /// value and emitting a method are separately expensive the first time an evaluator does
        /// them - the dynamic assembly appears, and a good deal of the compiler gets jitted - and
        /// this class is built during a hot reload, where a frame may run long but nothing is
        /// blocked on a five-second budget. Left to the first request instead, that cost lands on
        /// a main-thread job that answers 503 when it overruns.
        /// </summary>
        private void Warm()
        {
            try
            {
                object value;
                bool valueSet;
                _evaluator.Evaluate("0;", out value, out valueSet);

                CompiledMethod method;
                _evaluator.Compile("(bool)(true)", out method);
            }
            catch (Exception e)
            {
                LoaderLog.Warn("eval: warm-up failed, the first request will be slower: " + e.Message);
            }
        }

        private static List<Assembly> Loaded()
        {
            List<Assembly> found = new List<Assembly>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (Array.IndexOf(ReferencedAssemblies, assembly.GetName().Name) >= 0)
                {
                    found.Add(assembly);
                }
            }

            return found;
        }

        private static string Describe(object value)
        {
            if (value == null)
            {
                return "null";
            }

            try
            {
                return value.ToString();
            }
            catch (Exception e)
            {
                return value.GetType().FullName + " (ToString threw: " + e.Message + ")";
            }
        }

        private string Messages(string fallback)
        {
            string text = _messages.ToString().Trim();
            return text.Length == 0 ? fallback : text;
        }

        private void Clear()
        {
            _messages.GetStringBuilder().Length = 0;
            _printer.Reset();
        }
    }
}
