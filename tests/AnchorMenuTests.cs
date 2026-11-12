using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void AnchorMenuChecks()
    {
        foreach(int dpi in new[]{96,120,144,192,288})
        {
            var area=new Rectangle(-2560,0,2560,1400);var eye=new Rectangle(-1800,400,WindowLayout.Pixels(250,dpi),WindowLayout.Pixels(250,dpi));bool? side=null;Point? first=null;
            foreach(int height in new[]{58,116,126,164,58})
            {
                var size=WindowLayout.Pixels(new Size(480,height),dpi);var position=WindowLayout.PlaceBesideWidget(size,eye,area,dpi,ref side);
                Assert(!first.HasValue||position==first.Value,"greeting, shortcuts and notifications keep a single origin at "+dpi+" DPI");first=position;
                Assert(area.Contains(new Rectangle(position,size)),"anchored bubble fits a monitor with a negative origin at "+dpi+" DPI");
            }
        }
        bool? right=null;var screen=new Rectangle(0,0,2560,1400);var bubbleSize=new Size(480,164);
        WindowLayout.PlaceBesideWidget(bubbleSize,new Rectangle(10,400,250,250),screen,96,ref right);
        Assert(right==false,"bubble docks to the right when the left screen edge has no room");
        WindowLayout.PlaceBesideWidget(bubbleSize,new Rectangle(800,400,250,250),screen,96,ref right);
        Assert(right==false,"docking side stays stable after moving away from the edge");
        WindowLayout.PlaceBesideWidget(bubbleSize,new Rectangle(2250,400,250,250),screen,96,ref right);
        Assert(right==true,"docking side changes only when its current side no longer fits");
        var store=TestStore();using(var panel=new MainForm(store,new Settings()))using(var widget=new FairyWidget(store,panel))
        {
            widget.Show();var area=Screen.FromControl(widget).WorkingArea;widget.Location=new Point(area.Left+700,area.Top+180);
            widget.Say("主人，委托已接收。");var bubble=WidgetDialogue(widget);Point origin=bubble.Location;
            bubble.OpenShortcuts(true);bubble.ShowFor(widget);Assert(bubble.Location==origin,"greeting does not move the anchored bubble");
            bubble.OpenShortcuts(false);bubble.ShowFor(widget);Assert(bubble.Location==origin,"shortcut expansion does not move the anchored bubble");
            widget.Say("主人，签到已完成。");bubble.Say("主人，8 秒后退出挂件。",true);bubble.ShowFor(widget);
            Assert(bubble.Location==origin,"normal and extended notification layouts keep the same position");
            bubble.ShowFor(panel);panel.Location=new Point(area.Left+40,area.Top+40);
            Assert(bubble.Owner==null&&bubble.Location==origin,"an independent bubble stays anchored to the widget when a page requests it");
            widget.OpenModule(FairySection.Schedule);var module=(FairyModuleForm)typeof(FairyWidget).GetField("module",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(widget);
            module.Location=new Point(area.Left+50,area.Top+60);module.Navigate(FairySection.Checkin,false);
            Assert(bubble.Location==origin,"moving and switching module pages leaves the bubble beside the widget");
            widget.Location=new Point(widget.Left+35,widget.Top+20);
            Assert(bubble.Location==new Point(origin.X+35,origin.Y+20),"moving the widget moves its bubble by the same offset");
            module.Close();panel.AllowExit=true;
        }
        MenuDismissChecks();Application.DoEvents();
    }
    static void OpenWidgetMenu(FairyWidget widget)
    {
        Point point=widget.PointToScreen(new Point(20,20));long packed=((long)(point.Y&65535)<<16)|(uint)(point.X&65535);
        ScrollMessage(widget.Handle,0x7B,widget.Handle,new IntPtr(packed));Application.DoEvents();
    }
    static void MenuDismissChecks()
    {
        var store=TestStore();using(var panel=new MainForm(store,new Settings()))using(var widget=new FairyWidget(store,panel))
        using(var outside=new Form{Opacity=0,ShowInTaskbar=false,Size=new Size(160,100)})
        {
            widget.Show();var menu=(FairyContextMenu)widget.ContextMenuStrip;
            OpenWidgetMenu(widget);Assert(menu.Visible&&menu.AutoClose,"explicit widget context-menu interaction opens a normally auto-closing menu");
            var escape=typeof(FairyContextMenu).GetMethod("ProcessDialogKey",BindingFlags.NonPublic|BindingFlags.Instance);
            escape.Invoke(menu,new object[]{Keys.Escape});Application.DoEvents();Assert(!menu.Visible,"Escape dismisses the root menu");
            OpenWidgetMenu(widget);var sizes=menu.Items.OfType<FairyMenuItem>().Single(i=>i.Text=="挂件大小");sizes.ShowDropDown();Application.DoEvents();
            Assert(sizes.DropDown.Visible,"compact size submenu opens normally");
            escape.Invoke(sizes.DropDown,new object[]{Keys.Escape});Application.DoEvents();Assert(!menu.Visible&&!sizes.DropDown.Visible,"Escape closes the complete root and submenu tree");
            OpenWidgetMenu(widget);menu.Items.OfType<FairyMenuItem>().Single(i=>i.Text=="暂停动画").PerformClick();Application.DoEvents();
            Assert(!menu.Visible,"choosing an ordinary action dismisses its menu before running the action");
            OpenWidgetMenu(widget);sizes.ShowDropDown();sizes.DropDownItems[0].PerformClick();Application.DoEvents();
            Assert(!menu.Visible&&!sizes.DropDown.Visible,"choosing a size closes the root and child menus together");
            OpenWidgetMenu(widget);outside.Show();outside.Activate();Application.DoEvents();
            Assert(!menu.Visible,"changing to another foreground window dismisses the widget menu");
            outside.Close();OpenWidgetMenu(widget);escape.Invoke(menu,new object[]{Keys.Escape});Application.DoEvents();
            Assert(!menu.Visible,"the menu can reopen and dismiss repeatedly without stale tracking state");panel.AllowExit=true;
        }
    }
}
