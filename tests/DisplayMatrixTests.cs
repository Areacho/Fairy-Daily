using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    // Windows CI desktops may be smaller than the virtual work area. Bypass
    // only the offscreen test window's native tracking limit, not OS settings.
    sealed class VirtualRenderBounds : NativeWindow,IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]struct Limits{public Point Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
        public VirtualRenderBounds(Form form){AssignHandle(form.Handle);}
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if(message.Msg!=0x24)return;
            var limits=(Limits)Marshal.PtrToStructure(message.LParam,typeof(Limits));
            limits.MaxSize=limits.MaxTrackSize=new Point(32767,32767);Marshal.StructureToPtr(limits,message.LParam,false);
        }
        public void Dispose(){ReleaseHandle();}
    }
    static string DisplayReviewDirectory()
    {
        var directory=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while(directory!=null&&!File.Exists(Path.Combine(directory.FullName,"tools","build.ps1")))directory=directory.Parent;
        string root=directory==null?AppDomain.CurrentDomain.BaseDirectory:directory.FullName;
        string output=Path.Combine(root,"build","display-review");Directory.CreateDirectory(output);return output;
    }
    static Rectangle ExposedArea(Control control)
    {
        var area=control.RectangleToScreen(control.ClientRectangle);
        for(Control parent=control.Parent;parent!=null;parent=parent.Parent)area.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
        return area;
    }
    static bool ContainsForeground(Bitmap frame,Rectangle area,Point origin)
    {
        area.Offset(-origin.X,-origin.Y);area.Intersect(new Rectangle(Point.Empty,frame.Size));
        for(int y=area.Top;y<area.Bottom;y+=2)for(int x=area.Left;x<area.Right;x+=2)if(frame.GetPixel(x,y).A>=249)return true;
        return false;
    }
    static int TextInkHeight(Bitmap frame,Rectangle area,Point origin)
    {
        area.Offset(-origin.X,-origin.Y);area.Intersect(new Rectangle(Point.Empty,frame.Size));int top=area.Bottom,bottom=area.Top;
        for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)
        {var pixel=frame.GetPixel(x,y);if(pixel.A==255&&pixel.R>120&&pixel.G>120&&pixel.B>120){top=Math.Min(top,y);bottom=Math.Max(bottom,y);break;}}
        return Math.Max(0,bottom-top+1);
    }
    static bool HasBrightEye(Bitmap frame,Rectangle area,Point origin)
    {
        area.Offset(-origin.X,-origin.Y);area.Intersect(new Rectangle(Point.Empty,frame.Size));
        for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)
        {var p=frame.GetPixel(x,y);if(p.A>=249&&p.R>170&&p.G>170&&p.B>170)return true;}
        return false;
    }
    static void DisplayMatrixChecks()
    {
        string output=DisplayReviewDirectory();var report=new List<object>();
        var scenes=new[]{FairySection.Checkin,FairySection.MiyouTasks,FairySection.HeyboxTasks,FairySection.Schedule};
        foreach(var screen in new[]{new Size(2560,1440),new Size(3840,2160)})
        foreach(int dpi in new[]{96,120,144,168,192,240,288})
        {
            var area=new Rectangle(Point.Empty,new Size(screen.Width,screen.Height-WindowLayout.Pixels(40,dpi)));
            using(var panel=new MainForm(TestStore(),new Settings()))
            using(var module=new FairyModuleForm(panel,FairySection.Checkin))
            {
                SetTestDpi(module,dpi);
                foreach(var section in scenes)
                {
                    module.Navigate(section,false);SetChildDpi(module,dpi);
                    var desired=module.ClientSize;
                    var fitted=WindowLayout.CenteredBounds(desired,area,dpi);
                    var root=module.Controls[0];root.Font=module.Font;module.Controls.Remove(root);
                    // Keep Windows settings untouched. A fully hidden native
                    // carrier renders the actual control tree into each virtual
                    // work area, including sizes larger than the real monitor.
                    using(var carrier=new PassiveForm{FormBorderStyle=FormBorderStyle.None,AutoScaleMode=AutoScaleMode.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ClientSize=fitted.Size,Font=module.Font})
                    {
                        var surface=PopupSurface.Attach(carrier);carrier.Opacity=0;
                        carrier.Controls.Add(root);root.Dock=DockStyle.Fill;carrier.Show();
                        using(var limits=new VirtualRenderBounds(carrier))LayerPosition(carrier.Handle,IntPtr.Zero,-20000,-20000,fitted.Width,fitted.Height,0x214);
                        SetChildDpi(carrier,dpi);carrier.PerformLayout();root.PerformLayout();
                        Assert(carrier.ClientSize==fitted.Size&&area.Contains(fitted),"complete "+section+" frame fits "+screen+" at "+dpi+" DPI");
                        var tiles=FindControls(carrier).Where(c=>c is GameTile||c is HeyboxTile).ToArray();
                        Assert(tiles.All(tile=>tile.Controls.Cast<Control>().All(c=>tile.ClientRectangle.Contains(c.Bounds))),"platform switches stay inside their cards at "+dpi+" DPI on "+screen);
                        using(var frame=surface.ComposeFrame())
                        {
                            Assert(frame.Size==fitted.Size,"composition retains native target pixels at "+dpi+" DPI on "+screen);
                            var heading=FindControls(carrier).OfType<SmoothLabel>().OrderByDescending(c=>c.Font.SizeInPoints).First();
                            Assert(TextInkHeight(frame,ExposedArea(heading),carrier.Location)>=heading.Font.SizeInPoints*dpi/72*.65,"heading glyphs render at the target DPI instead of the desktop memory DC DPI: "+dpi);
                            foreach(var component in FindControls(carrier).Where(c=>c.Visible&&(c is RoundButton||c is GameTile||c is HeyboxTile||c is DigitalTimePicker)))
                            {
                                var exposed=ExposedArea(component);if(exposed.Width<12||exposed.Height<12)continue;
                                Assert(ContainsForeground(frame,exposed,carrier.Location),"visible "+component.GetType().Name+" is present in the composed "+section+" frame at "+dpi+" DPI");
                            }
                            if(dpi==96||dpi==144||dpi==192||dpi==288)frame.Save(Path.Combine(output,screen.Width+"x"+screen.Height+"-"+(dpi*100/96)+"-"+section+".png"));
                        }
                        foreach(var button in FindControls(carrier).OfType<RoundButton>().Where(c=>c.Visible))
                        {
                            var scroll=button.Parent;while(scroll!=null&&!(scroll is ScrollableControl&&((ScrollableControl)scroll).AutoScroll))scroll=scroll.Parent;
                            if(scroll!=null)((ScrollableControl)scroll).ScrollControlIntoView(button);
                            var exposed=ExposedArea(button);
                            Assert(exposed.Width>=button.Width-2&&exposed.Height>=Math.Min(button.Height,WindowLayout.Pixels(30,dpi)),"action remains reachable by scrolling: "+section+" / "+button.Text+" at "+dpi+" DPI on "+screen);
                        }
                        if(dpi>=240&&section==FairySection.MiyouTasks)using(var bottom=surface.ComposeFrame())bottom.Save(Path.Combine(output,screen.Width+"x"+screen.Height+"-"+(dpi*100/96)+"-MiyouTasks-scrolled.png"));
                        report.Add(new{Width=screen.Width,Height=screen.Height,Dpi=dpi,ScalePercent=dpi*100/96,Section=section.ToString(),FrameWidth=fitted.Width,FrameHeight=fitted.Height,Passed=true});
                        carrier.Controls.Remove(root);module.Controls.Add(root);carrier.Hide();
                    }
                }
                // Check the compact action row separately, including the small
                // configuration/timer buttons and the circular dashboard icon.
                using(var bubble=new FairyDialogue(panel,()=>{}))
                {
                    SetTestDpi(bubble,dpi);bubble.OpenShortcuts(false);
                    using(var carrier=new PassiveForm{Opacity=0,FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,Bounds=new Rectangle(-20000,-20000,1,1)})
                    {
                        carrier.Show();bubble.ShowFor(carrier);bubble.Opacity=0;SetChildDpi(bubble,dpi);
                        bubble.ClientSize=WindowLayout.Pixels(new Size(480,116),dpi);
                        var size=WindowLayout.Pixels(new Size(480,116),dpi);
                        using(var limits=new VirtualRenderBounds(bubble))LayerPosition(bubble.Handle,IntPtr.Zero,bubble.Left,bubble.Top,size.Width,size.Height,0x214);
                        PopupSurface.Attach(bubble).Opacity=Theme.PopupOpacity;
                        var icon=FindControls(bubble).OfType<FairyPanelButton>().Single();
                        var actions=bubble.Controls.OfType<TableLayoutPanel>().Single();
                        Assert(actions.Right+WindowLayout.Pixels(8,dpi)<=icon.Left,"shortcut buttons and eye remain separate at "+dpi+" DPI on "+screen);
                        using(var frame=PopupSurface.Attach(bubble).ComposeFrame())
                        {
                            Assert(frame.Size==bubble.Size,"shortcut frame keeps every pixel at "+dpi+" DPI");
                            Assert(HasBrightEye(frame,ExposedArea(icon),bubble.Location),"dashboard eye keeps its visible white iris at "+dpi+" DPI");
                            foreach(var button in FindControls(bubble).OfType<RoundButton>())
                                Assert(ContainsForeground(frame,ExposedArea(button),bubble.Location),"shortcut "+button.Text+" remains visible at "+dpi+" DPI");
                            if(dpi==144||dpi==288)frame.Save(Path.Combine(output,screen.Width+"x"+screen.Height+"-"+(dpi*100/96)+"-shortcuts.png"));
                        }
                        bubble.HideNow();carrier.Hide();
                    }
                }
                panel.AllowExit=true;
            }
            Application.DoEvents();
        }
        File.WriteAllText(Path.Combine(output,"report.json"),Json.Write(new{Method="Virtual 2K/4K work areas, simulated framework DPI and full per-pixel composition of real control trees; Windows display settings unchanged",Cases=report}));
        Console.WriteLine("Display review: "+report.Count+" page frames plus shortcut frames at 100–300%: "+output);
    }
}
