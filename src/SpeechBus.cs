using System;
using System.IO;
using System.Linq;

namespace MiyouDaily
{
    public sealed class FairyMessage
    {
        public string Id {get;set;} public string Text {get;set;} public string At {get;set;}
    }
    public sealed class ScheduledNotice
    {
        public string Day {get;set;} public string Scope {get;set;} public string Text {get;set;}
    }
    public static class SpeechBus
    {
        public static void Publish(Store store,string text)
        {
            Store.AtomicWrite(Path.Combine(store.Root,"speech.json"),Json.Write(new FairyMessage{Id=Guid.NewGuid().ToString("N"),Text=text,At=DateTimeOffset.UtcNow.ToString("o")}));
        }
        public static FairyMessage Read(Store store)
        {
            string path=Path.Combine(store.Root,"speech.json");if(!File.Exists(path))return null;
            return Json.Read<FairyMessage>(File.ReadAllText(path));
        }
        public static bool PublishScheduled(Store store,Settings settings,string text)
        {
            string path=Path.Combine(store.Root,"scheduled-notice.json"),day=Clock.Day(Clock.ChinaNow),cookie="";
            if(settings.Games.Length+settings.Forums.Length>0){try{cookie=store.Decrypt(settings);}catch(InvalidOperationException){}}
            string scope=(cookie.Length==0?"logged-out":AccountScope.Prefix(cookie))+string.Join(",",settings.Games.OrderBy(x=>x))+"/"+string.Join(",",settings.Forums.OrderBy(x=>x));
            if(settings.HeyboxEnabled){try{var heybox=HeyboxAccount.Load(settings);scope+="/"+(heybox==null?"heybox-logged-out":heybox.Key);}catch(InvalidOperationException){scope+="/heybox-invalid";}}
            ScheduledNotice previous=null;
            try{if(File.Exists(path))previous=Json.Read<ScheduledNotice>(File.ReadAllText(path));}catch(ArgumentException){}catch(InvalidOperationException){}
            if(previous!=null&&previous.Day==day&&previous.Scope==scope&&previous.Text==text)return false;
            Publish(store,text);Store.AtomicWrite(path,Json.Write(new ScheduledNotice{Day=day,Scope=scope,Text=text}));return true;
        }
        public static bool HasDueWork(Store store,Settings settings,bool scheduled)
        {
            if(scheduled&&!Clock.IsDue(settings.DailyTime,Clock.ChinaNow))return false;
            bool miyouSelected=settings.Games.Length+settings.Forums.Length>0;string cookie="";if(miyouSelected){try{cookie=store.Decrypt(settings);}catch(InvalidOperationException){return true;}}if(cookie.Length==0&&miyouSelected)return true;
            var state=store.LoadState();state.ForDay(Clock.Day(Clock.ChinaNow));
            bool migrated;string prefix=miyouSelected?AccountScope.Prepare(state,cookie,out migrated):"";
            bool due=settings.Games.Any(x=>!state.Skip(prefix+"game/"+x,scheduled)) || settings.Forums.Any(x=>!state.Skip(prefix+"forum/"+x,scheduled));
            if(settings.HeyboxEnabled){try{var heybox=HeyboxAccount.Load(settings);due|=heybox==null||!state.Skip(heybox.Key,scheduled);}catch(InvalidOperationException){return true;}}
            return due;
        }
    }
}
