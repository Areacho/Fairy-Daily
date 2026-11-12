using System;
using System.IO;
using System.Linq;

namespace MiyouDaily
{
    public sealed class FairyMessage
    {
        public string Id {get;set;} public string Text {get;set;} public string At {get;set;}
        public bool IsCompletion {get;set;}
        public bool ExitAfterCompletion {get;set;}
        public bool IsProgress {get;set;}
        public string RunId {get;set;}
    }
    public sealed class ScheduledNotice
    {
        public string Day {get;set;} public string Scope {get;set;} public string Text {get;set;}
    }
    public sealed class NoticeReceipts
    {public string Day {get;set;} public string[] Ids {get;set;}}
    public static class SpeechBus
    {
        public static FairyMessage Publish(Store store,string text,bool completion=false,bool exitAfterCompletion=false,string runId=null)
        {
            var message=new FairyMessage{Id=Guid.NewGuid().ToString("N"),Text=text,At=DateTimeOffset.UtcNow.ToString("o"),IsCompletion=completion,ExitAfterCompletion=completion&&exitAfterCompletion,RunId=runId};
            Store.AtomicWrite(Path.Combine(store.Root,"speech.json"),Json.Write(message));return message;
        }
        public static FairyMessage PublishProgress(Store store,string text)
        {
            string id=Guid.NewGuid().ToString("N");
            var message=new FairyMessage{Id=id,RunId=id,Text=text,At=DateTimeOffset.UtcNow.ToString("o"),IsProgress=true};
            string json=Json.Write(message);Store.AtomicWrite(Path.Combine(store.Root,"speech-progress.json"),json);Store.AtomicWrite(Path.Combine(store.Root,"speech.json"),json);return message;
        }
        public static FairyMessage ReadProgress(Store store)
        {
            string path=Path.Combine(store.Root,"speech-progress.json");return File.Exists(path)?Json.Read<FairyMessage>(File.ReadAllText(path)):null;
        }
        public static FairyMessage Read(Store store)
        {
            string path=Path.Combine(store.Root,"speech.json");if(!File.Exists(path))return null;
            return Json.Read<FairyMessage>(File.ReadAllText(path));
        }
        public static bool PublishScheduled(Store store,Settings settings,string text)
        {return PublishScheduledMessage(store,settings,text,false)!=null;}
        public static FairyMessage PublishScheduledMessage(Store store,Settings settings,string text,bool completion,bool exitAfterCompletion=false,string runId=null)
        {
            string path=Path.Combine(store.Root,"scheduled-notice.json"),day=Clock.Day(Clock.ChinaNow),cookie="";
            if(settings.Games.Length+settings.Forums.Length>0){try{cookie=store.Decrypt(settings);}catch(InvalidOperationException){}}
            string scope=(cookie.Length==0?"logged-out":AccountScope.Prefix(cookie))+string.Join(",",settings.Games.OrderBy(x=>x))+"/"+string.Join(",",settings.Forums.OrderBy(x=>x));
            if(settings.HeyboxEnabled){try{var heybox=HeyboxAccount.Load(settings);scope+="/"+(heybox==null?"heybox-logged-out":heybox.Key);}catch(InvalidOperationException){scope+="/heybox-invalid";}}
            scope+="/auto-exit="+exitAfterCompletion;
            ScheduledNotice previous=null;
            try{if(File.Exists(path))previous=Json.Read<ScheduledNotice>(File.ReadAllText(path));}catch(ArgumentException){}catch(InvalidOperationException){}
            if(previous!=null&&previous.Day==day&&previous.Scope==scope&&previous.Text==text&&string.IsNullOrEmpty(runId))return null;
            var message=Publish(store,text,completion,exitAfterCompletion,runId);Store.AtomicWrite(path,Json.Write(new ScheduledNotice{Day=day,Scope=scope,Text=text}));return message;
        }
        static NoticeReceipts Receipts(Store store)
        {
            string path=Path.Combine(store.Root,"speech-receipts.json");
            try{if(File.Exists(path))return Json.Read<NoticeReceipts>(File.ReadAllText(path))??new NoticeReceipts();}catch(ArgumentException){}catch(InvalidOperationException){}
            return new NoticeReceipts();
        }
        public static bool IsPending(Store store,FairyMessage message,DateTimeOffset now)
        {
            DateTimeOffset at;if(message==null||!message.IsCompletion||string.IsNullOrEmpty(message.Id)||!DateTimeOffset.TryParse(message.At,out at)||Clock.Day(at)!=Clock.Day(now)||at>now.AddMinutes(5))return false;
            var receipts=Receipts(store);return receipts.Day!=Clock.Day(now)||receipts.Ids==null||!receipts.Ids.Contains(message.Id);
        }
        public static bool IsProgressPending(Store store,FairyMessage message,DateTimeOffset now)
        {
            DateTimeOffset at;
            if(message==null||!message.IsProgress||string.IsNullOrEmpty(message.Id)||!DateTimeOffset.TryParse(message.At,out at)||Clock.Day(at)!=Clock.Day(now)||now-at>TimeSpan.FromMinutes(2)||at>now.AddSeconds(5))return false;
            var receipts=Receipts(store);return receipts.Day!=Clock.Day(now)||receipts.Ids==null||!receipts.Ids.Contains(message.Id);
        }
        public static void MarkDelivered(Store store,FairyMessage message)
        {
            if(message==null||(!message.IsCompletion&&!message.IsProgress))return;string day=Clock.Day(Clock.ChinaNow);var receipts=Receipts(store);
            var ids=receipts.Day==day&&receipts.Ids!=null?receipts.Ids:new string[0];
            Store.AtomicWrite(Path.Combine(store.Root,"speech-receipts.json"),Json.Write(new NoticeReceipts{Day=day,Ids=ids.Concat(new[]{message.Id}).Distinct().Reverse().Take(64).Reverse().ToArray()}));
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
