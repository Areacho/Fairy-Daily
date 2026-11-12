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
        internal static Point PlaceBesideWidget(Size bubble,Rectangle widget,Rectangle area,int dpi,ref bool? dockLeft)
        {
            int gap=Pixels(6,dpi),overlap=Pixels(22,dpi);
            int left=widget.Left-bubble.Width+overlap;
            int right=widget.Right-overlap;
            bool fitsLeft=left>=area.Left+gap,fitsRight=right+bubble.Width<=area.Right-gap;
            bool useLeft=dockLeft.GetValueOrDefault(true);
            if(!fitsLeft&&fitsRight)useLeft=false;else if(!fitsRight&&fitsLeft)useLeft=true;
            dockLeft=useLeft;left=useLeft?left:right;
            int height=Math.Max(bubble.Height,Pixels(164,dpi));
            return new Point(Math.Max(area.Left+gap,Math.Min(left,area.Right-bubble.Width-gap)),Math.Max(area.Top+gap,Math.Min(widget.Top+Pixels(30,dpi),area.Bottom-height-gap)));
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
