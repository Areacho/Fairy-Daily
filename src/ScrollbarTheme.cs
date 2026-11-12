using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    // Repaint the native scrollbar's non-client surface. Windows continues to
    // own hit testing, dragging, keyboard navigation, wheel input and DPI sizing.
    public sealed class ScrollbarTheme : NativeWindow,IDisposable
    {
        static readonly ConditionalWeakTable<Control,ScrollbarTheme> bindings=new ConditionalWeakTable<Control,ScrollbarTheme>();
        readonly Control owner;bool painting,disposed;
        public static void Attach(Control control)
        {bindings.GetValue(control,c=>new ScrollbarTheme(c));}
        ScrollbarTheme(Control control)
        {
            owner=control;owner.HandleCreated+=Created;owner.HandleDestroyed+=Destroyed;owner.Disposed+=OwnerDisposed;
            if(owner.IsHandleCreated)AssignHandle(owner.Handle);
        }
        void Created(object sender,EventArgs e){if(Handle!=IntPtr.Zero)ReleaseHandle();AssignHandle(owner.Handle);}
        void Destroyed(object sender,EventArgs e){if(Handle!=IntPtr.Zero)ReleaseHandle();}
        void OwnerDisposed(object sender,EventArgs e){Dispose();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            owner.HandleCreated-=Created;owner.HandleDestroyed-=Destroyed;owner.Disposed-=OwnerDisposed;
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
                    IntPtr dc=GetWindowDC(Handle);if(dc==IntPtr.Zero)return;
                    try{PaintBars(dc);}finally{ReleaseDC(Handle,dc);}break;
            }
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
