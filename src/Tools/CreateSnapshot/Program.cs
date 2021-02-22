using Microsoft.Win32;
using NHM.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace CreateSnapshot
{
    class Program
    {
        // arg0 = operation path - zip/unzip
        // arg1 = installation path - absolute
        // arg2 = tag
        static void Main(string[] args)
        {
            try
            {
                var path = args.Length > 1 ? args[1] : Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var tag = args.Length > 2 ? args[2] : "";
                var snapTag = "snap_" + DateTime.UtcNow.ToString("yyyy_MM_dd_HH_mm_ss") + tag;
                switch (args[0])
                {
                    case "-push":
                        //CreateSnapshotOfRegistries(path);
                        CreateSnapshotArchive(path, snapTag);
                        break;
                    case "-pop":
                        UnzipSnapshot(path);
                        //var registryJson = File.ReadAllText(Path.Combine(args[1], "snapshots", "registryKeys.json"));
                        //SaveRegistryValues(registryJson);
                        break;
                    default:
                        break;
                }
                //CreateSnapshotArchive(args[1], tag);
                //UnzipSnapshot(args[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine($"CreateSnapshot error: {e}");
                //Console.ReadKey();
            }
        }

        private static bool IsValidSnapshotPath(string path)
        {
            return !path.Contains("tools")
                && !path.Contains("snapshots")
                && !path.Contains("_tmp");
        }

        private static void CreateSnapshotArchive(string nhmRootPath, string snapTag)
        {
            var snapshotsLocation = Path.Combine(nhmRootPath, "tools");
            if (!Directory.Exists(snapshotsLocation)) Directory.CreateDirectory(snapshotsLocation);
            var filesToPack = Directory.GetFiles(nhmRootPath, "*.*", SearchOption.AllDirectories)
                .Where(IsValidSnapshotPath);


            string archiveFileName = $"{snapTag}.zip";
            Console.Write($"Preparing logs archive file '{archiveFileName}'...");

            var archivePath = Path.Combine(snapshotsLocation, archiveFileName);
            using (var fileStream = new FileStream(archivePath, FileMode.Create))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
            {
                foreach (var filePath in filesToPack)
                {
                    var entryPath = filePath.Replace(nhmRootPath, "").Substring(1);
                    var zipFile = archive.CreateEntry(entryPath);
                    using (var readFile = File.OpenRead(filePath))
                    using (var entryStream = zipFile.Open())
                    {
                        readFile.CopyTo(entryStream);
                    }
                }
            }
        }

        private static void CreateSnapshotOfRegistries(string nhmRootPath)
        {
            var registryValues = new List<Tuple<string, string, string, object>>();
            var val = Registry.GetValue("HKEY_LOCAL_MACHINE" + @"\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "nonExisting");
            if (val.ToString() != "nonExisting") registryValues.Add(Tuple.Create("HKEY_LOCAL_MACHINE", @"SOFTWARE\Microsoft\Cryptography\", "MachineGuid", val)); //string

            val = Registry.GetValue("HKEY_CURRENT_USER" + @"\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "NiceHash Miner", "nonExisting");
            if (val.ToString() != "nonExisting") registryValues.Add(Tuple.Create("HKEY_CURRENT_USER", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\", "NiceHash Miner", val)); //string

            val = Registry.GetValue("HKEY_CURRENT_USER" + @"\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "DontShowUI", "nonExisting");
            if (val.ToString() != "nonExisting") registryValues.Add(Tuple.Create("HKEY_CURRENT_USER", @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\", "DontShowUI", val)); //int

            var regKey = Registry.CurrentUser.OpenSubKey(@"Software\" + APP_GUID.GUID);
            if (regKey != null)
            {
                var subKeys = regKey.GetValueNames();
                foreach (var subKey in subKeys)
                {
                    val = regKey.GetValue(subKey, "nonExisting");
                    if (val.ToString() != "nonExisting") registryValues.Add(Tuple.Create("HKEY_CURRENT_USER", $"SOFTWARE\\{APP_GUID.GUID}\\", subKey, val));
                }
            }


            regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + APP_GUID.GUID);
            if (regKey != null)
            {
                var subKeys = regKey.GetValueNames();
                foreach (var subKey in subKeys)
                {
                    val = regKey.GetValue(subKey, "nonExisting");
                    if (val.ToString() != "nonExisting") registryValues.Add(Tuple.Create("HKEY_CURRENT_USER", $"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{APP_GUID.GUID}\\", subKey, val));
                }
            }

            var serializedRegistries = Newtonsoft.Json.JsonConvert.SerializeObject(registryValues);
            var snapshotsLocation = Path.Combine(nhmRootPath, "snapshots");
            if (!Directory.Exists(snapshotsLocation)) Directory.CreateDirectory(snapshotsLocation);
            File.WriteAllText(Path.Combine(snapshotsLocation, "registryKeys.json"), serializedRegistries);
        }

        private static void UnzipSnapshot(string snapshotPath)
        {
            var nhmRootPath = new Uri(Path.Combine(snapshotPath, @"..\..\")).LocalPath;
            var tmpMove = Path.Combine(nhmRootPath, "_tmp");
            if (Directory.Exists(tmpMove)) Directory.Delete(tmpMove, true);
            if (!Directory.Exists(tmpMove)) Directory.CreateDirectory(tmpMove);

            var directoriesToDelete = Directory.GetDirectories(nhmRootPath)
                .Where(IsValidSnapshotPath);
            foreach (var directoryPath in directoriesToDelete)
            {
                var subPath = directoryPath.Replace(nhmRootPath, "");
                var moveToPath = Path.Combine(tmpMove, subPath);
                Console.WriteLine($"Directory '{directoryPath}' -> '{moveToPath}'");
                Directory.Move(directoryPath, moveToPath);
            }

            var filesToDelete = Directory.GetFiles(nhmRootPath)
                .Where(IsValidSnapshotPath);
            foreach (var filePath in filesToDelete)
            {
                var fileName = Path.GetFileName(filePath);
                var moveToPath = Path.Combine(tmpMove, fileName);
                Console.WriteLine($"File '{filePath}' -> '{moveToPath}'");
                File.Move(filePath, moveToPath);
            }

            ZipFile.ExtractToDirectory(snapshotPath, nhmRootPath);
        }

        private static void SaveRegistryValues(string registryJson)
        {
            var savedRegistryEntries = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Tuple<string, string, string, object>>>(registryJson);
            foreach (var regEntry in savedRegistryEntries)
            {
                var typeOfRegistry = regEntry.Item1;
                var keyName = regEntry.Item2;
                var valueName = regEntry.Item3;
                var value = regEntry.Item4;

                switch (typeOfRegistry)
                {
                    //case "HKEY_LOCAL_MACHINE":
                    //    var localKey = Registry.LocalMachine.OpenSubKey(keyName, true);
                    //    localKey.SetValue(valueName, value);
                    //    break;
                    case "HKEY_CURRENT_USER":
                        var userKey = Registry.CurrentUser.OpenSubKey(keyName, true);
                        userKey.SetValue(valueName, value);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
