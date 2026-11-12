using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void AutoExitChecks()
    {
        Assert(!new Settings().ExitWidgetAfterCheckin&&!Json.Read<Settings>("{\"DailyTime\":\"09:00\"}").ExitWidgetAfterCheckin,"automatic widget exit defaults off for new and older configurations");
        var store=TestStore();var settings=Setup(store,new string[0],new[]{"5"});store.SaveState(CompletedState(settings,FakeCookie));
        var off=CheckinNotifications.Publish(store,settings,new RunSummary(),true);
        Assert(!off.ExitAfterCompletion,"default completed scheduled task keeps the widget running");
        settings.ExitWidgetAfterCheckin=true;store.Save(settings);
        Assert(store.Load().ExitWidgetAfterCheckin,"automatic exit preference persists independently of widget position");
        var scheduled=CheckinNotifications.Publish(store,settings,new RunSummary(),true);
        Assert(scheduled!=null&&scheduled.ExitAfterCompletion&&SpeechBus.Read(store).ExitAfterCompletion,"enabling auto-exit permits a new scheduled notice and persists its successful exit intent");
        Assert(CheckinNotifications.Publish(store,settings,new RunSummary(),true)==null,"successful scheduled auto-exit notice still deduplicates on the same day");
        Assert(!CheckinNotifications.Publish(store,settings,new RunSummary{Failed=1},false).ExitAfterCompletion,"failed run never requests an automatic exit even with prior completed records");
        var all=Json.Read<Settings>(Json.Write(settings));all.Forums=new[]{"5","2"};
        Assert(!CheckinNotifications.Publish(store,settings,new RunSummary(),false,all).ExitAfterCompletion,"single-platform completion cannot exit while another configured target is unfinished");
        using(var panel=new MainForm(store,settings))using(var widget=new FairyWidget(store,panel))
        {
            var dialogue=(FairyDialogue)typeof(FairyWidget).GetField("dialogue",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(widget);
            var notice=CheckinNotifications.Publish(store,settings,new RunSummary(),false);widget.DeliverCompletion(notice,true);
            Assert(widget.AutoExitPending&&dialogue.Visible&&dialogue.Controls.OfType<Label>().Single().Text.Contains("8 秒后退出挂件"),"completion dialogue appears before automatic exit with a visible delay and cancellation instruction");
            double due=widget.AutoExitDeadline;
            Assert(!widget.ProcessAutoExit(due-1,true)&&!widget.IsDisposed,"automatic exit waits until the notification delay expires");
            Assert(!widget.ProcessAutoExit(due+1,false)&&widget.AutoExitDeadline>=due+9&&dialogue.KeepNoticeOpen,"locking the desktop postpones exit and prevents the completion dialogue from expiring unseen");
            using(store.AcquireRunLock())Assert(!widget.ProcessAutoExit(widget.AutoExitDeadline+1,true)&&!widget.IsDisposed,"another active check-in worker prevents automatic exit");
            var operation=typeof(MainForm).GetField("operation",BindingFlags.Instance|BindingFlags.NonPublic);using(var active=new CancellationTokenSource())
            {operation.SetValue(panel,active);Assert(!widget.ProcessAutoExit(widget.AutoExitDeadline+1,true)&&widget.AutoExitPending,"active foreground work postpones automatic exit");operation.SetValue(panel,null);}
            dialogue.Dismiss();Assert(!widget.AutoExitPending,"closing the notification cancels this exit without disabling the saved preference");
            widget.DeliverCompletion(CheckinNotifications.Publish(store,settings,new RunSummary(),false),true);dialogue.Close();
            Assert(!widget.AutoExitPending&&!dialogue.Visible&&!dialogue.IsDisposed&&!dialogue.KeepNoticeOpen,"window close or Alt-F4 cancels pending exit and keeps the notification reusable");
            widget.DeliverCompletion(CheckinNotifications.Publish(store,settings,new RunSummary(),false),true);widget.Say("主人，配置通道已接入。");
            Assert(!widget.AutoExitPending&&settings.ExitWidgetAfterCheckin,"new interaction cancels only the current countdown");
            widget.DeliverCompletion(CheckinNotifications.Publish(store,settings,new RunSummary(),false),true);
            typeof(Control).GetMethod("OnMouseDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(widget,new object[]{new MouseEventArgs(MouseButtons.Left,1,10,10,0)});
            typeof(Control).GetMethod("OnMouseUp",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(widget,new object[]{new MouseEventArgs(MouseButtons.Left,1,10,10,0)});
            Assert(!widget.AutoExitPending,"clicking the eye cancels automatic exit");
            widget.DeliverCompletion(CheckinNotifications.Publish(store,settings,new RunSummary(),false),true);
            widget.ContextMenuStrip.Show(new Point(30,30));widget.ContextMenuStrip.Close();Assert(!widget.AutoExitPending,"opening the widget context menu cancels automatic exit");
            widget.DeliverCompletion(CheckinNotifications.Publish(store,settings,new RunSummary(),false),true);
            settings.ExitWidgetAfterCheckin=false;Assert(!widget.ProcessAutoExit(widget.AutoExitDeadline+1,true)&&!widget.AutoExitPending,"disabling auto-exit before the deadline cancels the pending exit");settings.ExitWidgetAfterCheckin=true;
            widget.DeliverCompletion(CheckinNotifications.Publish(store,settings,new RunSummary(),false),true);store.SaveState(new RunState());
            Assert(!widget.ProcessAutoExit(widget.AutoExitDeadline+1,true)&&!widget.IsDisposed,"a changed or incomplete daily state cancels pending automatic exit");
            store.SaveState(CompletedState(settings,FakeCookie));panel.AllowExit=true;
        }
        using(var panel=new MainForm(store,settings))using(var page=new FairyModuleForm(panel,FairySection.Schedule))
        {
            var toggle=FindControls(page).OfType<ToggleButton>().Single(c=>c.AccessibleName=="签到完成后自动退出挂件");
            Assert(toggle.Checked&&toggle.Text.Contains("全部签到"),"timer page shows the persisted automatic exit toggle with the all-target completion condition");panel.AllowExit=true;
        }
        using(var panel=new MainForm(store,settings))using(var widget=new FairyWidget(store,panel))
        {
            widget.DeliverCompletion(CheckinNotifications.Publish(store,settings,new RunSummary(),false),true);
            Assert(widget.ProcessAutoExit(widget.AutoExitDeadline+1,true)&&widget.IsDisposed&&panel.IsDisposed,"successful completion closes the widget and dashboard after the visible delay");
        }
    }
    static void ContextMenuChecks()
    {
        using(var panel=new MainForm(TestStore(),new Settings()))using(var widget=new FairyWidget(TestStore(),panel))
        {
            var menu=(FairyContextMenu)widget.ContextMenuStrip;menu.PrepareLayout();
            Assert(menu.Items[0] is FairyMenuHeader&&menu.Items.OfType<FairyMenuItem>().Count()==10,"widget menu groups check-in, settings and desktop controls below its Fairy header");
            Assert(!menu.ShowImageMargin&&!menu.ShowCheckMargin&&menu.Renderer is FairyMenuRenderer,"HDD menu removes native gutters and uses the shared custom renderer");
            Assert(menu.Region!=null&&!menu.Region.IsVisible(0,0)&&menu.Region.IsVisible(menu.Width/2,menu.Height/2),"native context menu has a rounded visible region");
            var run=menu.Items.OfType<FairyMenuItem>().First();
            Assert(menu.GetNextItem(run,ArrowDirection.Down).Text=="签到配置"&&run.Bounds.Left>=menu.Padding.Left&&Math.Abs((menu.Width-run.Bounds.Right)-run.Bounds.Left)<=2,"menu preserves native vertical navigation with balanced horizontal margins");
            var top=menu.Items.OfType<FairyMenuItem>().Single(i=>i.Text=="始终置顶");top.PerformClick();Assert(!top.Checked&&top.Hint=="已关闭"&&!widget.TopMost,"pin menu item updates its status badge and actual widget state");
            var pause=menu.Items.OfType<FairyMenuItem>().Single(i=>i.Text=="暂停动画");pause.PerformClick();Assert(pause.Checked&&pause.Hint=="已暂停","pause menu item updates its animation status badge");
            var size=menu.Items.OfType<FairyMenuItem>().Single(i=>i.Text=="挂件大小");size.DropDownItems[0].PerformClick();Assert(size.Hint=="小号"&&widget.Width==WindowLayout.Pixels(180,widget.DeviceDpi),"size submenu keeps the active size and widget dimensions in sync");
            Assert(size.DropDown is FairyContextMenu&&menu.Items.OfType<FairyMenuItem>().Single(i=>i.Danger).Text=="退出挂件","submenu shares the HDD style and exit has a distinct visual role");
            int nativeDpi=menu.DeviceDpi;
            foreach(int dpi in new[]{96,144,192,288})
            {
                SetChildDpi(menu,dpi);menu.PrepareLayout();
                Assert(menu.Items.OfType<FairyMenuItem>().All(i=>i.Height>=WindowLayout.Pixels(40,dpi)&&i.Width>=WindowLayout.Pixels(260,dpi)),"menu target sizes scale with native DPI at "+dpi+" DPI");
            }
            SetChildDpi(menu,nativeDpi);menu.PrepareLayout();menu.MaximumSize=new Size(0,WindowLayout.Pixels(320,nativeDpi));
            var area=Screen.PrimaryScreen.WorkingArea;menu.Opacity=0;menu.Show(new Point(area.Left+40,area.Top+40));
            var exit=menu.Items.OfType<FairyMenuItem>().Single(i=>i.Danger);run.Select();
            var arrow=typeof(ToolStrip).GetMethod("ProcessArrowKey",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int step=1;step<menu.Items.OfType<FairyMenuItem>().Count();step++)arrow.Invoke(menu,new object[]{Keys.Down});
            Application.DoEvents();
            Assert(exit.Selected&&area.Contains(menu.Bounds)&&menu.ClientRectangle.Contains(new Point(exit.Bounds.Left+1,exit.Bounds.Top+exit.Height/2)),"height-constrained native menu keeps its final action reachable by keyboard scrolling");menu.Close();
            panel.AllowExit=true;
        }
    }
}
