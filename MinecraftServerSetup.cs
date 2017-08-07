using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace MinecraftServerSetup
{
    public class MinecraftServerSetup
    {
        FileTransactionScheduler fileTransactions;

        public MinecraftServerSetup(SetupOptions options)
        {
            fileTransactions = new FileTransactionScheduler();
            Options = options;
        }

        public SetupOptions Options { get; private set; }
        public MinecraftServerProperties Properties { get; private set; }


        public void StartSetup()
        {
            if (!Directory.Exists(Options.ServerName))
                Directory.CreateDirectory(Options.ServerName);

            CheckOptions();
        }
        public MinecraftServerSetup FinishSetup()
        {
            CheckOptions();

            ;
            return null;
        }

        void CheckOptions()
        {
            if (!string.IsNullOrEmpty(Options.ServerPropertiesSource))
                ScheduleBackup(Path.Combine(Options.ServerPropertiesSource, "server.properties"), Options.ServerName);

            if (!string.IsNullOrEmpty(Options.WhitelistSource))
                ScheduleBackup(Path.Combine(Options.WhitelistSource,"whitelist.json"), Options.ServerName);

            if (!string.IsNullOrEmpty(Options.BanlistSource))
            {
                ScheduleBackup(Path.Combine(Options.BanlistSource, "banned-ips.json"), Options.ServerName);
                ScheduleBackup(Path.Combine(Options.BanlistSource, "banned-players.json"), Options.ServerName);
            }

            if (!string.IsNullOrEmpty(Options.UsercacheSource))
                ScheduleBackup(Path.Combine(Options.UsercacheSource,"usercache.json"), Options.ServerName);

            if (!string.IsNullOrEmpty(Options.OperatorsSource))
                ScheduleBackup(Path.Combine(Options.OperatorsSource,"ops.json"), Options.ServerName);

            if (!string.IsNullOrEmpty(Options.MapDataSource))
            {
                var levelName = "world";
                var sourceProps = new MinecraftServerProperties(Path.Combine(Options.MapDataSource, "server.properties"));

                if (sourceProps.ContainsKey("level-name"))
                    levelName = sourceProps["level-name"];

                ScheduleDirectoryBackup(Path.Combine(Options.MapDataSource, levelName),
                                        Path.Combine(Options.ServerName,Options.ServerName));
            }
        }

        public AsyncProgressResult BeginFileTransactions()
        {
            return fileTransactions.BeginTransations();
        }

        public void ScheduleCleanupDirectory()
        {
            var dir = new DirectoryInfo(Options.ServerName);

            var files = dir.GetFiles()
                           .Where(f => !f.Name.EndsWith(".back"));
            var directories = dir.GetDirectories()
                           .Where(d => !d.Name.EndsWith(".back"));


            foreach (var file in files)
                fileTransactions.ScheduleFileDelete(file);

            foreach (var directory in directories)
                fileTransactions.ScheduleDirectoryDelete(directory);
        }

        public void ScheduleRestoreBackups()
        {
            var dir = new DirectoryInfo(Options.ServerName);

            var files = dir.GetFiles()
                           .Where(f => f.Name.EndsWith(".back"));
            var directories = dir.GetDirectories()
                           .Where(d => d.Name.EndsWith(".back"));

            foreach (var file in files)
            {
                var destName = file.FullName.Remove(file.FullName.Length - 5);
                if (File.Exists(destName))
                    fileTransactions.ScheduleFileDelete(destName);
                fileTransactions.ScheduleFileMove(file.FullName, destName);
            }
            //file.MoveTo(file.Name.Remove(file.Name.Length - 5));

            foreach (var directory in directories)
            {
                var destName = directory.FullName.Remove(directory.FullName.Length - 5);
                if (Directory.Exists(destName))
                    fileTransactions.ScheduleDirectoryDelete(destName);
                fileTransactions.ScheduleDirectoryMove(directory.FullName, destName);
            }
        }
        void ScheduleBackup(string sourcePath, string destDirectory)
        {
            var file = new FileInfo(sourcePath);
            var destPath = Path.Combine(destDirectory, file.Name + ".back");

            // If there is already a backup file with this name, delete it
            if (File.Exists(destPath))
                fileTransactions.ScheduleFileDelete(destPath);

            fileTransactions.ScheduleFileCopy(sourcePath,
                destPath,
                optional: true);
        }

        void ScheduleDirectoryBackup(string sourceDirectory, string destDirectory)
        {
            var destination = destDirectory + ".back";
            if (Directory.Exists(destination))
                fileTransactions.ScheduleFileDelete(destination);

            fileTransactions.ScheduleDirectoryCopy(sourceDirectory,
                destination,
                optional: true);
        }

        public void ScheduleDownloadServerJar()
        {
            var installerFile = Options.GetServerJarName();

            if (!Directory.Exists(".shared"))
                Directory.CreateDirectory(".shared");
            else if (File.Exists(Path.Combine(".shared", installerFile)))
                return;

            var version = Options.ServerVersion;

            var installerAddress = string.Format("https://s3.amazonaws.com/Minecraft.Download/versions/{0}.{1}/", version[0], version[1]);

            var installerSource = Path.Combine(installerAddress, installerFile);
            var installerDest = Path.Combine(".shared", installerFile);

            fileTransactions.ScheduleFileDownload(installerSource, installerDest);
        }

        public void RunJar()
        {

            var process = new Process();
            process.StartInfo.FileName = "java.exe";
            process.StartInfo.WorkingDirectory = Path.GetFullPath(Options.ServerName);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.Arguments = GetServerDefaultCommandLineArgs(Options.GetServerJarName());

            process.Start();

            // Minecraft Server V1.8, V1.7, and V1.6 keeps the server instance up after first-time setup
            // Send them the "stop" command so they'll end immediately after setup
            if (Options.ServerVersion.CompareTo(1, 8) <= 0)
                process.StandardInput.WriteLine("stop");

            process.WaitForExit();
        }

        public bool AgreeToEula()
        {
            // Minecraft Server versions before 1.8 didn't require the EULA
            //  We'll keep the statement in anyways! But no need to edit the file
            if (Options.ServerVersion.CompareTo(1, 8) >= 0)
            {
                var eulaPath = Path.Combine(Options.ServerName, "eula.txt");
                if (!File.Exists(eulaPath))
                    return false;

                var sr = new StreamReader(eulaPath);
                string data = sr.ReadToEnd();
                sr.Close();

                var sw = new StreamWriter(eulaPath, false);
                sw.Write(data.Replace("false", "true"));
                sw.Close();
            }
            return true;
        }

        public void AlterServerPropertiesFile()
        {
            var propertiesPath = Path.Combine(Options.ServerName, "server.properties");
            var properties = new MinecraftServerProperties(propertiesPath);


            if (!properties.ContainsKey("motd") || (properties.ContainsKey("motd") && properties["motd"] == "A Minecraft Server") )
                properties["motd"] = string.Format("Welcome to {0}\\!", Options.ServerName);
            else if (properties.ContainsKey("level-name"))
                properties["motd"] = properties["motd"].Replace(properties["level-name"], Options.ServerName);
            
            properties["level-name"] = Options.ServerName;

            if (!string.IsNullOrWhiteSpace(Options.ServerSeed))
                properties["level-seed"] = Options.ServerSeed;

            properties.WriteToFile(propertiesPath);
        }

        public void CreateServerLaunchBin()
        {
            var sw = new StreamWriter(Path.Combine(Options.ServerName, "LaunchServer.bat"), false);

            sw.Write("java {0}\npause", GetServerDefaultCommandLineArgs(Options.GetServerJarName()) );
            sw.Flush();
            sw.Close();
        }


        public static string GetServerDefaultCommandLineArgs(string serverJar)
        {
            return string.Format("-Xmx1024M -Xms1024M -jar \"..\\.shared\\{0}\" nogui", serverJar);
        }






        public static IEnumerable<DirectoryInfo> GetExistingServerInstances()
        {
            return from dir in Directory.GetDirectories(Directory.GetCurrentDirectory())
                   let dirInfo = new DirectoryInfo(dir)
                   let files = dirInfo.GetFiles()
                   where files.Any(file => file.Name.ToLower() == "server.properties") &&
                         files.Any(file => Regex.IsMatch(file.Name.ToLower(), "launchserver.bat"))
                   select dirInfo;
        }

        public static bool ServerExists(string name)
        {
            return Directory.Exists(name) &&
                   Directory.GetFiles(name).Any(file => file.ToLower().EndsWith("server.properties"));
        }

        public static FileInfo GetServerJar(string serverName)
        {
            if (!Directory.Exists(serverName))
                throw new ArgumentException(string.Format("Server does not exist: {0}", serverName));

            var dirInfo = new DirectoryInfo(serverName);
            var jar = dirInfo.GetFiles(serverName)
                      .FirstOrDefault(file => Regex.IsMatch(file.Name.ToLower(), @"minecraft_server.*\.jar"));

            return jar;
        }

        public static bool ServerVersion(string name)
        {
            return Directory.Exists(name) &&
                   Directory.GetFiles(name).Any(file => file.ToLower().EndsWith("server.properties"));
        }



        public struct SetupOptions
        {
            public string ServerName { get; set; }
            public string ServerPropertiesSource { get; set; }
            public string ServerSeed { get; set; }
            public string WhitelistSource { get; set; }
            public string BanlistSource { get; set; }
            public string UsercacheSource { get; set; }
            public string OperatorsSource { get; set; }
            public string MapDataSource { get; set; }

            public string ExistingServerSource { get; set; }
            public Version ServerVersion { get; set; }


            public bool InvalidServerName()
            {
                return string.IsNullOrWhiteSpace(ServerName) ||
                        Regex.IsMatch(ServerName, @"[^-_a-zA-Z0-9\s]");
            }

            public string GetServerJarName()
            {
                return string.Format("minecraft_server.{0}.{1}.jar", ServerVersion[0], ServerVersion[1]);
            }

            public string GetServerJarPath()
            {
                return Path.Combine(ServerName, GetServerJarName());
            }

            public static SetupOptions Empty
            {
                get { return new SetupOptions(); }
            }
        }
    }
}
