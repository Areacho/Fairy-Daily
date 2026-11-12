using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    internal sealed class WindowRedrawBatch : IDisposable
    {
        readonly Control control;readonly IntPtr handle;
        internal WindowRedrawBatch(Control control)
        {
            this.control=control;
            // Re-enabling redraw also sets WS_VISIBLE. Never apply this to a
            // hidden window, or finish an outer batch that already disabled it.
            if(control.IsHandleCreated&&IsWindowVisible(control.Handle))
            {handle=control.Handle;SendMessage(handle,0xB,IntPtr.Zero,IntPtr.Zero);}
        }
        public void Dispose()
        {
            if(handle==IntPtr.Zero||control.IsDisposed||!control.IsHandleCreated||control.Handle!=handle)return;
            SendMessage(handle,0xB,new IntPtr(1),IntPtr.Zero);
            RedrawWindow(handle,IntPtr.Zero,IntPtr.Zero,0x1|0x4|0x80|0x100|0x400);
        }
        [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
        [DllImport("user32.dll")]static extern bool RedrawWindow(IntPtr window,IntPtr update,IntPtr region,uint flags);
    }
}
