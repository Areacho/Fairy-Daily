using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    // Preserve native scrolling state and keyboard/wheel navigation. Popup
    // surfaces repaint native bars; opaque panels can cover them with small
    // client controls whose paint is independent of Windows theme redraws.
    public sealed class ScrollbarTheme : NativeWindow,IDisposable
    {
        static readonly ConditionalWeakTable<Control,ScrollbarTheme> bindings=new ConditionalWeakTable<Control,ScrollbarTheme>();
        readonly Control owner;bool painting,disposed,useOverlay,synchronizing;
        ScrollbarOverlay verticalOverlay,horizontalOverlay;Form overlayHost;
        readonly List<Control> ancestors=new List<Control>();
        public static void Attach(Control control,bool clientOverlay=false)
        {var theme=bindings.GetValue(control,c=>new ScrollbarTheme(c));if(clientOverlay){theme.useOverlay=true;theme.TrackAncestors();theme.UpdateOverlays();}}
        ScrollbarTheme(Control control)
        {
            owner=control;owner.HandleCreated+=Created;owner.HandleDestroyed+=Destroyed;owner.Disposed+=OwnerDisposed;owner.ParentChanged+=ParentChanged;
            if(owner.IsHandleCreated)AssignHandle(owner.Handle);
        }
        void Created(object sender,EventArgs e){if(Handle!=IntPtr.Zero)ReleaseHandle();AssignHandle(owner.Handle);TrackAncestors();UpdateOverlays();}
        void Destroyed(object sender,EventArgs e){if(verticalOverlay!=null&&!verticalOverlay.IsDisposed)verticalOverlay.Hide();if(horizontalOverlay!=null&&!horizontalOverlay.IsDisposed)horizontalOverlay.Hide();if(Handle!=IntPtr.Zero)ReleaseHandle();}
        void ParentChanged(object sender,EventArgs e){TrackAncestors();UpdateOverlays();}
        void OwnerDisposed(object sender,EventArgs e){Dispose();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            owner.HandleCreated-=Created;owner.HandleDestroyed-=Destroyed;owner.Disposed-=OwnerDisposed;owner.ParentChanged-=ParentChanged;UntrackAncestors();
            if(verticalOverlay!=null)verticalOverlay.Dispose();if(horizontalOverlay!=null)horizontalOverlay.Dispose();
            if(Handle!=IntPtr.Zero)ReleaseHandle();bindings.Remove(owner);
        }
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if(disposed||painting||Handle==IntPtr.Zero)return;
            if(message.Msg==0x317&&((long)message.LParam&2)!=0){PaintBars(message.WParam);return;} // WM_PRINT, PRF_NONCLIENT
            switch(message.Msg)
            {
                case 0x85:case 0xF:case 0x5:case 0xA0:case 0x2A2: // NC paint, paint, size, hover/leave
                case 0xA1:case 0xA2:case 0x200:case 0x201:case 0x202:
                case 0x113:case 0x114:case 0x115:case 0x20A:case 0x20E:case 0x215:
                case 0xC:case 0x30:case 0x47: // text, font, position
                case 0x100:case 0x101:case 0x102: // native keyboard scrolling
                case 0xB1:case 0xB6:case 0xB7:case 0xC2:case 0x437:case 0x461:case 0x4DE: // RichEdit selection, append and scroll
                case 0x7D:case 0x1A:case 0x31A: // style, settings, theme
                    if(useOverlay){UpdateOverlays();break;}
                    IntPtr dc=GetWindowDC(Handle);if(dc==IntPtr.Zero)return;
                    try{PaintBars(dc);}finally{ReleaseDC(Handle,dc);}break;
            }
        }
        void UntrackAncestors()
        {
            foreach(var control in ancestors){control.Layout-=AncestorLayout;control.LocationChanged-=AncestorChanged;control.SizeChanged-=AncestorChanged;control.VisibleChanged-=AncestorChanged;var scroll=control as ScrollableControl;if(scroll!=null)scroll.Scroll-=AncestorScroll;}
            ancestors.Clear();
        }
        void TrackAncestors()
        {
            if(!useOverlay||disposed)return;UntrackAncestors();
            for(Control control=owner;control!=null;control=control.Parent){ancestors.Add(control);control.Layout+=AncestorLayout;control.LocationChanged+=AncestorChanged;control.SizeChanged+=AncestorChanged;control.VisibleChanged+=AncestorChanged;var scroll=control as ScrollableControl;if(scroll!=null)scroll.Scroll+=AncestorScroll;}
        }
        void AncestorLayout(object sender,LayoutEventArgs e){UpdateOverlays();}
        void AncestorChanged(object sender,EventArgs e){UpdateOverlays();}
        void AncestorScroll(object sender,ScrollEventArgs e){UpdateOverlays();}
        void UpdateOverlays()
        {
            if(!useOverlay||disposed||synchronizing||Handle==IntPtr.Zero)return;synchronizing=true;
            try
            {
                var form=owner.FindForm();if(form==null||form.IsDisposed||form.Disposing||owner.Disposing)return;
                if(overlayHost!=form){if(verticalOverlay!=null)verticalOverlay.Dispose();if(horizontalOverlay!=null)horizontalOverlay.Dispose();overlayHost=form;verticalOverlay=new ScrollbarOverlay(owner,true);horizontalOverlay=new ScrollbarOverlay(owner,false);form.Controls.Add(verticalOverlay);form.Controls.Add(horizontalOverlay);}
                UpdateOverlay(verticalOverlay,true);UpdateOverlay(horizontalOverlay,false);
            }
            finally{synchronizing=false;}
        }
        void UpdateOverlay(ScrollbarOverlay overlay,bool vertical)
        {
            ScrollbarInfo info;if(!owner.Visible||!TryGetBar(Handle,vertical,out info)){overlay.Hide();return;}
            var clip=info.Bounds.Rectangle;
            for(Control parent=owner.Parent;parent!=null;parent=parent.Parent)clip.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
            if(clip.Width<1||clip.Height<1){overlay.Hide();return;}
            overlay.UpdateBar(info,clip,overlayHost.PointToClient(clip.Location));if(!overlay.Visible){overlay.Show();overlay.BringToFront();}
        }
        // A small opaque sibling covers the native non-client bar, so a later
        // Windows theme repaint cannot reintroduce a white strip. Native wheel,
        // keyboard and scroll positions remain authoritative; no repaint timer.
        internal sealed class ScrollbarOverlay : Control
        {
            readonly Control target;readonly bool vertical;ScrollbarInfo info;Rectangle clip;bool dragging;int offset;
            internal Control Target{get{return target;}}
            internal bool Vertical{get{return vertical;}}
            internal ScrollbarOverlay(Control target,bool vertical)
            {this.target=target;this.vertical=vertical;TabStop=false;AccessibleRole=AccessibleRole.ScrollBar;AccessibleName=(target.AccessibleName??"页面")+(vertical?"垂直滚动条":"水平滚动条");SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.Opaque,true);SetStyle(ControlStyles.Selectable,false);BackColor=Theme.Surface;Visible=false;}
            internal void UpdateBar(ScrollbarInfo next,Rectangle exposed,Point location)
            {
                bool changed=info.ThumbStart!=next.ThumbStart||info.ThumbEnd!=next.ThumbEnd||info.LineButton!=next.LineButton||info.Bounds.Rectangle!=next.Bounds.Rectangle||clip!=exposed;
                info=next;clip=exposed;var bounds=new Rectangle(location,exposed.Size);if(Bounds!=bounds)Bounds=bounds;if(changed)Invalidate();
            }
            protected override void OnPaintBackground(PaintEventArgs e){}
            protected override void OnPaint(PaintEventArgs e){UiRendering.Quality(e.Graphics);DrawBar(e.Graphics,info,new NativeRect{Left=clip.Left,Top=clip.Top},vertical,target.DeviceDpi/96f);}
            protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}
            protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
            int Axis(Point point){return vertical?point.Y+clip.Top-info.Bounds.Top:point.X+clip.Left-info.Bounds.Left;}
            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;int axis=Axis(e.Location),length=vertical?info.Bounds.Height:info.Bounds.Width;
                if(axis<info.LineButton)Scroll(0);else if(axis>=length-info.LineButton)Scroll(1);
                else if(axis>=info.ThumbStart&&axis<info.ThumbEnd){dragging=true;offset=axis-info.ThumbStart;Capture=true;}
                else Scroll(axis<info.ThumbStart?2:3);
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);if(!dragging)return;
                var range=new ScrollInfo{Size=(uint)Marshal.SizeOf(typeof(ScrollInfo)),Mask=0x17};if(!GetScrollInfo(target.Handle,vertical?1:0,ref range))return;
                int length=vertical?info.Bounds.Height:info.Bounds.Width,travel=length-info.LineButton*2-(info.ThumbEnd-info.ThumbStart);
                int maximum=Math.Max(range.Minimum,range.Maximum-(int)range.Page+1);
                int position=range.Minimum+(int)Math.Round(Math.Max(0,Math.Min(1,(Axis(e.Location)-offset-info.LineButton)/(double)Math.Max(1,travel)))*(maximum-range.Minimum));
                var page=target as ScrollableControl;
                if(page!=null)page.AutoScrollPosition=vertical?new Point(-page.AutoScrollPosition.X,position):new Point(position,-page.AutoScrollPosition.Y);
                else SendMessage(target.Handle,vertical?0x115:0x114,new IntPtr(((position&0xffff)<<16)|5),IntPtr.Zero);
            }
            protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Scroll(8);}
            protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)dragging=false;}
            protected override void OnMouseWheel(MouseEventArgs e){SendMessage(target.Handle,0x20A,new IntPtr(e.Delta<<16),IntPtr.Zero);}
            void Scroll(int command){if(!target.IsDisposed&&target.IsHandleCreated)SendMessage(target.Handle,vertical?0x115:0x114,new IntPtr(command),IntPtr.Zero);}
            [StructLayout(LayoutKind.Sequential)]struct ScrollInfo{public uint Size,Mask;public int Minimum,Maximum;public uint Page;public int Position,Track;}
            [DllImport("user32.dll")]static extern bool GetScrollInfo(IntPtr window,int bar,ref ScrollInfo info);
            [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
        }
        void PaintBars(IntPtr dc)
        {
            if(dc==IntPtr.Zero)return;NativeRect window;if(!GetWindowRect(Handle,out window))return;
            ScrollbarInfo vertical,horizontal;bool v=TryGetBar(Handle,true,out vertical),h=TryGetBar(Handle,false,out horizontal);
            if(!v&&!h)return;painting=true;
            try
            {
                using(var g=Graphics.FromHdc(dc))
                {
                    UiRendering.Quality(g);float z=owner.DeviceDpi/96f;
                    if(v)DrawBar(g,vertical,window,true,z);
                    if(h)DrawBar(g,horizontal,window,false,z);
                    if(v&&h)using(var brush=new SolidBrush(Theme.Surface))g.FillRectangle(brush,vertical.Bounds.Left-window.Left,horizontal.Bounds.Top-window.Top,vertical.Bounds.Width,horizontal.Bounds.Height);
                }
            }
            finally{painting=false;}
        }
        static void DrawBar(Graphics g,ScrollbarInfo info,NativeRect window,bool vertical,float z)
        {
            var r=new Rectangle(info.Bounds.Left-window.Left,info.Bounds.Top-window.Top,info.Bounds.Width,info.Bounds.Height);
            var background=r;background.Inflate(1,1);
            using(var brush=new SolidBrush(Theme.Surface))g.FillRectangle(brush,background);
            int length=vertical?r.Height:r.Width,arrow=Math.Min(info.LineButton,Math.Max(0,length/2));
            var cursor=Cursor.Position;bool hot=info.Bounds.Rectangle.Contains(cursor);
            float breadth=vertical?r.Width:r.Height,inset=Math.Max(2,Math.Min(4*z,breadth*.3f)),thickness=breadth-inset*2;
            float start=Math.Max(arrow,info.ThumbStart),end=Math.Min(length-arrow,info.ThumbEnd);
            if(thickness>0&&end>start)
            {
                var thumb=vertical?new RectangleF(r.Left+inset,r.Top+start+1,thickness,Math.Max(1,end-start-2)):new RectangleF(r.Left+start+1,r.Top+inset,Math.Max(1,end-start-2),thickness);
                Theme.FillRound(g,thumb,Math.Min(4*z,thickness/2),hot?Theme.Cyan:Color.FromArgb(65,109,157),Color.Transparent);
            }
            if(arrow<4)return;
            using(var pen=new Pen(hot?Theme.Cyan:Theme.Muted,Math.Max(1,1.2f*z)))
            {
                float half=Math.Min(3*z,arrow/4f),cross=vertical?r.Left+r.Width/2f:r.Top+r.Height/2f;
                float first=(vertical?r.Top:r.Left)+arrow/2f,last=(vertical?r.Bottom:r.Right)-arrow/2f;
                if(vertical)
                {g.DrawLines(pen,new[]{new PointF(cross-half,first+half/2),new PointF(cross,first-half/2),new PointF(cross+half,first+half/2)});g.DrawLines(pen,new[]{new PointF(cross-half,last-half/2),new PointF(cross,last+half/2),new PointF(cross+half,last-half/2)});}
                else
                {g.DrawLines(pen,new[]{new PointF(first+half/2,cross-half),new PointF(first-half/2,cross),new PointF(first+half/2,cross+half)});g.DrawLines(pen,new[]{new PointF(last-half/2,cross-half),new PointF(last+half/2,cross),new PointF(last-half/2,cross+half)});}
            }
        }
        internal static bool TryGetBar(IntPtr handle,bool vertical,out ScrollbarInfo info)
        {
            info=new ScrollbarInfo{Size=(uint)Marshal.SizeOf(typeof(ScrollbarInfo)),State=new uint[6]};
            return GetScrollBarInfo(handle,vertical?-5:-6,ref info)&&(info.State[0]&0x18000)==0&&info.Bounds.Width>0&&info.Bounds.Height>0;
        }
        [StructLayout(LayoutKind.Sequential)]internal struct NativeRect
        {
            public int Left,Top,Right,Bottom;
            public int Width {get{return Right-Left;}}public int Height {get{return Bottom-Top;}}
            public Rectangle Rectangle {get{return new Rectangle(Left,Top,Width,Height);}}
        }
        [StructLayout(LayoutKind.Sequential)]internal struct ScrollbarInfo
        {
            public uint Size;public NativeRect Bounds;public int LineButton,ThumbStart,ThumbEnd,Reserved;
            [MarshalAs(UnmanagedType.ByValArray,SizeConst=6)]public uint[] State;
        }
        [DllImport("user32.dll")]static extern bool GetScrollBarInfo(IntPtr handle,int objectId,ref ScrollbarInfo info);
        [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr handle,out NativeRect rectangle);
        [DllImport("user32.dll")]static extern IntPtr GetWindowDC(IntPtr handle);
        [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr handle,IntPtr dc);
    }
}
