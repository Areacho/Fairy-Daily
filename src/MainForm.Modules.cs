using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed partial class MainForm
    {
        public event Action<FairySection> ModuleRequested;
        public event Action DialogueDismissRequested;
        public event Action<FairyMessage> CheckinCompleted;
        internal string ReportCompletion(Settings selected,RunSummary result)
        {
            var notice=CheckinNotifications.Publish(store,selected,result,false,settings);
            if(CheckinCompleted!=null)CheckinCompleted(notice);else Speak(notice.Text);return notice.Text;
        }
        int dialogueVersion;
        internal void DismissPageDialogue(){dialogueVersion++;if(DialogueDismissRequested!=null)DialogueDismissRequested();}
        internal bool HasMiyouLogin {get{try{return store.Decrypt(settings).Length>0;}catch(InvalidOperationException){return false;}}}
        internal bool HasHeyboxLogin {get{try{return HeyboxAccount.Load(settings)!=null;}catch(InvalidOperationException){return false;}}}
        internal Settings ModuleSettings {get{CaptureSelection();return Json.Read<Settings>(Json.Write(settings));}}
        internal bool ShouldExitAfterCompletion(FairyMessage notice)
        {
            DateTimeOffset at;
            return notice!=null&&notice.IsCompletion&&notice.ExitAfterCompletion&&settings.ExitWidgetAfterCheckin&&
                DateTimeOffset.TryParse(notice.At,out at)&&Clock.Day(at)==Clock.Day(Clock.ChinaNow)&&CheckinStatus.IsTodayComplete(store,settings);
        }
        public void RequestModule(FairySection section){if(ModuleRequested!=null)ModuleRequested(section);}
        public void Tell(string text){Speak(text);}
        internal string AccountDescription(FairyPlatform platform)
        {
            if(platform==FairyPlatform.Miyou)
            {try{string raw=store.Decrypt(settings);return raw.Length==0?"尚未登录 · 等待主人授权":"已接入 · UID "+Cookies.Account(raw);}catch(InvalidOperationException){return "登录状态需更新，请重新接入";}}
            try{var account=HeyboxAccount.Load(settings);return account==null?"尚未登录 · 支持短信验证码":"已接入 · 盒友 ID "+account.session.heybox_id;}
            catch(InvalidOperationException){return "登录状态需更新，请重新接入";}
        }
        internal Task LoginMiyou(Form owner){Tell("主人，米游社登录通道正在展开。请用米游社 App 扫码，剩下的交给我。");return Login(owner);}
        internal void ImportMiyou(Form owner){Tell("主人，请将米游社凭证交给本机保管。我会加密保存。");ImportCookie(owner);}
        internal void LoginHeybox(Form owner){Tell("主人，小黑盒登录通道已就绪。验证码只用于这次接入。");OpenHeybox(owner);}
        internal Task ImportHeyboxAccount(Form owner){Tell("主人，请选择小黑盒账号配置。我会先校验，再加密保存。");return ImportHeybox(owner);}
        void ApplyModuleSettings(Settings next,string response)
        {
            if(IsWorking)throw new InvalidOperationException("当前委托正在执行，请稍后再调整设定。");
            next.Validate();store.Save(next);settings=next;
            foreach(var tile in tiles){if(tile.GameSwitch!=null)tile.GameSwitch.Checked=settings.Games.Contains(tile.Key);tile.ForumSwitch.Checked=settings.Forums.Contains(tile.ForumId);}
            heyboxTile.SignSwitch.Checked=settings.HeyboxEnabled;time.Value=DateTime.Today+Clock.ParseTime(settings.DailyTime);
            if(Scheduler.Exists())Scheduler.Install(store,settings,Application.ExecutablePath);
            RefreshStatus();Write(response);Tell(response);
        }
        internal void SaveMiyouOptions(string[] games,string[] forums)
        {var next=ModuleSettings;next.Games=games;next.Forums=forums;ApplyModuleSettings(next,"主人，米游社委托已确认。选中的游戏福利和社区签到，我都记下了。");}
        internal void SaveHeyboxOptions(bool enabled)
        {var next=ModuleSettings;next.HeyboxEnabled=enabled;ApplyModuleSettings(next,enabled?"主人，小黑盒已加入每日委托。执行时间与定时设定保持一致。":"主人，小黑盒每日委托已暂停，需要时再交给我。");}
        internal void SaveModuleTime(string value)
        {var next=ModuleSettings;next.DailyTime=value;ApplyModuleSettings(next,"主人，时间坐标已确认：每天北京时间 "+value+"。");}
        internal void SetModuleAutoExit(bool enabled)
        {
            if(IsWorking)throw new InvalidOperationException("当前委托正在执行，请稍后再调整设定。");
            var next=ModuleSettings;next.ExitWidgetAfterCheckin=enabled;next.Validate();store.Save(next);settings=next;
            string response=enabled?"主人，全部签到完成后，我会先通知您，8 秒后退出挂件。每日定时仍会照常执行。":"主人，签到完成后我会继续待命，挂件不会自动退出。";
            Write(response);Tell(response);
        }
        internal void InstallModuleSchedule(string value)
        {
            var next=ModuleSettings;next.DailyTime=value;
            if(!((next.Games.Length+next.Forums.Length>0&&HasMiyouLogin)||(next.HeyboxEnabled&&HasHeyboxLogin)))throw new InvalidOperationException("请先为选中的签到平台完成登录，再开启每日定时。");
            ApplyModuleSettings(next,"主人，每日委托已同步。正在接入定时系统。");
            Scheduler.Install(store,settings,Application.ExecutablePath);RefreshStatus();Tell("主人，定时系统已上线。每天北京时间 "+value+"，Fairy 会按约执行。");
        }
        internal void RemoveModuleSchedule()
        {if(IsWorking)throw new InvalidOperationException("当前委托正在执行，请稍候。");if(Scheduler.Exists())Scheduler.Remove();RefreshStatus();Tell("主人，定时委托已暂停。您随时可以手动呼叫我签到。");}
        internal void SetModuleStartup(bool enabled)
        {if(IsWorking)throw new InvalidOperationException("当前委托正在执行，请稍候。");autoStart.Checked=enabled;}
        internal async Task<string> RunPlatform(FairyPlatform platform,CancellationToken cancel)
        {
            int speechVersion=dialogueVersion;
            if(IsWorking)throw new InvalidOperationException("主人，当前委托仍在执行，请稍候。");
            var selected=ModuleSettings;string name=platform==FairyPlatform.Miyou?"米游社":"小黑盒";
            if(platform==FairyPlatform.Heybox)
            {if(!HasHeyboxLogin)throw new InvalidOperationException("请先接入小黑盒账号，我才能为您执行签到。");selected=HeyboxAccount.Only(selected);}
            else
            {if(!HasMiyouLogin)throw new InvalidOperationException("请先接入米游社账号，我才能为您执行签到。");selected.HeyboxEnabled=false;if(selected.Games.Length+selected.Forums.Length==0)throw new InvalidOperationException("请先在米游社签到配置中选择委托项目。");}
            operation=CancellationTokenSource.CreateLinkedTokenSource(cancel);SetBusy(true);
            try
            {
                Tell("主人，"+name+"签到委托已接收。Fairy 正在核对今日进度，请稍候。");
                if(CheckinStatus.IsTodayComplete(store,selected))return ReportCompletion(selected,new RunSummary());
                using(var transport=new Transport())
                {var result=await new Runner(store,transport,Write).Run(selected,false,operation.Token);if(!CheckinStatus.IsTodayComplete(store,selected))cancel.ThrowIfCancellationRequested();return ReportCompletion(selected,result);}
            }
            catch(OperationCanceledException){if(!cancel.IsCancellationRequested&&speechVersion==dialogueVersion)Tell("主人，委托已停止。已完成的签到记录会为您保留。");throw;}
            finally{operation.Dispose();operation=null;if(!closing)SetBusy(false);}
        }
    }
}
