using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed partial class MainForm
    {
        CancellationTokenSource startupWait;
        bool startupCheckinStarted;
        internal void SetModuleStartupCheckin(bool enabled)
        {
            if(IsWorking)throw new InvalidOperationException("当前委托正在执行，请稍后再调整设定。");
            // Change this preference without confirming unrelated tile edits.
            var saved=store.Load();saved.CheckinOnStartup=enabled;store.Save(saved);settings.CheckinOnStartup=enabled;
            if(!enabled&&startupWait!=null)startupWait.Cancel();
            Write(enabled?"已开启随程序启动签到，下次启动生效。":"已关闭随程序启动签到。");
        }
        internal async Task RunStartupCheckin()
        {
            if(startupCheckinStarted||IsDisposed)return;startupCheckinStarted=true;startupWait=new CancellationTokenSource();var source=startupWait;
            EventHandler cancel=(s,e)=>source.Cancel();Disposed+=cancel;
            try
            {
                var startup=new StartupCheckin(store,LogStartup);
                await startup.RunAsync(()=>store.Load(),()=>IsWorking||Application.OpenForms.Cast<Form>().Any(f=>f.Visible&&f.Modal),s=>CheckinStatus.IsTodayComplete(store,s),RunVerifiedStartup,source.Token);
            }
            catch(OperationCanceledException){}
            catch(Exception){LogStartup("启动签到未完成，请检查网络、登录状态或手动签到。");}
            finally{Disposed-=cancel;source.Dispose();if(startupWait==source)startupWait=null;}
        }
        void LogStartup(string message)
        {
            if(IsDisposed||closing)return;
            try{store.Log(message);}catch(IOException){}catch(UnauthorizedAccessException){}
            Write(message);
        }
        async Task<bool> RunVerifiedStartup(Settings selected,CancellationToken cancel)
        {
            if(IsWorking||closing||IsDisposed)return false;
            cancel.ThrowIfCancellationRequested();operation=CancellationTokenSource.CreateLinkedTokenSource(cancel);SetBusy(true);
            try
            {
                Action<string> output=message=>{if(message.StartsWith("启动签到开始",StringComparison.Ordinal))Speak("主人，启动检查已通过，今日签到交给 Fairy。");Write(message);};
                using(var transport=new Transport()){var result=await new Runner(store,transport,output).RunStartup(selected,operation.Token);ReportCompletion(selected,result);}
                return true;
            }
            catch(OperationCanceledException){LogStartup("启动签到已停止，已完成的项目会保留。");return true;}
            finally{operation.Dispose();operation=null;if(!closing&&!IsDisposed)SetBusy(false);}
        }
    }
}
