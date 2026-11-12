using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll",EntryPoint="SetWindowPos")]static extern bool LayerPosition(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    static bool AboveWindow(IntPtr upper,IntPtr lower)
    {
        for(var window=GetWindow(lower,3);window!=IntPtr.Zero;window=GetWindow(window,3))if(window==upper)return true;
        return false;
    }
    static void DialogueLayerChecks()
    {
        var store=TestStore();Store.AtomicWrite(Path.Combine(store.Root,"widget.json"),Json.Write(new WidgetPreferences{Topmost=false}));
        using(var panel=new MainForm(store,new Settings()))using(var widget=new FairyWidget(store,panel))
        using(var page=new PassiveForm{TopMost=true,ShowInTaskbar=false,Opacity=0,StartPosition=FormStartPosition.Manual})
        {
            widget.Say("主人，通道已接入。");var bubble=WidgetDialogue(widget);page.Bounds=bubble.Bounds;page.Show(widget);
            Action[] entries={
                ()=>widget.Say("主人，普通操作提示。"),
                ()=>{bubble.OpenShortcuts(false);bubble.ShowFor(widget);},
                ()=>{bubble.OpenShortcuts(true);bubble.ShowFor(widget);},
                ()=>widget.DeliverCompletion(SpeechBus.Publish(store,"主人，签到已完成。",true),true),
                ()=>widget.DeliverProgress(CheckinNotifications.PublishStarted(store,true),true)
            };
            foreach(var show in entries)
            {
                LayerPosition(page.Handle,new IntPtr(-1),0,0,0,0,0x213);var foreground=GetForegroundWindow();var capture=GetCapture();var cursor=Cursor.Position;
                show();Application.DoEvents();
                Assert(bubble.Visible&&AboveWindow(bubble.Handle,page.Handle)&&(WindowStyle(bubble.Handle,-20)&8)!=0,"every dialogue entry raises the actual native window above an overlapping topmost page");
                Assert(GetForegroundWindow()==foreground&&GetCapture()==capture&&Cursor.Position==cursor,"raising a dialogue preserves foreground focus, mouse capture and pointer position");
            }
            Assert(!widget.TopMost&&(WindowStyle(widget.Handle,-20)&8)==0,"dialogue visibility is independent of the widget's disabled pin preference");
            using(var login=new PassiveForm{TopMost=true,Opacity=0,ShowInTaskbar=false,Bounds=bubble.Bounds,StartPosition=FormStartPosition.Manual})
            {
                login.Show(page);LayerPosition(login.Handle,new IntPtr(-1),0,0,0,0,0x213);
                Assert(AboveWindow(login.Handle,bubble.Handle),"regression fixture opens a new owned login page above the already visible bubble");
                var foreground=GetForegroundWindow();var capture=GetCapture();var cursor=Cursor.Position;
                bubble.MaintainLayer();
                Assert(AboveWindow(bubble.Handle,login.Handle),"visible dialogue rises above a login page opened after the original notification");
                Assert(GetForegroundWindow()==foreground&&GetCapture()==capture&&Cursor.Position==cursor,"maintaining dialogue layering never activates the popup or changes mouse state");
            }
            widget.TopMost=true;widget.TopMost=false;bubble.MaintainLayer();
            Assert((WindowStyle(bubble.Handle,-20)&8)!=0&&!widget.TopMost&&(WindowStyle(widget.Handle,-20)&8)==0,"changing the owner's pin state cannot leave its dialogue in the normal window band");
            LayerPosition(page.Handle,new IntPtr(-1),0,0,0,0,0x213);bubble.Dismiss();bubble.MaintainLayer();
            Assert(AboveWindow(page.Handle,bubble.Handle),"dismissing a bubble stops its layer maintenance during the fade-out");
            bubble.HideNow();bubble.MaintainLayer();Assert(!bubble.Visible,"layer maintenance never reopens a dismissed or hidden bubble");
            panel.AllowExit=true;
        }
    }
}
