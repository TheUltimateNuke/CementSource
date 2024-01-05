using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;

namespace CementTools.Patches
{
    public static class Patch_MMHook
    {
        internal static void PatchHooks()
        {
            Assembly assembly = Assembly.LoadFile(Path.Combine("..", "MMHOOK", "MMHOOK_Assembly-CSharp.dll"));
            foreach (Type typ in assembly.GetTypes())
            {
                foreach (EventInfo even in typ.GetEvents(AccessTools.all))
                {
                    Harmony harmony = new Harmony("cmt-mmhook-patch-harmony");

                    harmony.Patch(even.RaiseMethod, new HarmonyMethod(typeof(Patch_MMHook).GetMethod(nameof(BeforeHook))));
                }
            }
        }

        public static void BeforeHook()
        {
            bool isCementMod = Path.GetFullPath(Cement.MODS_FOLDER_PATH) == Path.GetFullPath(Path.Combine(Assembly.GetCallingAssembly().Location, ".."))
            if (isCementMod)
            {
                throw new NotImplementedException();
            }
        }
    }
}
