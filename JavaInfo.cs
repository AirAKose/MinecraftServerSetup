using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MinecraftServerSetup
{
    public static class JavaInfo
    {
        public enum Result
        {
            Error = -1, Success = 0, Warning = 1
        }

        public static string GetNewestJavaBinPath(bool promptUserIfUnknown = true)
        {
            var java64Path = "C:\\Program Files\\Java";
            var java32Path = "C:\\Program Files (x86)\\Java";

            var dir = SearchForJavaBinFrom(java64Path);
            if (!string.IsNullOrEmpty(dir))
                return dir;

            dir = SearchForJavaBinFrom(java32Path);
            if (promptUserIfUnknown && string.IsNullOrEmpty(dir))
                return PromptUserJavaBinDir();
            
            return dir;
        }

        static string SearchForJavaBinFrom(string basePath)
        {
            if (!Directory.Exists(basePath))
                return null;

            // Get all the directories that reference the jre or jdk
            // Strip off any trailing forward / backslashes
            var dirs = from dir in Directory.GetDirectories(basePath)
                       where dir.Contains("jre") || dir.Contains("jdk")
                       orderby Version.FindFromPathEnd(dir) descending
                       select dir;

            // Sort by version
            //dirs.OrderByDescending(dir => Version.FindFromPathEnd(dir));
            
            foreach(var dir in dirs)
            {
                if (File.Exists(Path.Combine(dir, "bin\\java.exe")))
                    return Path.Combine(dir, "bin");
            }

            return null;
        }

        static string StripVersionFromDir(string dir)
        {
            int i = dir.Length-1;
            for(;i>=0;i--)
            {
                var chara = dir[i];
                if (  !(char.IsNumber(chara) || chara == '.' || chara == '_')  )
                    break;
            }
            return dir.Substring(i+1);
        }

        static string PromptUserJavaBinDir()
        {
            do
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();

                dialog.RootFolder = Environment.SpecialFolder.ProgramFilesX86;
                dialog.Description = "Unable to determine the latest Java bin folder. Please find the 'bin' folder in the latest version of Java on your system.";

                var result = dialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    var info = new DirectoryInfo(dialog.SelectedPath);

                    if (info.Name == "bin")
                    {
                        if(File.Exists(dialog.SelectedPath + "\\java.exe"))
                        {
                            return dialog.SelectedPath;
                        }
                        else
                        {
                            MessageBox.Show("This is a 'bin' folder, but it does not contain the Java runtime. Please find another 'bin' folder for Java.\n\nLikely paths include under C:\\Program Files\\Java or C:\\Program Files (x86)\\Java","Bad Path");
                            continue;
                        }
                    }
                    else
                    {
                        MessageBox.Show("The selected directory is not 'bin'. Please select the 'bin' folder in your latest Java folder", "Bad Path");
                        continue;
                    }
                }
                else
                {
                    break;
                }
            }
            while (true);

            return null;
        }

        public static string CmdLineJavaCommand()
        {
            return Path.Combine(GetNewestJavaBinPath(), "java.exe");
        }

        public static Result CheckOrCreatePathEnvVar(bool forceUpdate = false)
        {
            var path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";

            // Match any path that ends in bin and references the jre or jdk
            var match = Regex.Match(path, @"[^;\/\\]+:(?:\\|\/)[^;]+(?:\/|\\)(?:jre|jdk)([^;]*)(?:\/|\\)bin");
            if ( string.IsNullOrWhiteSpace(match.Value) )
            {
                var binPath = GetNewestJavaBinPath();
                // If it doesn't exist, add it to the Path Environment Variable!
                if (!string.IsNullOrWhiteSpace(path) && path.Last() != ';')
                    path += ";";
                path += binPath + ";";
                Environment.SetEnvironmentVariable("Path", path, EnvironmentVariableTarget.User);
                return Result.Success;
            }

            var foundPath = GetNewestJavaBinPath(false);
            // If we're forcing the update or the path value doesn't exist on the file system, replace the path entry
            if (forceUpdate || (!Directory.Exists(match.Value) && !File.Exists(match.Value)) )
            {
                path = path.Replace(match.Value, foundPath);
                Environment.SetEnvironmentVariable("Path", path, EnvironmentVariableTarget.User);
            }
            else if (!string.IsNullOrWhiteSpace(foundPath))
            {
                var setVersion = new Version(match.Groups[1].Value);
                var foundVersion = Version.FindFromPathEnd(foundPath);

                if (setVersion.CompareTo(foundVersion) < 0)
                    return Result.Warning;
            }
            

            return Result.Success;
        }

        public static Result ForceUpdatePathEnvVar()
        {
            return CheckOrCreatePathEnvVar(true);
        }

        public static bool TryGetJavaVersion(out string version)
        {
            try
            {
                var localMachineReg = Registry.LocalMachine;
                var javaSubReg = localMachineReg.OpenSubKey("SOFTWARE\\JavaSoft\\Java Runtime Environment");

                version = javaSubReg.GetValue("CurrentVersion").ToString();
            }
            catch(Exception e)
            {
                version = e.ToString();
                return false;
            }
            return !string.IsNullOrEmpty(version);
        }

        public static void OpenDownloadPage()
        {
            System.Diagnostics.Process.Start("https://java.com/en/download/");
        }
    }
}
