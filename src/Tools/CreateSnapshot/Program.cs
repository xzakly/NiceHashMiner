using Microsoft.Win32;
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
                        CreateSnapshotArchive(path, snapTag);
                        break;
                    case "-pop":
                        UnzipSnapshot(path);
                        break;
                    default:
                        break;
                }
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

            //create unzip bat script
            var scriptContent = $@"rem this script should be located inside %NHM_ROOT_PATH%\tools
                echo %cd% 
                .\CreateSnapshot.exe -pop %cd%\{snapTag}.zip               
                cd ..\
                echo %cd% 
                @echo off
                setlocal ENABLEDELAYEDEXPANSION
                for /f ""tokens=*"" %%k in (registrySnapshot.txt) do (		
                        echo.%%~k | FIND /I ""HKEY"">Nul && (
                             REM echo Found ""HKEY""
                             set key=%%~k
                             echo !key!
		                ) || (
                          REM echo Did not find ""HKEY""
                          REM echo to je k %% k
                          for /F ""tokens=1-3"" %%a in (""%%k"") do (
                          echo Value name %%a
                          echo Type %%b
                          echo Data %%c
                          reg add !key! /f /v %%a /t %%b /d %%c
		                  )
		                )
	                )
	
                endlocal
                pause";

            File.WriteAllText(Path.Combine(snapshotsLocation, $"{snapTag}.bat"), scriptContent);
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
    }
}
