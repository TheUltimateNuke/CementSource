using CementTools.ModMenuTools;
using CementTools.Modules.InputModule;
using CementTools.Modules.NotificationModule;
using CementTools.Modules.PoolingModule;
using CementTools.Modules.SceneModule;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace CementTools.ModLoading
{
    public static class ModLoader
    {
        public static GameObject modHolder;

        private static void LoadAllModules()
        {
            modHolder.AddComponent<InputManager>();
            modHolder.AddComponent<CustomSceneManager>();
            modHolder.AddComponent<Pool>();
            modHolder.AddComponent<NotificationModule>();
        }

        public static void Setup()
        {
            modHolder = new GameObject("Cement Mods");
            GameObject.DontDestroyOnLoad(modHolder);

            LoadAllModules();

            foreach (string subDirectory in Directory.GetDirectories(CementTools.Cement.MODBIN_PATH))
            {
                CementTools.Cement.Log($"PROCESSING SUB {subDirectory}");

                ModFile modFile = CementTools.Cement.Singleton.GetModFileFromName(subDirectory);
                CementTools.Cement.Log("CREATED MOD FILE. LOADING DLLS...");
                LoadModAssemblies(subDirectory, modFile);
                CementTools.Cement.Log("FINISHED LOADING DLLS");
                modFile.GotLoaded();
            }

            CementTools.Cement.Singleton.CreateSummary();
            CementTools.Cement.Log("SETTING UP MOD MENU");
            ModMenu.Singleton.SetupModMenu();
            CementTools.Cement.Log("DONE SETTING UP MOD MENU");
        }

        private static void LoadModAssemblies(string directory, ModFile modFile)
        {
            string[] assemblyPaths = Directory.GetFiles(directory, "*.dll");
            foreach (string path in assemblyPaths)
            {
                try
                {
                    Assembly assembly = Assembly.LoadFile(path);
                    foreach (AssemblyName referencedAssembly in assembly.GetReferencedAssemblies())
                    {
                        foreach (string sub in Directory.GetDirectories(CementTools.Cement.MODBIN_PATH))
                        {
                            string assemblyPath = Path.Combine(sub, referencedAssembly.Name + ".dll");
                            if (File.Exists(assemblyPath))
                            {
                                Assembly.LoadFile(assemblyPath);
                                break;
                            }
                        }

                    }
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (typeof(CementMod).IsAssignableFrom(type) || type.IsAssignableFrom(typeof(Cement)))
                        {
                            try
                            {
                                CementMod mod = InstantiateMod(type);
                                mod.modDirectoryPath = directory;
                                mod.modFile = modFile;
                                mod.enabled = !modFile.GetBool("Disabled");
                                CementModSingleton.Add(type, mod);
                                CementTools.Cement.Log($"Succesfully loaded {type.Name}.");
                            }
                            catch (Exception e)
                            {
                                CementTools.Cement.Log($"Error occurred while loading {type.Name}: {e}");
                                CementTools.Cement.Singleton.AddToSummary(CementTools.Cement.FAILED_TAG);
                                CementTools.Cement.Singleton.AddToSummary($"Error occurred while loading {type.Name}: {e}\n");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    CementTools.Cement.Log($"Error loading assembly {path}. {e}");
                    CementTools.Cement.Singleton.AddToSummary(CementTools.Cement.FAILED_TAG);
                    CementTools.Cement.Singleton.AddToSummary($"Error occurred while loading assembly {IOExtender.GetFileName(path)}.");
                }
            }
        }

        private static CementMod InstantiateMod(Type mod)
        {
            return modHolder.AddComponent(mod) as CementMod;
        }
    }
}
