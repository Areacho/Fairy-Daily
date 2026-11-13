using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void WindowBehaviorChecks()
    {
        var store=TestStore();var settings=new Settings();
        using(var panel=new MainForm(store,settings))
        using(var page=new FairyModuleForm(panel,FairySection.Schedule))
        using(var login=new HeyboxForm(store,settings))
        using(var qr=new QrLoginForm())
        {
            foreach(var form in new Form[]{panel,page,login,qr})
            {
                Materialize(form);
                Assert((WindowStyle(form.Handle,-16)&0x40000)==0&&!form.MaximizeBox,"fixed window has no native resize border or maximize command: "+form.GetType().Name);
            }
            foreach(int dpi in new[]{96,120,144,168,192,240,288})
            using(var parent=new Panel{BackColor=Color.Magenta,Size=WindowLayout.Pixels(new Size(540,90),dpi)})
            using(var toggle=new ToggleButton{Text="登录 Windows 后启动 Fairy",Location=new Point(0,0),Size=WindowLayout.Pixels(new Size(500,42),dpi)})
            using(var image=new Bitmap(toggle.Width,toggle.Height))
            {
                parent.Controls.Add(toggle);SetChildDpi(parent,dpi);image.SetResolution(dpi,dpi);toggle.DrawToBitmap(image,toggle.ClientRectangle);
                Assert(image.GetPixel(0,0).ToArgb()==Color.Magenta.ToArgb()&&image.GetPixel(image.Width-1,image.Height-1).ToArgb()==Color.Magenta.ToArgb(),"setting-row corners reveal the parent at "+dpi+" DPI");
                Assert(image.GetPixel(image.Width/2,image.Height/2).ToArgb()!=Color.Magenta.ToArgb(),"rounded setting row retains a filled body at "+dpi+" DPI");
                if(dpi==192||dpi==288)image.Save(Path.Combine(DisplayReviewDirectory(),"rounded-setting-"+dpi+".png"));
            }
            using(var widget=new FairyWidget(store,panel))
            {
                widget.Show();Application.DoEvents();var render=typeof(FairyWidget).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance);
                int before=widget.PresentedFrames;
                ScrollMessage(page.Handle,0x231,IntPtr.Zero,IntPtr.Zero);
                Assert(WindowInteraction.IsMoving,"moving a page gates all background UI animation");
                for(int i=0;i<60;i++)render.Invoke(widget,null);
                Assert(widget.PresentedFrames==before,"the eye reuses its static pixels during another window's drag");
                ScrollMessage(page.Handle,0x232,IntPtr.Zero,IntPtr.Zero);render.Invoke(widget,null);
                Assert(!WindowInteraction.IsMoving&&widget.PresentedFrames>before,"eye rendering resumes after the native move loop");
                WindowInteraction.SetMoving(widget,true);render.Invoke(widget,null);before=widget.PresentedFrames;
                for(int i=0;i<60;i++)render.Invoke(widget,null);
                Assert(widget.PresentedFrames==before,"custom widget dragging also reuses the cached eye");
                WindowInteraction.SetMoving(widget,false);
                widget.HideToTray();var notice=SpeechBus.Publish(store,"offline completion",true);
                widget.Say("offline narration");widget.PollNotifications(true);
                Assert(!widget.Visible&&!WidgetDialogue(widget).Visible&&SpeechBus.IsPending(store,notice,DateTimeOffset.UtcNow),"background notifications preserve manual tray hiding and retain the unread result");
                widget.RestoreFromTray();widget.PollNotifications(true);
                Assert(widget.Visible&&WidgetDialogue(widget).Visible&&!SpeechBus.IsPending(store,notice,DateTimeOffset.UtcNow),"explicit tray restore reveals the retained notification");
                widget.Close();
                Assert(!WidgetSession.BackgroundAllowed(store),"closing marks background suppression before the fade-out finishes");
            }
            panel.AllowExit=true;
        }
        Assert(!WindowInteraction.IsMoving,"disposing UI windows releases every movement gate");
        var shared=new Store(store.Root);
        Assert(!WidgetSession.BeginLaunch(shared,true),"an already queued notification launch respects the closed session in another store instance");
        CheckinNotifications.EnsureWidget(shared,new FairyMessage{IsCompletion=true},"C:\\must-not-launch.exe");
        Assert(!File.Exists(shared.LatestLog),"a suppressed background launch never attempts to execute a replacement widget");
        Assert(WidgetSession.BeginLaunch(shared,false)&&WidgetSession.BackgroundAllowed(store),"a direct launch or Windows logon starts a new visible session");
    }
}
