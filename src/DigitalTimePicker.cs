using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed class DigitalTimePicker : Control
    {
        readonly ToolTip help=new ToolTip();
        DateTime value=DateTime.Today.AddHours(9);
        int selectedPart,typedDigits,typedNumber;
        bool hover;
        float DisplayScale{get{return Math.Max(.1f,Math.Min(Width/206f,Height/64f));}}
        public event EventHandler ValueChanged;
        public DateTime Value
        {
            get{return value;}
            set
            {
                var next=value.Date.AddHours(value.Hour).AddMinutes(value.Minute);
                if(this.value==next)return;this.value=next;Text=next.ToString("HH:mm");Invalidate();
                if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);
            }
        }
        internal int SelectedPart{get{return selectedPart;}set{selectedPart=value==0?0:1;typedDigits=0;Invalidate();}}
        public DigitalTimePicker()
        {
            DoubleBuffered=true;SetStyle(ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor,true);
            BackColor=Color.Transparent;Size=new Size(206,64);TabStop=true;Cursor=Cursors.Hand;
            AccessibleName="每日签到时间";AccessibleRole=AccessibleRole.SpinButton;Text=value.ToString("HH:mm");
            help.SetToolTip(this,"点击小时或分钟后输入数字；↑↓ 或滚轮调整，←→ 切换。");
        }
        protected override void Dispose(bool disposing){if(disposing)help.Dispose();base.Dispose(disposing);}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);typedDigits=0;Invalidate();}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();float z=DisplayScale;
            if(e.X>=Width-39*z){Step(e.Y<Height/2?1:-1);return;}
            SelectedPart=e.X<70*z?0:1;
        }
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(Focused)Step(e.Delta>0?1:-1);}
        protected override bool IsInputKey(Keys keyData)
        {var key=keyData&Keys.KeyCode;return key==Keys.Up||key==Keys.Down||key==Keys.Left||key==Keys.Right||base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){SelectedPart=e.KeyCode==Keys.Left?0:1;e.Handled=true;}
            if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down){Step(e.KeyCode==Keys.Up?1:-1);e.Handled=true;}
        }
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);if(e.KeyChar==':'){SelectedPart=1;e.Handled=true;return;}
            if(e.KeyChar>='0'&&e.KeyChar<='9'){EnterDigit(e.KeyChar-'0');e.Handled=true;}
        }
        internal void EnterDigit(int digit)
        {
            int limit=selectedPart==0?24:60;int next=typedDigits==0?digit:typedNumber*10+digit;
            if(next>=limit){typedDigits=0;next=digit;}
            typedNumber=next;typedDigits++;
            Value=selectedPart==0?value.Date.AddHours(next).AddMinutes(value.Minute):value.Date.AddHours(value.Hour).AddMinutes(next);
            if(typedDigits>=2){SelectedPart=selectedPart==0?1:0;}
        }
        internal void Step(int delta)
        {
            typedDigits=0;
            if(selectedPart==0)Value=value.Date.AddHours(((value.Hour+delta)%24+24)%24).AddMinutes(value.Minute);
            else Value=value.Date.AddHours(value.Hour).AddMinutes(((value.Minute+delta)%60+60)%60);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;UiRendering.Quality(g);float z=DisplayScale;
            Theme.FillRound(g,new RectangleF(.5f,.5f,Width-1,Height-1),14*z,Theme.Background,Focused?Theme.Cyan:hover?Color.FromArgb(70,128,158):Theme.Edge);
            Theme.FillRound(g,new RectangleF(5*z,5*z,Width-10*z,Height-10*z),10*z,Theme.Tile,Theme.Edge);
            using(var f=AppFonts.Create(7.5f*z*96/UiRendering.PixelDpi(g)))
            {
                UiRendering.Text(g,"DAILY TIMER",f,new Rectangle((int)(15*z),(int)(5*z),(int)(87*z),(int)(14*z)),Theme.Muted,false);
                UiRendering.Text(g,"UTC +08",f,new Rectangle((int)(105*z),(int)(5*z),(int)(54*z),(int)(14*z)),Theme.Muted,false);
            }
            string digits=value.ToString("HHmm");float[] positions={16,40,80,104};
            for(int i=0;i<4;i++)Digit(g,digits[i]-'0',positions[i]*z,23*z,z);
            using(var b=new SolidBrush(Enabled?Theme.Cyan:Theme.Muted)){g.FillEllipse(b,68*z,32*z,3*z,3*z);g.FillEllipse(b,68*z,44*z,3*z,3*z);}
            if(Focused)using(var p=new Pen(Color.FromArgb(150,Theme.Cyan),1.5f*z)){float x=(selectedPart==0?16:80)*z;g.DrawLine(p,x,59*z,x+42*z,59*z);}
            float arrowX=Width-34*z;
            Theme.FillRound(g,new RectangleF(arrowX,9*z,25*z,21*z),6*z,Theme.Surface,Theme.Edge);
            Theme.FillRound(g,new RectangleF(arrowX,35*z,25*z,21*z),6*z,Theme.Surface,Theme.Edge);
            using(var p=new Pen(Enabled?Theme.Cyan:Theme.Muted,1.5f*z))
            {float x=arrowX+12.5f*z;g.DrawLines(p,new[]{new PointF(x-4*z,21*z),new PointF(x,17*z),new PointF(x+4*z,21*z)});g.DrawLines(p,new[]{new PointF(x-4*z,43*z),new PointF(x,47*z),new PointF(x+4*z,43*z)});}
        }
        static readonly int[] Masks={0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
        static readonly PointF[][] Segments={
            new[]{new PointF(3,0),new PointF(15,0),new PointF(17,2),new PointF(15,4),new PointF(3,4),new PointF(1,2)},
            new[]{new PointF(16,3),new PointF(18,5),new PointF(18,13),new PointF(16,15),new PointF(14,13),new PointF(14,5)},
            new[]{new PointF(16,17),new PointF(18,19),new PointF(18,27),new PointF(16,29),new PointF(14,27),new PointF(14,19)},
            new[]{new PointF(3,28),new PointF(15,28),new PointF(17,30),new PointF(15,32),new PointF(3,32),new PointF(1,30)},
            new[]{new PointF(0,19),new PointF(2,17),new PointF(4,19),new PointF(4,27),new PointF(2,29),new PointF(0,27)},
            new[]{new PointF(0,5),new PointF(2,3),new PointF(4,5),new PointF(4,13),new PointF(2,15),new PointF(0,13)},
            new[]{new PointF(3,14),new PointF(15,14),new PointF(17,16),new PointF(15,18),new PointF(3,18),new PointF(1,16)}
        };
        void Digit(Graphics g,int number,float x,float y,float z)
        {
            var state=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(z,z);
            for(int i=0;i<Segments.Length;i++)
            {
                bool on=(Masks[number]&(1<<i))!=0;
                if(on&&Enabled)using(var glow=new Pen(Color.FromArgb(25,126,220,255),2.5f))g.DrawPolygon(glow,Segments[i]);
                using(var b=new SolidBrush(on?(Enabled?Color.FromArgb(155,233,255):Theme.Muted):Color.FromArgb(28,43,43)))g.FillPolygon(b,Segments[i]);
            }
            g.Restore(state);
        }
    }
}
