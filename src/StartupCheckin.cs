using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace MiyouDaily
{
    public sealed class RunBusyException : InvalidOperationException
    {public RunBusyException():base("另一项签到正在运行，请稍后查看日志。") {}}

    internal sealed class StartupCheckin
    {
        readonly Func<bool> online;
        readonly Func<Settings,CancellationToken,Task<Settings>> verify;
        readonly Func<TimeSpan,CancellationToken,Task> delay;
        readonly Action<string> log;
        readonly HashSet<string> messages=new HashSet<string>(StringComparer.Ordinal);
        bool started;
        internal StartupCheckin(Store store,Action<string> log)
        {
            this.log=log;online=NetworkInterface.GetIsNetworkAvailable;delay=Task.Delay;
            verify=async (settings,cancel)=>{using(var miyou=new Transport())using(var heybox=new HeyboxTransport())return await VerifyAccounts(store,settings,miyou,heybox,Log,cancel);};
        }
        internal StartupCheckin(Action<string> log,Func<bool> online,Func<Settings,CancellationToken,Task<Settings>> verify,Func<TimeSpan,CancellationToken,Task> delay)
        {this.log=log;this.online=online;this.verify=verify;this.delay=delay;}
        void Log(string text){if(messages.Add(text)&&log!=null)log(text);}
        internal static bool ShouldRun(Settings settings,string[] args)
        {return settings.CheckinOnStartup&&!args.Any(a=>a=="--notify"||a=="--run"||a=="--scheduled"||a=="--self-check");}
        internal async Task RunAsync(Func<Settings> load,Func<bool> busy,Func<Settings,bool> complete,Func<Settings,CancellationToken,Task<bool>> execute,CancellationToken cancel)
        {
            if(started)return;started=true;
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                deadline.CancelAfter(TimeSpan.FromMinutes(3));var token=deadline.Token;
                try
                {
                    await Task.Yield();
                    for(int attempt=0;attempt<18;attempt++)
                    {
                        token.ThrowIfCancellationRequested();var current=load();if(!current.CheckinOnStartup)return;
                        var snapshot=Json.Read<Settings>(Json.Write(current));snapshot.Validate();string revision=Json.Write(snapshot);
                        if(complete(snapshot)){Log("启动签到：今日所选项目已完成，无需重复执行。");return;}
                        if(!busy()&&online())
                        {
                            Settings selected=null;bool networkReady=true;
                            try{selected=await verify(snapshot,token);}
                            catch(IOException){networkReady=false;Log("启动签到：平台暂不可达，正在等待网络恢复。");}
                            token.ThrowIfCancellationRequested();
                            if(networkReady)
                            {
                                var latest=load();if(!latest.CheckinOnStartup)return;
                                // A login or selection may have changed during a
                                // read-only probe. Never submit that old snapshot.
                                if(Json.Write(latest)!=revision)continue;
                                if(selected==null){Log("启动签到：没有可用的已登录平台，请登录后手动签到。");return;}
                                if(complete(selected)){Log("启动签到：已登录平台的今日项目已完成。");return;}
                                // The three-minute deadline only limits waiting.
                                // A verified run retains the application's own
                                // cancellation token and may finish normally.
                                try{if(!busy()&&await execute(selected,cancel))return;}
                                catch(RunBusyException){Log("启动签到：另一项签到正在执行，稍后重新检查。");}
                            }
                        }
                        else Log("启动签到：正在等待网络或当前操作结束。");
                        if(attempt<17)await delay(TimeSpan.FromSeconds(10),token);
                    }
                    Log("启动签到：本次等待已结束，可在网络和登录正常后手动签到。");
                }
                catch(OperationCanceledException)
                {if(cancel.IsCancellationRequested)throw;Log("启动签到：等待超时，本次已跳过。");}
            }
        }
        internal static async Task<Settings> VerifyAccounts(Store store,Settings settings,ITransport miyou,ITransport heybox,Action<string> log,CancellationToken cancel)
        {
            var selected=Json.Read<Settings>(Json.Write(settings));bool needMiyou=selected.Games.Length+selected.Forums.Length>0,miyouReady=false,heyboxReady=false;
            if(needMiyou)
            {
                try
                {
                    string cookie=store.Decrypt(settings);
                    if(cookie.Length>0)
                    {
                        var api=new MiyouApi(settings,cookie,miyou);var game=Catalog.Games.FirstOrDefault(g=>settings.Games.Contains(g.Key));
                        if(game!=null)await api.Roles(game,cancel);
                        if(settings.Forums.Length>0)await api.Missions(cancel);
                        miyouReady=true;
                    }
                }
                catch(ApiException ex){if(!ex.NeedsAction)throw new IOException("米游社暂不可用。",ex);}
                catch(InvalidOperationException){}catch(ArgumentException){}
                if(!miyouReady&&log!=null)log("启动签到：米游社未登录、登录失效或需要验证，本次跳过此平台。");
            }
            if(selected.HeyboxEnabled)
            {
                try{var account=HeyboxAccount.Load(settings);if(account!=null){await new HeyboxApi(account,heybox).State(cancel);heyboxReady=true;}}
                catch(ApiException ex){if(!ex.NeedsAction)throw new IOException("小黑盒暂不可用。",ex);}
                catch(InvalidOperationException){}catch(ArgumentException){}
                if(!heyboxReady&&log!=null)log("启动签到：小黑盒未登录、登录失效或需要验证，本次跳过此平台。");
            }
            if(!miyouReady){selected.Games=new string[0];selected.Forums=new string[0];}selected.HeyboxEnabled=heyboxReady;
            if(!miyouReady&&!heyboxReady)return null;selected.Validate();return selected;
        }
    }
}
