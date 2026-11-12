using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    [DllImport("user32.dll",EntryPoint="SendMessage")]static extern IntPtr ScrollMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
    static void ScrollbarChecks()
    {
        using(var host=new Form{ClientSize=new Size(520,420),Opacity=0,ShowInTaskbar=false})
        using(var page=new Panel{Location=new Point(10,10),Size=new Size(460,190),AutoScroll=true,AutoScrollMinSize=new Size(700,1000),BackColor=Theme.Background})
        using(var log=new RichTextBox{Location=new Point(10,220),Size=new Size(460,170),ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Theme.Surface,ForeColor=Theme.Muted})
        {
            host.Controls.Add(page);host.Controls.Add(log);ScrollbarTheme.Attach(page);ScrollbarTheme.Attach(page);ScrollbarTheme.Attach(log);
            log.Text=string.Join(Environment.NewLine,Enumerable.Range(1,100).Select(i=>"Fairy 离线滚动检查 · "+i));
            host.Show();Application.DoEvents();ScrollbarTheme.ScrollbarInfo first,last;
            Assert(ScrollbarTheme.TryGetBar(page.Handle,true,out first)&&ScrollbarTheme.TryGetBar(page.Handle,false,out last),"overflowing page exposes both native scrolling axes");
            AssertDarkScrollbars(page,"page");AssertDarkScrollbars(log,"log");
            ScrollMessage(page.Handle,0x115,new IntPtr(7),IntPtr.Zero);Application.DoEvents();
            Assert(page.AutoScrollPosition.Y<0&&ScrollbarTheme.TryGetBar(page.Handle,true,out last)&&last.ThumbStart>first.ThumbStart,"themed page preserves native bottom scrolling and thumb position");
            ScrollMessage(page.Handle,0x114,new IntPtr(7),IntPtr.Zero);Assert(page.AutoScrollPosition.X<0,"themed page preserves horizontal scrolling");
            ScrollMessage(log.Handle,0x115,new IntPtr(7),IntPtr.Zero);int bottom=ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32();
            ScrollMessage(log.Handle,0x115,new IntPtr(6),IntPtr.Zero);int top=ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32();
            Assert(bottom>top&&top==0,"themed log preserves native text scrolling without changing its contents");
            AssertDarkScrollbars(page,"scrolled page");AssertDarkScrollbars(log,"scrolled log");
            log.AppendText(Environment.NewLine+"Fairy 新增委托记录");log.SelectionStart=log.TextLength;log.ScrollToCaret();
            Assert(ScrollMessage(log.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32()>0,"new log output still follows the latest line");AssertDarkScrollbars(log,"appended log");
            log.BorderStyle=BorderStyle.FixedSingle;log.BorderStyle=BorderStyle.None;Application.DoEvents();
            AssertDarkScrollbars(log,"recreated log handle");
            page.AutoScrollMinSize=Size.Empty;page.Size=new Size(460,190);page.PerformLayout();Application.DoEvents();
            Assert(!ScrollbarTheme.TryGetBar(page.Handle,true,out first)&&!ScrollbarTheme.TryGetBar(page.Handle,false,out last),"scrollbars disappear normally when page content fits");
            host.Close();
        }
    }
    static void AssertDarkScrollbars(Control control,string label)
    {
        using(var bitmap=new Bitmap(control.Width,control.Height))
        {
            control.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));ScrollbarTheme.ScrollbarInfo info;
            foreach(bool vertical in new[]{true,false})
            {
                if(!ScrollbarTheme.TryGetBar(control.Handle,vertical,out info))continue;
                var windowOrigin=control.PointToScreen(Point.Empty);var clientOffset=new Point((control.Width-control.ClientSize.Width)-(vertical?info.Bounds.Width:0),0);
                var bounds=info.Bounds.Rectangle;bounds.Offset(-windowOrigin.X+clientOffset.X,-windowOrigin.Y);bounds.Inflate(1,1);
                bounds.Intersect(new Rectangle(Point.Empty,bitmap.Size));int white=0,blue=0;
                for(int y=bounds.Top;y<bounds.Bottom;y++)for(int x=bounds.Left;x<bounds.Right;x++)
                {var c=bitmap.GetPixel(x,y);if(c.R>220&&c.G>220&&c.B>220)white++;if(c.B>c.R+12)blue++;}
                Assert(bounds.Width>0&&bounds.Height>0&&white==0&&blue>bounds.Width*bounds.Height/2,label+" native "+(vertical?"vertical":"horizontal")+" scrollbar paints navy and blue without white strips");
            }
        }
    }
}
