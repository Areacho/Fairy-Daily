using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiyouDaily
{
    // Keep native menu navigation, accessibility and monitor-edge placement.
    public sealed class FairyContextMenu : ContextMenuStrip
    {
        public FairyContextMenu()
        {
            ShowImageMargin=false;ShowCheckMargin=false;ShowItemToolTips=false;
            BackColor=Theme.Surface;ForeColor=Theme.Text;Font=AppFonts.Create(10);
            Renderer=new FairyMenuRenderer();Padding=new Padding(8);Opacity=Theme.PopupOpacity;
            Opening+=(s,e)=>PrepareLayout();
        }
        public override Size GetPreferredSize(Size proposedSize)
        {
            var preferred=base.GetPreferredSize(proposedSize);int inset=WindowLayout.Pixels(8,DeviceDpi);
            return new Size(WindowLayout.Pixels(280,DeviceDpi),preferred.Height+inset*2-Padding.Vertical);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);int inset=WindowLayout.Pixels(8,DeviceDpi),delta=inset-Padding.Top;
            // The framework reserves a trailing arrow gutter even when image
            // margins are disabled. Stretch rows across that gutter, retaining
            // its vertical layout and native scrolling on smaller work areas.
            foreach(ToolStripItem item in Items)
            {
                var bounds=new Rectangle(inset,item.Bounds.Y+delta,Math.Max(1,Width-inset*2),item.Height);
                var action=item as FairyMenuItem;var header=item as FairyMenuHeader;var separator=item as FairyMenuSeparator;
                if(action!=null)action.Arrange(bounds);else if(header!=null)header.Arrange(bounds);else if(separator!=null)separator.Arrange(bounds);
            }
        }
        internal void PrepareLayout()
        {
            float z=DeviceDpi/96f;Padding=new Padding((int)(8*z));
            foreach(ToolStripItem item in Items)
            {
                item.Margin=Padding.Empty;
                if(item is ToolStripSeparator){item.AutoSize=false;item.Size=new Size((int)(264*z),(int)(12*z));}
            }
            PerformLayout();RoundWindow();
        }
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);RoundWindow();}
        void RoundWindow()
        {
            if(Width<2||Height<2)return;
            using(var path=Theme.Round(new RectangleF(0,0,Width,Height),15*DeviceDpi/96f))
            {var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}
        }
    }
    public sealed class FairyMenuItem : ToolStripMenuItem
    {
        public string Symbol {get;set;}
        public string Hint {get;set;}
        public bool Primary {get;set;}
        public bool Danger {get;set;}
        public FairyMenuItem(string text,string symbol,EventHandler click=null):base(text,null,click)
        {Symbol=symbol;AccessibleName=text;}
        public override Size GetPreferredSize(Size constraintSize)
        {float z=(Owner==null?96:Owner.DeviceDpi)/96f;return new Size((int)(264*z),(int)(42*z));}
        protected override void SetBounds(Rectangle bounds)
        {
            // MenuItem subtracts its owner's text gutter from X; our rows
            // already include their own icon area and need the actual inset.
            if(Owner is ToolStripDropDownMenu)bounds.X+=Owner.Padding.Left;
            base.SetBounds(bounds);
        }
        internal void Arrange(Rectangle bounds){SetBounds(bounds);}
    }
    public sealed class FairyMenuHeader : ToolStripLabel
    {
        public FairyMenuHeader():base("FAIRY · H.D.D."){Enabled=false;AccessibleName="Fairy 桌面事务控制";}
        public override Size GetPreferredSize(Size constraintSize)
        {float z=(Owner==null?96:Owner.DeviceDpi)/96f;return new Size((int)(264*z),(int)(64*z));}
        internal void Arrange(Rectangle bounds){SetBounds(bounds);}
    }
    public sealed class FairyMenuSeparator : ToolStripSeparator
    {
        internal void Arrange(Rectangle bounds){SetBounds(bounds);}
    }
    public sealed class FairyMenuRenderer : ToolStripProfessionalRenderer
    {
        public FairyMenuRenderer(){RoundedEdges=false;}
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {UiRendering.Quality(e.Graphics);Theme.HddBackground(e.Graphics,e.AffectedBounds);}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            UiRendering.Quality(e.Graphics);float z=e.ToolStrip.DeviceDpi/96f;
            using(var path=Theme.Round(new RectangleF(.5f,.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),15*z))
            using(var pen=new Pen(Theme.Edge))e.Graphics.DrawPath(pen,path);
        }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){}
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item=e.Item as FairyMenuItem;if(item==null){base.OnRenderMenuItemBackground(e);return;}
            UiRendering.Quality(e.Graphics);float z=e.ToolStrip.DeviceDpi/96f;
            var bounds=new RectangleF(1,2*z,e.Item.Width-2,e.Item.Height-4*z);
            Color fill=item.Danger?Color.FromArgb(32,22,43):item.Primary?Color.FromArgb(18,62,111):Color.FromArgb(11,30,57);
            if(item.Selected&&item.Enabled)fill=item.Danger?Color.FromArgb(69,35,57):Color.FromArgb(26,74,123);
            Theme.FillRound(e.Graphics,bounds,8*z,fill,item.Selected&&item.Enabled?Theme.Cyan:item.Primary?Theme.Edge:Color.Transparent);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var g=e.Graphics;UiRendering.Quality(g);float z=e.ToolStrip.DeviceDpi/96f;
            if(e.Item is FairyMenuHeader)
            {
                FairyArt.Eye(g,new RectangleF(5*z,4*z,48*z,48*z),0,PointF.Empty,false);
                using(var font=AppFonts.Create(12,FontStyle.Bold))UiRendering.Text(g,"FAIRY · H.D.D.",font,new Rectangle((int)(58*z),(int)(6*z),(int)(202*z),(int)(25*z)),Theme.Text,false);
                using(var font=AppFonts.Create(8.5f))UiRendering.Text(g,"桌面事务控制 / DAILY COMMISSION",font,new Rectangle((int)(58*z),(int)(33*z),(int)(204*z),(int)(21*z)),Theme.Muted,false);
                return;
            }
            var item=e.Item as FairyMenuItem;
            if(item==null){UiRendering.Text(g,e.Text,e.TextFont,e.TextRectangle,e.TextColor,false);return;}
            Color color=!item.Enabled?Theme.Muted:item.Danger?Color.FromArgb(255,172,190):Theme.Text;
            DrawSymbol(g,item.Symbol,new RectangleF(12*z,11*z,20*z,20*z),item.Enabled?(item.Danger?color:Theme.Cyan):Theme.Muted);
            bool hasArrow=item.HasDropDownItems;string hint=item.Hint??"";
            int right=(int)((hasArrow?24:12)*z),badge=hint.Length==0?0:(int)((hint.Length*11+18)*z);
            UiRendering.Text(g,item.Text,e.TextFont,new Rectangle((int)(44*z),0,Math.Max(1,item.Width-(int)(44*z)-right-badge),item.Height),color,false);
            if(badge>0)
            {
                var r=new RectangleF(item.Width-right-badge,11*z,badge,20*z);
                Theme.FillRound(g,r,6*z,item.Checked?Color.FromArgb(26,78,117):Color.FromArgb(12,28,50),Color.Transparent);
                using(var font=AppFonts.Create(8))UiRendering.Text(g,hint,font,Rectangle.Round(r),item.Checked?Theme.Cyan:Theme.Muted,true);
            }
            if(item.Checked&&hint.Length==0)
                using(var pen=new Pen(Theme.Cyan,1.7f*z))g.DrawLines(pen,new[]{new PointF(item.Width-27*z,21*z),new PointF(item.Width-23*z,25*z),new PointF(item.Width-16*z,17*z)});
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            float z=e.ToolStrip.DeviceDpi/96f;using(var pen=new Pen(Color.FromArgb(80,Theme.Edge)))
                e.Graphics.DrawLine(pen,12*z,e.Item.Height/2f,e.Item.Width-12*z,e.Item.Height/2f);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            UiRendering.Quality(e.Graphics);float z=e.Item.Owner.DeviceDpi/96f,x=e.Item.Width-14*z,y=e.Item.Height/2f;
            using(var pen=new Pen(Theme.Muted,1.5f*z))e.Graphics.DrawLines(pen,new[]{new PointF(x-3*z,y-4*z),new PointF(x+1*z,y),new PointF(x-3*z,y+4*z)});
        }
        static void DrawSymbol(Graphics g,string symbol,RectangleF bounds,Color color)
        {
            var saved=g.Save();g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/24,bounds.Height/24);
            using(var p=new Pen(color,1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
            {
                switch(symbol)
                {
                    case "check":g.DrawEllipse(p,2,2,20,20);g.DrawLines(p,new[]{new PointF(7,12),new PointF(10.5f,15.5f),new PointF(17,8.5f)});break;
                    case "panel":g.DrawRectangle(p,2,3,20,18);g.DrawLine(p,2,9,22,9);g.DrawLine(p,9,9,9,21);break;
                    case "config":for(int i=0;i<3;i++){float y=5+7*i,x=i==1?15:8;g.DrawLine(p,2,y,22,y);using(var b=new SolidBrush(Theme.Surface))g.FillEllipse(b,x-2.5f,y-2.5f,5,5);g.DrawEllipse(p,x-2.5f,y-2.5f,5,5);}break;
                    case "timer":g.DrawEllipse(p,2,2,20,20);g.DrawLines(p,new[]{new PointF(12,6),new PointF(12,12),new PointF(16,14)});break;
                    case "pin":g.DrawPolygon(p,new[]{new PointF(8,3),new PointF(19,3),new PointF(17,8),new PointF(20,12),new PointF(11,17),new PointF(9,12),new PointF(5,9)});g.DrawLine(p,10,14,4,21);break;
                    case "pause":g.DrawRectangle(p,5,4,4,16);g.DrawRectangle(p,15,4,4,16);break;
                    case "size":g.DrawRectangle(p,3,8,13,13);g.DrawLines(p,new[]{new PointF(10,3),new PointF(21,3),new PointF(21,14)});g.DrawLine(p,13,11,21,3);break;
                    case "tray":g.DrawLines(p,new[]{new PointF(3,14),new PointF(3,21),new PointF(21,21),new PointF(21,14)});g.DrawLine(p,12,2,12,15);g.DrawLines(p,new[]{new PointF(7,10),new PointF(12,15),new PointF(17,10)});break;
                    case "power":g.DrawArc(p,3,3,18,18,-50,280);g.DrawLine(p,12,1,12,11);break;
                    default:g.DrawRectangle(p,3,3,7,7);g.DrawRectangle(p,14,3,7,7);g.DrawRectangle(p,3,14,7,7);g.DrawRectangle(p,14,14,7,7);break;
                }
            }
            g.Restore(saved);
        }
    }
}
