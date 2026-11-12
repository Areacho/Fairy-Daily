using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
    static Rectangle FrameArea(PopupSurface surface,Control control)
    {var area=control.RectangleToScreen(control.ClientRectangle);area.Offset(-surface.VisibleBounds().Left,-surface.VisibleBounds().Top);return area;}
    static void PopupSurfaceChecks()
    {
        string review=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","build","surface-review"));Directory.CreateDirectory(review);
        Form frameWindow;
        using(var panel=new MainForm(TestStore(),new Settings()))
        using(var page=new FairyModuleForm(panel,FairySection.Login))
        {
            page.TopMost=true;page.Show();Application.DoEvents();
            var surface=PopupSurface.Attach(page);surface.RenderNow();frameWindow=surface.Background;
            Assert(frameWindow.Owner==page&&AboveWindow(frameWindow.Handle,page.Handle),"one owned frame follows the popup native window group");
            Assert(page.TransparencyKey.IsEmpty&&(WindowStyle(frameWindow.Handle,-20)&0x20)!=0,"the visual frame passes input to a solid native control tree without colour-key holes");
            LayerPosition(page.Handle,new IntPtr(-1),0,0,0,0,0x213);surface.RenderNow();
            var blankHit=WindowFromPoint(page.PointToScreen(new Point(2,2)));
            Assert(GetAncestor(blankHit,2)==page.Handle,"blank popup areas hit the native popup instead of another application");
            foreach(var section in new[]{FairySection.Login,FairySection.Checkin,FairySection.MiyouTasks,FairySection.HeyboxTasks,FairySection.Schedule,FairySection.Checkin})
            {
                page.Navigate(section,false);Application.DoEvents();surface.RenderNow();
                Assert(FindControls(page).All(c=>(WindowStyle(c.Handle,-20)&0x80000)==0),"controls remain native instead of independently layered after navigation: "+section);
                using(var frame=surface.ComposeFrame())
                {
                    Assert(frame.Size==surface.VisibleBounds().Size,"complete frame matches current popup dimensions: "+section);
                    var origin=page.PointToScreen(new Point(2,2));origin.Offset(-surface.VisibleBounds().Left,-surface.VisibleBounds().Top);
                    Assert(frame.GetPixel(origin.X,origin.Y).A==204,"blank artwork retains 80 percent opacity: "+section);
                    var heading=FindControls(page).OfType<SmoothLabel>().OrderByDescending(c=>c.Font.SizeInPoints).First();
                    var textArea=FrameArea(surface,heading);textArea.Intersect(new Rectangle(Point.Empty,frame.Size));bool opaqueText=false;
                    for(int y=textArea.Top;y<textArea.Bottom&&!opaqueText;y++)for(int x=textArea.Left;x<textArea.Right;x++)
                    {var pixel=frame.GetPixel(x,y);if(pixel.A==255&&pixel.R>140&&pixel.G>140&&pixel.B>140){opaqueText=true;break;}}
                    Assert(opaqueText,"standalone heading glyphs remain fully opaque: "+section);
                    foreach(var component in FindControls(page).Where(c=>c.Visible&&(c is RoundButton||c is GameTile||c is HeyboxTile||c is DigitalTimePicker)))
                    {
                        var area=FrameArea(surface,component);area.Intersect(new Rectangle(Point.Empty,frame.Size));if(area.IsEmpty)continue;
                        int x=area.Left+area.Width/2,y=area.Top+area.Height/2;
                        Assert(frame.GetPixel(x,y).A>=249,"component pixels survive complete-frame composition at 90 percent over the background: "+component.GetType().Name);
                    }
                    frame.Save(Path.Combine(review,section+".png"));
                }
                Assert(page.OwnedForms.Length==1,"page replacements reuse a single visual frame: "+section);
            }
            page.Location=new Point(page.Left+20,page.Top+10);surface.RenderNow();
            Assert(frameWindow.Bounds==surface.VisibleBounds(),"the full frame follows movement without leaving background windows behind");
            page.Hide();Assert(!frameWindow.Visible,"hiding a popup also hides its complete visual frame");
            using(var bubble=new FairyDialogue(panel,()=>{}))
            {
                var bubbleSurface=PopupSurface.Attach(bubble);bubble.OpenShortcuts(false);bubble.ShowFor(page);Application.DoEvents();bubbleSurface.Opacity=Theme.PopupOpacity;
                foreach(int height in new[]{116,164,58,126,116})
                {
                    bubble.ClientSize=new Size(WindowLayout.Pixels(480,bubble.DeviceDpi),WindowLayout.Pixels(height,bubble.DeviceDpi));bubbleSurface.RenderNow();
                    Assert(bubbleSurface.Background.Bounds==bubble.Bounds,"animated bubble replaces the whole surface at height "+height);
                }
                using(var frame=bubbleSurface.ComposeFrame()){Assert(frame.GetPixel(0,0).A==0,"rounded bubble corners are transparent without leftover resize frames");frame.Save(Path.Combine(review,"shortcuts.png"));}
                bubble.HideNow();Assert(!bubbleSurface.Background.Visible,"dismissing a bubble hides its owned frame immediately");
            }
            panel.AllowExit=true;
        }
        Assert(frameWindow.IsDisposed,"disposing a popup also disposes its visual frame");
    }
}
