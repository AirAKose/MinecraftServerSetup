using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace MinecraftServerSetup
{
    public static class FileIOExtensions
    {
        public static void CopyTo(this DirectoryInfo baseDir, string dest)
        {
            var basePath = baseDir.FullName;
            Stack<DirectoryInfo> dirsToCopy = new Stack<DirectoryInfo>();
            dirsToCopy.Push(null);
            dirsToCopy.Push(baseDir);

            DirectoryInfo sourceDir = null;
            // Avoid recursion because it wouldn't be tail-recursion and prone to not being optimized
            // AKA large folder structures would could cause stack overflow
            while ((sourceDir = dirsToCopy.Pop()) != null)
            {
                var files = sourceDir.GetFiles();
                string subDir = sourceDir.FullName.Substring(baseDir.FullName.Length);
                string destDir = (dest+subDir).Replace("\\\\","\\").Replace("/\\","\\").Replace("//","\\").Replace("\\/","\\");

                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                foreach (var f in files)
                {
                    f.CopyTo( Path.Combine(destDir, f.Name) );
                }

                var dirs = sourceDir.GetDirectories();
                foreach(var d in dirs)
                {
                    dirsToCopy.Push(d);
                }
            }
        }
    }
}
