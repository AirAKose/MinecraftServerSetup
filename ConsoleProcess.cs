using System.Diagnostics;

namespace MinecraftServerSetup
{
    public class ConsoleProcess
    {
        Process process;

        public ConsoleProcess()
        {
            process = new Process();

            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
        }

        public bool RunCommand(string cmd)//, out string output, out string errors)
        {
            process.StartInfo.Arguments = "/c " + cmd;
            process.Start();

            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            //if(errors == null || errors.Length == 0)
            //    return true;
            return false;
        }
    }
}
