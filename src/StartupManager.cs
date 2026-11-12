using System;
using System.IO;
using Microsoft.Win32;

namespace MiyouDaily
{
    public static class StartupManager
    {
        const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName="MiyouDaily.Fairy";
        public static bool IsEnabled()
        {using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return IsEnabled(key);}
        internal static bool IsEnabled(RegistryKey key)
        {return key!=null&&!string.IsNullOrWhiteSpace(key.GetValue(ValueName) as string);}
        internal static string Command(string exe)
        {
            string full=Path.GetFullPath(exe);
            if(full.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new ArgumentException("启动程序路径无效。");
            return "\""+full+"\" --autostart";
        }
        public static void SetEnabled(bool enabled,Store store,string sourceExe)
        {
            if(enabled)
            {
                string exe=AppInstallation.Ensure(store,sourceExe);
                using(var key=Registry.CurrentUser.CreateSubKey(RunKey))Apply(key,true,exe);
            }
            else using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true)){if(key!=null)Apply(key,false,null);}
        }
        internal static void Apply(RegistryKey key,bool enabled,string exe)
        {if(enabled)key.SetValue(ValueName,Command(exe),RegistryValueKind.String);else key.DeleteValue(ValueName,false);}
    }
}
