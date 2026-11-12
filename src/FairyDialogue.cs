using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed class FairyDialogue : PassiveForm
    {
        readonly Timer motion=new Timer(),idle=new Timer();
        readonly Stopwatch clock=new Stopwatch();
        readonly Stopwatch idleClock=new Stopwatch();
        readonly Label message;
        readonly TableLayoutPanel actions;
        readonly RoundButton loginAction,checkinAction,configAction,timerAction;
        readonly FairyPanelButton panelIcon;
        readonly ToolTip tips=new ToolTip();
        readonly MainForm panel;
        Form anchor;
        Point? noticeLocation;
        enum Mode{Greeting,Options,Notice,Closing}
        Mode mode;
        int targetHeight=126;
        bool compactGreeting,compactEye;
        public event Action Dismissed;
        internal bool KeepNoticeOpen {get;set;}
        double fromHeight,fromOpacity;
        public FairyDialogue(MainForm panel,Action openPanel,Func<System.Threading.Tasks.Task> runCheckin=null)
        {
            SuspendLayout();
            this.panel=panel;Text="Fairy · 快捷对话";Icon=AppAssets.Icon;Font=AppFonts.Create(10);
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Theme.Surface;DoubleBuffered=true;Opacity=Theme.PopupOpacity;
            ClientSize=new Size(480,126);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            var close=new RoundButton{Text="×",Size=new Size(29,29),Location=new Point(439,10),Anchor=AnchorStyles.Top|AnchorStyles.Right};close.Click+=(s,e)=>Dismiss();Controls.Add(close);
            message=new SmoothLabel{Location=new Point(24,30),Size=new Size(354,68),ForeColor=Theme.Text,BackColor=Color.Transparent,Font=AppFonts.Create(11,FontStyle.Bold)};Controls.Add(message);
            actions=new TableLayoutPanel{ColumnCount=3,RowCount=2,Location=new Point(22,14),Size=new Size(356,88),BackColor=Color.Transparent};
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));Controls.Add(actions);
            loginAction=Add("登录",()=>{Dismiss();panel.RequestModule(FairySection.Login);return System.Threading.Tasks.Task.FromResult(0);},0,0);actions.SetColumnSpan(loginAction,3);
            var execute=runCheckin??new Func<System.Threading.Tasks.Task>(panel.RunFromWidget);
            checkinAction=Add("签到",async()=>{Dismiss();await execute();},0,1);checkinAction.Primary=true;
            configAction=Add("配置",()=>{Dismiss();panel.RequestModule(FairySection.Checkin);return System.Threading.Tasks.Task.FromResult(0);},1,1);configAction.Font=AppFonts.Create(9,FontStyle.Bold);
            timerAction=Add("定时",()=>{Dismiss();panel.RequestModule(FairySection.Schedule);return System.Threading.Tasks.Task.FromResult(0);},2,1);timerAction.Font=AppFonts.Create(9,FontStyle.Bold);
            panelIcon=new FairyPanelButton();panelIcon.Click+=(s,e)=>{Dismiss();openPanel();};tips.SetToolTip(panelIcon,"打开总控制面板");Controls.Add(panelIcon);
            RefreshActions();
            motion.Interval=16;motion.Tick+=(s,e)=>Animate();
            idle.Interval=250;idle.Tick+=(s,e)=>{
                if(!Visible||mode==Mode.Greeting||mode==Mode.Closing||KeepNoticeOpen||panel.IsWorking||ClientRectangle.Contains(PointToClient(Cursor.Position))){idleClock.Restart();return;}
                if(idleClock.ElapsedMilliseconds>=12000)Dismiss();
            };
            KeyPreview=true;KeyDown+=(s,e)=>idleClock.Restart();
            VisibleChanged+=(s,e)=>{if(Visible){RefreshActions();clock.Restart();idleClock.Restart();motion.Start();idle.Start();}else{motion.Stop();idle.Stop();}};
            DpiChanged+=(s,e)=>BeginInvoke(new Action(()=>{LayoutMessage();LayoutActions(mode==Mode.Notice);RoundWindow();Reposition();}));
            ResumeLayout(true);WindowLayout.Watch(this,()=>Size.Empty);
        }
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80;return p;}}
        protected override void OnShown(EventArgs e){base.OnShown(e);LayoutMessage();LayoutActions(mode==Mode.Notice);RoundWindow();Reposition();}
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideNow();}
            base.OnFormClosing(e);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);if(IsHandleCreated)RoundWindow();
            if(panelIcon==null||message==null||actions==null)return;
            LayoutMessage();LayoutActions(!compactEye);
        }
        protected override void Dispose(bool disposing){if(disposing){motion.Dispose();idle.Dispose();tips.Dispose();}base.Dispose(disposing);}
        public void ShowFor(Form owner)
        {
            if(IsDisposed)return;PlaceNear(owner);
            if(Visible)return;
            if(owner==null)Show();else Show(owner);
        }
        RoundButton Add(string text,Func<System.Threading.Tasks.Task> action,int column,int row)
        {
            var button=new RoundButton{Text=text,Dock=DockStyle.Fill,Margin=new Padding(4),AccessibleName="Fairy快捷"+text};
            button.Click+=async(s,e)=>{idleClock.Restart();try{if(panel.IsWorking){Say("主人，当前任务正在执行，请稍候。");return;}await action();}catch(Exception){Say("主人，操作未完成，请打开签到面板查看。");}};
            actions.Controls.Add(button,column,row);
            return button;
        }
        void RefreshActions(){bool logged=panel.HasLogin;loginAction.Text=logged?"已登录":"登录";loginAction.AccessibleName="Fairy快捷"+loginAction.Text;loginAction.Enabled=!logged;checkinAction.Text="签到";}
        void Begin(Mode next,int height)
        {
            mode=next;compactGreeting=next==Mode.Greeting;compactEye=next==Mode.Greeting||next==Mode.Options;targetHeight=height;fromHeight=Visible?Height:(compactGreeting?58:next==Mode.Options?116:126)*DeviceDpi/96f;fromOpacity=Visible?Opacity:0;
            if(!Visible){Height=(int)fromHeight;Opacity=0;}
            LayoutMessage();LayoutActions(!compactEye);
            clock.Restart();idleClock.Restart();motion.Start();idle.Start();RoundWindow();Invalidate(true);
        }
        public void OpenShortcuts(bool greet)
        {
            RefreshActions();message.Visible=greet;message.Text="";actions.Visible=!greet;LayoutActions(false);
            Begin(greet?Mode.Greeting:Mode.Options,greet?58:116);
        }
        public void Say(string text,bool extended=false)
        {
            noticeLocation=null;
            RefreshActions();message.Text=text;message.Visible=true;actions.Visible=false;
            Begin(Mode.Notice,extended?164:126);
        }
        void LayoutActions(bool withMessage)
        {
            float z=DeviceDpi/96f;
            int left=(int)(22*z);
            int right=withMessage?ClientSize.Width-left:panelIcon.Left-(int)(10*z);
            actions.ColumnStyles[1].Width=70*z;actions.ColumnStyles[2].Width=70*z;
            configAction.Margin=timerAction.Margin=new Padding((int)(6*z),(int)(10*z),(int)(4*z),(int)(10*z));
            actions.SetBounds(left,(int)((withMessage?116:14)*z),Math.Max(1,right-left),(int)(88*z));
        }
        void LayoutMessage()
        {
            LayoutIcon();float z=DeviceDpi/96f;int left=(int)(24*z);
            message.SetBounds(left,(int)((compactGreeting?16:30)*z),Math.Max(1,panelIcon.Left-(int)(10*z)-left),(int)((compactGreeting?26:Math.Max(68,targetHeight-58))*z));
            message.TextAlign=compactGreeting?ContentAlignment.MiddleCenter:ContentAlignment.TopLeft;
        }
        void LayoutIcon()
        {
            if(panelIcon==null)return;float z=DeviceDpi/96f,size=(compactEye?42:69)*z;
            float y=compactEye?(Height-size)/2:33*z;
            panelIcon.SetBounds((int)(Width-(compactEye?90:97)*z),(int)y,(int)size,(int)size);
        }
        public void Dismiss()
        {
            if(!Visible)return;if(Dismissed!=null)Dismissed();fromOpacity=Opacity;mode=Mode.Closing;clock.Restart();idle.Stop();motion.Start();
        }
        public void HideNow()
        {
            if(IsDisposed)return;if(Dismissed!=null)Dismissed();motion.Stop();idle.Stop();clock.Reset();idleClock.Reset();Hide();
        }
        void Animate()
        {
            double ms=clock.Elapsed.TotalMilliseconds;
            if(mode==Mode.Closing){Opacity=Math.Max(0,fromOpacity*(1-ms/130));if(ms>=130)Hide();return;}
            if(mode==Mode.Greeting)
            {
                if(GreetingSession.StageAt(ms)==2){RefreshActions();message.Visible=false;actions.Visible=true;LayoutActions(false);Begin(Mode.Options,116);return;}
                message.Text=GreetingSession.VisibleTextAt(ms);
                double fade=ms<180?ms/180:ms>1290&&ms<1450?(1450-ms)/160:ms>=1450&&ms<1630?(ms-1450)/180:ms>3190?(3350-ms)/160:1;
                Opacity=Math.Max(.03,Math.Min(1,fade))*Theme.PopupOpacity;Invalidate();return;
            }
            double t=Math.Min(1,ms/210),ease=1-Math.Pow(1-t,3);
            Height=(int)(fromHeight+(targetHeight*DeviceDpi/96f-fromHeight)*ease);
            Opacity=Math.Max(.02,Math.Min(Theme.PopupOpacity,fromOpacity+(Theme.PopupOpacity-fromOpacity)*ease));
            RoundWindow();Reposition();Invalidate(true);
            if(t>=1){motion.Stop();idleClock.Restart();}
        }
        public void PlaceNear(Form widget){anchor=widget;noticeLocation=null;Reposition();}
        void Reposition()
        {
            if(anchor==null||anchor.IsDisposed)return;
            var area=Screen.FromRectangle(anchor.Bounds).WorkingArea;int gap=WindowLayout.Pixels(6,DeviceDpi),overlap=WindowLayout.Pixels(22,DeviceDpi);
            int left=anchor.Left-Width+overlap;if(left<area.Left+gap)left=anchor.Right-overlap;
            var proposed=new Point(Math.Max(area.Left+gap,Math.Min(left,area.Right-Width-gap)),Math.Max(area.Top+gap,Math.Min(anchor.Top+WindowLayout.Pixels(30,DeviceDpi),area.Bottom-Height-gap)));
            if(mode==Mode.Notice)
            {
                if(!noticeLocation.HasValue)noticeLocation=WindowLayout.PlaceNotice(new Rectangle(proposed,new Size(Width,WindowLayout.Pixels(targetHeight,DeviceDpi))),anchor.Bounds,area,Cursor.Position,DeviceDpi);
                Location=noticeLocation.Value;
            }
            else Location=proposed;
        }
        void RoundWindow(){if(Width<1||Height<1)return;using(var path=Theme.Round(new RectangleF(0,0,Width,Height),24*DeviceDpi/96f)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
        protected override void OnPaint(PaintEventArgs e)
        {
            PaintSurface(e.Graphics);
        }
        internal void PaintBackdrop(Graphics g,Point origin)
        {
            var saved=g.Save();
            try{g.TranslateTransform(-origin.X,-origin.Y);PaintSurface(g);}
            finally{g.Restore(saved);}
        }
        void PaintSurface(Graphics g)
        {
            g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float z=DeviceDpi/96f;
            using(var path=Theme.Round(new RectangleF(2,2,Width-4,Height-4),23*z))
            {
                var state=g.Save();g.SetClip(path,CombineMode.Intersect);Theme.HddBackground(g,ClientRectangle);g.Restore(state);
                using(var p=new Pen(Color.FromArgb(140,110,194,255),1.2f*z))g.DrawPath(p,path);
            }
        }
    }
}
