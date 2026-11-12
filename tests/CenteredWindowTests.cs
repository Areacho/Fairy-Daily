using System;
using System.Drawing;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static bool Centered(Rectangle bounds,Rectangle area)
    {return Math.Abs((bounds.Left*2+bounds.Width)-(area.Left*2+area.Width))<=1&&Math.Abs((bounds.Top*2+bounds.Height)-(area.Top*2+area.Height))<=1;}
    static void CenteredWindowChecks()
    {
        foreach(var area in new[]{new Rectangle(0,0,2560,1400),new Rectangle(-3840,-2160,3840,2080)})
        foreach(int dpi in new[]{96,120,144,168,192,240,288})
        {
            foreach(var size in new[]{new Size(580,310),new Size(860,650),new Size(1080,900),new Size(328,430)})
            {
                var result=WindowLayout.CenteredBounds(WindowLayout.Pixels(size,dpi),area,dpi);
                Assert(area.Contains(result)&&Centered(result,area),"window starts centered inside the work area at "+dpi+" DPI, size="+size);
            }
        }
        var store=TestStore();var settings=new Settings();
        using(var owner=new PassiveForm{Bounds=new Rectangle(40,40,250,250),Opacity=0,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual})
        using(var panel=new MainForm(store,settings))
        {
            owner.Show();var area=Screen.FromControl(owner).WorkingArea;panel.PlacementOwner=owner;
            using(var module=new FairyModuleForm(panel,FairySection.Checkin))
            using(var qr=new QrLoginForm())using(var heybox=new HeyboxForm(store,settings))using(var browser=new HeyboxBrowserDialog(store))
            {
                Assert(browser.StartPosition==FormStartPosition.Manual,"verification host uses the shared screen-position policy without starting its web runtime");
                Assert(heybox.StartPosition==FormStartPosition.Manual,"Heybox account dialog uses the shared screen-position policy");
                foreach(var form in new Form[]{panel,module,qr})
                {
                    form.Opacity=0;form.Show(owner);Application.DoEvents();
                    Assert(Centered(form.Bounds,area),form.GetType().Name+" opens at screen center even when its owner is near a corner");
                    form.Hide();form.Location=new Point(area.Left+40,area.Top+40);form.Show(owner);Application.DoEvents();
                    Assert(Centered(form.Bounds,area),form.GetType().Name+" recenters on reopening instead of reusing an old corner position");form.Hide();
                }
                module.Show(owner);Application.DoEvents();
                foreach(var section in new[]{FairySection.MiyouTasks,FairySection.Schedule,FairySection.Login,FairySection.HeyboxTasks})
                {
                    var previous=module.Bounds;module.Navigate(section,false);Application.DoEvents();
                    Assert(Math.Abs((previous.Left*2+previous.Width)-(module.Left*2+module.Width))<=1&&Math.Abs((previous.Top*2+previous.Height)-(module.Top*2+module.Height))<=1,"changing page size preserves the window center: "+section);
                }
                qr.ShowFor(owner);Application.DoEvents();var fixedQr=qr.Location;owner.Location=new Point(owner.Left+30,owner.Top+25);Application.DoEvents();
                Assert(qr.Location==fixedQr,"QR window no longer follows its source window after opening");qr.Hide();module.Hide();
            }
            panel.AllowExit=true;owner.Close();
        }
        using(var panel=new MainForm(TestStore(),new Settings()))using(var widget=new FairyWidget(TestStore(),panel))
        {
            widget.Show();var area=Screen.FromControl(widget).WorkingArea;widget.OpenPanel();
            Assert(Centered(panel.Bounds,area),"dashboard entry centers the existing window on the widget's screen");panel.Hide();
            widget.OpenModule(FairySection.Schedule);
            var module=(FairyModuleForm)typeof(FairyWidget).GetField("module",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(widget);
            Assert(Centered(module.Bounds,area),"module entry opens at the widget screen center");
            module.Location=new Point(area.Left+20,area.Top+20);widget.OpenModule(FairySection.Checkin);
            Assert(Centered(module.Bounds,area),"reusing a module from the widget entry resets its open position to screen center");
            module.WindowState=FormWindowState.Minimized;widget.OpenModule(FairySection.Schedule);
            Assert(module.WindowState==FormWindowState.Normal&&Centered(module.Bounds,area),"opening a minimized module restores it at screen center");module.Close();panel.AllowExit=true;
        }
    }
}
