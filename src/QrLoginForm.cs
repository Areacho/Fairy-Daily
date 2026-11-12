using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed class QrLoginForm : Form
    {
        readonly QrCanvas canvas=new QrCanvas();
        readonly SmoothLabel status=new SmoothLabel();
        readonly Timer fade=new Timer();
        readonly Stopwatch fadeClock=new Stopwatch();
        public QrLoginForm()
        {
            SuspendLayout();Text="Fairy · 扫码登录";Icon=AppAssets.Icon;Font=AppFonts.Create(10);
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterScreen;
            BackColor=Theme.Surface;Opacity=Theme.PopupOpacity;DoubleBuffered=true;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(328,430);
            var root=new FairyBackdrop{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(18)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));Controls.Add(root);
            var header=new Panel{Size=new Size(292,60),Dock=DockStyle.Fill,BackColor=Color.Transparent};
            header.Controls.Add(new SmoothLabel{Text="接入 / 扫码登录",Font=AppFonts.Create(14,FontStyle.Bold),ForeColor=Theme.Text,Location=new Point(0,0),Size=new Size(250,33)});
            header.Controls.Add(new SmoothLabel{Text="使用米游社 App 扫码并确认",ForeColor=Theme.Muted,Location=new Point(0,31),Size=new Size(250,25)});
            var close=new RoundButton{Text="×",Size=new Size(28,28),Anchor=AnchorStyles.Top|AnchorStyles.Right,Location=new Point(264,0)};close.Click+=(s,e)=>Close();header.Controls.Add(close);root.Controls.Add(header,0,0);
            var qrCard=new RoundedPanel{Dock=DockStyle.Fill,Fill=Color.White,Padding=new Padding(14),Margin=new Padding(0,6,0,2)};
            canvas.Dock=DockStyle.Fill;qrCard.Controls.Add(canvas);root.Controls.Add(qrCard,0,1);
            status.Text="正在生成二维码…";status.ForeColor=Theme.Lime;status.TextAlign=ContentAlignment.MiddleCenter;status.Dock=DockStyle.Fill;status.BackColor=Color.Transparent;root.Controls.Add(status,0,2);
            var cancel=new RoundButton{Text="取消登录",Dock=DockStyle.Fill};cancel.Click+=(s,e)=>Close();CancelButton=cancel;root.Controls.Add(cancel,0,3);
            root.Paint+=(s,e)=>{e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Theme.Round(new RectangleF(.5f,.5f,root.Width-1,root.Height-1),22*DeviceDpi/96f))using(var pen=new Pen(Theme.Edge))e.Graphics.DrawPath(pen,path);};
            fade.Interval=16;fade.Tick+=(s,e)=>{Opacity=Theme.PopupOpacity*Math.Min(1,fadeClock.Elapsed.TotalMilliseconds/170);if(Opacity>=Theme.PopupOpacity)fade.Stop();};
            DpiChanged+=(s,e)=>BeginInvoke(new Action(RoundWindow));ResumeLayout(true);WindowLayout.Watch(this,()=>Size.Empty,true);
        }
        public void SetStatus(string text){if(!IsDisposed)status.Text=text;}
        public void SetCode(Bitmap image)
        {
            if(IsDisposed){image.Dispose();return;}canvas.SetCode(image);status.Text="等待扫码，请在手机上确认登录";
        }
        public void ShowFor(Form owner)
        {
            Opacity=0;
            if(owner!=null){TopMost=owner.TopMost;Show(owner);}else Show();
        }
        protected override void OnShown(EventArgs e){base.OnShown(e);RoundWindow();fadeClock.Restart();fade.Start();}
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(IsHandleCreated)RoundWindow();}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Escape)Close();}
        void RoundWindow()
        {if(Width<1||Height<1)return;using(var path=Theme.Round(new RectangleF(0,0,Width,Height),22*DeviceDpi/96f)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
        protected override void Dispose(bool disposing){if(disposing)fade.Dispose();base.Dispose(disposing);}
        sealed class QrCanvas : Control
        {
            Bitmap code;
            public QrCanvas(){DoubleBuffered=true;BackColor=Color.White;}
            public void SetCode(Bitmap image){var old=code;code=image;if(old!=null)old.Dispose();Invalidate();}
            protected override void OnPaint(PaintEventArgs e)
            {
                var g=e.Graphics;g.Clear(Color.White);
                if(code==null){float eyeSize=WindowLayout.Pixels(96,DeviceDpi);FairyArt.Eye(g,new RectangleF((Width-eyeSize)/2,(Height-eyeSize)/2,eyeSize,eyeSize),0,PointF.Empty,false);return;}
                // QrTools renders each module at six pixels. Use whole module sizes
                // so resizing never blurs the black/white cells.
                int modules=Math.Max(1,code.Width/6),scale=Math.Max(1,Math.Min(Width,Height)/modules),side=modules*scale;
                g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                g.DrawImage(code,new Rectangle((Width-side)/2,(Height-side)/2,side,side),0,0,code.Width,code.Height,GraphicsUnit.Pixel);
            }
            protected override void Dispose(bool disposing){if(disposing&&code!=null){code.Dispose();code=null;}base.Dispose(disposing);}
        }
    }
}
