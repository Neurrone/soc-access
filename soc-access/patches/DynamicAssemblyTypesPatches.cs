using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SongsOfConquestAccess.Patches
{
    /// <summary>
    /// Development only: keeps the game's assembly scans working after a failed <c>/eval</c>.
    ///
    /// The dev server's REPL compiles each expression into a dynamic assembly in the game's own
    /// AppDomain. A compile that fails after parsing (a wrong namespace, a misspelled member) has
    /// already defined the expression's type there and never finishes it, and a dynamic assembly
    /// can never be unloaded. From then on Mono's <c>AssemblyBuilder.GetTypes</c> throws
    /// <see cref="ReflectionTypeLoadException"/> ("Type '&lt;InteractiveExpressionClass&gt;' is not
    /// finished") for that assembly - and the game calls <c>GetTypes()</c> on every assembly in the
    /// domain when it creates a game (<c>Lavapotion.Networking.Commands.AssemblyCommandLookup</c>),
    /// binds by convention, fills its auto-pools and registers chat commands. One mistyped eval made
    /// every Conquest launch fail with "Error during construction of type 'Game'" (2026-09-06).
    ///
    /// Mono computes the finished types and then throws them away because an unfinished one is
    /// among them. This finalizer answers with the finished types instead: an unfinished type is
    /// not a type anything could use, so hiding it is the truthful answer. Patched only while the
    /// dev server is up; a player's install never touches the corlib.
    /// </summary>
    [HarmonyPatch]
    public static class DynamicAssemblyTypesPatches
    {
        private static bool _reported;

        [HarmonyPrepare]
        private static bool Prepare()
        {
            SocAccessMod mod = SocAccessMod.Instance;
            return mod != null && mod.DevServerUp;
        }

        [HarmonyTargetMethod]
        private static MethodBase Target()
        {
            return AccessTools.Method(typeof(AssemblyBuilder), "GetTypes", new[] { typeof(bool) });
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(AssemblyBuilder __instance, Exception __exception, ref Type[] __result)
        {
            if (!(__exception is ReflectionTypeLoadException))
            {
                return __exception;
            }

            // ModuleBuilder.GetTypes never throws: it hands back created types as themselves and
            // unfinished ones as their TypeBuilder, which is exactly what AssemblyBuilder refuses.
            List<Type> finished = new List<Type>();
            foreach (Module module in __instance.GetModules())
            {
                foreach (Type type in module.GetTypes())
                {
                    if (type != null && !(type is TypeBuilder))
                    {
                        finished.Add(type);
                    }
                }
            }

            __result = finished.ToArray();
            if (!_reported)
            {
                _reported = true;
                SocAccessMod.Instance?.LogWarning(
                    "A dynamic assembly holds an unfinished type (a failed /eval); its finished types were reported to a type scan instead of throwing");
            }

            return null;
        }
    }
}
