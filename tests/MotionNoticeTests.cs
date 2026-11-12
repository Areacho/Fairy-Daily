using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern IntPtr GetCapture();
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int WindowStyle(IntPtr window,int index);
    static FairyDialogue WidgetDialogue(FairyWidget widget)
    {return (FairyDialogue)typeof(FairyWidget).GetField("dialogue",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(widget);}
    static void MotionNoticeChecks()
    {
        var motion=new EyeLifecycle();float first=motion.Openness;motion.Advance(.15);float middle=motion.Openness;
        Assert(first<middle&&middle<1&&motion.Alpha<1,"opening animation progressively reveals the iris and glow");
        motion.Advance(EyeLifecycle.OpenSeconds);Assert(motion.Phase==EyePhase.Ready&&motion.Openness==1&&motion.Alpha==1,"opening reaches the normal fully visible eye");
        motion.BeginClosing(1);motion.Advance(1.15);float shutting=motion.Openness;
        Assert(shutting<1&&shutting>.02f&&motion.Phase==EyePhase.Closing,"closing animation shuts the eye before removing the widget");
        motion.Advance(1+EyeLifecycle.CloseSeconds+.001);Assert(motion.Phase==EyePhase.Closed&&motion.Alpha==0,"closing ends on a fully transparent frame");
        motion.BeginOpening(2);motion.Advance(2.08);first=motion.Openness;float alpha=motion.Alpha;motion.BeginClosing(2.08);motion.Advance(2.08);
        Assert(Math.Abs(motion.Openness-first)<.001&&Math.Abs(motion.Alpha-alpha)<.001,"exiting during startup continues from the current eye frame without a brightness jump");
        using(var image=new Bitmap(240,240))using(var g=Graphics.FromImage(image))
        {
            g.Clear(Color.Transparent);FairyArt.FlareEye(g,new RectangleF(20,20,200,200),5,PointF.Empty,false,.22f);
            var center=image.GetPixel(120,120);
            Assert(center.R>170&&center.G>170&&center.B>170,"shutdown intermediate frame has a bright white optical streak at its center");
            Assert(image.GetPixel(120,40).A==0&&image.GetPixel(40,40).A==0,"the blue eye and surrounding area disappear while the horizontal flare remains");
            g.Clear(Color.Transparent);FairyArt.FlareEye(g,new RectangleF(20,20,200,200),5,PointF.Empty,false,0);
            Assert(image.GetPixel(120,120).A==0,"fully closed optical frame is transparent rather than a remaining blue disc");
        }
        var store=TestStore();var settings=Setup(store,new string[0],new[]{"5"});store.SaveState(CompletedState(settings,FakeCookie));
        var progress=CheckinNotifications.PublishStarted(store,true);
        Assert(progress.IsProgress&&!progress.IsCompletion&&progress.Text.Contains("定时委托已启动")&&CheckinNotifications.ShouldStartWidget(progress,false),"scheduled execution publishes its own start bubble and wakes a missing widget");
        var complete=CheckinNotifications.Publish(store,settings,new RunSummary(),true,runId:progress.RunId);
        Assert(complete.RunId==progress.RunId&&SpeechBus.ReadProgress(store).Id==progress.Id,"fast completion retains its paired start notification");
        using(var panel=new MainForm(store,settings))using(var widget=new FairyWidget(store,panel))
        {
            var foreground=GetForegroundWindow();var capture=GetCapture();var cursor=Cursor.Position;
            widget.PollNotifications(false);Assert(!widget.Visible&&SpeechBus.IsProgressPending(store,progress,DateTimeOffset.UtcNow),"locked desktop defers start bubbles without consuming them");
            widget.PollNotifications(true);Application.DoEvents();var bubble=WidgetDialogue(widget);
            Assert(GetForegroundWindow()==foreground&&GetCapture()==capture&&Cursor.Position==cursor,"automatic start bubble preserves foreground focus, mouse capture and cursor position; foreground="+foreground+" -> "+GetForegroundWindow()+", capture="+capture+" -> "+GetCapture()+", cursor="+cursor+" -> "+Cursor.Position);
            Assert((WindowStyle(widget.Handle,-20)&0x08000000)!=0&&(WindowStyle(bubble.Handle,-20)&0x08000000)!=0,"widget and dialogue both use the native no-activation window style");
            Assert(ScrollMessage(bubble.Handle,0x21,IntPtr.Zero,IntPtr.Zero).ToInt32()==3,"notification mouse activation keeps intentional clicks without taking keyboard focus");
            Assert(bubble.Controls.OfType<Label>().Single().Text==progress.Text&&bubble.KeepNoticeOpen&&!bubble.Controls.OfType<TableLayoutPanel>().Single().Visible,"automatic execution appears as a persistent message-only bubble");
            widget.PollNotifications(true);Assert(bubble.Controls.OfType<Label>().Single().Text==progress.Text&&SpeechBus.IsPending(store,complete,DateTimeOffset.UtcNow),"fast completion waits long enough for its start bubble to be readable");
            typeof(FairyWidget).GetField("progressShownAt",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(widget,-100.0);
            widget.PollNotifications(true);
            Assert(bubble.Controls.OfType<Label>().Single().Text==complete.Text&&!bubble.KeepNoticeOpen&&!SpeechBus.IsPending(store,complete,DateTimeOffset.UtcNow),"paired result replaces the start bubble once, after the minimum display interval");
            Assert(GetForegroundWindow()==foreground&&GetCapture()==capture&&Cursor.Position==cursor,"automatic completion also preserves foreground focus and mouse state");
            bubble.HideNow();widget.PollNotifications(true);Assert(!bubble.Visible,"acknowledged start and result bubbles do not reopen on polling");
            widget.Close();Assert(widget.IsClosingEye&&!widget.IsDisposed&&!bubble.Visible,"manual exit hides the bubble and plays a closing animation");
            widget.AdvanceLifecycle(1000000);Assert(widget.IsDisposed&&panel.IsDisposed,"manual exit releases all windows after its last animation frame");
        }
        var stale=CheckinNotifications.PublishStarted(store,true);stale.At=DateTimeOffset.UtcNow.AddMinutes(-3).ToString("o");
        Assert(!SpeechBus.IsProgressPending(store,stale,DateTimeOffset.UtcNow),"old execution notices cannot appear as a newly running task");
        var retry=CheckinNotifications.PublishStarted(store,true);var retryResult=CheckinNotifications.Publish(store,settings,new RunSummary(),true,runId:retry.RunId);
        Assert(retryResult!=null&&retryResult.RunId==retry.RunId,"an announced retry always publishes its terminal result even when the result text is unchanged");
        foreach(int dpi in new[]{96,144,192,288})
        {
            var area=new Rectangle(0,0,2560,1400);var size=WindowLayout.Pixels(new Size(480,164),dpi);bool? side=null;
            var point=WindowLayout.PlaceBesideWidget(size,new Rectangle(600,100,250,250),area,dpi,ref side);
            Assert(area.Contains(new Rectangle(point,size)),"widget-anchored notice stays on screen at "+dpi+" DPI");
        }
        SmoothNavigationChecks();
    }
    static void SmoothNavigationChecks()
    {
        using(var panel=new MainForm(TestStore(),new Settings()))using(var module=new FairyModuleForm(panel,FairySection.Login))
        {
            Materialize(module);module.Navigate(FairySection.MiyouTasks,false);Assert(!module.Visible&&(WindowStyle(module.Handle,-16)&0x10000000)==0,"preparing a hidden page never makes its window visible");
            var root=(TableLayoutPanel)module.Controls[0];bool completeBeforeSwap=true;int swaps=0;
            var field=typeof(FairyModuleForm).GetField("body",BindingFlags.NonPublic|BindingFlags.Instance);
            ControlEventHandler removed=(s,e)=>{if(e.Control is BufferedTableLayout){swaps++;var next=(Control)field.GetValue(module);completeBeforeSwap&=next.Controls.Count>0&&!e.Control.IsDisposed;}};
            root.ControlRemoved+=removed;module.Opacity=0;module.Show();Application.DoEvents();
            var watch=Stopwatch.StartNew();
            foreach(var page in new[]{FairySection.Checkin,FairySection.MiyouTasks,FairySection.Schedule,FairySection.HeyboxTasks,FairySection.Login})module.Navigate(page,false);
            watch.Stop();root.ControlRemoved-=removed;
            Assert(swaps==5&&completeBeforeSwap,"navigation keeps the old page until the complete replacement is built");
            Assert(module.Visible&&(WindowStyle(module.Handle,-16)&0x10000000)!=0&&module.Controls.Count==1,"atomic page changes restore drawing and retain a single content host");
            Assert((WindowStyle(module.Handle,-20)&0x02000000)!=0,"native popup controls use buffered painting without individually layered child windows");
            try{using(var redraw=new WindowRedrawBatch(module))throw new InvalidOperationException("test frame cleanup");}catch(InvalidOperationException){}
            Assert((WindowStyle(module.Handle,-16)&0x10000000)!=0,"drawing is restored even when a batched update fails");
            Console.WriteLine("Navigation review: five page replacements in "+watch.ElapsedMilliseconds+" ms at "+module.DeviceDpi+" DPI.");
            module.Close();panel.AllowExit=true;
        }
    }
}
