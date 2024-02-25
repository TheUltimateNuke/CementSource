using GB.Game;
using GB.Gamemodes;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace CementTools.Modules.SceneModule
{
    public class SceneModule : CementMod
    {
        public static CustomScene[] CustomMaps => _customMaps.ToArray();

        internal static List<CustomScene> _customMaps = new List<CustomScene>();

        internal static void RegisterCustomMaps()
        {
            GameModeSetupConfiguration setupConfig = (GameModeSetupConfiguration)AccessTools.Field(typeof(GameManagerNew), nameof(GameManagerNew.tracker)).GetValue(GameManagerNew.instance);

            foreach (var map in CustomMaps)
            {
                setupConfig.Maps.AvailableMaps.Add(map.mapStatus);
            }
        }
    }

    public class CustomScene
    {
        public readonly string name;
        public readonly AssetBundle mapBundle;
        
        public ModeMapStatus mapStatus = null;

        public CustomScene(string name, AssetBundle mapBundle, bool isMap=true)
        {
            this.name = name;
            this.mapBundle = mapBundle;
            if (isMap)
            {
                if (SceneModule._customMaps.Find(map => map.name == this.name) == null)
                {
                    mapStatus = new ModeMapStatus(this.name, true);
                    SceneModule._customMaps.Add(this);
                }
                else
                {
                    Cement.Log("A custom map named " + this.name + " already exists, but another scene of the same name attempted to instantiate.", BepInEx.Logging.LogLevel.Warning);
                    return;
                }
            }
        }
    }
}
