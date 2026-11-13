using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void MatchFreshButton(ButtonProbe button,Control parent,int dpi,bool hover,bool pressed)
    {
        button.Hover(hover);button.Press(pressed);
        using(var fresh=new ButtonProbe{Size=button.Size,Text=button.Text,Font=button.Font,Primary=button.Primary,Enabled=button.Enabled})
        using(var actual=new Bitmap(button.Width,button.Height))
        using(var expected=new Bitmap(button.Width,button.Height))
        {
            parent.Controls.Add(fresh);fresh.Hover(hover);fresh.Press(pressed);
            actual.SetResolution(dpi,dpi);expected.SetResolution(dpi,dpi);
            using(var g=Graphics.FromImage(actual))button.Render(g);
            using(var g=Graphics.FromImage(expected))fresh.Render(g);
            bool same=true;
            for(int y=0;y<actual.Height&&same;y++)for(int x=0;x<actual.Width;x++)
                if(actual.GetPixel(x,y)!=expected.GetPixel(x,y)){same=false;break;}
            Assert(same,"reused button geometry matches a fresh frame after text/state/size/DPI changes at "+dpi+" DPI");
        }
    }
    static void MainPanelPerformanceChecks()
    {
        using(var parent=new RoundedPanel{Size=new Size(620,160)})
        using(var button=new ButtonProbe{Size=new Size(540,102),Text="确认设定",Primary=true})
        using(var frame=new Bitmap(540,102))
        {
            parent.Controls.Add(button);frame.SetResolution(288,288);
            using(var g=Graphics.FromImage(frame))
            {
                foreach(bool hover in new[]{false,true}){button.Hover(hover);button.Render(g);}
                int rasterizations=button.ShapeRasterizations;
                var clock=Stopwatch.StartNew();
                for(int i=0;i<120;i++){button.Hover((i&1)==0);button.Render(g);}
                clock.Stop();Console.WriteLine("Main panel buttons: 120 warm hover paints at 300%, "+clock.ElapsedMilliseconds+" ms.");
                Assert(button.ShapeRasterizations==rasterizations,"repeated hover repaints allocate no supersampled button geometry once warmed");
            }
            button.Text="设定已修改";MatchFreshButton(button,parent,288,false,false);
            foreach(bool pressed in new[]{true,false})MatchFreshButton(button,parent,288,true,pressed);
            button.Enabled=false;MatchFreshButton(button,parent,288,false,false);
            button.Enabled=true;button.Primary=false;MatchFreshButton(button,parent,288,false,false);
            button.Width=420;MatchFreshButton(button,parent,288,true,false);
            foreach(int dpi in new[]{192,96,288})MatchFreshButton(button,parent,dpi,true,false);
        }
        using(var panel=new MainForm(TestStore(),new Settings()))
        {
            Assert(panel.Opacity==1&&!PopupSurface.Contains(panel),"the main panel keeps native opaque presentation and input");
            panel.Opacity=0;panel.Show();panel.Location=new Point(-12000,-12000);Application.DoEvents();
            Assert((WindowStyle(panel.Handle,-20)&0x02000000)==0,"the main panel avoids descendant-wide compositing stalls");
            using(var limits=new VirtualRenderBounds(panel))
            {
                SetTestDpi(panel,192);LayerPosition(panel.Handle,IntPtr.Zero,-12000,-12000,2160,1800,0x214);panel.PerformLayout();
                using(var bitmap=new Bitmap(panel.Width,panel.Height))
                {
                    bitmap.SetResolution(192,192);int previous=PopupSurface.RenderingDpi;PopupSurface.RenderingDpi=192;
                    try
                    {
                        panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,panel.Size));
                        var backdrop=(FairyBackdrop)panel.Controls[0];int backgrounds=backdrop.BackgroundRasterizations;
                        var clock=Stopwatch.StartNew();
                        for(int i=0;i<8;i++)panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,panel.Size));
                        clock.Stop();Console.WriteLine("Main panel: 8 warm full-window paints at 200%, "+clock.ElapsedMilliseconds+" ms.");
                        Assert(backdrop.BackgroundRasterizations==backgrounds,"visible scrollbars do not rebuild the main background on every child paint");
                        bitmap.Save(Path.Combine(DisplayReviewDirectory(),"main-panel-performance.png"));
                    }
                    finally{PopupSurface.RenderingDpi=previous;}
                }
            }
            var start=panel.Location;var move=Stopwatch.StartNew();
            ScrollMessage(panel.Handle,0x231,IntPtr.Zero,IntPtr.Zero);
            for(int i=0;i<60;i++){panel.Location=new Point(start.X+(i%15)*3,start.Y+(i%10)*2);Application.DoEvents();}
            ScrollMessage(panel.Handle,0x232,IntPtr.Zero,IntPtr.Zero);
            move.Stop();Console.WriteLine("Main panel: 60 native moves, "+move.ElapsedMilliseconds+" ms.");
            panel.AllowExit=true;panel.Close();
        }
    }
}
