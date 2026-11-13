// Native adaptation of Fairy-DSH's eye geometry and layered motion.
// Reference: Chengzhibense/Fairy-DSH, Apache-2.0. See FAIRY-NOTICE.txt.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    public static class FairyArt
    {
        public static void FlareEye(Graphics g,RectangleF bounds,double seconds,PointF look,bool busy,float reveal)
        {
            reveal=Math.Max(0,Math.Min(1,reveal));if(reveal<=0)return;
            if(reveal>=1){Eye(g,bounds,seconds,look,busy);return;}
            var saved=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;
            float cx=bounds.X+bounds.Width/2,cy=bounds.Y+bounds.Height/2;
            float opening=Math.Max(0,(reveal-.28f)/.72f);opening=opening*opening*(3-2*opening);
            if(opening>0)
            {
                var iris=g.Save();g.TranslateTransform(cx,cy);g.ScaleTransform(1,Math.Max(.008f,opening));g.TranslateTransform(-cx,-cy);
                // Keep pose and rotation continuous, while suppressing the
                // idle glitch accent throughout the optical transition.
                Eye(g,bounds,seconds,look,busy,1,true);g.Restore(iris);
            }
            float strength=1-opening,stretch=Math.Min(1,reveal/.28f);
            stretch=stretch*stretch*(3-2*stretch);
            float width=Math.Max(1,bounds.Width*.82f*stretch),unit=bounds.Width/180f;
            for(int layer=5;layer>=0;layer--)
            {
                float thickness=(layer==0?1.1f:2+layer*2.4f)*unit;
                int alpha=(int)(strength*(layer==0?255:24));if(alpha<=0)continue;
                var line=new RectangleF(cx-width/2,cy-thickness/2,width,Math.Max(1,thickness));
                using(var brush=new LinearGradientBrush(line,Color.Transparent,Color.Transparent,0f))
                {
                    brush.InterpolationColors=new ColorBlend{Positions=new[]{0f,.16f,.5f,.84f,1f},Colors=new[]{Color.Transparent,Color.FromArgb(alpha/3,168,220,255),Color.FromArgb(alpha,255,255,255),Color.FromArgb(alpha/3,168,220,255),Color.Transparent}};
                    using(var pen=new Pen(brush,thickness){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawLine(pen,cx-width/2,cy,cx+width/2,cy);
                }
            }
            float glow=(3+6*(1-stretch))*unit;
            using(var path=new GraphicsPath())
            {
                path.AddEllipse(cx-glow*2,cy-glow,glow*4,glow*2);
                using(var brush=new PathGradientBrush(path)){brush.CenterColor=Color.FromArgb((int)(210*strength),245,252,255);brush.SurroundColors=new[]{Color.Transparent};g.FillPath(brush,path);}
            }
            using(var pen=new Pen(Color.FromArgb((int)(200*strength),255,255,255),Math.Max(.7f,unit)))g.DrawLine(pen,cx,cy-glow*.9f,cx,cy+glow*.9f);
            g.Restore(saved);
        }
        public static readonly Color Lime = Color.FromArgb(221, 248, 64), Cyan = Color.FromArgb(121, 225, 255);
        static void Disc(Graphics g, Color color, float x, float y, float r)
        { using (var b = new SolidBrush(color)) g.FillEllipse(b, x-r, y-r, r*2, r*2); }
        public static void Eye(Graphics g, RectangleF bounds, double seconds, PointF look, bool busy,float openness=1,bool quiet=false)
        {
            var saved = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(bounds.X+bounds.Width/2, bounds.Y+bounds.Height/2);
            g.ScaleTransform(bounds.Width/180, bounds.Height/180);
            g.TranslateTransform(0, (float)Math.Sin(seconds*1.4)*2);
            double t = seconds*(busy ? 1.7 : 1);
            for (int r=82; r>68; r--) using(var p=new Pen(Color.FromArgb((82-r)/2+1, 135, 198, 255), 2)) g.DrawEllipse(p,-r,-r,r*2,r*2);
            using(var b=new LinearGradientBrush(new Point(-50,-68),new Point(50,68),Color.FromArgb(64,83,240),Color.FromArgb(61,80,200))) g.FillEllipse(b,-68,-68,136,136);
            using(var p=new Pen(Color.FromArgb(222,240,255),1.2f))g.DrawEllipse(p,-68,-68,136,136);
            var rotation=g.Save(); g.RotateTransform((float)(t*24));
            Disc(g,Color.FromArgb(43,51,136),0,0,51.75f);
            using(var b=new SolidBrush(Color.FromArgb(43,51,136))) g.FillRectangle(b,-43,-43,86,86); g.Restore(rotation);
            float blink=1; double phase=seconds%7.3;
            if(phase>6.98) blink=(float)Math.Max(.07,Math.Abs((phase-7.14)/.16));
            var iris=g.Save(); g.ScaleTransform(1,blink*Math.Max(.02f,Math.Min(1,openness)));
            float breath=(float)((Math.Sin(t*Math.PI/ .72)+1)/2);
            Disc(g,Color.FromArgb(238,240,245),0,0,43*(1-.065f*breath));
            g.TranslateTransform(look.X*5,look.Y*5);
            Disc(g,Color.FromArgb(157,174,224),0,0,30*(1-.10f*breath));
            Disc(g,Color.FromArgb(239,243,250),0,0,22*(1-.12f*breath));
            Disc(g,Color.FromArgb(49,123,207),0,0,21.4f*(1-.12f*breath));
            Disc(g,Color.FromArgb(59,61,138),0,0,14.5f*(1-.15f*breath));
            for(int r=15;r>10;r--) Disc(g,Color.FromArgb(9,240,248,255),16,18,r);
            Disc(g,Color.FromArgb(245,248,253),16,18,10);
            g.Restore(iris);
            using(var clip=new GraphicsPath()) {clip.AddEllipse(-67,-67,134,134); var scan=g.Save();g.SetClip(clip);
                using(var p=new Pen(Color.FromArgb(15,201,248,255))) for(int y=-64;y<67;y+=4)g.DrawLine(p,-68,y,68,y);
                if(!quiet&&seconds%13.0>12.83) using(var p=new Pen(Color.FromArgb(160,145,239,255),2)) {g.DrawLine(p,-61,-19,51,-19);g.DrawLine(p,-45,21,65,21);}
                g.Restore(scan);
            }
            float pulse=(float)(seconds%4/4);
            using(var p=new Pen(Color.FromArgb((int)(65*(1-pulse)),FairyArt.Cyan),.8f)) {
                float r=71+pulse*14;g.DrawArc(p,-r,-r,r*2,r*2,30+(float)(t*8),78);g.DrawArc(p,-r,-r,r*2,r*2,210+(float)(t*8),78);
            }
            g.Restore(saved);
        }
    }
    public sealed class FairyHeader : Control
    {
        public FairyHeader(){DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;Dock=DockStyle.Fill;}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBehind(this,e.Graphics);}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;UiRendering.Quality(g);float z=DeviceDpi/96f;
            using(var f=AppFonts.Create(34,FontStyle.Bold))UiRendering.Text(g,"FAIRY",f,new Rectangle(0,0,(int)(180*z),(int)(62*z)),Theme.Text,false);
            using(var f=AppFonts.Create(8.5f))UiRendering.Text(g,"日常事务代理  /  DAILY COMMISSION",f,new Rectangle((int)(2*z),(int)(59*z),(int)(450*z),(int)(20*z)),Theme.Muted,false);
            using(var p=new Pen(Color.FromArgb(90,Theme.Cyan)))g.DrawLine(p,Width-174*z,18*z,Width-12*z,18*z);
            using(var f=AppFonts.Create(9))UiRendering.Text(g,"委托已接入 · 剩下的交给 Fairy",f,new Rectangle((int)(Width-285*z),(int)(40*z),(int)(285*z),(int)(24*z)),Theme.Muted,false);
            using(var p=new Pen(Theme.Edge))g.DrawLine(p,200*z,26*z,Width-198*z,26*z);
        }
    }
    public sealed class WidgetPreferences
    {
        public int X {get;set;} public int Y {get;set;} public int Size {get;set;}
        public int CoordinatesDpi {get;set;}
        public bool Topmost {get;set;} public bool Paused {get;set;}
        public WidgetPreferences(){X=int.MinValue;Y=int.MinValue;Size=250;Topmost=true;CoordinatesDpi=96;}
    }
    public sealed class FairyWidget : PassiveForm
    {
        readonly Store store; readonly MainForm panel; readonly Timer timer=new Timer();
        readonly Stopwatch clock=Stopwatch.StartNew(); readonly NotifyIcon tray;
        readonly FairyContextMenu menu=new FairyContextMenu(); WidgetPreferences prefs;
        readonly FairyEyeRenderer eyeRenderer=new FairyEyeRenderer();readonly LayeredFrame eyePresentation=new LayeredFrame();
        Point anchor, origin; bool dragging, moved,userHidden; double frozen; string lastSpeech=""; long nextSpeechPoll;
        internal int PresentedFrames{get;private set;}
        readonly GreetingSession greeting=new GreetingSession();
        readonly FairyDialogue dialogue; readonly Timer clickDelay=new Timer();
        FairyModuleForm module;bool showingCompletion;
        readonly Timer exitTimer=new Timer();FairyMessage exitNotice;double exitDeadline;
        internal const int AutoExitDelaySeconds=8;
        internal bool AutoExitPending {get{return exitNotice!=null;}}
        internal double AutoExitDeadline {get{return exitDeadline;}}
        readonly EyeLifecycle lifecycle=new EyeLifecycle();bool allowAnimatedClose;
        string activeProgress;double progressShownAt;
        IntPtr menuReturnWindow;bool menuInteraction;
        internal const double MinimumProgressSeconds=1.2;
        internal bool IsClosingEye {get{return lifecycle.Phase==EyePhase.Closing;}}
        public FairyWidget(Store store, MainForm panel)
        {
            this.store=store;this.panel=panel;panel.PlacementOwner=this;panel.DisplayOptionsChanged+=DisplayChanged;
            try {prefs=Json.Read<WidgetPreferences>(File.ReadAllText(Path.Combine(store.Root,"widget.json")));}catch{prefs=new WidgetPreferences();}
            if(prefs==null)prefs=new WidgetPreferences();prefs.Size=Math.Max(180,Math.Min(380,prefs.Size));
            Text="Fairy · 桌面挂件";Icon=AppAssets.Icon;Font=AppFonts.Create(10); FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
            AutoScaleMode=AutoScaleMode.None;int pixels=(int)Math.Round(prefs.Size*DeviceDpi/96f);Size=new Size(pixels,pixels);TopMost=prefs.Topmost;
            var area=Screen.PrimaryScreen.WorkingArea;
            float coordinateScale=DeviceDpi/(float)Math.Max(96,prefs.CoordinatesDpi);
            Location=prefs.X==int.MinValue?new Point(area.Right-Width-36,area.Bottom-Height-48):new Point((int)(prefs.X*coordinateScale),(int)(prefs.Y*coordinateScale)); Clamp();
            menu.Items.Add(new FairyMenuHeader());
            var run=new FairyMenuItem("立即签到","check",async(s,e)=>{CancelAutoExit();await panel.RunFromWidget();}){Primary=true};menu.Items.Add(run);
            menu.Items.Add(new FairyMenuItem("签到配置","config",(s,e)=>OpenModule(FairySection.Checkin)));
            menu.Items.Add(new FairyMenuItem("定时托管","timer",(s,e)=>OpenModule(FairySection.Schedule)));
            menu.Items.Add(new FairyMenuItem("快捷委托","shortcuts",(s,e)=>{CancelAutoExit();dialogue.OpenShortcuts(false);dialogue.ShowFor(this);}));
            menu.Items.Add(new FairyMenuItem("总控制面板","panel",(s,e)=>OpenPanel()));
            menu.Items.Add(new FairyMenuSeparator());
            var top=new FairyMenuItem("始终置顶","pin"){Checked=prefs.Topmost,CheckOnClick=true,Hint=prefs.Topmost?"已开启":"已关闭"};top.CheckedChanged+=(s,e)=>{TopMost=prefs.Topmost=top.Checked;top.Hint=top.Checked?"已开启":"已关闭";Save();};menu.Items.Add(top);
            var pause=new FairyMenuItem("暂停动画","pause"){Checked=prefs.Paused,CheckOnClick=true,Hint=prefs.Paused?"已暂停":"运行中"};pause.CheckedChanged+=(s,e)=>{frozen=clock.Elapsed.TotalSeconds;prefs.Paused=pause.Checked;pause.Hint=pause.Checked?"已暂停":"运行中";timer.Interval=prefs.Paused?500:33;Save();Render();};menu.Items.Add(pause);
            var sizes=new FairyMenuItem("挂件大小","size"){Hint=SizeLabel(prefs.Size)};var sizeMenu=new FairyContextMenu();sizes.DropDown=sizeMenu;
            foreach(int size in new[]{180,250,330}) {int chosen=size;var item=new FairyMenuItem(SizeLabel(size),"size",(s,e)=>{prefs.Size=chosen;sizes.Hint=SizeLabel(chosen);ResizeForDpi();Save();Render();}){Checked=prefs.Size==size};sizeMenu.Items.Add(item);}
            sizeMenu.Opening+=(s,e)=>{for(int i=0;i<sizeMenu.Items.Count;i++)((FairyMenuItem)sizeMenu.Items[i]).Checked=prefs.Size==new[]{180,250,330}[i];};menu.Items.Add(sizes);
            menu.Items.Add(new FairyMenuSeparator());
            menu.Items.Add(new FairyMenuItem("收起到托盘","tray",(s,e)=>HideToTray()));
            menu.Items.Add(new FairyMenuItem("退出挂件","power",(s,e)=>Close()){Danger=true});
            menu.Opening+=(s,e)=>{e.Cancel=IsClosingEye;CancelAutoExit();run.Enabled=!panel.IsWorking;if(!e.Cancel)BeginMenuInteraction();};
            menu.Closed+=(s,e)=>EndMenuInteraction(e.CloseReason);ContextMenuStrip=menu;
            dialogue=new FairyDialogue(panel,OpenPanel);dialogue.BindWidget(this);dialogue.Dismissed+=CancelAutoExit;panel.SpeechRequested+=Say;
            panel.ModuleRequested+=OpenModule;
            panel.DialogueDismissRequested+=HideDialogue;
            panel.CheckinCompleted+=CheckinFinished;
            try{var last=SpeechBus.Read(store);if(last!=null&&!last.IsCompletion&&!last.IsProgress)lastSpeech=last.Id;}catch{}
            clickDelay.Interval=SystemInformation.DoubleClickTime;clickDelay.Tick+=(s,e)=>{clickDelay.Stop();if(dialogue.Visible)dialogue.Dismiss();else{dialogue.OpenShortcuts(greeting.Begin());dialogue.ShowFor(this);}};
            tray=new NotifyIcon{Icon=AppAssets.Icon,Text="Fairy 日签 · 双击显示挂件",Visible=true,ContextMenuStrip=menu};
            tray.DoubleClick+=(s,e)=>RestoreFromTray();
            MouseDown+=(s,e)=>{if(IsClosingEye)return;CancelAutoExit();if(e.Button==MouseButtons.Left){clickDelay.Stop();anchor=Cursor.Position;origin=Location;dragging=true;WindowInteraction.SetMoving(this,true);moved=false;Capture=true;}};
            MouseMove+=(s,e)=>{if(dragging){var p=Cursor.Position;if(Math.Abs(p.X-anchor.X)>SystemInformation.DragSize.Width/2||Math.Abs(p.Y-anchor.Y)>SystemInformation.DragSize.Height/2)moved=true;if(moved)Location=new Point(origin.X+p.X-anchor.X,origin.Y+p.Y-anchor.Y);}};
            MouseUp+=(s,e)=>{if(dragging){dragging=false;WindowInteraction.SetMoving(this,false);Capture=false;if(moved){Clamp();Save();}else if(e.Button==MouseButtons.Left){clickDelay.Stop();clickDelay.Start();}}};
            MouseCaptureChanged+=(s,e)=>{if(dragging&&!Capture){dragging=false;WindowInteraction.SetMoving(this,false);}};
            MouseDoubleClick+=(s,e)=>{if(e.Button==MouseButtons.Left){clickDelay.Stop();moved=true;dialogue.OpenShortcuts(greeting.Begin());dialogue.ShowFor(this);}};
            timer.Interval=prefs.Paused?500:33;timer.Tick+=(s,e)=>Render();
            exitTimer.Interval=250;exitTimer.Tick+=(s,e)=>{try{ProcessAutoExit(clock.Elapsed.TotalSeconds,CheckinNotifications.DesktopAvailable());}catch(IOException){CancelAutoExit();}catch(InvalidOperationException){CancelAutoExit();}};
            Shown+=(s,e)=>{lifecycle.BeginOpening(clock.Elapsed.TotalSeconds);ResizeForDpi();Render();timer.Start();Save();};
            DpiChanged+=(s,e)=>{ResizeForDpi();Render();};
            FormClosed+=(s,e)=>{panel.AllowExit=true;panel.Close();};
            WindowLayout.Watch(this,()=>Size.Empty);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing){exitTimer.Dispose();timer.Dispose();clickDelay.Dispose();eyeRenderer.Dispose();eyePresentation.Dispose();if(module!=null)module.Dispose();if(dialogue!=null)dialogue.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}menu.Dispose();if(panel!=null){panel.DisplayOptionsChanged-=DisplayChanged;panel.SpeechRequested-=Say;panel.ModuleRequested-=OpenModule;panel.DialogueDismissRequested-=HideDialogue;panel.CheckinCompleted-=CheckinFinished;}}
            base.Dispose(disposing);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(e.CloseReason==CloseReason.UserClosing&&!IsClosingEye)WidgetSession.Close(store);
            WindowInteraction.SetMoving(this,false);
            if(!allowAnimatedClose&&Visible&&e.CloseReason==CloseReason.UserClosing)
            {
                e.Cancel=true;
                if(!IsClosingEye)
                {
                    CancelAutoExit();clickDelay.Stop();menu.Close();dialogue.HideNow();panel.Hide();if(module!=null&&!module.IsDisposed)module.Hide();
                    lifecycle.BeginClosing(clock.Elapsed.TotalSeconds);timer.Interval=16;timer.Start();Render();
                }
                return;
            }
            base.OnFormClosing(e);
        }
        internal bool AdvanceLifecycle(double now)
        {
            if(lifecycle.Advance(now)){allowAnimatedClose=true;Close();return true;}
            int interval=lifecycle.Phase==EyePhase.Opening||IsClosingEye?16:!Visible?750:prefs.Paused?500:33;
            if(timer.Interval!=interval)timer.Interval=interval;return false;
        }
        protected override void WndProc(ref Message message)
        {
            // A context menu is an explicit user interaction. Its owner must
            // be active before the menu captures input, so clicks in other
            // applications can dismiss it. Passive notifications stay unchanged.
            if(message.Msg==0x7B&&!IsClosingEye)BeginMenuInteraction();
            base.WndProc(ref message);
        }
        void BeginMenuInteraction()
        {
            if(!Visible||!IsHandleCreated)return;
            if(!menuInteraction){menuReturnWindow=GetForegroundWindow();menuInteraction=true;clickDelay.Stop();dialogue.HideNow();}
            SetForegroundWindow(Handle);
        }
        void EndMenuInteraction(ToolStripDropDownCloseReason reason)
        {
            IntPtr previous=menuReturnWindow;menuReturnWindow=IntPtr.Zero;menuInteraction=false;
            if(!IsHandleCreated||IsDisposed)return;
            PostMessage(Handle,0,IntPtr.Zero,IntPtr.Zero);
            if(previous==IntPtr.Zero||previous==Handle||reason==ToolStripDropDownCloseReason.AppClicked||reason==ToolStripDropDownCloseReason.AppFocusChange)return;
            BeginInvoke(new Action(()=>{if(!IsDisposed&&IsHandleCreated&&GetForegroundWindow()==Handle&&GetCapture()==IntPtr.Zero)SetForegroundWindow(previous);}));
        }
        public void Say(string message)
        {
            if(IsDisposed||IsClosingEye||userHidden||menuInteraction)return;if(InvokeRequired){BeginInvoke(new Action<string>(Say),message);return;}
            CancelAutoExit();
            if(!Visible){Show();timer.Interval=prefs.Paused?500:33;timer.Start();}dialogue.Say(message);dialogue.ShowFor(this);
        }
        internal void HideToTray(){userHidden=true;WindowInteraction.SetMoving(this,false);CancelAutoExit();Hide();dialogue.HideNow();timer.Interval=750;}
        internal void RestoreFromTray(){userHidden=false;CancelAutoExit();Show();timer.Interval=prefs.Paused?500:33;timer.Start();Render();}
        public void OpenPanel(){if(IsClosingEye)return;userHidden=false;CancelAutoExit();if(panel.IsDisposed)return;panel.WindowState=FormWindowState.Normal;WindowLayout.CenterOnScreen(panel,panel.MinimumLogical,this);panel.Show();panel.Activate();panel.Tell("主人，总控制面板已接入。全部委托、账号与运行记录都在这里。");}
        static string SizeLabel(int size){return size==180?"小号":size==250?"标准":size==330?"大号":"自定义";}
        void CheckinFinished(FairyMessage notice){DeliverCompletion(notice);}
        internal bool DeliverCompletion(FairyMessage notice,bool? desktopAvailable=null)
        {
            if(IsDisposed||IsClosingEye||userHidden||menuInteraction||showingCompletion||!SpeechBus.IsPending(store,notice,DateTimeOffset.UtcNow)||!(desktopAvailable??CheckinNotifications.DesktopAvailable()))return false;
            if(!string.IsNullOrEmpty(notice.RunId)&&notice.RunId==activeProgress&&clock.Elapsed.TotalSeconds-progressShownAt<MinimumProgressSeconds)return false;
            showingCompletion=true;
            try
            {
                if(!Visible){Show();timer.Interval=prefs.Paused?500:33;timer.Start();}
                CancelAutoExit();activeProgress=null;bool autoExit=panel.ShouldExitAfterCompletion(notice);
                string text=notice.Text+(autoExit?"\n8 秒后退出挂件，点击眼睛或关闭气泡可取消。":"");
                dialogue.Say(text,autoExit);dialogue.ShowFor(this);
                if(!dialogue.Visible)return false;SpeechBus.MarkDelivered(store,notice);lastSpeech=notice.Id;
                if(autoExit){dialogue.KeepNoticeOpen=true;exitNotice=notice;exitDeadline=clock.Elapsed.TotalSeconds+AutoExitDelaySeconds;exitTimer.Start();}return true;
            }
            finally{showingCompletion=false;}
        }
        internal void PollNotifications(bool? desktopAvailable=null)
        {
            if(IsClosingEye||userHidden||menuInteraction)return;
            var speech=SpeechBus.Read(store);if(speech==null)return;
            if(speech.IsCompletion)
            {
                var progress=SpeechBus.ReadProgress(store);
                if(progress!=null&&progress.RunId==speech.RunId&&DeliverProgress(progress,desktopAvailable))return;
                DeliverCompletion(speech,desktopAvailable);return;
            }
            if(speech.IsProgress){DeliverProgress(speech,desktopAvailable);return;}
            if(speech.Id==lastSpeech)return;lastSpeech=speech.Id;DateTimeOffset at;
            if(DateTimeOffset.TryParse(speech.At,out at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromMinutes(2))Say(speech.Text);
        }
        internal bool DeliverProgress(FairyMessage notice,bool? desktopAvailable=null)
        {
            if(IsDisposed||IsClosingEye||showingCompletion||!SpeechBus.IsProgressPending(store,notice,DateTimeOffset.UtcNow)||!(desktopAvailable??CheckinNotifications.DesktopAvailable()))return false;
            showingCompletion=true;
            try
            {
                Say(notice.Text);dialogue.KeepNoticeOpen=true;
                if(!dialogue.Visible)return false;
                activeProgress=notice.RunId;progressShownAt=clock.Elapsed.TotalSeconds;lastSpeech=notice.Id;SpeechBus.MarkDelivered(store,notice);return true;
            }
            finally{showingCompletion=false;}
        }
        internal void CancelAutoExit(){exitTimer.Stop();exitNotice=null;exitDeadline=0;if(dialogue!=null)dialogue.KeepNoticeOpen=false;}
        internal bool ProcessAutoExit(double now,bool desktopAvailable)
        {
            if(!AutoExitPending||IsDisposed)return false;
            if(!panel.ShouldExitAfterCompletion(exitNotice)){CancelAutoExit();return false;}
            if(!desktopAvailable||panel.IsWorking){exitDeadline=now+AutoExitDelaySeconds;return false;}
            if(now<exitDeadline)return false;
            // A separate scheduled worker may still own the account run lock.
            IDisposable lease;try{lease=store.AcquireRunLock();}catch(InvalidOperationException){exitDeadline=now+AutoExitDelaySeconds;return false;}
            using(lease){if(!panel.ShouldExitAfterCompletion(exitNotice)){CancelAutoExit();return false;}CancelAutoExit();Close();return true;}
        }
        void HideDialogue(){CancelAutoExit();clickDelay.Stop();dialogue.HideNow();}
        public void OpenModule(FairySection section)
        {
            if(IsClosingEye)return;
            userHidden=false;
            CancelAutoExit();
            if(panel.IsWorking){Say("主人，上一项委托还在处理，请稍候。");return;}
            if(module==null||module.IsDisposed){module=new FairyModuleForm(panel,section);module.Show(this);}else{module.WindowState=FormWindowState.Normal;module.Navigate(section,false);module.Show();}
            WindowLayout.CenterOnScreen(module,section==FairySection.MiyouTasks?new Size(820,600):new Size(520,280),this);
            module.Activate();string text=section==FairySection.Schedule?"主人，定时通道已展开。请设定您希望我开始工作的时间。":section==FairySection.Login?"主人，登录通道已就绪。请选择米游社或小黑盒完成授权。":"主人，签到通道已接入。选择平台，Fairy 随时待命。";Say(text);
        }
        void Clamp(){var a=Screen.FromRectangle(Bounds).WorkingArea;Location=new Point(Math.Max(a.Left,Math.Min(Left,a.Right-Width)),Math.Max(a.Top,Math.Min(Top,a.Bottom-Height)));}
        void ResizeForDpi(){int pixels=(int)Math.Round(prefs.Size*DeviceDpi/96f);Size=new Size(pixels,pixels);Clamp();}
        void Save(){prefs.X=Left;prefs.Y=Top;prefs.CoordinatesDpi=DeviceDpi;try{Store.AtomicWrite(Path.Combine(store.Root,"widget.json"),Json.Write(prefs));}catch(IOException){} }
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000;if(!ShowInTaskbar)p.ExStyle|=0x80;return p;}}
        void Render()
        {
            if(!IsClosingEye&&(WindowInteraction.IsMoving||menuInteraction))return;
            if(IsDisposed||AdvanceLifecycle(clock.Elapsed.TotalSeconds))return;
            if(!IsClosingEye&&clock.ElapsedMilliseconds>nextSpeechPoll)
            {
                nextSpeechPoll=clock.ElapsedMilliseconds+750;
                try{PollNotifications();}catch(IOException){}catch(ArgumentException){}
            }
            if(!Visible)return;
            var cursor=PointToClient(Cursor.Position);float lookDistance=WindowLayout.Pixels(200,DeviceDpi);var look=new PointF(Math.Max(-1,Math.Min(1,(cursor.X-Width/2f)/lookDistance)),Math.Max(-1,Math.Min(1,(cursor.Y-Height/2f)/lookDistance)));
            var bmp=eyeRenderer.Render(Size,prefs.Paused?frozen:clock.Elapsed.TotalSeconds,look,panel.IsWorking||activeProgress!=null,lifecycle.Openness,panel.DisplayOptions.EdgeQuality);
            Present(bmp,(byte)Math.Round(255*lifecycle.Alpha));
        }
        void DisplayChanged(){if(!IsDisposed)Render();}
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")]static extern IntPtr GetCapture();
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
        void Present(Bitmap bmp,byte alpha)
        {
            PresentedFrames++;
            eyePresentation.Present(Handle,bmp,Location,alpha);
        }
    }
}
