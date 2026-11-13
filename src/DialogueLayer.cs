using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MiyouDaily
{
    internal static class DialogueLayer
    {
        static readonly HashSet<ToolStripDropDown> menus=new HashSet<ToolStripDropDown>();
        static bool yielding;
        internal static bool MenuActive
        {
            get{if(menus.Count>0)return true;var info=new GuiThreadInfo{Size=(uint)Marshal.SizeOf(typeof(GuiThreadInfo))};return GetGUIThreadInfo(GetCurrentThreadId(),ref info)&&(info.Flags&0x1c)!=0;}
        }
        internal static bool NotificationsPaused
        {get{if(MenuActive)return true;foreach(Form form in Application.OpenForms)if(form.Modal&&form.Visible&&!form.IsDisposed)return true;return false;}}
        internal static void MenuOpened(ToolStripDropDown menu)
        {
            menus.Add(menu);
            foreach(var bubble in Application.OpenForms.OfType<FairyDialogue>().ToArray())
                if(bubble.Visible&&!bubble.IsDisposed){MoveBelow(bubble,menu.Handle);PopupSurface.Attach(bubble).Sync();}
        }
        internal static void MenuClosed(ToolStripDropDown menu){menus.Remove(menu);}
        internal static void PrepareInteraction(Form page)
        {
            if(yielding||WindowInteraction.IsMoving||page.IsDisposed||page.Disposing||!page.IsHandleCreated||!page.Visible||page is FairyWidget||page is FairyDialogue||PopupSurface.IsBackground(page))return;
            yielding=true;
            try{foreach(var bubble in Application.OpenForms.OfType<FairyDialogue>().ToArray())if(bubble.Visible&&!bubble.IsDisposed&&bubble.Bounds.IntersectsWith(page.Bounds)){Synchronize(bubble,page.Handle,false);PopupSurface.Attach(bubble).Sync();}}
            finally{yielding=false;}
        }
        internal static void PrepareNativeInteraction(IntPtr page)
        {
            uint process;NativeRect bounds;
            if(yielding||WindowInteraction.IsMoving||page==IntPtr.Zero||GetWindowThreadProcessId(page,out process)==0||process!=GetCurrentProcessId()||!GetWindowRect(page,out bounds))return;
            yielding=true;var area=Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom);
            try{foreach(var bubble in Application.OpenForms.OfType<FairyDialogue>().ToArray())if(bubble.Visible&&!bubble.IsDisposed&&bubble.Bounds.IntersectsWith(area)){Synchronize(bubble,page,false);PopupSurface.Attach(bubble).Sync();}}
            finally{yielding=false;}
        }
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
            if(bubble.IsDisposed||!bubble.Visible||!bubble.IsHandleCreated||MenuActive)return;
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
                MoveBelow(bubble,page);
            }
            else
            {
                bubble.TopMost=true;
                SetWindowPos(bubble.Handle,new IntPtr(-1),0,0,0,0,flags);
            }
        }
        static void MoveBelow(Form bubble,IntPtr page)
        {
            bool pinned=(GetWindowLong(page,-20)&8)!=0,changed=bubble.TopMost!=pinned;
            if(changed)bubble.TopMost=pinned;
            if(changed||Above(bubble.Handle,page))SetWindowPos(bubble.Handle,page,0,0,0,0,0x213);
        }
        static bool Above(IntPtr upper,IntPtr lower)
        {
            IntPtr window=GetWindow(lower,3);
            for(int count=0;window!=IntPtr.Zero&&count<512;count++,window=GetWindow(window,3))if(window==upper)return true;
            return false;
        }
        [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]struct GuiThreadInfo{public uint Size,Flags;public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;public NativeRect CaretBounds;}
        [DllImport("user32.dll")]static extern bool GetGUIThreadInfo(uint thread,ref GuiThreadInfo info);
        [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
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
