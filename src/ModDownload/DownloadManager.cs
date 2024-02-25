using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CementTools;

public static class DownloadManager
{
    public static void DownloadAllModFiles(Action<bool> callback)
    {
        ThreadPool.QueueUserWorkItem(delegate (object data)
        {
            foreach (string path in Directory.GetFiles(CementTools.Cement.MODS_FOLDER_PATH, $"*.{CementTools.Cement.MOD_FILE_EXTENSION}"))
            {
                Task<bool> _dl = DownloadModsFromFile(path);
                _dl.Wait();
                bool succeeded = _dl.Result;
                if (!succeeded)
                {
                    callback.Invoke(false);
                    return;
                }
            }

            callback.Invoke(true);
        });
    }

    public static void DownloadMods(string directory)
    {
        foreach (string path in Directory.GetFiles(directory, $"*.{CementTools.Cement.MOD_FILE_EXTENSION}"))
        {
            ThreadPool.QueueUserWorkItem(delegate (object data)
            {
                HandleDownloadMod(path);
            });
        }
    }

    private static async Task<bool> DownloadModFile(string link, string path)
    {
        Cement.Log($"Starting download of {link}");
        if (!await DownloadHelper.DownloadFile(link, path, null))
        {
            Cement.Log($"Error with downloading {link}");
            return false;
        }

        Cement.Log($"Finished download of {link}. Downloading all mods for it.");
        return await DownloadModsFromFile(path);
    }

    private static async Task<bool> DownloadModsFromFile(string path)
    {
        ModFile modFile = ModFile.Get(path);
        string name = modFile.GetString("Name");
        Cement.Log($"Installing mod files for mod {name}");

        string rawLinks = modFile.GetString("Links");
        if (rawLinks == null)
        {
            Cement.Log("No links.");
            // we dont want to return false and prevent other legitamate mods from being downloaded
            modFile.FlagAsBad(); // stops it from being loaded by Mod Menu and from trying to download files from it
            return true;
        }

        List<ModFile> requiredMods = new List<ModFile>();

        string[] links = rawLinks.Split(',');
        foreach (string link in links)
        {
            if (!LinkHelper.IsLinkToMod(link))
            {
                continue;
            }

            string nameFromLink = LinkHelper.GetNameFromLink(link);

            string pathToMod = Path.Combine(Cement.MODS_FOLDER_PATH, nameFromLink);

            bool fileExists = File.Exists(pathToMod);

            bool succeeded = true;
            if (!fileExists)
            {
                Cement.Log("Download new mod file.");
                await DownloadModFile(link, pathToMod);
            }

            if (!succeeded)
            {
                return false;
            }

            ModFile childFile = ModFile.Get(pathToMod);

            requiredMods.Add(childFile);
            childFile.AddRequiredBy(modFile);
        }

        modFile.RequiredMods = requiredMods.ToArray();

        Cement.Log($"Done installing mod files for {name}");
        return true;
    }

    private static void HandleDownloadMod(string pathToMod)
    {
        CementTools.Cement.Singleton.totalMods++;
        ModDownloadHandler handler = new ModDownloadHandler(pathToMod);

        CementTools.Cement.Log($"CREATING HANDLER FOR MOD {pathToMod}");

        handler.OnProgress += (float percentage) => CementTools.Cement.Singleton.OnProgress(pathToMod, percentage);
        handler.Download(callback: CementTools.Cement.Singleton.FinishedDownloadingMod);
    }
}