using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

namespace MiyouDaily
{
    // Mouse-wheel messages may still target a previously focused editor. Route
    // them once by the pointer position within this page, preserving editor
    // scrolling, native wheel settings and the focused clock's spin action.
    internal sealed class PageWheelRouter : IMessageFilter,IDisposable
    {
        readonly Form form;readonly ScrollableControl page;bool disposed;Control wheelTarget;int remainder;
        internal PageWheelRouter(Form form,ScrollableControl page)
        {this.form=form;this.page=page;Application.AddMessageFilter(this);form.Disposed+=FormDisposed;}
        void FormDisposed(object sender,EventArgs e){Dispose();}
        public void Dispose(){if(disposed)return;disposed=true;Application.RemoveMessageFilter(this);form.Disposed-=FormDisposed;}
        public bool PreFilterMessage(ref Message message)
        {
            if(disposed||message.Msg!=0x20A||!form.Visible||!form.Enabled||!page.AutoScroll||DialogueLayer.MenuActive)return false;
            using(var dpi=new WheelDpiScope(page.Handle))return Route(ref message);
        }
        bool Route(ref Message message)
        {
            var receiver=Control.FromChildHandle(message.HWnd);if(receiver==null||receiver.FindForm()!=form)return false;
            if((message.WParam.ToInt64()&8)!=0)return false; // retain Ctrl+wheel editor zoom
            long packed=message.LParam.ToInt64();var point=new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff));
            if(!page.RectangleToScreen(page.ClientRectangle).Contains(point))return false;
            Control hover=Deepest(form,point);var overlay=hover as ScrollbarTheme.ScrollbarOverlay;if(overlay!=null)hover=overlay.Target;
            for(Control control=hover;control!=null&&control!=page;control=control.Parent)
            {
                if(control is DigitalTimePicker&&control.Focused)return false;
                if(control is TextBoxBase&&CanScroll(control,message.WParam))return Scroll(control,message.WParam);
            }
            // Consume at the page edge as well: the original focused editor
            // must not start scrolling just because this page reached its end.
            if(CanScroll(page,message.WParam))Scroll(page,message.WParam);return true;
        }
        static Control Deepest(Control parent,Point screen)
        {
            while(true)
            {
                var point=parent.PointToClient(screen);Control next=null;
                foreach(Control child in parent.Controls)if(child.Visible&&child.Enabled&&child.Bounds.Contains(point)){next=child;break;}
                if(next==null)return parent;parent=next;
            }
        }
        static bool CanScroll(Control control,IntPtr wparam)
        {
            if(!control.IsHandleCreated)return false;var info=new ScrollInfo{Size=(uint)Marshal.SizeOf(typeof(ScrollInfo)),Mask=0x17};
            if(!GetScrollInfo(control.Handle,1,ref info))return false;int delta=(short)((wparam.ToInt64()>>16)&0xffff),maximum=Math.Max(info.Minimum,info.Maximum-(int)info.Page+1);
            return delta>0?info.Position>info.Minimum:delta<0&&info.Position<maximum;
        }
        bool Scroll(Control target,IntPtr wparam)
        {
            int delta=(short)((wparam.ToInt64()>>16)&0xffff);if(wheelTarget!=target){wheelTarget=target;remainder=0;}remainder+=delta;
            int steps=remainder/SystemInformation.MouseWheelScrollDelta;remainder%=SystemInformation.MouseWheelScrollDelta;if(steps==0)return true;
            int lines=SystemInformation.MouseWheelScrollLines;if(lines==0)return true;
            var scroll=target as ScrollableControl;
            if(scroll!=null)
            {
                int amount=lines<0?Math.Max(1,scroll.ClientSize.Height-WindowLayout.Pixels(24,scroll.DeviceDpi)):WindowLayout.Pixels(18,scroll.DeviceDpi)*lines;
                var info=new ScrollInfo{Size=(uint)Marshal.SizeOf(typeof(ScrollInfo)),Mask=0x17};GetScrollInfo(target.Handle,1,ref info);int maximum=Math.Max(info.Minimum,info.Maximum-(int)info.Page+1);
                int position=(int)Math.Max(info.Minimum,Math.Min((long)maximum,(long)info.Position-(long)steps*amount));scroll.AutoScrollPosition=new Point(-scroll.AutoScrollPosition.X,position);
            }
            else if(lines<0){for(int i=0;i<Math.Abs(steps);i++)SendMessage(target.Handle,0x115,new IntPtr(steps>0?2:3),IntPtr.Zero);}
            else SendMessage(target.Handle,0xB6,IntPtr.Zero,new IntPtr(-steps*lines));
            return true;
        }
        [StructLayout(LayoutKind.Sequential)]struct ScrollInfo{public uint Size,Mask;public int Minimum,Maximum;public uint Page;public int Position,Track;}
        struct WheelDpiScope : IDisposable
        {
            readonly IntPtr previous;
            internal WheelDpiScope(IntPtr window){previous=SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(window));}
            public void Dispose(){if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
        }
        [DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")]static extern bool GetScrollInfo(IntPtr window,int bar,ref ScrollInfo info);
        [DllImport("user32.dll",EntryPoint="SendMessageW")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
    }
}
