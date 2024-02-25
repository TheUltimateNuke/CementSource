using BepInEx;
using CementTools.ModLoading;
using CementTools.ModMenuTools;
using CementTools.Modules.NotificationModule;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CementTools
{
    [BepInPlugin("org.gangbeastsmodding.cement.blastfurnaceslag", "Cement", "3.0.0")]
    public class Cement : BaseUnityPlugin
    {
        public static Cement Singleton
        {
            get
            {
                return _singleton;
            }
        }

        public static bool HasInternet
        {
            get
            {
                return _hasInternet;
            }
        }

        private static bool _hasInternet;

        private static Cement _singleton;

        bool loadedMods = false;

        public int totalMods = 0;
        int totalModsProcessed = 0;

        TMP_Text summary;

        string modMessageText = "";

        string summaryText = $"{FAILED_TAG}<b>IMPORTANT! PLEASE READ!</b></color>\n\nNote: if a mod failed to process, make sure you have a good internet connection or try restarting your game." +
        " If it still doesn't work open the 'Cement' menu from the main menu and clear your cache. If that doesn't work then there is a problem with the mod file, which the mod's creator must fix.\n\n" +
        "If you want assistance with trying to get your mods to work, go to <link=\"website\"><u>our website</u></link>, and join the discord server. " +
        "There is a #modding-questions channel.\n\nAlso everything the Cement Team does is for free. All of us are students or work full time, so we would be really grateful "
        + "if you bought us a coffee, with the following link: <link=\"coffee\"><u>CLICK ME!</u></link>.\n\n";

        Slider progressBar;
        TMP_Text progressText;

        GameObject cementGUI;
        GameObject summaryGUI;

        AssetBundle _bundle;

        private readonly Dictionary<string, float> _percentages = new Dictionary<string, float>();
        private readonly Dictionary<string, ModFile> _nameToModFile = new Dictionary<string, ModFile>();

        private float _currentProgressBarValue;

        public const string FAILED_TAG = "<color=#B41447>";
        private const string SUCCEEDED_TAG = "<color=#4DD11D>";
        public const string MOD_FILE_EXTENSION = "cmt";

        private const string CEMENT_VERSION_URL = "https://raw.githubusercontent.com/CementGB/cementresources/main/BepInEx/plugins/Cement/version";

        public static string MODS_FOLDER_PATH
        {
            get
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods"));
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
        }

        public static string MODBIN_PATH
        {
            get
            {
                string path = Path.Combine(Application.dataPath, "modbin");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
        }

        public static string CACHE_PATH
        {
            get
            {
                string path = Path.Combine(Application.dataPath, "modcache");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
        }

        public static string CEMENT_PATH
        {
            get
            {
                string path = Path.Combine(Application.dataPath, "..", "BepInEx", "plugins", "Cement");
                if (!Directory.Exists(path))
                {
                    DirectoryInfo hidden = Directory.CreateDirectory(path);
                    hidden.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
                }

                return path;
            }
        }

        private EventSystem _oldEventSystem;
        private EventSystem _cementEventSystem;
        private bool _usingCementEventSystem;

        // this is where the main processing happens, so look here to see how Cement works.
        private void Awake()
        {
            _singleton = this;
            DontDestroyOnLoad(_singleton);
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patches.Patch_Credits));
            LoadCement();
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Patches.Patch_Credits));
        }


        private void Update()
        {
            IsThisEnoughCement();
            HandleClickingLinks();
            DisplayProgressBarChanges();
            HandleEventSystems();
        }

        private void OnDestroy()
        {
            Logger.LogError($"GETTING DESTROYED {transform.parent.name}");
        }

        private void LoadCement()
        {
            _hasInternet = IsConnectedToWifi();
            Cement.Log($"IS CONNECTED TO WIFI? {_hasInternet}");
            SceneManager.sceneLoaded += OnSceneLoaded;

            try
            {
                IOExtender.DeleteFilesInDirectory(MODBIN_PATH);
            }
            catch (Exception)
            {
                // yowser
            }

            GetLatestCementVersion(delegate (bool succeeded, string latestVersion)
            {
                Cement.Log($"LATEST CEMENT VERSION: {latestVersion}");
                if (succeeded)
                {
                    if (latestVersion != GetCurrentCementVersion())
                    {
                        UpdateCement();
                        // return; // removed this in order for an outdated Cement version to still work if the user doesn't want to update
                    }
                }

                DeleteTempInstaller();

                try
                {
                    CreateGUI();
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error while creating gui: {e}");
                }
                if (ModsPresent())
                {
                    DownloadManager.DownloadAllModFiles(delegate (bool succeeded2)
                    {
                        Cement.Log($"DONE DOWNLOADING ALL MOD FILES. DID SUCCEED? {succeeded2}");
                        if (succeeded2)
                        {
                            DownloadManager.DownloadMods(MODS_FOLDER_PATH);
                        }
                        else
                        {
                            summaryText += $"\n\n{FAILED_TAG}Failed to download all required mods. Try restarting your game, or make sure you have a good internet connection.</color>\n\n";
                            if (cementGUI != null) Destroy(cementGUI);
                            Cement.Singleton.CreateSummary();
                        }
                    });
                }
                else
                {
                    if (cementGUI != null) Destroy(cementGUI); // Destroy the GUI if no mods are present
                    CementTools.Cement.Singleton.CreateSummary();
                    CementTools.Cement.Log("SETTING UP MOD MENU");
                    ModMenu.Singleton.SetupModMenu();
                    CementTools.Cement.Log("DONE SETTING UP MOD MENU");
                }
            });
        }

        private void LoadAllMods()
        {
            if (cementGUI != null)
            {
                Destroy(cementGUI);
            }
            ModLoader.Setup();
        }

        public ModFile GetModFileFromName(string name)
        {
            return _nameToModFile[name];
        }

        public void AddToSummary(string text)
        {
            summaryText += text;
        }

        public void UseCementEventSystem()
        {
            _usingCementEventSystem = true;
        }

        public void RevertEventSystem()
        {
            _usingCementEventSystem = false;
        }

        private void InternalLog(object o, BepInEx.Logging.LogLevel logLevel=BepInEx.Logging.LogLevel.Info)
        {
            Logger.Log(logLevel, o);
        }

        public static void Log(object o, BepInEx.Logging.LogLevel logLevel=BepInEx.Logging.LogLevel.Info) // TODO: Add log levels, like ERROR, WARNING, etc.
        {
            Singleton.InternalLog(o, logLevel);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "Menu")
            {
                Cement.Log("Spawned cement button is false");
                StartCoroutine(WaitUntilTextSectionExists(SpawnInCementButton));
            }
        }

        private IEnumerator WaitUntilTextSectionExists(Action callback)
        {
            Cement.Log("Waiting for TextSection...");
            yield return new WaitUntil(() => GameObject.Find("TextSection") != null);
            GameObject g = GameObject.Find("TextSection");
            Cement.Log("Waiting for Local...");
            yield return new WaitUntil(() => GameObject.Find("TextSection").transform.Find("Local") != null);
            Cement.Log("Found Local!");
            callback.Invoke();
        }

        private void SpawnInCementButton()
        {
            Cement.Log("Spawning in Cement button");
            Transform textSection = GameObject.Find("TextSection").transform;
            if (textSection == null)
            {
                Cement.Log("TextSection is null. Backing out");
                return;
            }

            textSection.localPosition += new Vector3(0, 10f, 0);

            Button localButton = textSection.Find("Local").GetComponent<Button>();
            Button creditsButton = textSection.Find("Credits").GetComponent<Button>();
            Button settingsButton = textSection.Find("Settings").GetComponent<Button>();
            Button costumesButton = textSection.Find("Costumes").GetComponent<Button>();

            Cement.Log("Just past getting the buttons");

            Navigation creditsButtonNav = new Navigation();
            Navigation settingsButtonNav = new Navigation();
            Navigation cementButtonNav = new Navigation();

            Cement.Log("Cement button being created");
            GameObject cementButton = Instantiate(localButton.gameObject, textSection);

            cementButton.name = "Cement";
            cementButton.GetComponent<TMP_Text>().text = "Cement";
            cementButton.transform.SetSiblingIndex(textSection.childCount - 4);
            Cement.Log("Adjusting text and sibling index");

            Button button = cementButton.GetComponent<Button>();

            FieldInfo onClickForButton = typeof(Button).GetField("m_OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
            Button.ButtonClickedEvent onClickedEvent = new Button.ButtonClickedEvent();
            onClickForButton.SetValue(button, onClickedEvent);

            button.onClick.AddListener(ModMenu.Singleton.Enable);

            cementButtonNav.selectOnUp = settingsButton;
            cementButtonNav.selectOnDown = creditsButton;
            creditsButtonNav.selectOnDown = creditsButton;
            creditsButtonNav.selectOnUp = button;
            settingsButtonNav.selectOnDown = button;
            settingsButtonNav.selectOnUp = costumesButton;

            settingsButton.navigation = settingsButtonNav;
            creditsButton.navigation = creditsButtonNav;
            button.navigation = cementButtonNav;

            Cement.Log("Finished creating Cement button!");
        }

        private void CreateEventSystem()
        {
            try
            {
                GameObject mainObject = Instantiate(_bundle.LoadAsset<GameObject>("EventSystem"));
                _cementEventSystem = mainObject.GetComponent<EventSystem>();
                InputSystemUIInputModule inputModule = mainObject.GetComponent<InputSystemUIInputModule>();

                FieldInfo currentModule = typeof(EventSystem).GetField("m_CurrentInputModule", BindingFlags.NonPublic | BindingFlags.Instance);
                currentModule.SetValue(_cementEventSystem, inputModule);

                _cementEventSystem.enabled = true;

                DontDestroyOnLoad(mainObject);
            }
            catch (Exception e)
            {
                Cement.Log($"ERROR WHILE CREATING EVENT SYSTEM: {e}");
            }
        }

        private void CreateGUI()
        {
            _bundle = AssetBundle.LoadFromFile(Path.Combine(Paths.BepInExRootPath, "plugins", "Cement", "cement"));
            cementGUI = Instantiate(_bundle.LoadAsset<GameObject>("CementLoadingScreen"));
            summaryGUI = Instantiate(_bundle.LoadAsset<GameObject>("CementSummaryCanvas"));

            Button okButton = summaryGUI.transform.Find("Scroll View").Find("OK").GetComponent<Button>();
            okButton.onClick.AddListener(CloseSummaryMenu);

            DontDestroyOnLoad(cementGUI);
            DontDestroyOnLoad(summaryGUI);

            CreateEventSystem();

            Transform parent = cementGUI.transform.Find("Background").Find("LoadingBar");
            progressBar = parent.GetComponent<Slider>();
            progressText = parent.Find("Fill Area").GetComponentInChildren<TMP_Text>();

            progressText.text = "0%";
            progressBar.value = 0f;

            try
            {
                new ModMenu(_bundle);
            }
            catch (Exception e)
            {
                Cement.Log($"EXCEPTION OCCURRED WHILE CREATING MOD MENU {e}");
            }

            _bundle.Unload(false);
        }

        public static void CloseSummaryMenu()
        {
            Cement.Log($"CLICKED OK BUTTON! CURRENT SCENE: {SceneManager.GetActiveScene().name}");
            // checks if it is the menu, so that you can't click the ok button while the loading screen is active.
            if (SceneManager.GetActiveScene().name == "Menu")
            {
                Destroy(Singleton.summaryGUI);
                Singleton.RevertEventSystem();
            }
        }

        public void CreateSummary()
        {
            if (summaryGUI == null)
            {
                return;
            }

            summaryGUI.SetActive(true);

            Transform background = summaryGUI.transform.Find("Scroll View");
            summary = background.Find("Viewport/Content").GetComponent<TMP_Text>();

            summary.text = "";
            if (!HasInternet)
            {
                summary.text = $"{FAILED_TAG}<b>NOT CONNECTED TO WIFI</color></b>\n\n";
            }
            summary.text += summaryText + "\n" + modMessageText;

            UseCementEventSystem();
        }

        private bool ModsPresent()
        {
            return Directory.GetFiles(MODS_FOLDER_PATH, $"*.{MOD_FILE_EXTENSION}").Length > 0;
        }


        private void CopyCacheToBin(string directoryName, string pathToMod)
        {
            string cachePath = Path.Combine(CACHE_PATH, directoryName);
            string binPath = Path.Combine(MODBIN_PATH, IOExtender.GetFileName(pathToMod).Split('.')[0]);

            _nameToModFile[binPath] = ModFile.Get(pathToMod);

            if (!Directory.Exists(cachePath))
            {
                return;
            }

            if (!Directory.Exists(binPath))
            {
                Directory.CreateDirectory(binPath);
            }

            foreach (string path in Directory.GetFiles(cachePath))
            {
                string name = IOExtender.GetFileName(path);
                string binFilePath = Path.Combine(binPath, name);

                if (File.Exists(binFilePath))
                    File.Delete(binFilePath);

                File.Copy(Path.Combine(cachePath, name), binFilePath);
            }
        }

        private void OnApplicationQuit()
        {
            try
            {
                IOExtender.DeleteFilesInDirectory(MODBIN_PATH);
            }
            catch
            {
                // yowser
            }
        }

        private string GetCurrentCementVersion()
        {
            string path = Path.Combine(CEMENT_PATH, "version");
            if (!File.Exists(path))
            {
                return null;
            }
            return File.ReadAllText(path);
        }

        private void GetLatestCementVersion(Action<bool, string> callback)
        {
            PersistentWebClient client = new PersistentWebClient();
            client.OnPersistentDownloadStringComplete += callback;
            client.DownloadStringPersistent(CEMENT_VERSION_URL);
        }

        private void UpdateCement()
        {
            string tempPath = Path.Combine(Application.dataPath, "CementInstaller.exe");

            if (!File.Exists(tempPath))
                File.Copy(Path.Combine(CEMENT_PATH, "CementInstaller.exe"), tempPath);

            Process process = new Process();
            process.StartInfo.FileName = tempPath;
            process.StartInfo.Verb = "runas";
            try
            {
                process.Start();
            }
            catch (Win32Exception e)
            {
                Cement.Log($"FAILED TO UPDATE CEMENT! Please make sure you ran CementInstaller.exe as admin.\n{e}");
            }
        }

        private void DeleteTempInstaller()
        {
            string tempPath = Path.Combine(Application.dataPath, "CementInstaller.exe");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        private bool IsConnectedToWifi()
        {
            try
            {
                using (var client = new WebClient())
                using (var stream = client.OpenRead("http://www.google.com"))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public void FinishedDownloadingMod(ProcessedModData data)
        {
            if (data.succeeded)
            {
                summaryText += SUCCEEDED_TAG + $"Successfully downloaded all files for {data.name}. </color>\n";
                CopyCacheToBin(data.directoryName, data.pathToMod);
            }
            else
            {
                if (data.name == null || data.name == "")
                {
                    data.name = "incorrectly formatted mod";
                }
                summaryText += FAILED_TAG + $"Failed to process {data.name}. </color>\n";
            }

            if (data.message != null)
            {
                modMessageText += $"<b>{data.name} message</b>:\n\n{data.message}\n\n";
            }
            totalModsProcessed++;
            UpdateProgressBar();

            if (!loadedMods && totalModsProcessed == totalMods)
            {
                loadedMods = true;

                LoadAllMods();
            }
        }

        public void OnProgress(string mod, float percentage)
        {
            Cement.Log($"MOD {mod} IS {percentage}% DONE");
            _percentages[mod] = percentage;
            UpdateProgressBar();
        }

        public void UpdateProgressBar()
        {
            float value = 0f;
            foreach (float percentage in _percentages.Values)
            {
                value += percentage;
            }
            value /= totalMods;
            if (value > 100f)
            {
                value = 100f;
            }

            _currentProgressBarValue = value / 100f;
            progressText.text = $"{Mathf.Round(value * 10) * 0.1f}%";

            if (!loadedMods && totalModsProcessed == totalMods)
            {
                loadedMods = true;
                LoadAllMods();
            }
        }


        bool pressedNo = false;
        private void IsThisEnoughCement()
        {
            // easter egg for: is this enough cements? obviously no
            if (Keyboard.current.nKey.isPressed && Keyboard.current.oKey.isPressed && !pressedNo)
            {
                pressedNo = true;
                SpawnInCementButton();
            }

            if (Keyboard.current.nKey.wasReleasedThisFrame || Keyboard.current.oKey.wasReleasedThisFrame)
            {
                pressedNo = false;
            }
        }

        private void DisplayProgressBarChanges()
        {
            if (progressBar != null)
            {
                progressBar.value = Mathf.Lerp(progressBar.value, _currentProgressBarValue, 10f * Time.deltaTime);
            }
        }

        private void HandleEventSystems()
        {
            if (_usingCementEventSystem)
            {
                if (_cementEventSystem == null)
                {
                    Cement.Log("CEMENT EVENT SYSTEM IS NULL! PANIC!!!");
                    return;
                }
                EventSystem.current = _cementEventSystem;
            }
            else
            {
                if (_oldEventSystem == null)
                {
                    GameObject global = GameObject.Find("Global(Clone)");
                    if (global == null)
                    {
                        return;
                    }
                    Transform _eventSystemTransform = global.transform.Find("Input/EventSystem");
                    if (_eventSystemTransform != null) _oldEventSystem = _eventSystemTransform.GetComponent<EventSystem>();
                }
                if (_oldEventSystem == null)
                {
                    Cement.Log("OLD EVENT SYSTEM IS NULL! PANIC!!!");
                    return;
                }
                EventSystem.current = _oldEventSystem;
            }

            GameObject _splashGo = GameObject.Find("Splash");
            if (_splashGo != null) _splashGo.GetComponentInChildren<AnyInputOnClick>(true).enabled = !_usingCementEventSystem;
        }

        private void HandleClickingLinks()
        {
            if (summary == null)
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(summary, Mouse.current.position.ReadValue(), Camera.current);
            if (linkIndex == -1)
            {
                return;
            }

            string linkId = summary.textInfo.linkInfo[linkIndex].GetLinkID();

            switch (linkId)
            {
                case "website":
                    Application.OpenURL("https://cementgb.github.io");
                    break;
                case "coffee":
                    Application.OpenURL("https://www.buymeacoffee.com/cementgb");
                    break;
            }
        }

        public static void RestartThroughHelper()
        {
            string cementHelper = Path.Combine(CEMENT_PATH, "CementHelper.exe");
            if (!File.Exists(cementHelper))
            {
                File.Copy(Path.Combine(CEMENT_PATH, "CementInstaller.exe"), cementHelper);
            }

            var startInfo = new ProcessStartInfo()
            {
                FileName = cementHelper,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = "--no-install"
            };

            Process.Start(startInfo);
        }

        public static void RestartCommand()
        {
            string gangBeardPath = Path.Combine(Application.dataPath, "..", "Gang Beasts.exe");
            string arguments = $"/C echo RESTARTING GANG BEASTS && timeout /T 3 /nobreak && \"{gangBeardPath}\"";

            var startInfo = new ProcessStartInfo()
            {
                FileName = "cmd.exe",
                UseShellExecute = true,
                Verb = "runas",
                Arguments = arguments
            };

            Process.Start(startInfo);
            Application.Quit();
        }

        public static void ClearCache()
        {
            IOExtender.DeleteFilesInDirectory(CACHE_PATH);
            foreach (CementMod mod in CementModSingleton.GetAll())
            {
                if (File.Exists(mod.modFile.path))
                {
                    mod.modFile.SetString("CurrentVersion", "Null");
                    mod.modFile.UpdateFile();
                }
            }
            RestartThroughHelper();
        }
    }
}