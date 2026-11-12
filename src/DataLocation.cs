using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MiyouDaily
{
    public static class DataLocation
    {
        public static Store Open()
        {
            return Open(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }
        // Keep data outside MSIX's redirected AppData, so desktop and Task
        // Scheduler processes always open the same encrypted credentials.
        internal static Store Open(string profile,string local)
        {
            var store=new Store(Path.Combine(profile,".miyou-daily"));
            if(File.Exists(store.ConfigPath))return store;
            using(store.AcquireRunLock())
            {
                if(File.Exists(store.ConfigPath))return store;
                var candidates=new List<string>{Path.Combine(local,"MiyouDaily")};
                string packages=Path.Combine(local,"Packages");
                if(Directory.Exists(packages))
                    foreach(string package in Directory.GetDirectories(packages,"OpenAI.Codex_*"))
                        candidates.Add(Path.Combine(package,@"LocalCache\Local\MiyouDaily"));
                var available=new List<KeyValuePair<string,Settings>>();
                foreach(string candidate in candidates)
                {
                    string config=Path.Combine(candidate,"config.json");if(!File.Exists(config))continue;
                    var settings=Json.Read<Settings>(File.ReadAllText(config));
                    if(settings==null||settings.Version!=1)continue;
                    settings.Validate();available.Add(new KeyValuePair<string,Settings>(candidate,settings));
                }
                var chosen=available.OrderByDescending(x=>!string.IsNullOrEmpty(x.Value.ProtectedCookie))
                    .ThenByDescending(x=>File.GetLastWriteTimeUtc(Path.Combine(x.Key,"config.json"))).FirstOrDefault();
                if(chosen.Key==null)return store;
                foreach(string file in new[]{"state.json","widget.json"})
                {
                    string source=Path.Combine(chosen.Key,file);
                    if(File.Exists(source))Store.AtomicWrite(Path.Combine(store.Root,file),File.ReadAllText(source));
                }
                string logs=Path.Combine(chosen.Key,"logs");
                if(Directory.Exists(logs))
                {
                    string destination=Path.Combine(store.Root,"logs");Directory.CreateDirectory(destination);
                    foreach(string file in Directory.GetFiles(logs,"*.log"))
                    {
                        string target=Path.Combine(destination,Path.GetFileName(file));if(!File.Exists(target))File.Copy(file,target,false);
                    }
                }
                // Commit the configuration last; leave the old data untouched.
                store.Save(chosen.Value);
                store.Log("已迁移旧版数据，桌面挂件与 Windows 定时任务使用统一数据目录。");
                return store;
            }
        }
    }
}
