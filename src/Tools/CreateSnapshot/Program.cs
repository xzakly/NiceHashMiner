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
                if(args.Length < 2)
                {
                    Console.WriteLine("Not enough parameters.");
                }
                var tag = args.Length > 2 ? args[2] : "";
                switch (args[0])
                {
                    case "zip":
                        CreateSnapshotOfRegistries(args[1]);
                        CreateSnapshotArchive(args[1], tag);
                        break;
                    case "unzip":
                        UnzipSnapshot(args[1]);
                        //var registryJson = File.ReadAllText(Path.Combine(args[1], "snapshots", "registryKeys.json"));
                        //SaveRegistryValues(registryJson);
                        break;
                    default:
                        break;
                }
                //CreateSnapshotArchive(args[1], tag);
                UnzipSnapshot(args[1]);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            Console.WriteLine("end");
            Console.ReadKey();

            
        }

        private static void CreateSnapshotArchive(string nhmRootPath, string tag)
        {
            var snapshotsLocation = Path.Combine(nhmRootPath, "snapshots");
            if (!Directory.Exists(snapshotsLocation)) Directory.CreateDirectory(snapshotsLocation);
            var filesToPack = Directory.GetFiles(nhmRootPath, "*.*", SearchOption.AllDirectories)
                .Where(path => !path.Contains("tools"))
                .Where(path => !path.Contains("snapshots"));

            string archiveFileName = $"{DateTime.UtcNow.ToString("dd_MM_yyyy_HH_mm_ss")}.zip";
            Console.Write($"Preparing logs archive file '{archiveFileName}'...");

            var archivePath = Path.Combine(snapshotsLocation, archiveFileName);

            using (var fileStream = new FileStream(archivePath, FileMode.Create))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
            {
                foreach (var filePath in filesToPack)
                {
                    var entryPath = filePath.Replace(nhmRootPath, "");
                    entryPath = entryPath.Substring(1);
                    var zipFile = archive.CreateEntry(entryPath);
                    byte[] fileRawBytes;
                    if (filePath.Contains("logs"))
                    {
                        File.Copy(filePath, "tmpLog.txt");
                        fileRawBytes = File.ReadAllBytes("tmpLog.txt");
                        File.Delete("tmpLog.txt");
                    }
                    else
                    {
                        fileRawBytes = File.ReadAllBytes(filePath);
                    }
                    using (var entryStream = zipFile.Open())
                    using (var b = new BinaryWriter(entryStream))
                    {
                        b.Write(fileRawBytes);
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

        private static void UnzipSnapshot(string nhmRootPath)
        {
            var snapshotLocation = new DirectoryInfo(Path.Combine(nhmRootPath, "snapshots"));
            var lastSnapshot = snapshotLocation.GetFiles()
             .OrderByDescending(snapshot => snapshot.LastWriteTime)
             .First();

            var directoriesToDelete = Directory.GetDirectories(nhmRootPath)
                .Where(path => !path.Contains("tools"))
                .Where(path => !path.Contains("snapshots"));
            foreach (var directory in directoriesToDelete)
            {
                Directory.Delete(directory, true);
            }

            var filesToDelete = Directory.GetFiles(nhmRootPath);
            foreach (var file in filesToDelete)
            {
                File.Delete(file);
            }

            ZipFile.ExtractToDirectory(lastSnapshot.FullName, nhmRootPath);
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
