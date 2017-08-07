using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Diagnostics;

namespace MinecraftServerSetup
{
    class Program
    {
        static MinecraftServerSetup.SetupOptions options;

        [STAThread]
        static void Main(string[] args)
        {
            bool agree = false;

            DisplayGreeting();

            CheckOrCreateIPUtil();
            if (!CheckJava())
                return;

            if (JavaInfo.CheckOrCreatePathEnvVar() == JavaInfo.Result.Warning)
            {
                agree = PromptYesNo("The version of Java currently set for your account's Path Environment Variable",
                                        "  is not the newest version available on your system!",
                                        "Would you like to update the Path Environment Variable?");

                if (agree)
                    JavaInfo.ForceUpdatePathEnvVar();
            }

            options = MinecraftServerSetup.SetupOptions.Empty;

            PromptServerName();
            PromptServerVersion();
            PromptServerSeed();

            DisplayNewScreen();
            PromptCopySettings();
            
            var setup = new MinecraftServerSetup(options);

            setup.StartSetup();

            setup.ScheduleDownloadServerJar();


            var asyncErrors = WaitAsyncProgress(setup.BeginFileTransactions(),
                                "Making settings backups and loading server JAR");
            if(asyncErrors.Count() > 0)
            {
                ReportErrors(asyncErrors);
                Console.WriteLine("\n[ERROR]: Critical errors have occurred. The setup process is unable to continue...");
                Console.ReadLine();
                return;
            }
            
            setup.ScheduleCleanupDirectory();
            asyncErrors = WaitAsyncProgress(setup.BeginFileTransactions(),
                                "Cleaning directory for setup");
            if (asyncErrors.Count() > 0)
            {
                ReportErrors(asyncErrors);
                Console.WriteLine("\n[ERROR]: Critical errors have occurred. The setup process is unable to continue...");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Executing first-time setup, this may take a minute...");
            setup.RunJar();
            Console.WriteLine();

            DisplayNewScreen();
            if (!PromptEula())
                return;

            if(!setup.AgreeToEula())
            {
                Console.WriteLine("Unable to agree to the EULA. Please try to edit it by hand");
            }
            setup.ScheduleRestoreBackups();

            asyncErrors = WaitAsyncProgress(setup.BeginFileTransactions(),
                                "Restoring saved settings");
            ReportErrors(asyncErrors);

            setup.CreateServerLaunchBin();

            setup.AlterServerPropertiesFile();

            setup.FinishSetup();

            Console.WriteLine("\nDone!");

            DisplayNewScreen();
            DisplayGetIPInfo();

            Console.WriteLine("From now on, just start the Server using LaunchServer.bat in the {0} folder. Enjoy!", options.ServerName);

            DisplaySeperator();

            agree = PromptYesNo("Would you like to start the server now?");
            if(agree)
            {
                Console.WriteLine("Starting server JAR...");
                var process = Process.Start(new ProcessStartInfo() {
                    FileName = "java.exe",
                    WorkingDirectory = Path.GetFullPath(options.ServerName),
                    Arguments = MinecraftServerSetup.GetServerDefaultCommandLineArgs(options.GetServerJarName()),
                    CreateNoWindow = false,
                    UseShellExecute = true
                } );
                Console.WriteLine();
            }

            Console.WriteLine("Setup will now close...");
            Thread.Sleep(2000);
        }
        static void DisplayFillLine(char character)
        {
            Console.WriteLine(new String(character, Console.WindowWidth));
        }
        static void Error(string err, params object[] args)
        {
            Console.Error.WriteLine(string.Format(err, args));
        }
        static void ReportErrors(IEnumerable<Exception> errors)
        {
            foreach (Exception e in errors)
                Console.WriteLine("[ERROR]: {0}", e.Message);
        }
        static void ReportErrors(IEnumerable<string> errors)
        {
            foreach (string msg in errors)
                Console.WriteLine("[ERROR]: {0}", msg);
        }
        static IEnumerable<Exception> WaitAsyncProgress(AsyncProgressResult async, string output)
        {
            var cursorPos = Console.CursorLeft;
            while (!async.IsCompleted)
            {
                Console.CursorLeft = cursorPos;
                Console.Write("{0}: {1}%", output, Thread.VolatileRead(ref async.AsyncState.progress));
                DisplayClearRemainingRow();

                Thread.Sleep(1000);
            }
            Console.CursorLeft = cursorPos;
            Console.WriteLine("{0}: 100%\n", output);
            DisplayClearRemainingRow();

            return async.AsyncState.errors;
        }

        static void DisplayGreeting()
        {
            DisplaySeperator();

            Console.WriteLine("* (C)2016 Erekose Craft - airakose.com");
            Console.WriteLine("* This software is free to use and distribute so long as");
            Console.WriteLine("*  this notice remains intact and unaltered.");
            Console.WriteLine("* This software is distributed as-is and without warranty");
            Console.WriteLine("*");
            Console.WriteLine("* Minecraft is owned and maintained by Mojang and Microsoft");
            Console.WriteLine("* The server JARs used belong to Mojang and no credit is claimed");

            DisplaySeperator();
            Console.WriteLine();
            Console.WriteLine("~~ Welcome to the Unofficial Windows Minecraft Server Setup Utility ~~");
            Console.WriteLine();

            Console.WriteLine("This utility will guide you through setting up your Minecraft Servers");
            Console.WriteLine("  on a Windows machine using the official server application.");
            Console.WriteLine();
            Console.WriteLine("+ An internet connection may be required to download the server if it's not already present");

            DisplaySeperator();
        }

        public static void DisplayNewScreen()
        {
            var row = Console.CursorTop;
            var col = Console.CursorLeft;

            Console.Write(new String(' ', Console.WindowHeight * Console.WindowWidth));

            Console.CursorTop = row;
            Console.CursorLeft = col;
            Console.SetWindowPosition(col, row);
        }

        public static void DisplayGetIPInfo()
        {
            DisplaySeperator();

            Console.WriteLine("Now that everything is set, all you need is to get people to join your server!");
            Console.WriteLine();

            Console.WriteLine("For people to join your server, they will need your machine's IP address.");
            Console.WriteLine();

            Console.WriteLine("Provided with this utility is a file called GetIPInfo, running that should");
            Console.WriteLine("  give you your IP at the time- usually next to 'IPv4 Address'");
            Console.WriteLine();

            Console.WriteLine("+ Note that this address may change occassionally if it is not static");

            DisplaySeperator();

            var address = GetServerIPv4();

            if (address != null)
                Console.WriteLine("Your IPv4 Address is currently: {0}", address);
            else
                Console.WriteLine("Unable to detect your machine's current IPv4 Address.");

            DisplaySeperator();
        }

        public static void DisplayClearRemainingRow()
        {
            var row = Console.CursorTop;
            var col = Console.CursorLeft;
            
            for (var i = Console.WindowWidth; i > col; --i)
                Console.Write(" ");
            Console.CursorLeft = col;
            Console.CursorTop = row;
        }

        static string GetServerIPv4()
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());

