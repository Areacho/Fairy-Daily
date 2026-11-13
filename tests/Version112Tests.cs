using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    sealed class PreparedProbe : PreparedForm
    {
        internal PreparedProbe(){ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-12000,-12000);ClientSize=new Size(360,220);BackColor=Theme.Background;Controls.Add(new RoundButton{Text="离线首帧检查",Location=new Point(20,30),Size=new Size(280,40)});}
        protected override bool ShowWithoutActivation{get{return true;}}
    }
    static void Version112Checks()
    {
        using(var ready=new PreparedProbe())
        {
            bool prepared=true;ready.VisibleChanged+=(s,e)=>{if(ready.Visible&&ready.Opacity>0)prepared&=ready.PreparedFrames>0;};
            ready.Show();Assert(prepared&&ready.PreparedFrames==1&&ready.Opacity==1,"opaque windows finish their first frame before becoming visible");
            Assert((WindowStyle(ready.Handle,-20)&0x02000000)==0,"first-frame composition is removed before ordinary interaction");
            ready.Hide();ready.Show();Assert(ready.PreparedFrames==2,"reopening an opaque configuration window prepares its final frame again");
        }
        var store=TestStore();Store.AtomicWrite(Path.Combine(store.Root,"display.json"),"{\"Layout\":\"auto\",\"Resolution\":\"4k\"}");
        var options=DisplayPreferences.Load(store);Assert(options.PreferredClientSize(false,120)==new Size(1350,1125),"a legacy 4K override is ignored in favour of automatic DPI-based window sizing");
        using(var panel=new MainForm(store,new Settings()))
        using(var settings=panel.CreateDisplaySettings())
        {
            settings.Opacity=0;settings.Show();
            Assert(FindControls(settings).OfType<RoundChoice>().Count()==1&&!FindControls(settings).Any(c=>c.AccessibleName=="窗口分辨率"),"configuration removes the window-size selector completely");
            Assert(settings.ClientSize.Height<=WindowLayout.Pixels(360,settings.DeviceDpi)&&Screen.FromControl(settings).WorkingArea.Contains(settings.Bounds),"the shortened settings window fits its current screen");
            panel.AllowExit=true;
        }
        using(var panel=new MainForm(store,new Settings()))
        using(var widget=new FairyWidget(store,panel))
        using(var page=new PassiveForm{Opacity=0,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual})
        {
            widget.Say("offline immediate layering regression");var bubble=WidgetDialogue(widget);var surface=PopupSurface.Attach(bubble);surface.RenderNow();
            WindowInteraction.Attach(page);page.Bounds=bubble.Bounds;page.Show();
            Assert(AboveWindow(page.Handle,bubble.Handle)&&AboveWindow(page.Handle,surface.Background.Handle),"opening an overlapping page immediately places its controls above both bubble windows");
            LayerPosition(bubble.Handle,new IntPtr(-1),0,0,0,0,0x213);
            ScrollMessage(page.Handle,0x21,page.Handle,new IntPtr(0x2010001));
            Assert(AboveWindow(page.Handle,bubble.Handle)&&AboveWindow(page.Handle,surface.Background.Handle),"mouse activation repairs bubble layering before a page control processes its click");
            LayerPosition(bubble.Handle,new IntPtr(-1),0,0,0,0,0x213);ScrollMessage(panel.Handle,0x121,IntPtr.Zero,page.Handle);
            Assert(AboveWindow(page.Handle,bubble.Handle)&&AboveWindow(page.Handle,surface.Background.Handle),"a native dialog idle notification restores page priority before waiting for input");
            int compositions=surface.CompositionCount;
            for(int i=0;i<20;i++)DialogueLayer.PrepareInteraction(page);
            Assert(surface.CompositionCount==compositions,"synchronous interaction ordering reuses the bubble pixels without recapturing its controls");
            using(var menu=new FairyContextMenu{Opacity=1})
            {
                menu.Items.Add(new FairyMenuItem("offline menu choice",""));menu.Show(page,new Point(20,20));
                Assert(DialogueLayer.MenuActive&&AboveWindow(menu.Handle,bubble.Handle)&&AboveWindow(menu.Handle,surface.Background.Handle),"a dropdown immediately takes priority over bubble input and pixels");
                var foreground=GetForegroundWindow();var capture=GetCapture();
                for(int i=0;i<10;i++){DialogueLayer.Synchronize(bubble,page.Handle,true);surface.Sync();}
                Assert(menu.Visible&&AboveWindow(menu.Handle,bubble.Handle)&&GetForegroundWindow()==foreground&&GetCapture()==capture,"bubble maintenance cannot raise itself or change capture during dropdown interaction");
                var notice=SpeechBus.Publish(store,"offline result during dropdown",true);widget.PollNotifications(true);
                Assert(SpeechBus.IsPending(store,notice,DateTimeOffset.UtcNow),"completion notices remain pending while a dropdown is open");menu.Close();
            }
            Application.DoEvents();Assert(!DialogueLayer.MenuActive,"closing a dropdown releases its interaction guard");
            using(var modal=new Form{Opacity=0,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=page.Bounds})
            {
                Exception failure=null;modal.Shown+=(s,e)=>{try{Assert(DialogueLayer.NotificationsPaused,"modal controls pause new bubble delivery");var notice=SpeechBus.Publish(store,"offline result during modal input",true);widget.PollNotifications(true);Assert(SpeechBus.IsPending(store,notice,DateTimeOffset.UtcNow),"modal input leaves completion notices unread until the dialog closes");}catch(Exception ex){failure=ex;}finally{modal.Close();}};
                modal.ShowDialog(page);if(failure!=null)throw failure;
            }
            Assert(!DialogueLayer.NotificationsPaused,"closing a modal restores pending notification delivery");widget.PollNotifications(true);Assert(!SpeechBus.IsPending(store,SpeechBus.Read(store),DateTimeOffset.UtcNow),"the retained completion notice is delivered after modal interaction");
            bubble.HideNow();panel.AllowExit=true;
        }
    }
}
