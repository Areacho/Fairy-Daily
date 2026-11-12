using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MiyouDaily
{
    public static class Theme
    {
        public const double PopupOpacity=.80;
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        public static void DarkTitlebar(IntPtr handle){try{int dark=1;DwmSetWindowAttribute(handle,20,ref dark,4);}catch(DllNotFoundException){}}
        public static readonly Color Background=Color.FromArgb(4,12,30), Surface=Color.FromArgb(10,27,54), Tile=Color.FromArgb(7,21,44), Text=Color.FromArgb(225,242,255), Muted=Color.FromArgb(143,174,213), Edge=Color.FromArgb(39,77,126), Cyan=Color.FromArgb(121,218,255), Lime=Color.FromArgb(121,218,255);
        public static void HddBackground(Graphics g,Rectangle bounds)
        {
            if(bounds.Width<1||bounds.Height<1)return;
            using(var brush=new LinearGradientBrush(bounds,Color.FromArgb(8,26,56),Color.FromArgb(3,8,27),115))g.FillRectangle(brush,bounds);
            Glow(g,new RectangleF(bounds.X+bounds.Width*.12f,bounds.Y-bounds.Height*.12f,bounds.Width*.95f,bounds.Height*1.2f),Color.FromArgb(125,35,105,245));
            Glow(g,new RectangleF(bounds.X-bounds.Width*.3f,bounds.Y+bounds.Height*.35f,bounds.Width*.85f,bounds.Height*.9f),Color.FromArgb(30,24,107,204));
            using(var pen=new Pen(Color.FromArgb(6,117,181,255)))for(int y=bounds.Top;y<bounds.Bottom;y+=3)g.DrawLine(pen,bounds.Left,y,bounds.Right,y);
            using(var pen=new Pen(Color.FromArgb(13,106,169,235),1))
            {
                float w=bounds.Width,h=bounds.Height,x=bounds.X,y=bounds.Y;
                g.DrawLines(pen,new[]{new PointF(x+w*.58f,y+h),new PointF(x+w*.76f,y+h*.64f),new PointF(x+w,y+h*.64f)});
                g.DrawLines(pen,new[]{new PointF(x,y+h*.79f),new PointF(x+w*.17f,y+h*.79f),new PointF(x+w*.3f,y+h)});
            }
        }
        static void Glow(Graphics g,RectangleF bounds,Color color)
        {using(var path=new GraphicsPath()){path.AddEllipse(bounds);using(var brush=new PathGradientBrush(path)){brush.CenterColor=color;brush.SurroundColors=new[]{Color.FromArgb(0,color)};g.FillPath(brush,path);}}}
        public static void Checker(Graphics g,float x,float y,int count,float unit,Color color)
        {using(var b=new SolidBrush(color))for(int i=0;i<count;i++)for(int j=0;j<2;j++)if((i+j)%2==0)g.FillRectangle(b,x+i*unit,y+j*unit,unit,unit);}
        public static void Bezel(Graphics g,RectangleF r,float radius,Color fill)
        {
            FillRound(g,r,radius,Color.FromArgb(3,11,29),Color.FromArgb(30,62,106));
            r.Inflate(-2,-2);FillRound(g,r,Math.Max(2,radius-2),fill,Edge);
            using(var p=new Pen(Color.FromArgb(32,255,255,255)))g.DrawLine(p,r.Left+radius,r.Top+1,r.Right-radius,r.Top+1);
        }
        public static GraphicsPath Round(RectangleF r,float radius)
        {
            radius=Math.Min(radius,Math.Min(r.Width,r.Height)/2);float d=radius*2;var p=new GraphicsPath();
            p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        public static void FillRound(Graphics g,RectangleF r,float radius,Color fill,Color border)
        {using(var path=Round(r,radius)){using(var b=new SolidBrush(fill))g.FillPath(b,path);if(border.A>0)using(var pen=new Pen(border))g.DrawPath(pen,path);}}
        internal static void PaintBehind(Control child,Graphics g)
        {
            // Replay only our background artwork. Invoking a parent's WinForms
            // Paint pipeline on a button DC can replay text/native button layers.
            Point origin=child.Location;
            for(Control parent=child.Parent;parent!=null;parent=parent.Parent)
            {
                var dialogue=parent as FairyDialogue;
                if(dialogue!=null){dialogue.PaintBackdrop(g,origin);return;}
                var backdrop=parent as FairyBackdrop;
                var panel=parent as RoundedPanel;
                if(backdrop!=null||panel!=null)
                {
                    var saved=g.Save();
                    try
                    {
                        g.TranslateTransform(-origin.X,-origin.Y);
                        if(backdrop!=null)backdrop.PaintSurface(g);else panel.PaintSurface(g);
                    }
                    finally{g.Restore(saved);}
                    return;
                }
                if(parent.BackColor.A==255)
                {using(var brush=new SolidBrush(parent.BackColor))g.FillRectangle(brush,child.ClientRectangle);return;}
                origin.Offset(parent.Location);
            }
            using(var brush=new SolidBrush(Background))g.FillRectangle(brush,child.ClientRectangle);
        }
    }
    public static class AppAssets
    {
        static readonly Dictionary<string,Image> Images=new Dictionary<string,Image>();
        static Icon icon;
        public static Image Image(string key)
        {
            Image img;if(Images.TryGetValue(key,out img))return img;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MiyouDaily."+key+".png"))
            {if(stream==null)throw new InvalidDataException("缺少图标："+key);using(var source=System.Drawing.Image.FromStream(stream))img=new Bitmap(source);}
            Images[key]=img;return img;
        }
        public static Icon Icon{get{if(icon==null)using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MiyouDaily.fairy.ico"))icon=new Icon(stream);return icon;}}
    }
    public sealed class FairyBackdrop : TableLayoutPanel
    {
        Bitmap texture;int textureDpi;
        public FairyBackdrop(){DoubleBuffered=true;BackColor=Theme.Background;SetStyle(ControlStyles.ResizeRedraw|ControlStyles.Opaque,true);}
        protected override void OnPaintBackground(PaintEventArgs e)
        { }
        protected override void OnPaint(PaintEventArgs e)
        {PaintSurface(e.Graphics);base.OnPaint(e);}
        internal void PaintSurface(Graphics g)
        {
            // Cache the HDD light field and scans; transparent children replay
            // the same artwork without repainting other controls.
            if(Width<1||Height<1)return;
            if(texture==null||textureDpi!=DeviceDpi||texture.Size!=ClientSize)
            {
                if(texture!=null)texture.Dispose();textureDpi=DeviceDpi;
                texture=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using(var tile=Graphics.FromImage(texture))
                {
                    Theme.HddBackground(tile,ClientRectangle);
                }
            }
            g.DrawImageUnscaled(texture,0,0);
        }
        protected override void Dispose(bool disposing){if(disposing&&texture!=null){texture.Dispose();texture=null;}base.Dispose(disposing);}
        static void Glow(Graphics g,RectangleF rect,Color color)
        {using(var p=new GraphicsPath()){p.AddEllipse(rect);using(var b=new PathGradientBrush(p)){b.CenterColor=color;b.SurroundColors=new[]{Color.FromArgb(0,color)};g.FillPath(b,p);}}}
    }
    public class RoundedPanel : Panel
    {
        public Color Fill=Theme.Surface;
        public RoundedPanel(){DoubleBuffered=true;BackColor=Color.Transparent;SetStyle(ControlStyles.ResizeRedraw,true);}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBehind(this,e.Graphics);}
        internal void PaintSurface(Graphics g)
        {
            using(var b=new SolidBrush(Fill))g.FillRectangle(b,ClientRectangle);
            g.SmoothingMode=SmoothingMode.AntiAlias;Theme.Bezel(g,new RectangleF(.5f,.5f,Width-1,Height-1),14*DeviceDpi/96f,Fill);
        }
        protected override void OnPaint(PaintEventArgs e)
        {e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Theme.Bezel(e.Graphics,new RectangleF(.5f,.5f,Width-1,Height-1),14*DeviceDpi/96f,Fill);base.OnPaint(e);}
    }
    public sealed class BufferedTableLayout : TableLayoutPanel
    {
        public BufferedTableLayout(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBehind(this,e.Graphics);}
    }
    public sealed class BufferedFlowLayout : FlowLayoutPanel
    {
        public BufferedFlowLayout(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBehind(this,e.Graphics);}
    }
    public class RoundButton : Button
    {
        public bool Primary;
        bool hover,pressed;
        public RoundButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw|ControlStyles.Opaque,true);BackColor=Theme.Surface;Cursor=Cursors.Hand;Font=AppFonts.Create(10,FontStyle.Bold);}
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Opaque owner drawing: OnPaint owns the complete frame, even when
            // Windows skips WM_ERASEBKGND during hover/focus/resize invalidation.
        }
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.PaintBehind(this,e.Graphics);
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float z=g.DpiX/96f;
            var fill=!Enabled?Color.FromArgb(13,28,48):Primary?(hover?Color.FromArgb(35,115,202):Color.FromArgb(24,81,156)):(hover?Color.FromArgb(24,61,104):Color.FromArgb(13,37,69));
            if(Width<8||Height<8)return;
            float top=pressed&&Enabled?3*z:1.5f*z;
            // Supersample geometry only; text is drawn at native resolution below.
            using(var layer=new Bitmap(Width*3,Height*3,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using(var hi=Graphics.FromImage(layer))
                {
                    hi.ScaleTransform(3,3);hi.SmoothingMode=SmoothingMode.AntiAlias;hi.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    float radius=Math.Min(12*z,(Height-6*z)/2);
                    Theme.FillRound(hi,new RectangleF(1.5f*z,3.5f*z,Width-3*z,Height-5*z),radius,Color.FromArgb(170,0,0,0),Color.Transparent);
                    Theme.FillRound(hi,new RectangleF(1.5f*z,top,Width-3*z,Height-6*z),radius,fill,Primary?Theme.Cyan:Theme.Edge);
                }
                var state=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                g.DrawImage(layer,new Rectangle(0,0,Width,Height),0,0,layer.Width,layer.Height,GraphicsUnit.Pixel);g.Restore(state);
            }
            var textRect=new Rectangle(0,(int)top,Width,Height-(int)(5*z));
            if(Image!=null)
            {
                float side=Math.Min(36*z,Height-14*z),scale=Math.Min(side/Image.Width,side/Image.Height);
                var icon=new RectangleF(14*z+(side-Image.Width*scale)/2,(Height-Image.Height*scale)/2,Image.Width*scale,Image.Height*scale);
                g.DrawImage(Image,icon);textRect.X=(int)(side+26*z);textRect.Width=Width-textRect.X-(int)(10*z);
            }
            UiRendering.Text(g,Text,Font,textRect,Enabled?Theme.Text:Theme.Muted,Image==null);
            if(Focused&&ShowFocusCues)using(var p=new Pen(Theme.Cyan)){using(var path=Theme.Round(new RectangleF(5*z,top+3*z,Width-10*z,Height-12*z),8*z))g.DrawPath(p,path);}
        }
    }
    public sealed class ToggleButton : CheckBox
    {
        public ToggleButton(){AutoSize=false;Appearance=Appearance.Button;SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);BackColor=Color.Transparent;Cursor=Cursors.Hand;}
        protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(Theme.Tile);g.SmoothingMode=SmoothingMode.AntiAlias;float z=DeviceDpi/96f;
            UiRendering.Text(g,Text,Font,new Rectangle(0,0,Width-(int)(44*z),Height),Enabled?Theme.Text:Theme.Muted,false);
            var track=new RectangleF(Width-39*z,(Height-21*z)/2,38*z,21*z);
            Theme.FillRound(g,track,7*z,Checked&&Enabled?Color.FromArgb(32,116,190):Color.FromArgb(24,44,69),Theme.Edge);
            using(var b=new SolidBrush(Checked?Theme.Text:Theme.Muted))g.FillEllipse(b,track.X+(Checked?20:3)*z,track.Y+3*z,15*z,15*z);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle,Theme.Cyan,Theme.Surface);
        }
    }
    public sealed class FairyPanelButton : Control
    {
        public FairyPanelButton()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Opaque|ControlStyles.Selectable,true);
            BackColor=Theme.Surface;Cursor=Cursors.Hand;AccessibleName="打开签到面板";AccessibleRole=AccessibleRole.PushButton;TabStop=true;
        }
        public void PerformClick(){if(CanSelect)OnClick(EventArgs.Empty);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)Focus();base.OnMouseDown(e);}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){e.Handled=true;e.SuppressKeyPress=true;PerformClick();}
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);if(Width<1||Height<1)return;
            using(var shape=new GraphicsPath())
            {
                shape.AddEllipse(0,0,Width,Height);var previous=Region;Region=new Region(shape);if(previous!=null)previous.Dispose();
            }
        }
        protected override AccessibleObject CreateAccessibilityInstance(){return new EyeAccessibility(this);}
        sealed class EyeAccessibility : ControlAccessibleObject
        {
            readonly FairyPanelButton eye;
            public EyeAccessibility(FairyPanelButton eye):base(eye){this.eye=eye;}
            public override string DefaultAction{get{return "打开签到面板";}}
            public override void DoDefaultAction(){eye.PerformClick();}
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;var dialogue=Parent as FairyDialogue;
            // Paint a complete frame ourselves; no native button background or
            // simulated transparency may leave a rectangular blue layer behind.
            if(dialogue!=null)dialogue.PaintBackdrop(g,Location);else g.Clear(Theme.Surface);
            UiRendering.Quality(g);
            FairyArt.Eye(g,new RectangleF(0,0,Width,Height),0,PointF.Empty,false);
            if(Focused&&ShowFocusCues)using(var pen=new Pen(Color.FromArgb(120,Theme.Muted),DeviceDpi/96f))g.DrawEllipse(pen,2,2,Width-5,Height-5);
        }
    }
    public sealed class HeyboxTile : Control
    {
        public readonly ToggleButton SignSwitch=new ToggleButton();
        public HeyboxTile()
        {
            DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;
            Dock=DockStyle.Fill;Margin=new Padding(6);AccessibleName="小黑盒签到设置";
            SignSwitch.Text="每日签到";SignSwitch.AccessibleName="小黑盒每日签到开关";Controls.Add(SignSwitch);
        }
        protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBehind(this,e.Graphics);}
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);LayoutSwitches();
        }
        protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);LayoutSwitches();Invalidate();}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);LayoutSwitches();}
        void LayoutSwitches(){float z=DeviceDpi/96f;int pad=(int)(14*z);SignSwitch.SetBounds(pad,Height-(int)(35*z),Math.Max(1,Width-pad*2),(int)(28*z));}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;UiRendering.Quality(g);float z=DeviceDpi/96f;
            Theme.Bezel(g,new RectangleF(.5f,.5f,Width-1,Height-1),12*z,Theme.Tile);Theme.Checker(g,Width-44*z,9*z,6,4*z,Theme.Edge);
            using(var p=new Pen(Theme.Edge))g.DrawLine(p,14*z,65*z,Width-14*z,65*z);
            var icon=new RectangleF(14*z,12*z,44*z,44*z);var saved=g.Save();using(var path=Theme.Round(icon,10*z)){g.SetClip(path);g.DrawImage(AppAssets.Image("heybox"),icon);}g.Restore(saved);
            using(var f=AppFonts.Create(10,FontStyle.Bold))UiRendering.Text(g,"小黑盒",f,new Rectangle((int)(68*z),(int)(14*z),Width-(int)(76*z),(int)(42*z)),Theme.Text,false,true);
        }
    }
    public sealed class GameTile : Control
    {
        public readonly string Key,ForumId; public readonly ToggleButton GameSwitch,ForumSwitch;
        readonly Image image; readonly string title;
        public GameTile(string key,string forumId,string title,bool gameOn,bool forumOn)
        {
            Key=key;ForumId=forumId;this.title=title;image=AppAssets.Image(key);DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;
            if(key!="miyoushe"){GameSwitch=new ToggleButton{Text="游戏福利",Checked=gameOn,AccessibleName=title+"游戏福利开关"};Controls.Add(GameSwitch);}
            ForumSwitch=new ToggleButton{Text="社区签到",Checked=forumOn,AccessibleName=title+"社区签到开关"};Controls.Add(ForumSwitch);
            AccessibleName=title+"签到设置";Dock=DockStyle.Fill;Margin=new Padding(6);
        }
        protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBehind(this,e.Graphics);}
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);LayoutSwitches();
        }
        protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);LayoutSwitches();Invalidate();}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);LayoutSwitches();}
        void LayoutSwitches()
        {
            float z=DeviceDpi/96f;int pad=(int)(14*z),row=(int)(28*z),width=Math.Max(1,Width-pad*2);
            if(GameSwitch!=null)GameSwitch.SetBounds(pad,Height-row*2-(int)(9*z),width,row);
            if(ForumSwitch!=null)ForumSwitch.SetBounds(pad,Height-row-(int)(7*z),width,row);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;UiRendering.Quality(g);float z=DeviceDpi/96f;
            Theme.Bezel(g,new RectangleF(.5f,.5f,Width-1,Height-1),12*z,Theme.Tile);
            Theme.Checker(g,Width-44*z,9*z,6,4*z,Theme.Edge);
            using(var p=new Pen(Theme.Edge))g.DrawLine(p,14*z,65*z,Width-14*z,65*z);
            var r=new RectangleF(14*z,12*z,44*z,44*z);var saved=g.Save();using(var path=Theme.Round(r,12*z)){g.SetClip(path);g.DrawImage(image,r);}g.Restore(saved);
            using(var f=AppFonts.Create(10,FontStyle.Bold))UiRendering.Text(g,title,f,new Rectangle((int)(68*z),(int)(14*z),Width-(int)(76*z),(int)(42*z)),Theme.Text,false,true);
        }
    }
}
