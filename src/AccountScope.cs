using System;
using System.Collections.Generic;
using System.Linq;

namespace MiyouDaily
{
    public static class AccountScope
    {
        public static string Prefix(string cookie)
        {
            string uid=Cookies.Account(cookie);
            if(uid.Length==0)throw new InvalidOperationException("登录凭证缺少账号 UID，请重新扫码登录。");
            return "uid-"+Hashes.Sha256("miyoushe/"+uid).Substring(0,16)+"/";
        }
        public static string Prepare(RunState state,string cookie,out bool changed)
        {
            string prefix=Prefix(cookie),legacy=Hashes.Sha256(cookie).Substring(0,16)+"/";changed=false;
            var aliases=new HashSet<string>(StringComparer.Ordinal){legacy};
            // A completed exact region/role binding is evidence linking an old
            // credential fingerprint to this account, even after QR token rotation.
            var bindings=new HashSet<string>(state.Targets.Where(x=>x.Value!=null&&x.Value.Complete&&(x.Key.StartsWith(legacy,StringComparison.Ordinal)||x.Key.StartsWith(prefix,StringComparison.Ordinal)))
                .Select(x=>x.Key.Substring(x.Key.IndexOf('/')+1)).Where(x=>x.StartsWith("game/",StringComparison.Ordinal)&&x.Split('/').Length==4),StringComparer.Ordinal);
            foreach(var item in state.Targets)
            {
                int slash=item.Key.IndexOf('/');
                if(slash==16&&item.Value!=null&&item.Value.Complete&&bindings.Contains(item.Key.Substring(slash+1)))aliases.Add(item.Key.Substring(0,slash+1));
            }
            foreach(var item in state.Targets.ToArray())
            {
                int slash=item.Key.IndexOf('/');if(slash<0||!aliases.Contains(item.Key.Substring(0,slash+1))||item.Value==null)continue;
                string key=prefix+item.Key.Substring(slash+1);Attempt previous;
                if(state.Targets.TryGetValue(key,out previous)&&previous!=null&&(previous.Complete||(!item.Value.Complete&&previous.Count>=item.Value.Count)))continue;
                var source=item.Value;
                state.Targets[key]=new Attempt{Count=source.Count,Complete=source.Complete,NeedsAction=source.Complete?false:source.NeedsAction,Message=source.Message};changed=true;
            }
            return prefix;
        }
    }
}
