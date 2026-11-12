using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    // The normal control tree receives input. One owned, mouse-transparent
    // window presents a complete frame, without individually layered children.
    internal sealed class PopupSurface : NativeWindow, IDisposable
    {
        internal const double InputOpacity=1.0/255;
        [ThreadStatic]static bool whiteMatte;
        [ThreadStatic]internal static int RenderingDpi;
        internal static Color ClearColor {get{return whiteMatte?Color.White:Color.Black;}}
        static readonly ConditionalWeakTable<Form,PopupSurface> surfaces=new ConditionalWeakTable<Form,PopupSurface>();
        readonly Form host;
        readonly FrameWindow presentation=new FrameWindow();
        readonly HashSet<Control> watched=new HashSet<Control>();
        readonly Timer caretTimer=new Timer{Interval=500};
        bool disposed,rendering,queued,interactiveMove,deferredFrame;
        Size renderedSize,renderedClientSize;
        int renderedDpi,chromeHover=-1;
        double opacity=Theme.PopupOpacity;
        internal int CompositionCount {get;private set;}
        internal static PopupSurface Attach(Form host)
        {
            PopupSurface value;if(surfaces.TryGetValue(host,out value))return value;
            value=new PopupSurface(host);surfaces.Add(host,value);return value;
        }
        internal static bool Contains(Form host){PopupSurface value;return host!=null&&surfaces.TryGetValue(host,out value);}
        internal static bool IsBackground(Form form){return form is FrameWindow;}
        internal Form Background {get{return presentation;}}
        internal double Opacity {get{return opacity;}set{double next=Math.Max(0,Math.Min(1,value));if(next==opacity)return;opacity=next;RequestFrame();}}
        PopupSurface(Form host)
        {
            this.host=host;host.TransparencyKey=Color.Empty;host.BackColor=Color.Black;host.Opacity=InputOpacity;
            host.HandleCreated+=Created;host.HandleDestroyed+=Destroyed;
            host.VisibleChanged+=Changed;host.RegionChanged+=Changed;host.Disposed+=HostDisposed;
            caretTimer.Tick+=(s,e)=>{if(host.ContainsFocus&&FocusedEditor(host)!=null)RequestFrame();};caretTimer.Start();
            Watch(host);if(host.IsHandleCreated)AssignHandle(host.Handle);
        }
        void Watch(Control control)
        {
            if(!watched.Add(control))return;
            control.ControlAdded+=ChildAdded;control.Disposed+=ChildDisposed;
            control.Invalidated+=Invalidated;control.Layout+=LayoutChanged;
            control.VisibleChanged+=Changed;control.TextChanged+=Changed;
            control.GotFocus+=Changed;control.LostFocus+=Changed;control.KeyUp+=KeyboardChanged;control.MouseUp+=MouseChanged;
            var scroll=control as ScrollableControl;if(scroll!=null)scroll.Scroll+=Scrolled;
            var rich=control as RichTextBox;if(rich!=null){rich.VScroll+=Changed;rich.HScroll+=Changed;rich.SelectionChanged+=Changed;}
            if(control is TextBoxBase)control.MouseMove+=EditorMouseMoved;
            foreach(Control child in control.Controls)Watch(child);
        }
        void Unwatch(Control control)
        {
            control.ControlAdded-=ChildAdded;control.Disposed-=ChildDisposed;
            control.Invalidated-=Invalidated;control.Layout-=LayoutChanged;
            control.VisibleChanged-=Changed;control.TextChanged-=Changed;watched.Remove(control);
            control.GotFocus-=Changed;control.LostFocus-=Changed;control.KeyUp-=KeyboardChanged;control.MouseUp-=MouseChanged;
            var scroll=control as ScrollableControl;if(scroll!=null)scroll.Scroll-=Scrolled;
            var rich=control as RichTextBox;if(rich!=null){rich.VScroll-=Changed;rich.HScroll-=Changed;rich.SelectionChanged-=Changed;}
            if(control is TextBoxBase)control.MouseMove-=EditorMouseMoved;
        }
        void ChildAdded(object sender,ControlEventArgs e){Watch(e.Control);RequestFrame();}
        void ChildDisposed(object sender,EventArgs e){Unwatch((Control)sender);RequestFrame();}
        void Invalidated(object sender,InvalidateEventArgs e){RequestFrame();}
        void LayoutChanged(object sender,LayoutEventArgs e){RequestFrame();}
        void Scrolled(object sender,ScrollEventArgs e){RequestFrame();}
        void EditorMouseMoved(object sender,MouseEventArgs e){if(((Control)sender).Capture)RequestFrame();}
        void KeyboardChanged(object sender,KeyEventArgs e){RequestFrame();}
        void MouseChanged(object sender,MouseEventArgs e){RequestFrame();}
        void Changed(object sender,EventArgs e){Sync();RequestFrame();}
        void Created(object sender,EventArgs e){AssignHandle(host.Handle);Sync();}
        void Destroyed(object sender,EventArgs e){ReleaseHandle();presentation.Hide();interactiveMove=false;deferredFrame=false;renderedDpi=0;}
        void HostDisposed(object sender,EventArgs e){Dispose();}
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x231)interactiveMove=true; // WM_ENTERSIZEMOVE
            base.WndProc(ref message);
            if(message.Msg==0x47||message.Msg==0x18||message.Msg==0x86)Sync();
            if(message.Msg==0xa0||message.Msg==0x2a2)UpdateChromeHover();
            if(message.Msg==0x232) // WM_EXITSIZEMOVE
            {
                interactiveMove=false;Sync();UpdateChromeHover();
                if(deferredFrame){deferredFrame=false;RequestFrame();}
            }
        }
        bool GeometryChanged()
        {return !presentation.Visible||renderedDpi!=host.DeviceDpi||renderedSize!=host.Size||renderedClientSize!=host.ClientSize;}
        void UpdateChromeHover()
        {
            if(disposed||!host.IsHandleCreated||host.FormBorderStyle==FormBorderStyle.None)return;
            using(var dpi=new WindowDpiScope(host))
            {
                var bounds=VisibleBounds();var cursor=Cursor.Position;int hover=-1;
                if(cursor.Y>=bounds.Top&&cursor.Y<host.PointToScreen(Point.Empty).Y&&cursor.X>=bounds.Left&&cursor.X<bounds.Right)
                {
                    int width=WindowLayout.Pixels(46,host.DeviceDpi),buttons=host.ControlBox?(host.MinimizeBox||host.MaximizeBox?3:1):0;
                    int index=2-(bounds.Right-1-cursor.X)/width;if(index>=3-buttons)hover=index;
                }
                if(hover!=chromeHover){chromeHover=hover;RequestFrame();}
            }
        }
        internal void Sync()
        {
            if(disposed||rendering)return;
            using(var dpi=new WindowDpiScope(host))
            {
                if(!host.Visible||!host.IsHandleCreated||host.Opacity==0||host.WindowState==FormWindowState.Minimized){presentation.Hide();interactiveMove=false;deferredFrame=false;return;}
                if(presentation.Visible){presentation.MoveTo(VisibleBounds().Location);AlignFrame();}
                // Position and Z-order changes reuse the already presented pixels.
                // A large frame must not be captured twice for every mouse move.
                if(GeometryChanged())RequestFrame();
            }
        }
        void RequestFrame()
        {
            if(disposed||rendering||queued||!host.IsHandleCreated||!host.Visible)return;
            if(interactiveMove&&!GeometryChanged()){deferredFrame=true;return;}
            queued=true;host.BeginInvoke(new Action(()=>{queued=false;RenderNow();}));
        }
        internal void RenderNow()
        {
            if(disposed||rendering||!host.Visible||host.Opacity==0||host.WindowState==FormWindowState.Minimized)return;
            if(interactiveMove&&!GeometryChanged()){deferredFrame=true;return;}
            using(var dpi=new WindowDpiScope(host))
            using(var frame=ComposeFrame())
            {
                // Replace pixels, size and position atomically, including pixels
                // exposed when an animated dialogue shrinks.
                presentation.Owner=host;presentation.Present(frame,VisibleBounds().Location);
                if(!presentation.Visible)presentation.Show(host);
                renderedSize=host.Size;renderedClientSize=host.ClientSize;renderedDpi=host.DeviceDpi;
                AlignFrame();
            }
        }
        void AlignFrame()
        {
            bool pinned=(GetWindowLong(host.Handle,-20)&8)!=0;
            if(pinned!=((GetWindowLong(presentation.Handle,-20)&8)!=0))
                SetWindowPos(presentation.Handle,new IntPtr(pinned?-1:-2),0,0,0,0,0x213);
            IntPtr previous=GetWindow(host.Handle,3);
            if(previous==presentation.Handle)return;
            if(previous!=IntPtr.Zero&&((GetWindowLong(previous,-20)&8)!=0)!=pinned)previous=IntPtr.Zero;
            // Immediately above its input window, never above a dialog that is
            // already above that window. No activation or independent pinning.
            SetWindowPos(presentation.Handle,previous,0,0,0,0,0x213);
        }
        static bool Component(Control control)
        {return control is ButtonBase||control is TextBoxBase||control is DigitalTimePicker||control is GameTile||control is HeyboxTile||control is FairyPanelButton;}
        internal Bitmap ComposeFrame()
        {
            using(var dpi=new WindowDpiScope(host))
            {
                CompositionCount++;
                rendering=true;int previousDpi=RenderingDpi;RenderingDpi=host.DeviceDpi;
                try
                {
                    var bounds=new Rectangle(Point.Empty,host.Size);
                    var client=host.RectangleToScreen(host.ClientRectangle);client.Offset(-host.Left,-host.Top);
                    var visible=VisibleBounds();visible.Offset(-host.Left,-host.Top);visible.Intersect(bounds);
                    using(var black=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb))
                    using(var white=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb))
                    using(var backdrop=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb))
                    using(var mask=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb))
                    {
                        black.SetResolution(host.DeviceDpi,host.DeviceDpi);white.SetResolution(host.DeviceDpi,host.DeviceDpi);
                        backdrop.SetResolution(host.DeviceDpi,host.DeviceDpi);mask.SetResolution(host.DeviceDpi,host.DeviceDpi);
                        // Two mattes recover antialiased glyph/edge coverage, so
                        // partially transparent pixels have no colour-key fringes.
                        whiteMatte=false;host.DrawToBitmap(black,bounds);
                        whiteMatte=true;host.DrawToBitmap(white,bounds);whiteMatte=false;
                        using(var g=Graphics.FromImage(backdrop)){g.TranslateTransform(client.X,client.Y);Theme.HddBackground(g,host.ClientRectangle);}
                        using(var g=Graphics.FromImage(mask)){g.Clear(Color.White);ComponentMask(host,g,client);}
                        var b=Pixels(black);var w=Pixels(white);var bg=Pixels(backdrop);var alpha=Pixels(mask);
                        for(int y=0;y<bounds.Height;y++)for(int x=0;x<bounds.Width;x++)
                        {
                            int i=(y*bounds.Width+x)*4;
                            if(!client.Contains(x,y)){b[i+3]=255;continue;}
                            double coverage=1-Math.Max(0,Math.Min(1,((w[i]-b[i])+(w[i+1]-b[i+1])+(w[i+2]-b[i+2]))/765.0));
                            double foreground=alpha[i]/255.0,remaining=opacity*(1-coverage*foreground);
                            for(int c=0;c<3;c++)b[i+c]=(byte)Math.Min(255,Math.Round(b[i+c]*foreground+bg[i+c]*remaining));
                            b[i+3]=(byte)Math.Round(255*(coverage*foreground+remaining));
                        }
                        var result=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppPArgb);
                        var data=result.LockBits(bounds,ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                        try{Marshal.Copy(b,0,data.Scan0,b.Length);}finally{result.UnlockBits(data);}
                        if(host.Region!=null)using(var g=Graphics.FromImage(result))
                        {
                            g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                            g.ExcludeClip(host.Region);using(var clear=new SolidBrush(Color.Transparent))g.FillRectangle(clear,bounds);
                        }
                        PaintChrome(result,client,visible);
                        PaintCaret(result);
                        if(visible==bounds)return result;
                        try{return result.Clone(visible,PixelFormat.Format32bppPArgb);}finally{result.Dispose();}
                    }
                }
                finally{whiteMatte=false;RenderingDpi=previousDpi;rendering=false;}
            }
        }
        static TextBoxBase FocusedEditor(Control control)
        {
            var editor=control as TextBoxBase;if(editor!=null&&editor.Focused&&!editor.ReadOnly)return editor;
            foreach(Control child in control.Controls)if(child.ContainsFocus)return FocusedEditor(child);
            return null;
        }
        void PaintCaret(Bitmap frame)
        {
            var editor=FocusedEditor(host);if(editor==null||editor.SelectionLength!=0||(Environment.TickCount&0x3ff)>=512)return;
            NativePoint caret;if(!GetCaretPos(out caret))return;
            var point=editor.PointToScreen(new Point(caret.X,caret.Y));point.Offset(-host.Left,-host.Top);
            int height=Math.Min(editor.ClientSize.Height-caret.Y,(int)Math.Ceiling(editor.Font.GetHeight(host.DeviceDpi)));
            if(height<=0)return;
            using(var g=Graphics.FromImage(frame))using(var pen=new Pen(Theme.Text,Math.Max(1,host.DeviceDpi/96f)))g.DrawLine(pen,point.X,point.Y,point.X,point.Y+height-1);
        }
        internal Rectangle VisibleBounds()
        {
            using(var dpi=new WindowDpiScope(host))
            {
                if(host.FormBorderStyle==FormBorderStyle.None)return host.Bounds;
                NativeRect rect;
                if(DwmGetWindowAttribute(host.Handle,9,out rect,Marshal.SizeOf(typeof(NativeRect)))!=0)return host.Bounds;
                // DWM always reports physical pixels. The native input window may
                // use virtualized coordinates (including compatibility DPI modes).
                // Position, crop and hit areas must all use that window's space.
                var topLeft=new NativePoint{X=rect.Left,Y=rect.Top};var bottomRight=new NativePoint{X=rect.Right,Y=rect.Bottom};
                if(!PhysicalToLogicalPointForPerMonitorDPI(host.Handle,ref topLeft)||!PhysicalToLogicalPointForPerMonitorDPI(host.Handle,ref bottomRight))return host.Bounds;
                return Rectangle.FromLTRB(topLeft.X,topLeft.Y,bottomRight.X,bottomRight.Y);
            }
        }
        void PaintChrome(Bitmap image,Rectangle client,Rectangle visible)
        {
            if(host.FormBorderStyle==FormBorderStyle.None)return;
            float z=host.DeviceDpi/96f;int height=client.Top-visible.Top;
            using(var g=Graphics.FromImage(image))
            {
                g.SetClip(client,System.Drawing.Drawing2D.CombineMode.Exclude);
                using(var fill=new SolidBrush(Color.FromArgb(24,28,36)))g.FillRectangle(fill,visible);
                g.ResetClip();
                int buttonWidth=WindowLayout.Pixels(46,host.DeviceDpi);
                var cursor=Cursor.Position;cursor.Offset(-host.Left,-host.Top);
                int buttons=host.ControlBox?(host.MinimizeBox||host.MaximizeBox?3:1):0;
                for(int i=3-buttons;i<3;i++)
                {
                    var button=new Rectangle(visible.Right-(3-i)*buttonWidth,visible.Top,buttonWidth,height);
                    if(button.Contains(cursor))using(var fill=new SolidBrush(i==2?Color.FromArgb(180,42,52):Theme.Edge))g.FillRectangle(fill,button);
                    float cx=button.Left+button.Width/2f,cy=button.Top+button.Height/2f,s=4*z;
                    using(var pen=new Pen((i==0&&!host.MinimizeBox)||(i==1&&!host.MaximizeBox)?Theme.Muted:Theme.Text,z))
                    {
                        if(i==0)g.DrawLine(pen,cx-s,cy,cx+s,cy);
                        else if(i==1)
                        {
                            if(host.WindowState==FormWindowState.Maximized)g.DrawRectangle(pen,cx-s+2*z,cy-s-2*z,s*2,s*2);
                            g.DrawRectangle(pen,cx-s,cy-s,s*2,s*2);
                        }
                        else{g.DrawLine(pen,cx-s,cy-s,cx+s,cy+s);g.DrawLine(pen,cx-s,cy+s,cx+s,cy-s);}
                    }
                }
                int icon=WindowLayout.Pixels(16,host.DeviceDpi),left=visible.Left+WindowLayout.Pixels(9,host.DeviceDpi);
                if(host.Icon!=null)g.DrawIcon(host.Icon,new Rectangle(left,visible.Top+(height-icon)/2,icon,icon));
                using(var font=AppFonts.Create(9))UiRendering.Text(g,host.Text,font,new Rectangle(left+icon+WindowLayout.Pixels(6,host.DeviceDpi),visible.Top,Math.Max(1,visible.Width-buttons*buttonWidth-icon-WindowLayout.Pixels(25,host.DeviceDpi)),height),Theme.Text,false);
            }
        }
        static byte[] Pixels(Bitmap bitmap)
        {
            var data=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{var bytes=new byte[bitmap.Width*bitmap.Height*4];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);return bytes;}
            finally{bitmap.UnlockBits(data);}
        }
        void ComponentMask(Control parent,Graphics g,Rectangle clip)
        {
            foreach(Control child in parent.Controls)
            {
                if(!child.Visible)continue;
                var area=child.RectangleToScreen(child.ClientRectangle);area.Offset(-host.Left,-host.Top);
                area.Intersect(clip);if(area.IsEmpty)continue;
                if(Component(child))using(var brush=new SolidBrush(Color.FromArgb(230,230,230)))g.FillRectangle(brush,area);
                ComponentMask(child,g,area);
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            host.HandleCreated-=Created;host.HandleDestroyed-=Destroyed;
            host.VisibleChanged-=Changed;host.RegionChanged-=Changed;host.Disposed-=HostDisposed;
            foreach(Control control in new List<Control>(watched))Unwatch(control);
            ReleaseHandle();presentation.Dispose();surfaces.Remove(host);
            caretTimer.Dispose();
        }
        sealed class FrameWindow : PassiveForm
        {
            public FrameWindow(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;}
            protected override void OnDpiChanged(DpiChangedEventArgs e)
            {
                // The host already supplied a frame in physical monitor pixels.
                // Applying the companion window's suggested DPI rectangle would
                // scale and offset that complete frame a second time.
                e.Cancel=true;base.OnDpiChanged(e);
            }
            protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20;return p;}}
            internal void MoveTo(Point location)
            {
                if(Location==location)return;
                SetWindowPos(Handle,IntPtr.Zero,location.X,location.Y,0,0,0x215);UpdateBounds();
            }
            internal void Present(Bitmap bitmap,Point location)
            {
                IntPtr screen=GetDC(IntPtr.Zero),dc=CreateCompatibleDC(screen),image=IntPtr.Zero,old=IntPtr.Zero;
                try
                {
                    image=bitmap.GetHbitmap(Color.FromArgb(0));old=SelectObject(dc,image);
                    var position=new NativePoint{X=location.X,Y=location.Y};var size=new NativeSize{Width=bitmap.Width,Height=bitmap.Height};
                    var source=new NativePoint();var blend=new Blend{Alpha=255,Format=1};
                    if(!UpdateLayeredWindow(Handle,screen,ref position,ref size,dc,ref source,0,ref blend,2))throw new System.ComponentModel.Win32Exception();
                    UpdateBounds();
                }
                finally{if(old!=IntPtr.Zero)SelectObject(dc,old);if(image!=IntPtr.Zero)DeleteObject(image);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}
            }
        }
        struct WindowDpiScope : IDisposable
        {
            readonly IntPtr previous;
            internal WindowDpiScope(Form window)
            {previous=window.IsHandleCreated?SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(window.Handle)):IntPtr.Zero;}
            public void Dispose(){if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
        }
        [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;}
        [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]struct NativeSize{public int Width,Height;}
        [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Operation,Flags,Alpha,Format;}
        [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr image);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr image);
        [DllImport("user32.dll")]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref NativePoint position,ref NativeSize size,IntPtr dc,ref NativePoint source,uint key,ref Blend blend,uint flags);
        [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out NativeRect value,int size);
        [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")]static extern bool GetCaretPos(out NativePoint point);
        [DllImport("user32.dll")]static extern bool PhysicalToLogicalPointForPerMonitorDPI(IntPtr window,ref NativePoint point);
        [DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    }
}