            foreach(var ip in addresses)
            {
                if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return null;
        }

        static void DisplaySeperator()
        {
            DisplayFillLine('=');
            Console.WriteLine();
        }

        static string[] GetLinesFitWidth(int width, string text)
        {
            var stack = new Stack<string>();
            int start = 0, end = 0;

            for (int i = 0; i < text.Length; ++i)
            {
                var chara = text[i];
                if (i - start >= width || chara == '\n')
                {
                    if (end <= start || end - start < width / 2)
                        end = i - 1;

                    if(chara == '\n')
                    {
                        stack.Push(text.Substring(start, end - start + 1));
                        continue;
                    }

                    stack.Push(text.Substring(start, end - start + 1));
                    start = i+1;
                }

                if (char.IsWhiteSpace(chara))
                    end = i - 1;
                else if (chara == '-')
                    end = i;
            }
            if(start < text.Length-1)
            {
                stack.Push(text.Substring(start, text.Length - start));
            }
            return stack.ToArray();
        }

        static void DisplayTextCentered(params string[] text)
        {

        }

        static void DisplayTitle(string title)
        {
            DisplayFillLine('=');

            Console.WriteLine();
        }

        static bool PromptYesNo(params string[] outputLines)
        {

            var answer = "";
            while (true)
            {
                for (var i = 0; i < outputLines.Length-1; ++i)
                {
                    Console.WriteLine(outputLines[i]);
                }
                Console.WriteLine("{0} (Y/N)",outputLines[outputLines.Length - 1]);
                answer = Console.ReadLine().ToLower();

                if (answer[0] == 'y' || answer[0] == 'n')
                    break;

                Console.WriteLine("Invalid answer. Please answer with a 'Y' for yes or an 'N' for no.");
            }
            Console.WriteLine();
            return answer[0] == 'y';
        }

