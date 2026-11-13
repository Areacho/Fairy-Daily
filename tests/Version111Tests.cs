using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void Version111Checks()
    {
        var store=TestStore();Store.AtomicWrite(Path.Combine(store.Root,"display.json"),"{\"Layout\":\"compact\",\"EdgeQuality\":2}");
        var legacy=DisplayPreferences.Load(store);Assert(legacy.Layout=="compact"&&legacy.Resolution=="auto","old display preferences migrate without a selectable eye-quality option");
        using(var panel=new MainForm(store,new Settings()))
        {
            panel.Opacity=0;panel.Show();panel.Location=new Point(-12000,-12000);
            Assert(FindControls(panel).OfType<RoundButton>().Count(b=>b.AccessibleName=="打开显示设置")==1&&!FindControls(panel).Any(c=>c.Text=="挂件边缘"),"one main-panel entry replaces the inline layout and quality row");
            var clock=FindControls(panel).OfType<DigitalTimePicker>().Single();var daily=clock.Parent;
            Assert(new[]{"开启每日定时","关闭每日定时"}.All(text=>daily.Controls.OfType<RoundButton>().Any(b=>b.Text==text))&&daily.Controls.OfType<ToggleButton>().Any(),"daily time, enable/disable and startup controls belong to the same card");
            foreach(int dpi in new[]{96,120,144,168,192,240,288})
            {
                SetTestDpi(panel,dpi);panel.ApplyDisplayLayout(false);
                Assert(daily.Controls.Cast<Control>().All(c=>daily.ClientRectangle.Contains(c.Bounds)),"all daily controls retain border insets at "+dpi+" DPI");
                var actions=daily.Controls.Cast<Control>().Where(c=>c is RoundButton||c is ToggleButton||c is DigitalTimePicker).ToArray();
                Assert(actions.All(a=>actions.All(b=>a==b||!a.Bounds.IntersectsWith(b.Bounds))),"daily time and actions never overlap at "+dpi+" DPI");
            }
            foreach(string mode in new[]{"auto","1080p","2k","4k"})
            {
                panel.SaveDisplayOptions(new DisplayPreferences{Resolution=mode});
                Assert(Screen.FromControl(panel).WorkingArea.Contains(panel.Bounds)&&DisplayPreferences.Load(store).Resolution==mode,"window resolution "+mode+" persists and remains within the real work area");
            }
            using(var settings=panel.CreateDisplaySettings())
            {
                settings.Opacity=0;settings.Show();settings.Location=new Point(-12000,-12000);
                Assert(!FindControls(settings).OfType<ComboBox>().Any()&&FindControls(settings).OfType<RoundChoice>().Count()==2,"display settings use rounded selectors without native light dropdown surfaces");
                settings.LayoutChoice.SelectedIndex=1;settings.ResolutionChoice.SelectedIndex=2;
                Assert(settings.Selection.Layout=="compact"&&settings.Selection.Resolution=="2k","settings resolve layout and physical window-resolution selections");
                Assert(DisplayPreferences.Load(store).Resolution=="4k","editing or cancelling settings leaves saved preferences unchanged");
                foreach(int dpi in new[]{96,120,144,168,192,240,288})
                {
                    SetTestDpi(settings,dpi);settings.MinimumSize=Size.Empty;using(var limits=new VirtualRenderBounds(settings))LayerPosition(settings.Handle,IntPtr.Zero,-12000,-12000,WindowLayout.Pixels(610,dpi),WindowLayout.Pixels(490,dpi),0x214);settings.PerformLayout();
                    foreach(var choice in new[]{settings.LayoutChoice,settings.ResolutionChoice})
                    {
                        Assert(choice.Parent.ClientRectangle.Contains(choice.Bounds)&&choice.Left>=WindowLayout.Pixels(120,dpi)&&choice.Right<=choice.Parent.Width-WindowLayout.Pixels(16,dpi),"rounded setting selector keeps its border inset at "+dpi+" DPI");
                        using(var frame=new Bitmap(choice.Width,choice.Height)){frame.SetResolution(dpi,dpi);choice.DrawToBitmap(frame,choice.ClientRectangle);Assert(frame.GetPixel(0,0).ToArgb()!=Color.White.ToArgb(),"selector corners remain in the navy palette at "+dpi+" DPI");}
                    }
                    if(dpi==120||dpi==192)using(var frame=new Bitmap(settings.Width,settings.Height)){frame.SetResolution(dpi,dpi);int old=PopupSurface.RenderingDpi;PopupSurface.RenderingDpi=dpi;try{settings.DrawToBitmap(frame,new Rectangle(Point.Empty,settings.Size));}finally{PopupSurface.RenderingDpi=old;}frame.Save(Path.Combine(DisplayReviewDirectory(),"settings-v111-"+dpi+".png"));}
                }
            }
            SetTestDpi(panel,120);panel.SaveDisplayOptions(new DisplayPreferences());
            var root=panel.Controls.OfType<FairyBackdrop>().Single();
            Assert((root.ClientSize.Width<root.AutoScrollMinSize.Width||!root.HorizontalScroll.Visible)&&root.AutoScrollPosition.X==0,"resolution and DPI changes discard stale horizontal extents while retaining scrolling on small desktops");
            using(var frame=new Bitmap(panel.Width,panel.Height)){frame.SetResolution(120,120);panel.DrawToBitmap(frame,new Rectangle(Point.Empty,frame.Size));frame.Save(Path.Combine(DisplayReviewDirectory(),"main-v111.png"));}
            panel.AllowExit=true;panel.Close();
        }
        using(var native=new MainForm(TestStore(),new Settings()))
        {
            native.Opacity=0;native.Show();Application.DoEvents();
            using(var image=new Bitmap(native.Width,native.Height)){image.SetResolution(native.DeviceDpi,native.DeviceDpi);native.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(DisplayReviewDirectory(),"main-v111-native.png"));}
            using(var settings=native.CreateDisplaySettings()){settings.Opacity=0;settings.Show();Application.DoEvents();using(var image=new Bitmap(settings.Width,settings.Height)){image.SetResolution(settings.DeviceDpi,settings.DeviceDpi);settings.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(DisplayReviewDirectory(),"settings-v111-native.png"));}}
            native.AllowExit=true;
        }
        ScrollbarOverlayChecks();
    }
    static void ScrollbarOverlayChecks()
    {
        using(var host=new Form{Opacity=0,ShowInTaskbar=false,ClientSize=new Size(520,420)})
        using(var page=new Panel{Location=new Point(12,12),Size=new Size(460,180),AutoScroll=true,AutoScrollMinSize=new Size(700,1200),BackColor=Theme.Surface})
        using(var log=new RichTextBox{Location=new Point(12,212),Size=new Size(460,180),BorderStyle=BorderStyle.None,BackColor=Theme.Surface,Text=string.Join(Environment.NewLine,Enumerable.Range(0,160).Select(i=>"离线日志 "+i))})
        {
            host.Controls.Add(page);host.Controls.Add(log);ScrollbarTheme.Attach(page,true);ScrollbarTheme.Attach(log,true);host.Show();host.Location=new Point(-12000,-12000);Application.DoEvents();
            var bars=host.Controls.OfType<ScrollbarTheme.ScrollbarOverlay>().ToArray();
            Assert(bars.Count(b=>b.Visible)==3,"opaque sibling scrollbars cover both page axes and the log");
            var bar=bars.Single(b=>b.Target==page&&b.Vertical);ScrollbarTheme.ScrollbarInfo info;ScrollbarTheme.TryGetBar(page.Handle,true,out info);
            Assert(bar.RectangleToScreen(bar.ClientRectangle)==info.Bounds.Rectangle,"opaque scroll surface exactly covers the native vertical bar");
            int width=bar.Width;var down=new IntPtr((width/2&0xffff)|((bar.Height-4)<<16));ScrollMessage(bar.Handle,0x201,new IntPtr(1),down);ScrollMessage(bar.Handle,0x202,IntPtr.Zero,down);
            Assert(page.AutoScrollPosition.Y<0,"rounded scrollbar arrow still scrolls the native page");
            ScrollMessage(page.Handle,0x115,new IntPtr(6),IntPtr.Zero);ScrollbarTheme.TryGetBar(page.Handle,true,out info);
            int thumb=(info.ThumbStart+info.ThumbEnd)/2;ScrollMessage(bar.Handle,0x201,new IntPtr(1),new IntPtr(width/2|(thumb<<16)));ScrollMessage(bar.Handle,0x200,new IntPtr(1),new IntPtr(width/2|((bar.Height-30)<<16)));ScrollMessage(bar.Handle,0x202,IntPtr.Zero,new IntPtr(width/2|((bar.Height-30)<<16)));
            Assert(page.AutoScrollPosition.Y<-200,"dragging the rounded thumb updates the native scroll position");
            ScrollMessage(log.Handle,0x115,new IntPtr(7),IntPtr.Zero);AssertDarkScrollbars(log,"overlay-backed log print");
            foreach(var overlay in bars.Where(b=>b.Visible))using(var frame=new Bitmap(overlay.Width,overlay.Height))
            {overlay.DrawToBitmap(frame,new Rectangle(Point.Empty,frame.Size));int white=0;for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){var c=frame.GetPixel(x,y);if(c.R>220&&c.G>220&&c.B>220)white++;}Assert(white==0,"the opaque scrollbar overlay contains no white theme pixels");}
            log.Hide();Assert(!bars.Any(b=>b.Target==log&&b.Visible),"scrollbar overlay hides with its target");log.Show();Assert(bars.Any(b=>b.Target==log&&b.Visible),"scrollbar overlay returns with its target");
            host.Close();
        }
    }
}
