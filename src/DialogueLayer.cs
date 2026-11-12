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
        sealed class LayerState {internal IntPtr RaisedOver;}
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form,LayerState> states=new System.Runtime.CompilerServices.ConditionalWeakTable<Form,LayerState>();
        internal static void Synchronize(Form bubble,IntPtr foreground,bool raise)
        {
            if(bubble.IsDisposed||!bubble.Visible||!bubble.IsHandleCreated)return;
            const uint flags=0x213;
            uint process;bool own=foreground!=IntPtr.Zero&&GetWindowThreadProcessId(foreground,out process)!=0&&process==GetCurrentProcessId();
            var state=states.GetOrCreateValue(bubble);
            // An intentional click must work even when the widget did not take
            // focus. Keep that layer until the user selects another window.
            if(raise)state.RaisedOver=foreground;
            bool selected=own||raise||(state.RaisedOver!=IntPtr.Zero&&state.RaisedOver==foreground);
            if(!selected)
            {
                state.RaisedOver=IntPtr.Zero;
                if(bubble.TopMost||(GetWindowLong(bubble.Handle,-20)&8)!=0)
                {
                    bubble.TopMost=false;SetWindowPos(bubble.Handle,new IntPtr(-2),0,0,0,0,flags);
                    if(foreground!=IntPtr.Zero&&(GetWindowLong(foreground,-20)&8)==0)
                        SetWindowPos(bubble.Handle,foreground,0,0,0,0,flags);
                }
                return;
            }
            // Interactive pages take priority over narration, including pages
            // opened after the bubble. Never repeatedly raise over a dialog.
            IntPtr page=IntPtr.Zero;
            foreach(Form candidate in Application.OpenForms)
            {
                if(candidate==bubble||candidate is FairyWidget||PopupSurface.IsBackground(candidate)||candidate.IsDisposed||!candidate.IsHandleCreated||!candidate.Visible||candidate.WindowState==FormWindowState.Minimized||!candidate.Bounds.IntersectsWith(bubble.Bounds))continue;
                if(page==IntPtr.Zero||Above(candidate.Handle,page))page=candidate.Handle;
            }
            if(foreground!=bubble.Handle)
            {
                var name=new StringBuilder(40);GetClassName(foreground,name,name.Capacity);NativeRect bounds;
                if(own&&name.ToString()=="#32770"&&GetWindowRect(foreground,out bounds)&&Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom).IntersectsWith(bubble.Bounds))page=foreground;
            }
            if(page!=IntPtr.Zero)
            {
                bool pinned=(GetWindowLong(page,-20)&8)!=0;
                if(bubble.TopMost!=pinned)bubble.TopMost=pinned;
                SetWindowPos(bubble.Handle,page,0,0,0,0,flags);
            }
            else
            {
                bubble.TopMost=true;
                SetWindowPos(bubble.Handle,new IntPtr(-1),0,0,0,0,flags);
            }
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
