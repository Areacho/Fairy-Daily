using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    sealed class LayeredTestHost : PassiveForm
    {protected override CreateParams CreateParams{get{var value=base.CreateParams;value.ExStyle|=0x80000;return value;}}}
    static void Version110Checks()
    {
        var store=TestStore();
        Assert(!new DisplayPreferences().Compact(new Rectangle(0,0,3840,2160),288)&&new DisplayPreferences().Compact(new Rectangle(0,0,2560,1440),288),"automatic layout uses available logical width rather than forcing narrow columns on a short wide screen");
        foreach(var area in new[]{new Rectangle(0,0,1920,1080),new Rectangle(0,0,2560,1440),new Rectangle(0,0,3840,2160)})
        foreach(int dpi in new[]{96,120,144,168,192,240,288})
        {
            var options=new DisplayPreferences();bool compact=options.Compact(area,dpi);
            var desired=WindowLayout.Pixels(compact?new Size(900,760):new Size(1080,900),dpi);
            Assert(area.Contains(WindowLayout.CenteredBounds(desired,area,dpi)),"automatic main-panel viewport fits "+area.Size+" at "+dpi+" DPI");
        }
        using(var panel=new MainForm(store,new Settings()))
        {
            bool preparedBeforeReveal=true;panel.VisibleChanged+=(s,e)=>{if(panel.Visible&&panel.Opacity>0)preparedBeforeReveal&=panel.PreparedFrames>0;};
            panel.Show();Application.DoEvents();
            Assert(preparedBeforeReveal&&panel.PreparedFrames==1&&panel.Opacity==1,"first main-panel show prepares the complete tree before revealing it");
            Assert((WindowStyle(panel.Handle,-20)&0x02000000)==0,"first-show buffering is removed before main-panel interaction");
            panel.Hide();panel.Show();Assert(panel.PreparedFrames==2,"reopening prepares the final layout before showing cached controls");
            Assert(!FindControls(panel).OfType<ComboBox>().Any()&&!FindControls(panel).OfType<RoundChoice>().Any(),"main panel exposes only a settings entry, without inline native display selectors");
            panel.SaveDisplayOptions(new DisplayPreferences{Layout="compact"});
            var grid=FindControls(panel).OfType<TableLayoutPanel>().Single(t=>t.Controls.OfType<GameTile>().Any());
            Assert(grid.ColumnCount==2&&grid.RowCount==4,"compact main panel reflows the eight cards into two columns");
            var loaded=DisplayPreferences.Load(store);Assert(loaded.Layout=="compact"&&loaded.Resolution=="auto","display preferences persist independently of account settings");
            panel.SaveDisplayOptions(new DisplayPreferences{Layout="wide"});Assert(grid.ColumnCount==4&&grid.RowCount==2,"wide main-panel layout restores four columns");
            var root=panel.Controls.OfType<FairyBackdrop>().Single();
            if(root.HorizontalScroll.Visible)root.AutoScrollPosition=new Point(root.HorizontalScroll.Maximum,root.VerticalScroll.Value);
            Assert(root.Controls.Cast<Control>().All(c=>c.Right<=root.ClientSize.Width-root.Padding.Right+1),"main-panel right edges remain reachable after layout changes, including a small CI desktop; client="+root.ClientSize+", widest="+root.Controls.Cast<Control>().Max(c=>c.Right));
            using(var screenshot=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(screenshot,new Rectangle(Point.Empty,panel.Size));screenshot.Save(Path.Combine(DisplayReviewDirectory(),"main-v110.png"));}
            using(var widget=new FairyWidget(store,panel))
            {
                widget.Say("offline menu regression");var bubble=WidgetDialogue(widget);Assert(bubble.Visible,"menu regression starts with a visible bubble");
                var menu=widget.ContextMenuStrip;var point=Screen.PrimaryScreen.WorkingArea.Location+new Size(40,40);
                ScrollMessage(bubble.Handle,0x7B,bubble.Handle,new IntPtr((point.X&0xffff)|((point.Y&0xffff)<<16)));
                Assert(menu.Visible&&menu.SourceControl==widget&&!bubble.Visible&&!PopupSurface.Attach(bubble).Background.Visible,"right-clicking the bubble opens a widget-owned menu and hides both bubble windows");
                var notice=SpeechBus.Publish(store,"offline notification during menu",true);widget.PollNotifications(true);widget.Say("deferred narration");
                Assert(menu.Visible&&!bubble.Visible&&SpeechBus.IsPending(store,notice,DateTimeOffset.UtcNow),"notifications cannot cover or dismiss an open context menu");
                menu.Close();widget.PollNotifications(true);Assert(bubble.Visible&&!SpeechBus.IsPending(store,notice,DateTimeOffset.UtcNow),"closing the context menu releases the pending notification");
                Assert(!FindControls(bubble).OfType<ComboBox>().Any(),"display preferences are never added to the bubble");
                bubble.HideNow();widget.Hide();
            }
            panel.AllowExit=true;
        }
        using(var renderer=new FairyEyeRenderer())
        using(var presentation=new LayeredFrame())
        using(var host=new LayeredTestHost{FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false})
        {
            for(int quality=2;quality<=3;quality++)
            {
                var bitmap=renderer.Render(new Size(250,250),1.2,PointF.Empty,false,1,quality);
                var cached=renderer.Render(new Size(250,250),1.3,PointF.Empty,false,1,quality);
                Assert(ReferenceEquals(bitmap,cached)&&bitmap.PixelFormat==PixelFormat.Format32bppPArgb,"eye rendering reuses a premultiplied frame at quality "+quality);
                var data=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);var pixels=new byte[bitmap.Width*bitmap.Height*4];
                try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{bitmap.UnlockBits(data);}
                bool premultiplied=true,transparent=false,covered=false;
                for(int i=0;i<pixels.Length;i+=4){int alpha=pixels[i+3];premultiplied&=pixels[i]<=alpha&&pixels[i+1]<=alpha&&pixels[i+2]<=alpha;transparent|=alpha==0;covered|=alpha>240;}
                Assert(premultiplied&&transparent&&covered,"eye pixels contain smooth alpha coverage without bright non-premultiplied fringes");
                presentation.Present(host.Handle,bitmap,new Point(-12000,-12000),255);int allocated=presentation.AllocationCount;
                presentation.Present(host.Handle,bitmap,new Point(-12010,-12010),200);
                Assert(presentation.AllocationCount==allocated,"eye presentation reuses its native bitmap and memory DC");
                var pointer=(IntPtr)typeof(LayeredFrame).GetField("bits",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(presentation);var native=new byte[pixels.Length];Marshal.Copy(pointer,native,0,native.Length);
                Assert(pixels.SequenceEqual(native),"the layered window receives the exact premultiplied pixels with no HBITMAP conversion");
                bitmap.Save(Path.Combine(DisplayReviewDirectory(),"eye-quality-"+quality+".png"));
                using(var preview=new Bitmap(bitmap.Width,bitmap.Height))
                {using(var g=Graphics.FromImage(preview)){g.Clear(Theme.Background);g.DrawImage(bitmap,new Rectangle(Point.Empty,bitmap.Size),0,0,bitmap.Width,bitmap.Height,GraphicsUnit.Pixel);}preview.Save(Path.Combine(DisplayReviewDirectory(),"eye-preview-"+quality+".png"));}
            }
        }
    }
    static void MainDisplayMatrixChecks()
    {
        int cases=0;
        foreach(var screen in new[]{new Size(2560,1440),new Size(3840,2160)})
        foreach(int dpi in new[]{96,120,144,168,192,240,288})
        foreach(string mode in new[]{"compact","wide"})
        {
            var store=TestStore();var options=new DisplayPreferences{Layout=mode};options.Save(store);
            using(var panel=new MainForm(store,new Settings()))
            {
                panel.Opacity=0;panel.Show();SetTestDpi(panel,dpi);
                var area=new Rectangle(Point.Empty,new Size(screen.Width,screen.Height-WindowLayout.Pixels(32,dpi)));
                var logical=mode=="compact"?new Size(900,760):new Size(1080,900);var bounds=WindowLayout.CenteredBounds(WindowLayout.Pixels(logical,dpi),area,dpi);
                // Match the virtual monitor's minimum-size cap, just as the
                // real DPI/display callback caps it to the actual work area.
                panel.MinimumSize=new Size(Math.Min(panel.MinimumSize.Width,bounds.Width),Math.Min(panel.MinimumSize.Height,bounds.Height));
                using(var limits=new VirtualRenderBounds(panel))LayerPosition(panel.Handle,IntPtr.Zero,-20000,-20000,bounds.Width,bounds.Height,0x214);
                panel.ApplyDisplayLayout(false);panel.PerformLayout();
                var cards=FindControls(panel).Where(c=>c is GameTile||c is HeyboxTile).ToArray();
                Assert(cards.Length==8&&cards.All(c=>c.Controls.Cast<Control>().All(child=>c.ClientRectangle.Contains(child.Bounds))),"main-panel switches fit their cards: "+screen+" / "+dpi+" DPI / "+mode);
                using(var image=new Bitmap(panel.Width,panel.Height))
                {
                    image.SetResolution(dpi,dpi);int previous=PopupSurface.RenderingDpi;PopupSurface.RenderingDpi=dpi;
                    try{panel.DrawToBitmap(image,new Rectangle(Point.Empty,panel.Size));}finally{PopupSurface.RenderingDpi=previous;}
                    Assert(area.Contains(bounds)&&image.Width==bounds.Width&&image.Height==bounds.Height,"complete main-panel viewport fits its virtual work area: "+screen+" / "+dpi+" / "+mode+" / expected="+bounds.Size+", actual="+image.Size);
                    if(dpi==192||dpi==288)image.Save(Path.Combine(DisplayReviewDirectory(),screen.Width+"x"+screen.Height+"-"+dpi+"-main-"+mode+".png"));
                }
                var root=panel.Controls.OfType<FairyBackdrop>().Single();
                Assert(root.AutoScroll,"virtual main-panel layout retains automatic scrolling at "+dpi+" DPI / "+mode);
                foreach(var button in FindControls(panel).OfType<RoundButton>())
                {
                    root.ScrollControlIntoView(button);var exposed=ExposedArea(button);
                    Assert(exposed.Width>=button.Width-2&&exposed.Height>=Math.Min(button.Height,WindowLayout.Pixels(30,dpi)),"main-panel action remains reachable: "+button.Text+" / "+dpi+" / "+mode+"; button="+button.Bounds+", parent="+button.Parent.ClientRectangle+", exposed="+exposed.Size);
                }
                panel.AllowExit=true;panel.Close();cases++;
            }
        }
        Console.WriteLine("Main display matrix: "+cases+" real control trees rendered at 2K/4K, 100-300%, compact and wide.");
    }
}
