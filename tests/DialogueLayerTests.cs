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
    [DllImport("user32.dll")]static extern IntPtr GetDesktopWindow();
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
                show();Application.DoEvents();DialogueLayer.Synchronize(bubble,page.Handle,true);
                Assert(bubble.Visible&&AboveWindow(page.Handle,bubble.Handle)&&(WindowStyle(bubble.Handle,-20)&8)!=0,"every dialogue entry stays below an overlapping interactive page");
                PopupSurface.Attach(bubble).RenderNow();
                Assert(AboveWindow(page.Handle,PopupSurface.Attach(bubble).Background.Handle),"interactive pages also cover the complete visible dialogue frame");
                Assert(GetForegroundWindow()==foreground&&GetCapture()==capture&&Cursor.Position==cursor,"raising a dialogue preserves foreground focus, mouse capture and pointer position; foreground="+foreground+" -> "+GetForegroundWindow()+", capture="+capture+" -> "+GetCapture()+", cursor="+cursor+" -> "+Cursor.Position);
            }
            Assert(!widget.TopMost&&(WindowStyle(widget.Handle,-20)&8)==0,"dialogue visibility is independent of the widget's disabled pin preference");
            using(var login=new PassiveForm{TopMost=true,Opacity=0,ShowInTaskbar=false,Bounds=bubble.Bounds,StartPosition=FormStartPosition.Manual})
            {
                login.Show(page);LayerPosition(login.Handle,new IntPtr(-1),0,0,0,0,0x213);
                Assert(AboveWindow(login.Handle,bubble.Handle),"regression fixture opens a new owned login page above the already visible bubble");
                var foreground=GetForegroundWindow();var capture=GetCapture();var cursor=Cursor.Position;
                DialogueLayer.Synchronize(bubble,login.Handle,false);
                Assert(AboveWindow(login.Handle,bubble.Handle),"a newly opened login page remains above the existing dialogue");
                PopupSurface.Attach(bubble).RenderNow();
                Assert(AboveWindow(login.Handle,PopupSurface.Attach(bubble).Background.Handle),"a later login window stays above the visible dialogue image");
                Assert(GetForegroundWindow()==foreground&&GetCapture()==capture&&Cursor.Position==cursor,"maintaining dialogue layering never activates the popup or changes mouse state");
            }
            var external=GetDesktopWindow();var originalForeground=GetForegroundWindow();var originalCapture=GetCapture();var originalCursor=Cursor.Position;
            widget.TopMost=true;LayerPosition(widget.Handle,new IntPtr(-1),0,0,0,0,0x213);DialogueLayer.Synchronize(bubble,external,false);
            Assert(bubble.Owner==null&&!bubble.TopMost&&(WindowStyle(bubble.Handle,-20)&8)==0,"switching away demotes the bubble even when its widget remains pinned");
            PopupSurface.Attach(bubble).RenderNow();
            Assert((WindowStyle(PopupSurface.Attach(bubble).Background.Handle,-20)&8)==0,"switching away also demotes the visible dialogue image");
            Assert(widget.TopMost&&(WindowStyle(widget.Handle,-20)&8)!=0,"demoting the bubble preserves the user's pinned widget preference");
            LayerPosition(page.Handle,new IntPtr(-1),0,0,0,0,0x213);
            for(int i=0;i<4;i++)DialogueLayer.Synchronize(bubble,external,false);
            Assert(AboveWindow(page.Handle,bubble.Handle)&&!bubble.TopMost&&(WindowStyle(bubble.Handle,-20)&8)==0,"background maintenance does not raise a bubble over a covering window again");
            bubble.HideNow();bubble.Say("主人，后台任务已完成。");bubble.ShowFor(widget);DialogueLayer.Synchronize(bubble,external,false);
            Assert(bubble.Owner==null&&!bubble.TopMost&&(WindowStyle(bubble.Handle,-20)&8)==0,"a new notification can remain in the normal band independently of a pinned widget");
            DialogueLayer.Synchronize(bubble,page.Handle,false);
            Assert(AboveWindow(page.Handle,bubble.Handle)&&bubble.TopMost,"returning to this application preserves interactive page priority");
            DialogueLayer.Synchronize(bubble,external,false);DialogueLayer.Synchronize(bubble,page.Handle,false);
            Assert(AboveWindow(page.Handle,bubble.Handle),"repeated application switches preserve interactive page priority");
            Assert(GetForegroundWindow()==originalForeground&&GetCapture()==originalCapture&&Cursor.Position==originalCursor,"application layer transitions preserve focus, capture and pointer position");
            page.Hide();
            DialogueLayer.Synchronize(bubble,external,true);
            Assert(bubble.TopMost,"explicit widget click raises the bubble even while another process has focus");
            DialogueLayer.Synchronize(bubble,external,false);
            Assert(bubble.TopMost,"maintenance keeps the clicked bubble visible until foreground changes");
            DialogueLayer.Synchronize(bubble,IntPtr.Zero,false);
            Assert(!bubble.TopMost,"a subsequent foreground change releases the clicked bubble layer");
            page.Show(widget);
            widget.TopMost=false;
            Assert(!widget.TopMost&&(WindowStyle(widget.Handle,-20)&8)==0,"dialogue layer transitions do not pin the widget as a side effect");
            LayerPosition(page.Handle,new IntPtr(-1),0,0,0,0,0x213);bubble.Dismiss();bubble.MaintainLayer();
            Assert(AboveWindow(page.Handle,bubble.Handle),"dismissing a bubble stops its layer maintenance during the fade-out");
            bubble.HideNow();bubble.MaintainLayer();Assert(!bubble.Visible,"layer maintenance never reopens a dismissed or hidden bubble");
            panel.AllowExit=true;
        }
    }
}
