using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    // Reveal an opaque window only after its final DPI layout and native child
    // tree have painted. The temporary bitmap and composition style are released
    // before interaction; ordinary moves and paints keep using control caches.
    public class PreparedForm : Form
    {
        bool preparingFrame;
        internal int PreparedFrames{get;private set;}
        protected virtual void PrepareVisibleFrame(){PerformLayout();}
        protected override void SetVisibleCore(bool value)
        {
            if(!value||Visible||preparingFrame){base.SetVisibleCore(value);return;}
            if(Opacity==0){base.SetVisibleCore(true);PrepareVisibleFrame();return;}
            preparingFrame=true;double opacity=Opacity;Opacity=0;IntPtr window=Handle;bool hadComposition=(WindowStyle(window,-20)&0x02000000)!=0;
            try
            {
                SetWindowStyle(window,-20,WindowStyle(window,-20)|0x02000000);
                base.SetVisibleCore(true);PrepareVisibleFrame();
                using(var frame=new Bitmap(Math.Max(1,Width),Math.Max(1,Height)))
                {
                    frame.SetResolution(DeviceDpi,DeviceDpi);int previous=PopupSurface.RenderingDpi;PopupSurface.RenderingDpi=DeviceDpi;
                    try{DrawToBitmap(frame,new Rectangle(Point.Empty,Size));}finally{PopupSurface.RenderingDpi=previous;}
                }
                PreparedFrames++;Refresh();Opacity=opacity;Refresh();
            }
            finally
            {
                if(!IsDisposed){Opacity=opacity;if(!hadComposition)SetWindowStyle(Handle,-20,WindowStyle(Handle,-20)&~0x02000000);}
                preparingFrame=false;
            }
        }
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int WindowStyle(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetWindowStyle(IntPtr window,int index,int value);
    }
}
