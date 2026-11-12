// Native adaptation of Fairy-DSH's eye geometry and layered motion.
// Reference: Chengzhibense/Fairy-DSH, Apache-2.0. See FAIRY-NOTICE.txt.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed class FairyMenuRenderer : ToolStripProfessionalRenderer
    {
        public FairyMenuRenderer():base(new FairyMenuColors()){}
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){UiRendering.Text(e.Graphics,e.Text,e.TextFont,e.TextRectangle,e.TextColor,false);}
    }
    public sealed class FairyMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return ToolStripDropDownBackground; } }
        public override Color ImageMarginGradientMiddle { get { return ToolStripDropDownBackground; } }
        public override Color ImageMarginGradientEnd { get { return ToolStripDropDownBackground; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(23,61,104); } }
        public override Color MenuItemBorder { get { return FairyArt.Cyan; } }
        public override Color MenuBorder { get { return Theme.Edge; } }
        public override Color CheckBackground { get { return Color.FromArgb(34,86,136); } }
    }
    public static class FairyArt
    {
        public static readonly Color Lime = Color.FromArgb(221, 248, 64), Cyan = Color.FromArgb(121, 225, 255);
        static void Disc(Graphics g, Color color, float x, float y, float r)
        { using (var b = new SolidBrush(color)) g.FillEllipse(b, x-r, y-r, r*2, r*2); }
        public static void Eye(Graphics g, RectangleF bounds, double seconds, PointF look, bool busy)
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
            var iris=g.Save(); g.ScaleTransform(1,blink);
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
                if(seconds%13.0>12.83) using(var p=new Pen(Color.FromArgb(160,145,239,255),2)) {g.DrawLine(p,-61,-19,51,-19);g.DrawLine(p,-45,21,65,21);}
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
            float badgeX=190*z;
            Theme.FillRound(g,new RectangleF(badgeX,13*z,100*z,27*z),4*z,Theme.Lime,Color.Black);
            using(var f=AppFonts.Create(10,FontStyle.Bold))UiRendering.Text(g,"H.D.D.",f,new Rectangle((int)badgeX,(int)(13*z),(int)(100*z),(int)(27*z)),Theme.Background,true);
            using(var p=new Pen(Color.FromArgb(90,Theme.Cyan)))g.DrawLine(p,Width-174*z,18*z,Width-12*z,18*z);
            using(var f=AppFonts.Create(9))UiRendering.Text(g,"委托已接入 · 剩下的交给 Fairy",f,new Rectangle((int)(Width-285*z),(int)(40*z),(int)(285*z),(int)(24*z)),Theme.Muted,false);
            using(var p=new Pen(Theme.Edge))g.DrawLine(p,315*z,26*z,Width-198*z,26*z);
        }
    }
    public sealed class WidgetPreferences
    {
        public int X {get;set;} public int Y {get;set;} public int Size {get;set;}
        public int CoordinatesDpi {get;set;}
        public bool Topmost {get;set;} public bool Paused {get;set;}
        public WidgetPreferences(){X=int.MinValue;Y=int.MinValue;Size=250;Topmost=true;CoordinatesDpi=96;}
    }
    public sealed class FairyWidget : Form
    {
        readonly Store store; readonly MainForm panel; readonly Timer timer=new Timer();
        readonly Stopwatch clock=Stopwatch.StartNew(); readonly NotifyIcon tray;
        readonly ContextMenuStrip menu=new ContextMenuStrip(); WidgetPreferences prefs;
        Point anchor, origin; bool dragging, moved; double frozen; string lastSpeech=""; long nextSpeechPoll;
        readonly GreetingSession greeting=new GreetingSession();
        readonly FairyDialogue dialogue; readonly Timer clickDelay=new Timer();
        FairyModuleForm module;
        public FairyWidget(Store store, MainForm panel)
        {
            this.store=store;this.panel=panel;
            try {prefs=Json.Read<WidgetPreferences>(File.ReadAllText(Path.Combine(store.Root,"widget.json")));}catch{prefs=new WidgetPreferences();}
            if(prefs==null)prefs=new WidgetPreferences();prefs.Size=Math.Max(180,Math.Min(380,prefs.Size));
            Text="Fairy · 桌面挂件";Icon=AppAssets.Icon;Font=AppFonts.Create(10); FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
            AutoScaleMode=AutoScaleMode.None;int pixels=(int)Math.Round(prefs.Size*DeviceDpi/96f);Size=new Size(pixels,pixels);TopMost=prefs.Topmost;
            var area=Screen.PrimaryScreen.WorkingArea;
            float coordinateScale=DeviceDpi/(float)Math.Max(96,prefs.CoordinatesDpi);
            Location=prefs.X==int.MinValue?new Point(area.Right-Width-36,area.Bottom-Height-48):new Point((int)(prefs.X*coordinateScale),(int)(prefs.Y*coordinateScale)); Clamp();
            menu.Items.Add("打开签到面板",null,(s,e)=>OpenPanel());
            menu.Items.Add("快捷委托",null,(s,e)=>{dialogue.OpenShortcuts(false);dialogue.ShowFor(this);});
            menu.Items.Add("定时托管",null,(s,e)=>OpenModule(FairySection.Schedule));
            menu.Items.Add(new ToolStripSeparator());
            var top=new ToolStripMenuItem("始终置顶"){Checked=prefs.Topmost,CheckOnClick=true};top.CheckedChanged+=(s,e)=>{TopMost=prefs.Topmost=top.Checked;Save();};menu.Items.Add(top);
            var pause=new ToolStripMenuItem("暂停动画"){Checked=prefs.Paused,CheckOnClick=true};pause.CheckedChanged+=(s,e)=>{frozen=clock.Elapsed.TotalSeconds;prefs.Paused=pause.Checked;timer.Interval=prefs.Paused?500:33;Save();Render();};menu.Items.Add(pause);
            var sizes=new ToolStripMenuItem("挂件大小");foreach(int size in new[]{180,250,330}) {int chosen=size;sizes.DropDownItems.Add(size==180?"小":size==250?"标准":"大",null,(s,e)=>{prefs.Size=chosen;ResizeForDpi();Save();Render();});}menu.Items.Add(sizes);
            menu.Items.Add("收起到托盘",null,(s,e)=>{Hide();dialogue.Hide();timer.Interval=750;});
            menu.Items.Add("退出挂件",null,(s,e)=>Close());
            ContextMenuStrip=menu;menu.Font=AppFonts.Create(10);
            menu.Renderer=new FairyMenuRenderer();menu.ForeColor=Color.FromArgb(236,240,248);
            foreach(ToolStripItem item in menu.Items){item.Font=menu.Font;item.ForeColor=menu.ForeColor;var sub=item as ToolStripMenuItem;if(sub!=null){sub.DropDown.Font=menu.Font;sub.DropDown.ForeColor=menu.ForeColor;sub.DropDown.Renderer=menu.Renderer;}}
            dialogue=new FairyDialogue(panel,OpenPanel);dialogue.TopMost=prefs.Topmost;panel.SpeechRequested+=Say;
            panel.ModuleRequested+=OpenModule;
            panel.DialogueDismissRequested+=HideDialogue;
            try{var last=SpeechBus.Read(store);if(last!=null)lastSpeech=last.Id;}catch{}
            clickDelay.Interval=SystemInformation.DoubleClickTime;clickDelay.Tick+=(s,e)=>{clickDelay.Stop();if(dialogue.Visible)dialogue.Dismiss();else{dialogue.OpenShortcuts(greeting.Begin());dialogue.ShowFor(this);}};
            LocationChanged+=(s,e)=>{if(dialogue.Visible)dialogue.PlaceNear(this);};
            tray=new NotifyIcon{Icon=AppAssets.Icon,Text="Fairy 日签 · 双击显示挂件",Visible=true,ContextMenuStrip=menu};
            tray.DoubleClick+=(s,e)=>{Show();timer.Interval=prefs.Paused?500:33;timer.Start();Render();};
            MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){clickDelay.Stop();anchor=Cursor.Position;origin=Location;dragging=true;moved=false;Capture=true;}};
            MouseMove+=(s,e)=>{if(dragging){var p=Cursor.Position;if(Math.Abs(p.X-anchor.X)>SystemInformation.DragSize.Width/2||Math.Abs(p.Y-anchor.Y)>SystemInformation.DragSize.Height/2)moved=true;if(moved)Location=new Point(origin.X+p.X-anchor.X,origin.Y+p.Y-anchor.Y);}};
            MouseUp+=(s,e)=>{if(dragging){dragging=false;Capture=false;if(moved){Clamp();Save();}else if(e.Button==MouseButtons.Left){clickDelay.Stop();clickDelay.Start();}}};
            MouseDoubleClick+=(s,e)=>{if(e.Button==MouseButtons.Left){clickDelay.Stop();moved=true;dialogue.OpenShortcuts(greeting.Begin());dialogue.ShowFor(this);}};
            timer.Interval=prefs.Paused?500:33;timer.Tick+=(s,e)=>Render();
            Shown+=(s,e)=>{ResizeForDpi();Render();timer.Start();Save();};
            DpiChanged+=(s,e)=>{ResizeForDpi();Render();};
            FormClosed+=(s,e)=>{panel.AllowExit=true;panel.Close();};
            WindowLayout.Watch(this,()=>Size.Empty);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing){timer.Dispose();clickDelay.Dispose();if(module!=null)module.Dispose();if(dialogue!=null)dialogue.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}menu.Dispose();if(panel!=null){panel.SpeechRequested-=Say;panel.ModuleRequested-=OpenModule;panel.DialogueDismissRequested-=HideDialogue;}}
            base.Dispose(disposing);
        }
        public void Say(string message)
        {
            if(IsDisposed)return;if(InvokeRequired){BeginInvoke(new Action<string>(Say),message);return;}
            if(!Visible){Show();timer.Interval=prefs.Paused?500:33;timer.Start();}dialogue.TopMost=TopMost;dialogue.Say(message);dialogue.ShowFor(this);
        }
        public void OpenPanel(){if(panel.IsDisposed)return;panel.Show();panel.WindowState=FormWindowState.Normal;panel.Activate();panel.Tell("主人，总控制面板已接入。全部委托、账号与运行记录都在这里。");}
        void HideDialogue(){clickDelay.Stop();dialogue.HideNow();}
        public void OpenModule(FairySection section)
        {
            if(panel.IsWorking){Say("主人，上一项委托还在处理，请稍候。");return;}
            if(module==null||module.IsDisposed){module=new FairyModuleForm(panel,section);module.Show(this);}else{module.Navigate(section,false);module.Show();}
            module.Activate();string text=section==FairySection.Schedule?"主人，定时通道已展开。请设定您希望我开始工作的时间。":section==FairySection.Login?"主人，登录通道已就绪。请选择米游社或小黑盒完成授权。":"主人，签到通道已接入。选择平台，Fairy 随时待命。";Say(text);
        }
        void Clamp(){var a=Screen.FromRectangle(Bounds).WorkingArea;Location=new Point(Math.Max(a.Left,Math.Min(Left,a.Right-Width)),Math.Max(a.Top,Math.Min(Top,a.Bottom-Height)));}
        void ResizeForDpi(){int pixels=(int)Math.Round(prefs.Size*DeviceDpi/96f);Size=new Size(pixels,pixels);Clamp();}
        void Save(){prefs.X=Left;prefs.Y=Top;prefs.CoordinatesDpi=DeviceDpi;try{Store.AtomicWrite(Path.Combine(store.Root,"widget.json"),Json.Write(prefs));}catch(IOException){} }
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000;if(!ShowInTaskbar)p.ExStyle|=0x80;return p;}}
        void Render()
        {
            if(IsDisposed)return;
            if(clock.ElapsedMilliseconds>nextSpeechPoll)
            {
                nextSpeechPoll=clock.ElapsedMilliseconds+750;
                try{var speech=SpeechBus.Read(store);if(speech!=null&&speech.Id!=lastSpeech){lastSpeech=speech.Id;DateTimeOffset at;if(DateTimeOffset.TryParse(speech.At,out at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromMinutes(2))Say(speech.Text);}}catch(IOException){}catch(ArgumentException){}
            }
            if(!Visible)return;
            using(var bmp=new Bitmap(Width,Height,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
                var cursor=PointToClient(Cursor.Position);float lookDistance=WindowLayout.Pixels(200,DeviceDpi);var look=new PointF(Math.Max(-1,Math.Min(1,(cursor.X-Width/2f)/lookDistance)),Math.Max(-1,Math.Min(1,(cursor.Y-Height/2f)/lookDistance)));
                FairyArt.Eye(g,new RectangleF(8,0,Width-16,Width-16),prefs.Paused?frozen:clock.Elapsed.TotalSeconds,look,panel.IsWorking);
                Present(bmp);
            }
        }
        [StructLayout(LayoutKind.Sequential)]struct Pt{public int X,Y;public Pt(int x,int y){X=x;Y=y;}}
        [StructLayout(LayoutKind.Sequential)]struct Sz{public int X,Y;public Sz(int x,int y){X=x;Y=y;}}
        [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Op,Flags,Alpha,Format;}
        [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
        [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")]static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr screen,ref Pt pos,ref Sz size,IntPtr dc,ref Pt src,int key,ref Blend blend,int flags);
        void Present(Bitmap bmp)
        {
            IntPtr screen=GetDC(IntPtr.Zero),dc=CreateCompatibleDC(screen),image=IntPtr.Zero,old=IntPtr.Zero;
            try{image=bmp.GetHbitmap(Color.FromArgb(0));old=SelectObject(dc,image);var pos=new Pt(Left,Top);var size=new Sz(Width,Height);var source=new Pt(0,0);var blend=new Blend{Alpha=255,Format=1};UpdateLayeredWindow(Handle,screen,ref pos,ref size,dc,ref source,0,ref blend,2);}
            finally{if(old!=IntPtr.Zero)SelectObject(dc,old);if(image!=IntPtr.Zero)DeleteObject(image);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}
        }
    }
}
