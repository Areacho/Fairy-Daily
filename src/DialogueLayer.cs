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
        {Synchronize(bubble,GetForegroundWindow(),true);}
        internal static void Maintain(Form bubble)
        {Synchronize(bubble,GetForegroundWindow(),false);}
        // Explicit foreground also permits native layering regression checks
        // without activating test windows on the user's desktop.
        internal static void Synchronize(Form bubble,IntPtr foreground,bool raise)
        {
            if(bubble.IsDisposed||!bubble.Visible||!bubble.IsHandleCreated)return;
            const uint flags=0x1|0x2|0x10|0x200;
            uint process;bool own=foreground!=IntPtr.Zero&&GetWindowThreadProcessId(foreground,out process)!=0&&process==GetCurrentProcessId();
            bool pinned=(GetWindowLong(bubble.Handle,-20)&8)!=0;
            if(!own)
            {
                if(bubble.TopMost||pinned)
                {
                    bubble.TopMost=false;
                    SetWindowPos(bubble.Handle,new IntPtr(-2),0,0,0,0,flags);
                    // Demoting alone places a window at the top of the normal
                    // band. Put it behind the app just selected by the user.
                    if(foreground!=IntPtr.Zero&&(GetWindowLong(foreground,-20)&8)==0)
                        SetWindowPos(bubble.Handle,foreground,0,0,0,0,flags);
                }
                else if(raise)SetWindowPos(bubble.Handle,IntPtr.Zero,0,0,0,0,flags);
                return;
            }
            bubble.TopMost=true;
            if(raise||!pinned){SetWindowPos(bubble.Handle,new IntPtr(-1),0,0,0,0,flags);return;}
            foreach(Form page in Application.OpenForms)
            {
                if(page!=bubble&&!page.IsDisposed&&page.IsHandleCreated&&page.Visible&&page.WindowState!=FormWindowState.Minimized&&page.Bounds.IntersectsWith(bubble.Bounds)&&Above(page.Handle,bubble.Handle))
                {SetWindowPos(bubble.Handle,new IntPtr(-1),0,0,0,0,flags);return;}
            }
            // Common file pickers and message boxes are native modal windows,
            // so they do not appear in Application.OpenForms.
            if(foreground==bubble.Handle)return;
            var name=new StringBuilder(40);GetClassName(foreground,name,name.Capacity);NativeRect bounds;
            if(name.ToString()=="#32770"&&GetWindowRect(foreground,out bounds)&&Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom).IntersectsWith(bubble.Bounds)&&Above(foreground,bubble.Handle))SetWindowPos(bubble.Handle,new IntPtr(-1),0,0,0,0,flags);
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
