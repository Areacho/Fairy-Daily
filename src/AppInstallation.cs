using System;
using System.IO;
using System.Security.Cryptography;

namespace MiyouDaily
{
    public static class AppInstallation
    {
        public static string Ensure(Store store,string sourceExe)
        {
            string source=Path.GetFullPath(sourceExe),directory=Path.Combine(store.Root,"app");Directory.CreateDirectory(directory);
            string target=Path.Combine(directory,"MiyouDaily.exe");
            if(!string.Equals(source,Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))
            {
                CopyIfChanged(source,target);
                if(File.Exists(source+".config"))CopyIfChanged(source+".config",target+".config");
                string sourceDirectory=Path.GetDirectoryName(source);
                foreach(string file in new[]{"Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll"})
                {
                    string dependency=Path.Combine(sourceDirectory,file);if(File.Exists(dependency))CopyIfChanged(dependency,Path.Combine(directory,file));
                }
                foreach(string arch in new[]{"x64","x86"})
                {
                    string relative=Path.Combine("runtimes","win-"+arch,"native","WebView2Loader.dll"),dependency=Path.Combine(sourceDirectory,relative);
                    if(File.Exists(dependency)){string installed=Path.Combine(directory,relative);Directory.CreateDirectory(Path.GetDirectoryName(installed));CopyIfChanged(dependency,installed);}
                }
            }
            return target;
        }
        static void CopyIfChanged(string source,string target)
        {
            if(File.Exists(target))
            {
                using(var hash=SHA256.Create())using(var first=File.OpenRead(source))using(var second=File.OpenRead(target))
                {if(first.Length==second.Length&&Convert.ToBase64String(hash.ComputeHash(first))==Convert.ToBase64String(hash.ComputeHash(second)))return;}
            }
            File.Copy(source,target,true);
        }
    }
}
