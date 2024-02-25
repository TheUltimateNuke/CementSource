using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CementTools.ModMenuTools
{
    public class ModMenu
    {
        private static ModMenu _singleton = null;
        public static ModMenu Singleton
        {
            get
            {
                return _singleton;
            }
        }

        private GameObject MODMENU;

        public GameObject modMenuCanvas => MODMENU;

        private ModFile[] modFiles;
        private GameObject _modUIPrefab;
        private readonly Dictionary<ModFile, ModUI> _modUIs = new Dictionary<ModFile, ModUI>();
        private readonly Dictionary<string, List<CementMod>> _modFileToMods = new Dictionary<string, List<CementMod>>();
        private readonly Dictionary<string, GameObject> _parameterPrefabs = new Dictionary<string, GameObject>();
        private Transform _contentParent;

        private readonly Dictionary<ModFile, Dictionary<string, ParameterUI>> _modFileParameterUIs = new Dictionary<ModFile, Dictionary<string, ParameterUI>>();

        public ModMenu(AssetBundle bundle)
        {
            if (_singleton != null)
            {
                throw new Exception("An instance of singleton ModMenu already exists.");
            }
            _singleton = this;

            SetupPrefabs(bundle);
        }

        public void SetupModMenu()
        {
            modFiles = ModFile.All;
            SetupModFileToMods();
            CreateUIForMods();
        }

        private void SetupModFileToMods()
        {
            foreach (CementMod mod in CementModSingleton.GetAll())
            {
                if (!_modFileToMods.ContainsKey(mod.modFile.path))
                {
                    _modFileToMods[mod.modFile.path] = new List<CementMod>();
                }
                
                CementTools.Cement.Log($"NEW MOD FILE PATH: {mod.modFile.path}");
                _modFileToMods[mod.modFile.path].Add(mod);
            }
        }

        private void SetupPrefabs(AssetBundle bundle)
        {
            GameObject modMenuPrefab = bundle.LoadAsset<GameObject>("CementModMenu");
            MODMENU = GameObject.Instantiate(modMenuPrefab);
            GameObject.DontDestroyOnLoad(MODMENU);
            MODMENU.SetActive(false);

            MODMENU.transform.Find("Scroll View/Close").GetComponent<Button>().onClick.AddListener(Disable);
            MODMENU.transform.Find("Scroll View/ClearCache").GetComponent<Button>().onClick.AddListener(CementTools.Cement.ClearCache);

            _contentParent = MODMENU.transform.Find("Scroll View/Viewport/Content");

            _modUIPrefab = bundle.LoadAsset<GameObject>("ModFileContainer");

            _parameterPrefabs["String"] = bundle.LoadAsset<GameObject>("StringParameterUI");
            _parameterPrefabs["Float"] = bundle.LoadAsset<GameObject>("FloatParameterUI");
            _parameterPrefabs["Boolean"] = bundle.LoadAsset<GameObject>("BoolParameterUI");
            _parameterPrefabs["Integer"] = bundle.LoadAsset<GameObject>("IntegerParameterUI");
        }

        private void CreateUIForMods()
        {
            foreach (ModFile mod in modFiles)
            {   
                if (mod.IsBad || !mod.IsLoaded)
                {
                    continue;
                }
                CementTools.Cement.Log($"Creating UI for mod {mod.path}");
                CreateUIForMod(mod);
            }
        }

        private GameObject GetPrefab(ModFile file, string key)
        {
            ModFileValue value = file.GetValue(key);
            foreach (string attribute in _parameterPrefabs.Keys)
            {
                if (value.HasAttribute(attribute))
                {
                    return _parameterPrefabs[attribute];
                }
            }

            return _parameterPrefabs["String"];
        }

        public void SetModActive(ModFile file, bool value)
        {
            _modUIs[file].modFileToggle.isOn = value;
            _modUIs[file].ToggleMod();
        }

        private void CreateUIForMod(ModFile file)
        {
            ModUI modUI = GameObject.Instantiate(_modUIPrefab, _contentParent).GetComponentInChildren<ModUI>();
            modUI.SetValues(file, !file.GetBool("Disabled"), file.GetString("Name"));

            _modUIs[file] = modUI;
            Transform parameterParent = modUI.GetParameterParent();

            _modFileParameterUIs[file] = new Dictionary<string, ParameterUI>();
            var parameters = file.Values;

            foreach (string key in parameters.Keys)
            {
                if (file.GetValue(key).HasAttribute("Editable"))
                {
                    ParameterUI parameterUI = GameObject.Instantiate(GetPrefab(file, key), parameterParent).GetComponent<ParameterUI>();
                    parameterUI.SetValues(key, file.GetString(key));
                    _modFileParameterUIs[file][key] = parameterUI;
                }
            }
            
            modUI.parameterToggle.isOn = false;
            modUI.ToggleParameters();
        }

        private void UpdateParameters(ModFile modFile)
        {
            foreach (string key in _modFileParameterUIs[modFile].Keys)
            {
                UpdateParameter(modFile, key, _modFileParameterUIs[modFile][key].GetValue());
            }
            modFile.UpdateFile();
            modFile.InvokeChangedValues();
        }

        private void UpdateParameter(ModFile modFile, string key, string rawValue)
        {
            modFile.SetString(key, rawValue);
        }

        public void Enable()
        {
            Cement.Singleton.UseCementEventSystem();
            MODMENU.SetActive(true);
        }

        private void ToggleRelevantMods()
        {
            foreach (ModFile file in modFiles)
            {
                CementTools.Cement.Log(file.path);
                bool enabled = !file.GetBool("Disabled");
                if (!_modFileToMods.ContainsKey(file.path))
                {
                    CementTools.Cement.Log("Doesn't contain key. SKIPPING!");
                    continue;
                }
                foreach (CementMod mod in _modFileToMods[file.path])
                {
                    mod.enabled = enabled;
                }
            }
        }

        public void Disable()
        {
            MODMENU.SetActive(false);
            foreach (ModFile modFile in modFiles)
            {
                try
                {
                    UpdateParameters(modFile);
                }
                catch(Exception e)
                {
                    Cement.Log($"ERROR UPDATING PARAMS: {e}");
                }
            }
            
            ToggleRelevantMods();
            Cement.Singleton.RevertEventSystem();
        }
    }
}