        static int PromptNoneOrList(IEnumerable list, params string[] outputLines)
        {

            foreach (string line in outputLines)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();
            Console.WriteLine("1) None");

            int num = 1;
            foreach (var obj in list)
            {
                Console.WriteLine("{0}) {1}", ++num, obj);
            }
            int maxNum = num;

            while (true)
            {
                Console.WriteLine("\nPlease select a number from the above list.");
                if (!int.TryParse(Console.ReadLine(), out num) ||
                    num < 1 || num > maxNum)
                {
                    Console.WriteLine("Invalid input. Please select a number from the list above.");
                    continue;
                }

                break;
            }
            Console.WriteLine();
            // None is option 1, we want it to be -1
            // Then the list indicies range from 0 to Length
            return num - 2;
        }
        static int PromptList(IEnumerable list, params string[] outputLines)
        {

            foreach (string line in outputLines)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();

            int num = 0;
            foreach (var obj in list)
            {
                Console.WriteLine("{0}) {1}", ++num, obj);
            }
            int maxNum = num;

            while (true)
            {
                Console.WriteLine("\nPlease select a number from the above list.");
                if (!int.TryParse(Console.ReadLine(), out num) ||
                    num < 1 || num > maxNum)
                {
                    Console.WriteLine("Invalid input.");
                    continue;
                }
                break;
            }
            Console.WriteLine();
            return num-1;
        }

        static void PromptServerName()
        {
            while (true)
            {
                Console.WriteLine("What would you like to name your server?");
                options.ServerName = Console.ReadLine() ?? "";

                if (!options.InvalidServerName())
                    break;

                Console.WriteLine("Invalid server name. Please only use spaces, letters, numbers, dashes, and underscores.");
            }
            Console.WriteLine();
        }
        static void PromptServerVersion()
        {
            var versionList = new string[] { "1.10", "1.9", "1.8", "1.7", "1.6" };
            var value = PromptList( versionList,
                                    "Which server version would you like to run?");

            options.ServerVersion = new Version( versionList[value] );
        }
        static void PromptServerSeed()
        {
            var agree = PromptYesNo("Would you like to use a custom seed for generating your map?",
                                    "Seeds are values that Minecraft uses to create maps 'randomly'");

            if (agree)
            {
                string seed = "";

                do
                {
                    Console.WriteLine("Please type in any text to use for your seed and hit [Enter] when you're done.");
                    seed = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(seed))
                        Console.WriteLine("Invalid seed! Please provide some text to use as your seed.");

                } while (string.IsNullOrWhiteSpace(seed));
                Console.WriteLine();

                options.ServerSeed = seed;
            }
        }
        static void PromptCopySettings()
        {
            var existingServers = MinecraftServerSetup.GetExistingServerInstances().ToList();

            if (existingServers.Count() > 0)
            {
                var agree = PromptYesNo("Some servers already exist on this machine, would you like to import any data or settings from any of them?");

                if (agree)
                {
                    PromptKeepSettingsAll(existingServers);
                }
                else
                {
                    if (MinecraftServerSetup.ServerExists(options.ServerName))
                    {
                        PromptServerExists();
                    }
                }
            }
        }

        static void PromptServerExists()
        {
            var agree = PromptYesNo(string.Format("There currently is already a server with this name: {0}", options.ServerName),
                        "!! It will be overwritten !!",
                        "Would you like to keep any of the existing settings?");

            if(!agree)
            {
                agree = PromptYesNo("There will be no way to recover the files once the operation is complete.",
                                    "Are you sure you /Do Not/ wish to retain any settings?");
                if (agree)
                {
                    return;
                }
            }
            PromptKeepSettingsFrom(options.ServerName);
        }

