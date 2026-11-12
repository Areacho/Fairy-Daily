using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    // Exercise virtual monitor sizes and DPI changes without altering Windows
    // display settings. The framework's DPI field is changed only in this test.
    static readonly FieldInfo dpiField=typeof(Control).GetField("deviceDpi",BindingFlags.NonPublic|BindingFlags.Instance);
    static void Materialize(Control control)
    {var handle=control.Handle;foreach(Control child in control.Controls)Materialize(child);}
    static void SetTestDpi(Form form,int dpi)
    {
        Materialize(form);form.AutoScaleMode=AutoScaleMode.None;
        float factor=dpi/(float)form.DeviceDpi;form.Scale(new SizeF(factor,factor));
        SetChildDpi(form,dpi);form.PerformLayout();
    }
    static void SetChildDpi(Control root,int dpi)
    {
        Materialize(root);var controls=new[]{root}.Concat(FindControls(root)).ToArray();
        foreach(var control in controls)dpiField.SetValue(control,dpi);
        var notify=typeof(Control).GetMethod("OnDpiChangedAfterParent",BindingFlags.NonPublic|BindingFlags.Instance);
        foreach(var control in controls){notify.Invoke(control,new object[]{EventArgs.Empty});control.PerformLayout();}
    }
    static void HighDpiChecks()
    {
        var report=new List<object>();
        foreach(var screen in new[]{new Size(2560,1440),new Size(3840,2160)})
        foreach(int dpi in new[]{96,120,144,168,192,240,288})
        {
            var area=new Rectangle(0,0,screen.Width,screen.Height-WindowLayout.Pixels(32,dpi));bool fits=true;
            foreach(var size in new[]{new Size(1080,900),new Size(860,650),new Size(620,470),new Size(650,448),new Size(328,430),new Size(480,126)})
            {
                var pixels=WindowLayout.Pixels(size,dpi);var bounds=WindowLayout.FitBounds(new Rectangle(-100,-100,pixels.Width,pixels.Height),area,dpi);
                fits&=area.Contains(bounds)&&bounds.Width>0&&bounds.Height>0;
                var again=WindowLayout.FitBounds(bounds,area,dpi);fits&=bounds==again;
            }
            Assert(fits,"all windows fit the "+screen.Width+"x"+screen.Height+" work area at "+(dpi*100/96)+" percent without progressive resizing");
            report.Add(new{Width=screen.Width,Height=screen.Height,Dpi=dpi,ScalePercent=dpi*100/96,AllWindowsFit=fits});
        }
        var negative=new Rectangle(-3840,-2160,3840,2080);
        Assert(negative.Contains(WindowLayout.FitBounds(new Rectangle(-10000,-10000,6000,5000),negative,240)),"window fitting supports monitors left of and above the primary display");
        using(var panel=new MainForm(TestStore(),new Settings()))
        using(var module=new FairyModuleForm(panel,FairySection.MiyouTasks))
        {
            Materialize(module);var title=FindControls(module).OfType<SmoothLabel>().First(c=>c.Font.SizeInPoints>=18);
            Assert(title.Height>=Math.Ceiling(title.Font.GetHeight(module.DeviceDpi)),"initial page title has enough height at the native monitor DPI");
            foreach(int dpi in new[]{96,120,144,168,192,240,288,192,96})
            {
                SetTestDpi(module,dpi);module.Navigate(FairySection.MiyouTasks,false);SetChildDpi(module,dpi);
                var tiles=FindControls(module).OfType<GameTile>().ToArray();
                bool contained=tiles.Length==7&&tiles.All(tile=>tile.Width>WindowLayout.Pixels(160,dpi)&&tile.Controls.Cast<Control>().All(c=>tile.ClientRectangle.Contains(c.Bounds)));
                Assert(contained,"inline game and community switches remain inside their cards at "+dpi+" DPI");
                var grid=(TableLayoutPanel)tiles[0].Parent;Assert(grid.ColumnCount==4&&grid.RowCount==2,"inline configuration retains its four-column layout at "+dpi+" DPI");
                var sample=tiles.Single(t=>t.Key=="genshin");sample.GameSwitch.Checked=false;
                int next=dpi==288?192:288;SetTestDpi(module,next);
                Assert(!sample.GameSwitch.Checked&&sample.ForumSwitch.Checked,"DPI changes retain unconfirmed independent game and community selections at "+dpi+" DPI");
                module.Navigate(FairySection.Schedule,false);SetChildDpi(module,next);
                var clock=FindControls(module).OfType<DigitalTimePicker>().Single();
                var buttons=FindControls(module).OfType<RoundButton>().Where(b=>b.Text=="确认时间"||b.Text=="开启定时"||b.Text=="暂停定时").ToArray();
                Assert(clock.Width>WindowLayout.Pixels(400,next)&&buttons.Length==3&&buttons.All(b=>b.Bounds.Height>WindowLayout.Pixels(30,next)&&b.Parent.ClientRectangle.Contains(b.Bounds)),"large timer and confirmation buttons remain usable at "+next+" DPI");
            }
            panel.AllowExit=true;
        }
        string path=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build","high-dpi-report.json"));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,Json.Write(new{Method="Virtual work areas and simulated framework DPI; native pixel text rendering checked separately",Cases=report}));
    }
}
