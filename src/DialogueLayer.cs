using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MiyouDaily
{
    internal static class DialogueLayer
    {
        internal static void Raise(Form bubble)
        {
            if(bubble.IsDisposed||!bubble.Visible||!bubble.IsHandleCreated)return;
            bubble.TopMost=true;
            // Reusing an already-visible, non-activating window does not lift
            // it automatically. Raise only the bubble, preserving input focus
            // and the owner's position in the native window stack.
            SetWindowPos(bubble.Handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10|0x200);
        }
        internal static void Maintain(Form bubble)
        {
            if(bubble.IsDisposed||!bubble.Visible||!bubble.IsHandleCreated)return;
            // Demoting a pinned owner can also demote its owned windows even
            // while their managed TopMost property is still true.
            if((GetWindowLong(bubble.Handle,-20)&8)==0){Raise(bubble);return;}
            foreach(Form page in Application.OpenForms)
            {
                if(page!=bubble&&!page.IsDisposed&&page.IsHandleCreated&&page.Visible&&page.WindowState!=FormWindowState.Minimized&&page.Bounds.IntersectsWith(bubble.Bounds)&&Above(page.Handle,bubble.Handle))
                {Raise(bubble);return;}
            }
            // Common file pickers and message boxes are native modal windows,
            // so they do not appear in Application.OpenForms.
            IntPtr foreground=GetForegroundWindow();uint process;
            if(foreground==IntPtr.Zero||foreground==bubble.Handle||GetWindowThreadProcessId(foreground,out process)==0||process!=GetCurrentProcessId())return;
            var name=new StringBuilder(40);GetClassName(foreground,name,name.Capacity);NativeRect bounds;
            if(name.ToString()=="#32770"&&GetWindowRect(foreground,out bounds)&&Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom).IntersectsWith(bubble.Bounds)&&Above(foreground,bubble.Handle))Raise(bubble);
        }
        static bool Above(IntPtr upper,IntPtr lower)
        {
            IntPtr window=GetWindow(lower,3);
            for(int count=0;window!=IntPtr.Zero&&count<512;count++,window=GetWindow(window,3))if(window==upper)return true;
            return false;
        }
        [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("kernel32.dll")]static extern uint GetCurrentProcessId();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder name,int maximum);
        [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out NativeRect bounds);
    }
}
