using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace CementTools.Modules.HookModule
{
    /// <summary>
    /// This is a really simple hooking library that uses Harmony, basically modular UltiLib.
    /// Mods should use this for private getters, setters, and methods that can't be hooked with BepInEx.MonoMod.HookGenPatcher
    /// or if the mod's hook needs to be disabled once the mod is toggled off in-game.
    /// </summary>
    public class HookModule : CementMod
    {
        /// <summary>
        /// A struct containing information required to create toggleable Harmony "hooks", or patches, with Cement.
        /// </summary>
        public struct CementHook
        {
            public MethodInfo original;
            public CementMod callingMod;
            public MethodInfo hook;
            public bool isPrefix;

            public CementHook(MethodInfo original, MethodInfo hook, CementMod callingMod, bool isPrefix = false) : this()
            {
                this.original = original;
                this.callingMod = callingMod;
                this.hook = hook;
                this.isPrefix = isPrefix;
            }
        }

        private static readonly Dictionary<string, Harmony> modHarmonies = new Dictionary<string, Harmony>();

        /// <summary>
        /// Create a hook on a method that will toggle on and off with the passed CementMod.
        /// </summary>
        /// <param name="hook">The <see cref="CementHook"/> info to patch with.</param>
        public static void CreateHook(CementHook hook)
        {
            Harmony modHarmony = modHarmonies[hook.callingMod.name] ?? new Harmony(hook.callingMod.name);
            if (!modHarmonies.ContainsKey(modHarmony.Id)) modHarmonies.Add(modHarmony.Id, modHarmony);

            EnableHook(hook);
            hook.callingMod.modFile.ChangedValues += () =>
            {
                if (hook.callingMod.modFile.GetBool("Disabled")) DisableHook(hook);
                else EnableHook(hook);
            };

            Cement.Log($"New {(hook.isPrefix ? "PREFIX" : "POSTFIX")} hook on {hook.original.DeclaringType.Name}.{hook.original.Name} to {hook.hook.DeclaringType.Name}.{hook.hook.Name}");
        }

        private static void EnableHook(CementHook hook)
        {
            HarmonyMethod prefix = hook.isPrefix ? new HarmonyMethod(hook.hook) : null;
            HarmonyMethod postfix = hook.isPrefix ? null : new HarmonyMethod(hook.hook);
            modHarmonies[hook.callingMod.name].Patch(hook.original, prefix, postfix);
        }

        private static void DisableHook(CementHook hook)
        {
            modHarmonies[hook.callingMod.name].Unpatch(hook.original, hook.hook);
        }
    }
}
