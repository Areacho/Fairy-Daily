using System;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    [DllImport("user32.dll",EntryPoint="PostMessageW")]static extern bool PostWheelMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
    static IntPtr WheelPoint(Point point){return new IntPtr((point.X&0xffff)|((point.Y&0xffff)<<16));}
    static void PostWheel(Control receiver,Point point,int delta)
    {PostWheelMessage(receiver.Handle,0x20A,new IntPtr(delta<<16),WheelPoint(point));Application.DoEvents();}
    static void Version113Checks()
    {
        using(var panel=new MainForm(TestStore(),new Settings()))
        {
            panel.Opacity=0;panel.Show();Application.DoEvents();var root=panel.Controls.OfType<FairyBackdrop>().Single();
            var log=FindControls(panel).OfType<RichTextBox>().Single();log.Text=string.Join(Environment.NewLine,Enumerable.Range(1,160).Select(i=>"离线滚轮回归 "+i));
            root.AutoScrollPosition=Point.Empty;
            var login=FindControls(panel).OfType<RoundButton>().Single(b=>b.AccessibleName=="米游社扫码登录");var point=login.PointToScreen(new Point(login.Width/2,login.Height/2));
            int firstLine=ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32();
            PostWheel(log,point,-120);
            Assert(root.AutoScrollPosition.Y<0&&ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32()==firstLine,"wheel over a page button scrolls the page even when Windows targets the log editor");
            root.AutoScrollPosition=Point.Empty;for(int i=0;i<4;i++)PostWheel(log,point,-30);
            Assert(root.AutoScrollPosition.Y<0,"small wheel deltas are retained while hovering page controls");
            for(int i=0;i<256&&-root.AutoScrollPosition.Y<root.VerticalScroll.Maximum-root.VerticalScroll.LargeChange;i++)PostWheel(log,point,-120);
            Assert(-root.AutoScrollPosition.Y>=root.VerticalScroll.Maximum-root.VerticalScroll.LargeChange,"wheel reaches the complete page extent instead of stalling before the footer");
            firstLine=ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32();PostWheel(log,point,-120);
            Assert(ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32()==firstLine,"a wheel tick at the page edge cannot escape to the previously focused log");
            root.ScrollControlIntoView(log);ScrollMessage(log.Handle,0x115,new IntPtr(6),IntPtr.Zero);var exposed=ExposedArea(log);point=new Point(exposed.Left+exposed.Width/2,exposed.Top+exposed.Height/2);
            var pageAt=root.AutoScrollPosition;PostWheel(login,point,-120);
            Assert(ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32()>0&&root.AutoScrollPosition==pageAt,"wheel over the log scrolls the log without moving the whole panel; log area="+exposed+", page="+pageAt+" -> "+root.AutoScrollPosition+", line="+ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero));
            ScrollMessage(log.Handle,0x115,new IntPtr(6),IntPtr.Zero);pageAt=root.AutoScrollPosition;PostWheel(login,point,120);
            Assert(root.AutoScrollPosition.Y>pageAt.Y,"wheel continues into the page when the hovered log reaches its upper edge");
            int layouts=0;panel.Layout+=(s,e)=>layouts++;var clock=Stopwatch.StartNew();
            for(int i=0;i<30;i++){root.AutoScrollPosition=new Point(0,(i&1)==0?root.VerticalScroll.Maximum:0);Application.DoEvents();}
            clock.Stop();Console.WriteLine("Main panel: 30 native scroll positions, "+clock.ElapsedMilliseconds+" ms.");
            Assert(layouts==0,"scrollbar decoration never relayouts the form or pulls its active control back into view");
            panel.AllowExit=true;panel.Close();
        }
        using(var host=new Form{Opacity=0,ShowInTaskbar=false,ClientSize=new Size(620,360)})
        using(var root=new FairyBackdrop{Dock=DockStyle.Fill,AutoScroll=true,AutoScrollMinSize=new Size(1000,1800)})
        {
            host.Controls.Add(root);host.Show();Application.DoEvents();
            using(var before=new Bitmap(root.ClientSize.Width,root.ClientSize.Height))
            using(var after=new Bitmap(root.ClientSize.Width,root.ClientSize.Height))
            {
                using(var g=Graphics.FromImage(before))root.PaintSurface(g);int rasterizations=root.BackgroundRasterizations;
                root.AutoScrollPosition=new Point(0,73);int shift=-root.AutoScrollPosition.Y;
                using(var g=Graphics.FromImage(after))root.PaintSurface(g);
                bool aligned=true;for(int y=0;y<after.Height-shift&&aligned;y+=3)for(int x=0;x<after.Width;x+=3)if(after.GetPixel(x,y)!=before.GetPixel(x,y+shift)){aligned=false;break;}
                Assert(shift==73&&aligned,"cached background pixels follow the same content displacement as native scrolling");
                using(var copied=new Bitmap(before.Width,before.Height))
                {
                    using(var g=Graphics.FromImage(copied)){g.DrawImage(before,new Rectangle(0,0,before.Width,before.Height-shift),0,shift,before.Width,before.Height-shift,GraphicsUnit.Pixel);g.SetClip(new Rectangle(0,before.Height-shift,before.Width,shift));root.PaintSurface(g);}
                    bool matches=true;for(int y=0;y<after.Height&&matches;y+=3)for(int x=0;x<after.Width;x+=3)if(copied.GetPixel(x,y)!=after.GetPixel(x,y)){matches=false;break;}
                    Assert(matches,"retained scroll pixels plus the newly painted strip match a complete fresh background");
                }
                Assert(root.BackgroundRasterizations==rasterizations,"scrolling reuses the viewport-sized background without rasterizing another image");
                root.AutoScrollPosition=new Point(47,146);
                using(var diagonal=new Bitmap(root.ClientSize.Width,root.ClientSize.Height))
                {
                    using(var g=Graphics.FromImage(diagonal))root.PaintSurface(g);
                    bool mirrored=true;for(int y=0;y<after.Height-73&&mirrored;y+=3)for(int x=0;x<after.Width-47;x+=3)if(diagonal.GetPixel(x,y)!=after.GetPixel(x+47,y+73)){mirrored=false;break;}
                    Assert(mirrored,"horizontal and vertical scroll copies remain consistent across reflected cache edges");
                }
            }
        }
    }
}