        static void PromptKeepSettingsAll(IList<DirectoryInfo> servers)
        {
            var serverNames = servers.Select(dir => dir.Name);

            var value = PromptNoneOrList(serverNames,
                            "Would you like to keep the Server Properties for any of these?");
            if (value > -1)
                options.ServerPropertiesSource = servers[value].FullName;


            value = PromptNoneOrList(serverNames,
                            "Would you like to keep the Banlists for any of these?");
            if (value > -1)
                options.BanlistSource = servers[value].FullName;


            value = PromptNoneOrList(serverNames,
                            "Would you like to keep the Whitelists for any of these?");
            if (value > -1)
                options.WhitelistSource = servers[value].FullName;


            value = PromptNoneOrList(serverNames,
                            "Would you like to keep the Player Data for any of these?");
            if (value > -1)
                options.UsercacheSource = servers[value].FullName;


            value = PromptNoneOrList(serverNames,
                            "Would you like to keep the Operators list (AKA Moderators) for any of these?");
            if (value > -1)
                options.OperatorsSource = servers[value].FullName;
            

            value = PromptNoneOrList(serverNames,
                            "Would you like to keep the Map for any of these?");
            if (value > -1)
                options.MapDataSource = servers[value].FullName;
        }

        static void PromptKeepSettingsFrom(string serverName)
        {
            var agree = PromptYesNo("Would you like to keep the existing Server Properties?");
            if (agree)
                options.ServerPropertiesSource = serverName;


            agree = PromptYesNo("Would you like to keep the existing Banlists?");
            if (agree)
                options.BanlistSource = serverName;


            agree = PromptYesNo("Would you like to keep the existing Whitelists?");
            if (agree)
                options.WhitelistSource = serverName;


            agree = PromptYesNo("Would you like to keep the existing Player Data?");
            if (agree)
                options.UsercacheSource = serverName;


            agree = PromptYesNo("Would you like to keep the existing Operators list (AKA Moderators)?");
            if (agree)
                options.OperatorsSource = serverName;


            agree = PromptYesNo("Would you like to keep the existing Map?");
            if (agree)
                options.MapDataSource = serverName;
        }

        static bool PromptEula()
        {
            var agree = PromptYesNo("Do you agree to the EULA listed at https://account.mojang.com/documents/minecraft_eula?");
            if (!agree)
            {
                agree = !PromptYesNo("\nAre you sure?",
                                    "If you don't agree with the EULA, you cannot continue to use this software.",
                                    "Are you certain that you /Do Not/ agree with the EULA at https://account.mojang.com/documents/minecraft_eula?");
            }
            return agree;
        }

        static string GetLocalServerVersion()
        {
            // Check if there's already a copy of this jar in the folder
            var jar = new FileInfo(Path.Combine(options.ServerName, options.GetServerJarName()));
            if (  jar.Exists  )
            {
                return jar.FullName;
            }

            var servers = MinecraftServerSetup.GetExistingServerInstances();

            foreach(var s in servers)
            {
                jar = s.GetFiles().FirstOrDefault(f => f.Name.Contains( options.GetServerJarName() ));
                if( jar != null )
                {
                    return jar.FullName;
                }
            }
            return null;
        }

        static bool CheckJava()
        {
            string javaVer = "";

            if (!JavaInfo.TryGetJavaVersion(out javaVer))
            {
                Console.WriteLine("You do not currently have Java installed. Please install java, close this window, then try again!");
                Console.WriteLine("The Java website should open in a few seconds...");
                Thread.Sleep(3000);
                JavaInfo.OpenDownloadPage();

                return PromptYesNo("Do you believe this message is in error?");
            }

            return true;
        }
        static void CheckOrCreateIPUtil()
        {
            var dest = "GetIPInfo.bat";
            if (File.Exists(dest))
                return;

            var sw = new StreamWriter(dest, false);

            sw.WriteLine("ipconfig");
            sw.Write("pause");
            sw.Flush();
            sw.Close();
        }
    }
}
