using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void StartupCheckinSuite()
    {
        // Run the injected, non-UI async checks without inheriting a WinForms
        // synchronization context, then create controls on the test STA thread.
        Task.Run(()=>StartupCheckinChecks()).GetAwaiter().GetResult();StartupScheduleUiChecks();
    }
    static readonly Func<TimeSpan,CancellationToken,Task> NoStartupDelay=(time,cancel)=>{cancel.ThrowIfCancellationRequested();return Task.FromResult(0);};
    static async Task StartupCheckinChecks()
    {
        Assert(!new Settings().CheckinOnStartup,"startup check-in is disabled for new and migrated configurations");
        var enabled=new Settings{CheckinOnStartup=true};
        Assert(StartupCheckin.ShouldRun(enabled,new string[0])&&StartupCheckin.ShouldRun(enabled,new[]{"--autostart"}),"normal and Windows-logon launches can request startup check-in");
        foreach(string argument in new[]{"--notify","--run","--scheduled","--self-check"})Assert(!StartupCheckin.ShouldRun(enabled,new[]{argument}),"background launch does not retrigger startup check-in: "+argument);
        var store=TestStore();var config=Setup(store,new[]{"genshin"},new[]{"5"});config.CheckinOnStartup=true;store.Save(config);
        var miyou=new Mock().Add("GET","getUserGameRolesByCookie","{\"retcode\":0,\"data\":{\"list\":[]}}").Add("GET","getUserMissionsState","{\"retcode\":0,\"data\":{\"total_points\":1}}");var box=new Mock();
        var messages=new List<string>();var selected=await StartupCheckin.VerifyAccounts(store,config,miyou,box,messages.Add,None);
        Assert(selected!=null&&selected.Games.SequenceEqual(config.Games)&&selected.Forums.SequenceEqual(config.Forums)&&miyou.Count==2&&box.Count==0,"startup verifies game and community login through read-only provider requests");
        Assert(!File.Exists(store.StatePath)&&store.Load().ProtectedCookie==config.ProtectedCookie,"login preflight never changes attempt counts or saved credentials");
        config.HeyboxEnabled=true;HeyboxAccount.Set(config,BoxConfig("12345678"));
        miyou=new Mock().Add("GET","getUserGameRolesByCookie","{\"retcode\":-100}");box=new Mock().Add("GET","get_sign_state",BoxState(false));
        selected=await StartupCheckin.VerifyAccounts(store,config,miyou,box,messages.Add,None);
        Assert(selected.Games.Length==0&&selected.Forums.Length==0&&selected.HeyboxEnabled&&box.Count==1,"an expired Miyou session is excluded while a verified Heybox session remains eligible");
        config.Games=new string[0];miyou=new Mock().Add("GET","getUserMissionsState","{\"retcode\":0,\"data\":{\"total_points\":1}}");box=new Mock().Add("GET","get_sign_state","{\"status\":\"lack_token\"}");
        selected=await StartupCheckin.VerifyAccounts(store,config,miyou,box,messages.Add,None);
        Assert(selected!=null&&selected.Forums.Length==1&&!selected.HeyboxEnabled,"an expired Heybox session never prevents a verified Miyou platform from proceeding");
        miyou=new Mock();box=new Mock();selected=await StartupCheckin.VerifyAccounts(store,new Settings{CheckinOnStartup=true},miyou,box,messages.Add,None);
        Assert(selected==null&&miyou.Count==0&&box.Count==0&&!File.Exists(store.StatePath),"missing login data causes no provider requests and no sign-in state writes");
        Assert(!string.Join(" ",messages).Contains("SYNTHETIC")&&!string.Join(" ",messages).Contains("FAKE_LTOKEN"),"startup diagnostics contain no credentials or raw authentication replies");
        await StartupCoordinatorChecks();
        var runStore=TestStore();var runConfig=new Settings{Games=new string[0],Forums=new string[0],HeyboxEnabled=true,CheckinOnStartup=true,DailyTime="23:59"};var account=BoxConfig("12345678");HeyboxAccount.Set(runConfig,account);runStore.Save(runConfig);
        box=new Mock().Add("GET","get_sign_state",BoxState(false)).Add("GET","sign_v3/sign",BoxOk).Add("GET","get_sign_state",BoxState(true));
        var result=await new Runner(runStore,new Mock(),null,box).RunStartup(runConfig,None);
        Assert(result.Success==1&&box.Count==3&&CheckinStatus.IsTodayComplete(runStore,runConfig),"startup execution runs independently of the daily scheduled time and confirms provider completion");
        box=new Mock();result=await new Runner(runStore,new Mock(),null,box).RunStartup(runConfig,None);Assert(result.Skipped==1&&box.Count==0,"startup runs never submit an already completed target again");
        var state=runStore.LoadState();state.Targets[account.Key]=new Attempt{Count=3};runStore.SaveState(state);result=await new Runner(runStore,new Mock(),null,box).RunStartup(runConfig,None);
        Assert(result.Skipped==1&&box.Count==0,"startup execution respects automatic retry caps instead of treating startup as a manual retry");
        state.Targets[account.Key]=new Attempt{Count=1,NeedsAction=true};runStore.SaveState(state);result=await new Runner(runStore,new Mock(),null,box).RunStartup(runConfig,None);
        Assert(result.Skipped==1&&box.Count==0,"startup does not replay targets requiring human validation");
    }
    static async Task StartupCoordinatorChecks()
    {
        var config=new Settings{CheckinOnStartup=true};int waits=0,probes=0,runs=0;var messages=new List<string>();
        var service=new StartupCheckin(messages.Add,()=>waits>=2,(s,c)=>{probes++;return Task.FromResult(s);},(t,c)=>{waits++;return Task.FromResult(0);});
        Func<Settings,CancellationToken,Task<bool>> execute=(s,c)=>{runs++;return Task.FromResult(true);};
        await service.RunAsync(()=>config,()=>false,s=>false,execute,None);
        Assert(waits==2&&probes==1&&runs==1,"startup waits for the network before authentication and submits only once when ready");
        await service.RunAsync(()=>config,()=>false,s=>false,execute,None);Assert(runs==1,"the startup coordinator cannot replay the same launch");
        service=new StartupCheckin(messages.Add,()=>{throw new Exception("unexpected network check");},(s,c)=>{throw new Exception("unexpected login check");},NoStartupDelay);
        await service.RunAsync(()=>config,()=>false,s=>true,execute,None);Assert(runs==1,"already completed startup work is skipped without network or login requests");
        waits=0;service=new StartupCheckin(messages.Add,()=>false,(s,c)=>{throw new Exception("unexpected login check");},(t,c)=>{waits++;config.CheckinOnStartup=false;return Task.FromResult(0);});
        await service.RunAsync(()=>config,()=>false,s=>false,execute,None);Assert(waits==1&&runs==1,"disabling the option during an offline wait prevents check-in");
        config.CheckinOnStartup=true;service=new StartupCheckin(messages.Add,()=>true,(s,c)=>{config.CheckinOnStartup=false;return Task.FromResult(s);},NoStartupDelay);
        await service.RunAsync(()=>config,()=>false,s=>false,execute,None);Assert(runs==1,"disabling startup during authentication blocks the already verified submission");
        config.CheckinOnStartup=true;probes=0;service=new StartupCheckin(messages.Add,()=>true,(s,c)=>{probes++;if(probes==1)config.DeviceFp="changed-during-login-probe";return Task.FromResult(s);},NoStartupDelay);
        bool fresh=false;await service.RunAsync(()=>config,()=>false,s=>false,(s,c)=>{fresh=s.DeviceFp==config.DeviceFp;return Task.FromResult(true);},None);
        Assert(probes==2&&fresh,"configuration changes during authentication invalidate the old verified snapshot");
        probes=0;runs=0;service=new StartupCheckin(messages.Add,()=>true,(s,c)=>{probes++;if(probes<3)throw new IOException("synthetic network error with secret=not-for-logs");return Task.FromResult(s);},NoStartupDelay);
        await service.RunAsync(()=>config,()=>false,s=>false,execute,None);Assert(probes==3&&runs==1&&!string.Join(" ",messages).Contains("not-for-logs"),"temporary provider outages retry read-only checks without exposing response details");
        runs=0;service=new StartupCheckin(messages.Add,()=>true,(s,c)=>Task.FromResult(s),NoStartupDelay);
        try{await service.RunAsync(()=>config,()=>false,s=>false,(s,c)=>{runs++;throw new IOException("write failed after submission");},None);throw new Exception("execution failure swallowed");}
        catch(IOException){Assert(runs==1,"a failure after execution begins never replays the whole startup submission");}
        runs=0;service=new StartupCheckin(messages.Add,()=>true,(s,c)=>Task.FromResult(s),NoStartupDelay);
        await service.RunAsync(()=>config,()=>false,s=>false,(s,c)=>{runs++;if(runs==1)throw new RunBusyException();return Task.FromResult(true);},None);
        Assert(runs==2,"a cross-process run lock defers startup without duplicating an active run");
        using(var lifetime=new CancellationTokenSource())
        {
            service=new StartupCheckin(messages.Add,()=>true,(s,c)=>Task.FromResult(s),NoStartupDelay);bool callerToken=false;
            await service.RunAsync(()=>config,()=>false,s=>false,(s,c)=>{callerToken=c==lifetime.Token;return Task.FromResult(true);},lifetime.Token);
            Assert(callerToken,"waiting timeout does not truncate a verified run after it starts");
        }
        using(var cancel=new CancellationTokenSource())
        {
            service=new StartupCheckin(messages.Add,()=>false,(s,c)=>Task.FromResult(s),(t,c)=>{cancel.Cancel();c.ThrowIfCancellationRequested();return Task.FromResult(0);});
            try{await service.RunAsync(()=>config,()=>false,s=>false,execute,cancel.Token);throw new Exception("startup cancellation ignored");}catch(OperationCanceledException){Assert(true,"closing or cancelling startup stops an outstanding network wait");}
        }
        waits=0;service=new StartupCheckin(messages.Add,()=>false,(s,c)=>Task.FromResult(s),(t,c)=>{waits++;return Task.FromResult(0);});
        await service.RunAsync(()=>config,()=>false,s=>false,execute,None);Assert(waits>0&&waits<30,"startup network waiting is bounded and never polls indefinitely");
    }
    static void StartupScheduleUiChecks()
    {
        var store=TestStore();var config=Setup(store,new[]{"genshin"},new[]{"5"});string savedCookie=config.ProtectedCookie;
        using(var panel=new MainForm(store,config))using(var page=new FairyModuleForm(panel,FairySection.Schedule))
        {
            page.Opacity=0;page.Show();var toggle=FindControls(page).OfType<ToggleButton>().Single(c=>c.AccessibleName=="随程序启动执行签到");
            FindControls(panel).OfType<GameTile>().Single(t=>t.Key=="genshin").GameSwitch.Checked=false;toggle.Checked=true;
            Assert(store.Load().CheckinOnStartup&&store.Load().ProtectedCookie==savedCookie&&store.Load().Games.Contains("genshin"),"the rounded schedule switch preserves credentials and does not confirm unrelated pending game edits");
            toggle.Checked=false;Assert(!store.Load().CheckinOnStartup,"turning startup check-in off is persisted immediately");
            foreach(int dpi in new[]{96,120,144,168,192,240,288})
            {
                SetTestDpi(page,dpi);page.Navigate(FairySection.Schedule,false);SetChildDpi(page,dpi);
                toggle=FindControls(page).OfType<ToggleButton>().Single(c=>c.AccessibleName=="随程序启动执行签到");
                Assert((toggle.Right<=toggle.Parent.ClientSize.Width||((ScrollableControl)toggle.Parent).HorizontalScroll.Visible)&&toggle.Height>=WindowLayout.Pixels(28,dpi),"startup switch retains its usable rounded row within the viewport or scrollable width at "+dpi+" DPI");
                var body=(ScrollableControl)toggle.Parent;body.ScrollControlIntoView(toggle);Assert(ExposedArea(toggle).Height>=WindowLayout.Pixels(28,dpi),"startup option remains reachable by scrolling at "+dpi+" DPI");
            }
            SetTestDpi(page,120);page.Navigate(FairySection.Schedule,false);SetChildDpi(page,120);using(var frame=PopupSurface.Attach(page).ComposeFrame())frame.Save(Path.Combine(DisplayReviewDirectory(),"startup-schedule-v115.png"));
            panel.AllowExit=true;
        }
    }
}
