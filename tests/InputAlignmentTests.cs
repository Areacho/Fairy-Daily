using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]static extern int MapWindowPoints(IntPtr from,IntPtr to,ref Point point,uint count);
    [StructLayout(LayoutKind.Sequential)]struct InputRect{internal int Left,Top,Right,Bottom;}
    [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out InputRect value,int size);
    static Rectangle PhysicalInputBounds(Form form)
    {
        InputRect rect;if(DwmGetWindowAttribute(form.Handle,9,out rect,Marshal.SizeOf(typeof(InputRect)))!=0)throw new Exception("DWM geometry unavailable");
        return Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
    }
    sealed class InputDpiContext : IDisposable
    {
        readonly IntPtr previous;
        public InputDpiContext(int context){previous=SetThreadDpiAwarenessContext(new IntPtr(context));}
        public void Dispose(){if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
    }
    sealed class InputGeometryForm : Form
    {
        internal int ExtraCaption;
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if(message.Msg==0x83&&ExtraCaption>0)
            {int top=Marshal.ReadInt32(message.LParam,4);Marshal.WriteInt32(message.LParam,4,top+ExtraCaption);}
        }
    }
    static bool FieldColor(Color pixel){return pixel.G>95&&pixel.R>35&&pixel.B<100;}
    static void InputAlignmentChecks()
    {
        string output=DisplayReviewDirectory();
        foreach(int awareness in new[]{-4,-2,-1})using(var context=new InputDpiContext(awareness))
        foreach(var screen in Screen.AllScreens)
        foreach(int extra in new[]{0,80})
        foreach(int targetDpi in awareness==-4?new[]{0,96,144,168,192,240,288}:new[]{0})
        using(var form=new InputGeometryForm{Text="Offline text input geometry",ExtraCaption=extra,AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi,StartPosition=FormStartPosition.Manual,Location=new Point(screen.WorkingArea.Left+40,screen.WorkingArea.Top+40),ClientSize=new Size(650,448),Font=AppFonts.Create(10),ShowInTaskbar=false})
        {
            var surface=PopupSurface.Attach(form);form.Opacity=0;
            var root=new FairyBackdrop{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=3};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,55));root.RowStyles.Add(new RowStyle(SizeType.Absolute,175));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));form.Controls.Add(root);
            root.Controls.Add(new SmoothLabel{Text="离线输入框校验",Dock=DockStyle.Fill,ForeColor=Theme.Text},0,0);
            var fields=new BufferedTableLayout{ColumnCount=3,RowCount=3,Dock=DockStyle.Fill,BackColor=Color.Transparent};
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,75));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,146));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute,47));fields.RowStyles.Add(new RowStyle(SizeType.Absolute,47));fields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var card=new RoundedPanel{Dock=DockStyle.Fill,Padding=new Padding(18,12,18,12),Margin=new Padding(0,5,0,6)};card.Controls.Add(fields);root.Controls.Add(card,0,1);
            var field=new TextBox{BackColor=Color.FromArgb(80,150,40),ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,Dock=DockStyle.Top,Margin=new Padding(3,8,14,0)};
            fields.Controls.Add(new SmoothLabel{Text="输入 A",Dock=DockStyle.Fill},0,0);fields.Controls.Add(field,1,0);
            fields.Controls.Add(new RoundButton{Text="占位",Dock=DockStyle.Fill,Enabled=false},2,0);
            var log=new RichTextBox{Text="Offline rendering fixture",Dock=DockStyle.Fill,ReadOnly=true,BackColor=Theme.Surface};root.Controls.Add(log,0,2);ScrollbarTheme.Attach(log);
            form.Show();
            using(var limit=new VirtualRenderBounds(form))
            {
                if(targetDpi!=0)SetTestDpi(form,targetDpi);
                form.ClientSize=new Size(WindowLayout.Pixels(650,form.DeviceDpi),WindowLayout.Pixels(448,form.DeviceDpi));form.PerformLayout();root.PerformLayout();
                var actual=field.RectangleToScreen(field.ClientRectangle);var visible=surface.VisibleBounds();
                Point visualInputCenter;
                using(var frame=surface.ComposeFrame())
                {
                    int x=actual.Left+actual.Width*3/4-visible.Left,top=frame.Height,bottom=-1;
                    Assert(frame.Size==visible.Size&&form.Bounds.Contains(visible),"DWM frame bounds use the native input window's coordinate space");
                    Assert(x>=0&&x<frame.Width,"native input X lies inside its visual frame in DPI context "+awareness);
                    for(int y=0;y<frame.Height;y++)if(FieldColor(frame.GetPixel(x,y))){top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                    int expectedTop=actual.Top-visible.Top,expectedBottom=actual.Bottom-visible.Top-1;
                    Console.WriteLine("Input geometry: awareness="+awareness+", DPI="+form.DeviceDpi+", caption extra="+extra+", native Y="+expectedTop+".."+expectedBottom+", image Y="+top+".."+bottom);
                    frame.Save(Path.Combine(output,"input-geometry-"+awareness+"-"+form.DeviceDpi+"-"+extra+".png"));
                    Assert(Math.Abs(top-expectedTop)<=2&&Math.Abs(bottom-expectedBottom)<=2,"rendered input edges align with the native click rectangle independently of nonclient height");
                    visualInputCenter=new Point(x,(top+bottom)/2);
                }
                form.Opacity=PopupSurface.InputOpacity;surface.RenderNow();
                var physicalHost=PhysicalInputBounds(form);var physicalFrame=PhysicalInputBounds(surface.Background);
                Console.WriteLine("Physical input="+physicalHost+", visual="+physicalFrame);
                Assert(Math.Abs(physicalFrame.Left-physicalHost.Left)<=1&&Math.Abs(physicalFrame.Top-physicalHost.Top)<=1&&Math.Abs(physicalFrame.Right-physicalHost.Right)<=1&&Math.Abs(physicalFrame.Bottom-physicalHost.Bottom)<=1,"presented layered frame and native hit window occupy the same physical pixels");
                var click=visualInputCenter;MapWindowPoints(surface.Background.Handle,field.Handle,ref click,1);
                Assert(field.ClientRectangle.Contains(click),"clicking the displayed input maps inside the native text box");
                using(var callback=new InputDpiContext(awareness==-1?-4:-1))
                {
                    var caller=GetThreadDpiAwarenessContext();surface.RenderNow();surface.Sync();
                    Assert(AreDpiAwarenessContextsEqual(caller,GetThreadDpiAwarenessContext()),"presentation restores its caller's DPI context");
                }
                Assert(PhysicalInputBounds(surface.Background)==physicalFrame,"callbacks with another DPI context cannot shift or rescale the input's visual frame");
                surface.Background.Hide();
                ScrollMessage(field.Handle,0x102,new IntPtr('7'),IntPtr.Zero);
                Assert(field.Text=="7","the offline input receives native character messages");
                form.Hide();
            }
        }
    }
}
