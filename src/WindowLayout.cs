using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MiyouDaily
{
    public static class WindowLayout
    {
        public static int Pixels(int logical,int dpi)
        {return (int)Math.Round(logical*Math.Max(96,dpi)/96.0,MidpointRounding.AwayFromZero);}
        public static Size Pixels(Size logical,int dpi)
        {return new Size(Pixels(logical.Width,dpi),Pixels(logical.Height,dpi));}
        public static Rectangle FitBounds(Rectangle proposed,Rectangle workingArea,int dpi)
        {
            int gap=Math.Min(Pixels(12,dpi),Math.Max(0,Math.Min(workingArea.Width,workingArea.Height)/8));
            var available=Rectangle.Inflate(workingArea,-gap,-gap);
            int width=Math.Max(1,Math.Min(proposed.Width,available.Width)),height=Math.Max(1,Math.Min(proposed.Height,available.Height));
            int left=Math.Max(available.Left,Math.Min(proposed.Left,available.Right-width)),top=Math.Max(available.Top,Math.Min(proposed.Top,available.Bottom-height));
            return new Rectangle(left,top,width,height);
        }
        internal static Point PlaceNotice(Rectangle proposed,Rectangle anchor,Rectangle area,Point cursor,int dpi)
        {
            int gap=Pixels(12,dpi);
            var points=new[]{proposed.Location,new Point(anchor.Right+gap,proposed.Top),new Point(proposed.Left,anchor.Top-proposed.Height-gap),new Point(proposed.Left,anchor.Bottom+gap),new Point(area.Left+gap,area.Top+gap),new Point(area.Right-proposed.Width-gap,area.Top+gap),new Point(area.Left+gap,area.Bottom-proposed.Height-gap),new Point(area.Right-proposed.Width-gap,area.Bottom-proposed.Height-gap)};
            foreach(var point in points)
            {
                var candidate=FitBounds(new Rectangle(point,proposed.Size),area,dpi);
                if(!Rectangle.Inflate(candidate,gap,gap).Contains(cursor))return candidate.Location;
            }
            return FitBounds(proposed,area,dpi).Location;
        }
        public static void Fit(Form form,Size minimumLogical)
        {
            if(form.IsDisposed||!form.IsHandleCreated)return;
            var area=Screen.FromControl(form).WorkingArea;var maximum=FitBounds(area,area,form.DeviceDpi).Size;
            var minimum=Pixels(minimumLogical,form.DeviceDpi);
            form.MinimumSize=new Size(Math.Min(minimum.Width,maximum.Width),Math.Min(minimum.Height,maximum.Height));
            if(form.WindowState==FormWindowState.Normal)form.Bounds=FitBounds(form.Bounds,area,form.DeviceDpi);
        }
        public static void Watch(Form form,Func<Size> minimumLogical)
        {
            Action fit=()=>{if(!form.IsDisposed){Fit(form,minimumLogical());form.PerformLayout();form.Invalidate(true);}};
            Action queue=()=>{if(form.IsDisposed||!form.IsHandleCreated)return;try{form.BeginInvoke(fit);}catch(InvalidOperationException){}};
            form.Shown+=(s,e)=>fit();form.DpiChanged+=(s,e)=>queue();
            EventHandler changed=(s,e)=>queue();SystemEvents.DisplaySettingsChanged+=changed;
            form.Disposed+=(s,e)=>SystemEvents.DisplaySettingsChanged-=changed;
        }
    }
}
