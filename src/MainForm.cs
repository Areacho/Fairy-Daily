using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed partial class MainForm : Form
    {
        readonly Store store;
        Settings settings;
        readonly Label account = new SmoothLabel(), heyboxAccount = new SmoothLabel(), schedule = new SmoothLabel();
        readonly List<GameTile> tiles = new List<GameTile>();
        readonly HeyboxTile heyboxTile = new HeyboxTile();
        readonly TableLayoutPanel projectGrid = new BufferedTableLayout();
        readonly DigitalTimePicker time = new DigitalTimePicker();
        readonly ToggleButton autoStart = new ToggleButton();
        readonly RichTextBox log = new RichTextBox();
        readonly FlowLayoutPanel buttons = new BufferedFlowLayout();
        readonly Button cancel = new RoundButton();
        public event Action<string> SpeechRequested;
        void Speak(string message) { if(SpeechRequested!=null)SpeechRequested(message); }
        public async Task LoginFromWidget(Form owner){var uiHandle=Handle;await Login(owner);}
        public void ConfirmFromWidget(){try{Save();Speak("主人，设定已确认。签到时间为每天北京时间 "+settings.DailyTime+"。");}catch(Exception ex){ShowError(ex);}}
        CancellationTokenSource operation;
        bool closing,updatingStartup;
        public bool AllowExit { get; set; }
        public bool IsWorking { get { return operation != null; } }
        internal bool HasLogin {get{return HasMiyouLogin||HasHeyboxLogin;}}
        public async Task RunFromWidget() { var uiHandle=Handle;try { await RunNow(); } catch(Exception ex) { ShowError(ex); } }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e);try{int dark=1;DwmSetWindowAttribute(Handle,20,ref dark,4);}catch(DllNotFoundException){} }
        // Compose child HWNDs into one frame: first show and maximize must not
        // expose their erase backgrounds or a mixture of old/new layouts.
        protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x02000000;return p;}}
        static readonly Color Ink = Theme.Text, Muted = Theme.Muted, Accent = Theme.Lime;

        public MainForm(Store store, Settings settings)
        {
            SuspendLayout();
            DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);
            this.store = store; this.settings = settings;
            Text = "FAIRY / 日常事务代理"; Font = AppFonts.Create(10F); ForeColor = Ink; Icon=AppAssets.Icon;
            BackColor = Theme.Background;Opacity=1; Size = new Size(1080, 900); MinimumSize = new Size(980, 810);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleDimensions=new SizeF(96,96);AutoScaleMode = AutoScaleMode.Dpi;
            var root = new FairyBackdrop { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(26, 18, 26, 14),AutoScroll=true };
            foreach (var row in new[] { new RowStyle(SizeType.Absolute, 82), new RowStyle(SizeType.Absolute, 142), new RowStyle(SizeType.Absolute, 340), new RowStyle(SizeType.Absolute, 56), new RowStyle(SizeType.Absolute, 32), new RowStyle(SizeType.Percent, 100) }) root.RowStyles.Add(row);
            Controls.Add(root);
            var heading = new FairyHeader();
            root.Controls.Add(heading, 0, 0);

            var accountPanel = Card("01   账号接入");
            var miyouName=new SmoothLabel{Text="米游社",BackColor=Color.Transparent,ForeColor=Accent,Font=AppFonts.Create(10,FontStyle.Bold),Location=new Point(18,40),Size=new Size(70,34)};
            var heyboxName=new SmoothLabel{Text="小黑盒",BackColor=Color.Transparent,ForeColor=Accent,Font=AppFonts.Create(10,FontStyle.Bold),Location=new Point(18,88),Size=new Size(70,34)};
            account.AccessibleName="米游社登录状态";heyboxAccount.AccessibleName="小黑盒登录状态";
            account.AutoSize=heyboxAccount.AutoSize=false;account.Size=heyboxAccount.Size=new Size(260,34);account.BackColor=heyboxAccount.BackColor=Color.Transparent;
            account.Location=new Point(96,40);heyboxAccount.Location=new Point(96,88);
            accountPanel.Controls.Add(miyouName);accountPanel.Controls.Add(heyboxName);accountPanel.Controls.Add(account);accountPanel.Controls.Add(heyboxAccount);
            var qr = MakeButton("扫码登录", true); qr.AccessibleName="米游社扫码登录";qr.Click += async (s, e) => await Login(); accountPanel.Controls.Add(qr);
            var cookie = MakeButton("导入 Cookie", false);cookie.AccessibleName="米游社导入Cookie";cookie.Click += (s, e) => ImportCookie(); accountPanel.Controls.Add(cookie);
            var heyboxLogin=MakeButton("短信登录",true);heyboxLogin.AccessibleName="小黑盒短信登录";heyboxLogin.Click+=(s,e)=>OpenHeybox();accountPanel.Controls.Add(heyboxLogin);
            var heyboxImport=MakeButton("导入配置",false);heyboxImport.AccessibleName="小黑盒导入配置";heyboxImport.Click+=async(s,e)=>await ImportHeybox();accountPanel.Controls.Add(heyboxImport);
            var timeLabel=new SmoothLabel { BackColor=Color.Transparent, Text = "每天 / 北京时间", TextAlign=ContentAlignment.MiddleCenter, Size=new Size(206,26) };accountPanel.Controls.Add(timeLabel);
            time.Size=new Size(206,64);
            var runtime=new RoundedPanel{Fill=Theme.Tile};
            autoStart.Text="开机自启";autoStart.AccessibleName="开机自启开关";autoStart.AccessibleDescription="登录 Windows 后自动启动 Fairy 桌面挂件";autoStart.Checked=StartupManager.IsEnabled();runtime.Controls.Add(autoStart);
            runtime.Layout+=(s,e)=>{float z=DeviceDpi/96f;autoStart.SetBounds((int)(10*z),(int)(3*z),runtime.Width-(int)(20*z),(int)(28*z));};
            autoStart.CheckedChanged+=(s,e)=>ChangeStartup();accountPanel.Controls.Add(runtime);
            accountPanel.Resize+=(s,e)=>{
                float z=DeviceDpi/96f;time.Left=accountPanel.ClientSize.Width-time.Width-(int)(20*z);time.Top=(int)(34*z);
                timeLabel.SetBounds(time.Left,(int)(5*z),time.Width,(int)(26*z));runtime.SetBounds(time.Left,(int)(101*z),time.Width,(int)(35*z));
                cookie.Left=heyboxImport.Left=time.Left-(int)(20*z)-cookie.Width;qr.Left=heyboxLogin.Left=cookie.Left-(int)(8*z)-qr.Width;
                qr.Top=cookie.Top=(int)(40*z);heyboxLogin.Top=heyboxImport.Top=(int)(88*z);
                account.Width=heyboxAccount.Width=Math.Max((int)(70*z),qr.Left-account.Left-(int)(12*z));
            };
            accountPanel.Controls.Add(time); root.Controls.Add(accountPanel, 0, 1);

            var projects=Card("02   每日签到委托");projects.Margin=new Padding(0,8,0,5);
            projectGrid.Dock=DockStyle.Fill;projectGrid.BackColor=Color.Transparent;projects.Padding=new Padding(0,38,0,0);projectGrid.Padding=new Padding(10,0,10,10);projectGrid.ColumnCount=4;projectGrid.RowCount=2;
            for(int i=0;i<4;i++)projectGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            for(int i=0;i<2;i++)projectGrid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            string[] gids={"2","6","8","1","4","3"};
            for(int i=0;i<Catalog.Games.Length;i++)
            {
                var game=Catalog.Games[i];var tile=new GameTile(game.Key,gids[i],game.Name,settings.Games.Contains(game.Key),settings.Forums.Contains(gids[i]));tiles.Add(tile);projectGrid.Controls.Add(tile,i%4,i/4);
            }
            var bbs=new GameTile("miyoushe","5","大别野",false,settings.Forums.Contains("5"));tiles.Add(bbs);projectGrid.Controls.Add(bbs,2,1);
            heyboxTile.SignSwitch.Checked=settings.HeyboxEnabled;projectGrid.Controls.Add(heyboxTile,3,1);
            projects.Controls.Add(projectGrid);projectGrid.BringToFront();root.Controls.Add(projects,0,2);

            buttons.BackColor=Color.Transparent; buttons.Dock = DockStyle.Fill; buttons.Padding = new Padding(0, 8, 0, 0); buttons.WrapContents = false;
            AddAction("立即签到", true, async () => await RunNow());
            AddAction("确认设定", false, async () => { ConfirmFromWidget(); await Task.FromResult(0); });
            AddAction("开启每日定时", false, async () => { Save(); RequireLogin(); string path = Scheduler.Install(store, settings, Application.ExecutablePath); RefreshStatus(); Write("每日定时已开启；程序位置：" + path);Speak("主人，每日签到已安排妥当。我会在北京时间 "+settings.DailyTime+" 执行签到。"); await Task.FromResult(0); });
            AddAction("关闭每日定时", false, async () => { if (Scheduler.Exists()) Scheduler.Remove(); RefreshStatus(); Write("每日定时已关闭。");Speak("主人，每日定时已关闭，需要时可以随时叫我签到。"); await Task.FromResult(0); });
            AddAction("查看数据", false, async () => { System.Diagnostics.Process.Start("explorer.exe", "\"" + store.Root + "\""); await Task.FromResult(0); });
            cancel.Text = "停止"; cancel.Size = new Size(72, 34); cancel.FlatStyle=FlatStyle.Flat;cancel.FlatAppearance.BorderColor=Theme.Edge;cancel.BackColor=Theme.Tile; cancel.Enabled = false; cancel.Click += (s, e) => { if (operation != null) operation.Cancel(); }; buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 3);
            schedule.BackColor=Color.Transparent; schedule.AutoSize = true; schedule.ForeColor = Accent; schedule.Padding = new Padding(0, 6, 0, 0); root.Controls.Add(schedule, 0, 4);
            log.Dock = DockStyle.Fill; log.ReadOnly = true; log.BorderStyle = BorderStyle.None; log.BackColor = Theme.Surface; log.ForeColor = Theme.Muted; log.Font = AppFonts.Chinese(9.5F);
            var logCard=new RoundedPanel{Dock=DockStyle.Fill,Padding=new Padding(18,40,18,14)};logCard.Controls.Add(log);logCard.Paint+=(s,e)=>{using(var f=AppFonts.Create(10,FontStyle.Bold))UiRendering.Text(e.Graphics,"03   执行记录  /  ACTIVITY LOG",f,new Rectangle((int)(18*logCard.DeviceDpi/96f),(int)(10*logCard.DeviceDpi/96f),(int)(500*logCard.DeviceDpi/96f),(int)(24*logCard.DeviceDpi/96f)),Theme.Muted,false);};log.DetectUrls = false; root.Controls.Add(logCard, 0, 5);
            time.Value = DateTime.Today + Clock.ParseTime(settings.DailyTime);
            RefreshStatus();
            if (File.Exists(store.LatestLog)){log.Text = File.ReadAllText(store.LatestLog);UiRendering.FormatLog(log,0,log.TextLength);}
            if (log.TextLength == 0) Write("欢迎使用。米游社与小黑盒可在上方分别登录，在每日签到委托中选择项目，确认设定后统一执行。");
            Action scrollMetrics=()=>root.AutoScrollMinSize=WindowLayout.Pixels(new Size(940,790),DeviceDpi);
            Shown+=(s,e)=>scrollMetrics();DpiChanged+=(s,e)=>BeginInvoke(scrollMetrics);WindowLayout.Watch(this,()=>new Size(980,810));
            VisibleChanged+=(s,e)=>{if(Visible){updatingStartup=true;try{autoStart.Checked=StartupManager.IsEnabled();}finally{updatingStartup=false;}}};
            FormClosing += (s, e) => {
                DismissPageDialogue();
                if (!AllowExit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
                closing = true;
                if (operation != null) operation.Cancel();
            };
            ResumeLayout(true);
        }
        Panel Card(string title)
        {
            var p = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
            p.AccessibleName=title;
            p.Paint+=(s,e)=>{float z=p.DeviceDpi/96f;using(var f=AppFonts.Create(11,FontStyle.Bold))UiRendering.Text(e.Graphics,title,f,new Rectangle((int)(18*z),(int)(7*z),p.Width-(int)(36*z),(int)(28*z)),Accent,false);};return p;
        }
        Button MakeButton(string label, bool primary)
        {
            var button = new RoundButton { Text = label, Primary=primary, Size = new Size(120, 34), Margin = new Padding(0, 0, 9, 0) };
            button.FlatAppearance.BorderSize = primary ? 0 : 1; button.FlatAppearance.BorderColor = Theme.Edge;
            button.ForeColor = Ink;
            button.FlatAppearance.MouseOverBackColor=Color.FromArgb(34,86,136); return button;
        }
        void AddAction(string label, bool primary, Func<Task> action)
        {
            var button = MakeButton(label, primary); button.Width = label.Length > 5 ? 140 : 112;
            button.Click += async (s, e) => { try { await action(); } catch (Exception ex) { ShowError(ex); } }; buttons.Controls.Add(button);
        }
        void Write(string message)
        {
            if (closing || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Write), message); return; }
            int start=log.TextLength;log.AppendText(Clock.ChinaNow.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);UiRendering.FormatLog(log,start,log.TextLength-start);log.SelectionStart = log.TextLength; log.ScrollToCaret();
        }
        void ShowError(Exception ex)
        {
            string raw = ""; try { raw = store.Decrypt(settings); } catch { }
            string message = Store.Redact(ex.Message, raw); Write(message);
            if (!closing) Speak("主人，操作还需要处理："+message);
        }
        internal void CaptureSelection()
        {
            settings.Games = tiles.Where(x=>x.GameSwitch!=null&&x.GameSwitch.Checked).Select(x=>x.Key).ToArray();
            settings.Forums = tiles.Where(x=>x.ForumSwitch.Checked).Select(x=>x.ForumId).ToArray(); settings.DailyTime = time.Value.ToString("HH:mm");
            settings.HeyboxEnabled=heyboxTile.SignSwitch.Checked;
        }
        void Save()
        {
            CaptureSelection();
            store.Save(settings);
            if (Scheduler.Exists()) Scheduler.Install(store, settings, Application.ExecutablePath);
            RefreshStatus();
        }
        void RequireLogin() { if (!HasLogin) throw new InvalidOperationException("请先在账号接入区域登录米游社或小黑盒，并选择签到项目。"); }
        public void FocusHeybox(){heyboxTile.SignSwitch.Focus();}
        void HeyboxChanged()
        {
            if(string.IsNullOrEmpty(settings.ProtectedHeyboxConfig))heyboxTile.SignSwitch.Checked=false;
            RefreshStatus();if(Scheduler.Exists())Scheduler.Install(store,settings,Application.ExecutablePath);
        }
        public void OpenHeybox(Form owner=null)
        {
            if(operation!=null){Speak("主人，当前任务正在执行，请稍候。");return;}
            operation=new CancellationTokenSource();SetBusy(true);
            try
            {
                using(var dialog=new HeyboxForm(store,settings,Write,Speak,HeyboxChanged,DismissPageDialogue))dialog.ShowDialog(owner??this);
                RefreshStatus();
            }
            catch(Exception ex){ShowError(ex);}
            finally{operation.Dispose();operation=null;if(!closing)SetBusy(false);}
        }
        async Task ImportHeybox(Form owner=null)
        {
            if(operation!=null)return;operation=new CancellationTokenSource();SetBusy(true);
            try{if(await HeyboxForm.ImportConfiguration(owner??this,store,settings,operation.Token)){HeyboxChanged();Write("小黑盒配置已校验并加密导入。");Speak("主人，小黑盒登录状态已更新。");}}
            catch(OperationCanceledException){}catch(Exception ex){ShowError(ex);}
            finally{operation.Dispose();operation=null;if(!closing)SetBusy(false);}
        }
        void ChangeStartup()
        {
            if(updatingStartup)return;bool enabled=autoStart.Checked;
            try
            {
                StartupManager.SetEnabled(enabled,store,Application.ExecutablePath);
                Write(enabled?"开机自启已开启，登录 Windows 后自动启动 Fairy。":"开机自启已关闭。");
                Speak(enabled?"主人，开机自启已开启。下次登录 Windows 时，Fairy 会自动来陪您。":"主人，开机自启已关闭。");
            }
            catch(Exception ex){updatingStartup=true;autoStart.Checked=!enabled;updatingStartup=false;ShowError(ex);}
        }
        void RefreshStatus()
        {
            string raw="";try{raw=store.Decrypt(settings);}catch(InvalidOperationException){}string uid = Cookies.Account(raw);
            account.Text = raw.Length == 0 ? "尚未登录 · 推荐扫码登录" : "已登录 · UID " + uid;
            try{var config=HeyboxAccount.Load(settings);heyboxAccount.Text=config==null?"尚未登录 · 支持短信登录":"已登录 · 盒友 ID "+config.session.heybox_id;}
            catch(InvalidOperationException){heyboxAccount.Text="登录状态需要更新，请重新登录";}
            bool installed = Scheduler.Exists();
            schedule.Text = installed ? "● 每日定时已开启 · 北京时间 " + settings.DailyTime + " · 开机联网并保持 Windows 登录，锁屏可执行" : "○ 每日定时尚未开启 · 可先手动签到测试";
        }
        void SetBusy(bool value)
        {
            buttons.Enabled = true;
            foreach (Control c in buttons.Controls) c.Enabled = !value;
            cancel.Enabled = value; projectGrid.Enabled = !value; time.Enabled = !value;autoStart.Enabled=!value;
            // Login controls also remain locked while a network operation is active.
            foreach (Control c in time.Parent.Controls) if (c is Button) c.Enabled = !value;
        }
        async Task RunNow()
        {
            if (operation != null) return;
            int speechVersion=dialogueVersion;
            Save(); RequireLogin();
            if (TryReportAlreadyComplete()) return;
            operation = new CancellationTokenSource(); SetBusy(true);Speak("主人，签到委托已接收，正在为您处理。");
            try
            {
                using (var transport = new Transport()) { var result=await new Runner(store, transport, Write).Run(settings, false, operation.Token);ReportCompletion(settings,result); }
            }
            catch (OperationCanceledException) { Write("已停止，完成的项目已保留。");if(speechVersion==dialogueVersion)Speak("主人，签到已停止，完成的项目已经记下了。"); }
            finally { operation.Dispose(); operation = null; if (!closing) SetBusy(false); }
        }
        internal bool TryReportAlreadyComplete()
        {
            if (!CheckinStatus.IsTodayComplete(store, settings)) return false;
            Write(CheckinStatus.AlreadyCompleteMessage);ReportCompletion(settings,new RunSummary());return true;
        }
        void ImportCookie(Form owner=null)
        {
            try
            {
                using (var dialog = new Form { Text = "导入登录凭证", Size = new Size(650, 235), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = Font, BackColor=Theme.Surface, ForeColor=Theme.Text,Opacity=Theme.PopupOpacity })
                {
                    dialog.SuspendLayout();dialog.AutoScaleDimensions=new SizeF(96,96);dialog.AutoScaleMode=AutoScaleMode.Dpi;
                    dialog.FormClosed+=(s,e)=>{if(dialog.DialogResult!=DialogResult.OK)DismissPageDialogue();};
                    var backdrop=new FairyBackdrop{Dock=DockStyle.Fill,ColumnCount=1,RowCount=1};backdrop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));backdrop.RowStyles.Add(new RowStyle(SizeType.Percent,100));dialog.Controls.Add(backdrop);dialog.Shown+=(s,e)=>Theme.DarkTitlebar(dialog.Handle);
                    var content=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,BackColor=Color.Transparent};backdrop.Controls.Add(content);
                    content.Controls.Add(new SmoothLabel { Text = "粘贴完整 Cookie。社区签到需 stuid/stoken；网页 Cookie 通常只能签到游戏。", BackColor=Color.Transparent, Location = new Point(18, 16), Size = new Size(600, 48) });
                    var field = new TextBox { Location = new Point(18, 71), Width = 595, UseSystemPasswordChar = true, MaxLength = 32000, BackColor=Theme.Tile, ForeColor=Theme.Text, BorderStyle=BorderStyle.FixedSingle }; content.Controls.Add(field);
                    var save = MakeButton("加密保存", true); save.Location = new Point(490, 124); save.DialogResult = DialogResult.OK; content.Controls.Add(save); dialog.AcceptButton = save;
                    WindowLayout.Watch(dialog,()=>new Size(520,210));
                    dialog.ResumeLayout(true);
                    if (dialog.ShowDialog(owner??this) != DialogResult.OK) return;
                    store.SetCookie(settings, field.Text); field.Clear(); store.Save(settings); RefreshStatus(); Write("Cookie 已加密保存。可点击“立即签到”检查两类签到。");Speak("主人，登录凭证已保存，可以开始签到啦。");
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }
        async Task Login(Form owner=null)
        {
            if (operation != null) return;
            int speechVersion=dialogueVersion;
            operation = new CancellationTokenSource(); SetBusy(true);
            var qrForm = new QrLoginForm();string completion=null;Exception failure=null;bool finishing=false;
            qrForm.FormClosed += (s, e) => { if (operation != null) operation.Cancel();if(!finishing)DismissPageDialogue(); };
            try
            {
                qrForm.ShowFor(owner??(Visible?this:null));
                using (var transport = new Transport())
                {
                    var api = new MiyouApi(settings, "", transport); var data = await api.CreateQr(operation.Token);
                    operation.Token.ThrowIfCancellationRequested();qrForm.SetCode(QrTools.Render(Json.Str(data, "url")));
                    var deadline = DateTime.UtcNow.AddMinutes(2); bool done = false;
                    while (DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(2000, operation.Token);
                        var state = await api.PollQr(Json.Str(data, "ticket"), operation.Token); string qrStatus = Json.Str(state, "status");
                        if (qrStatus == "Scanned") qrForm.SetStatus("已扫码，请在手机上确认登录");
                        if (qrStatus == "Expired" || qrStatus == "Cancelled") throw new InvalidOperationException("二维码已失效，请重新扫码。");
                        if (qrStatus != "Confirmed") continue;
                        qrForm.SetStatus("正在完成登录…");
                        string raw = await api.FinishLogin(state, operation.Token);
                        store.SetCookie(settings, raw); store.Save(settings); done = true; RefreshStatus(); Write("扫码登录成功，凭证已加密保存。请点击“立即签到”验证。");completion="主人，登录成功。Fairy 已准备就绪。"; break;
                    }
                    if (!done) throw new InvalidOperationException("扫码超时，请重新点击扫码登录。");
                }
            }
            catch (OperationCanceledException) { Write("已取消扫码登录。");completion="主人，扫码登录已取消，需要时可以再叫我。"; }
            catch (Exception ex) { failure=ex; }
            finally
            {
                finishing=true;if(!qrForm.IsDisposed)qrForm.Close();qrForm.Dispose();
                operation.Dispose(); operation = null; if (!closing) SetBusy(false);
            }
            if(!closing&&speechVersion==dialogueVersion&&(owner==null||!owner.IsDisposed)){if(failure!=null)ShowError(failure);else if(completion!=null)Speak(completion);}
        }
    }
}
