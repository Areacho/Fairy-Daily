using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiyouDaily
{
    public enum FairyPlatform { Miyou, Heybox }
    public enum FairySection { Login, Checkin, MiyouAccount, HeyboxAccount, MiyouTasks, HeyboxTasks, MiyouOptions, HeyboxOptions, Schedule }
    public sealed class FairyModuleForm : Form
    {
        readonly MainForm panel;
        BufferedTableLayout body=new BufferedTableLayout();
        readonly SmoothLabel heading=new SmoothLabel(),description=new SmoothLabel();
        readonly RoundButton back=new RoundButton();
        readonly List<RoundButton> buttons=new List<RoundButton>();
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        readonly Stack<FairySection> history=new Stack<FairySection>();
        FairySection section;bool working,disposed;
        public FairySection Section {get{return section;}}
        float LayoutScale {get{return IsHandleCreated?DeviceDpi/96f:1f;}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Theme.DarkTitlebar(Handle);}
        public FairyModuleForm(MainForm panel,FairySection section)
        {
            SuspendLayout();
            this.panel=panel;Icon=AppAssets.Icon;Font=AppFonts.Create(10);BackColor=Theme.Background;ForeColor=Theme.Text;Opacity=Theme.PopupOpacity;DoubleBuffered=true;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;MinimumSize=new Size(520,280);StartPosition=FormStartPosition.CenterScreen;MaximizeBox=false;
            var root=new FairyBackdrop{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(24,16,24,22)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
            var header=new Panel{Dock=DockStyle.Fill,BackColor=Color.Transparent};back.Text="返回";back.Size=new Size(66,32);back.Location=new Point(0,10);back.Click+=(s,e)=>GoBack();header.Controls.Add(back);
            heading.Font=AppFonts.Create(19,FontStyle.Bold);heading.BackColor=Color.Transparent;heading.ForeColor=Theme.Text;heading.Location=new Point(84,5);heading.Size=new Size(520,43);header.Controls.Add(heading);root.Controls.Add(header,0,0);
            description.Dock=DockStyle.Fill;description.BackColor=Color.Transparent;description.ForeColor=Theme.Muted;root.Controls.Add(description,0,1);
            body.Dock=DockStyle.Fill;body.BackColor=Color.Transparent;body.ColumnCount=1;body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.AutoScroll=true;root.Controls.Add(body,0,2);
            FormClosing+=(s,e)=>lifetime.Cancel();FormClosed+=(s,e)=>panel.DismissPageDialogue();Navigate(section,false);
            DpiChanged+=(s,e)=>BeginInvoke(new Action(()=>{UpdateHeader();UpdateScrollMetrics();}));WindowLayout.Watch(this,MinimumLogical);
            ResumeLayout(true);
        }
        Size MinimumLogical(){return section==FairySection.MiyouTasks?new Size(820,600):new Size(520,280);}
        void UpdateScrollMetrics(){body.AutoScrollMinSize=WindowLayout.Pixels(new Size(MinimumLogical().Width-64,0),DeviceDpi);}
        void UpdateHeader()
        {
            float z=LayoutScale;bool showBack=history.Count>0||(section!=FairySection.Login&&section!=FairySection.Checkin&&section!=FairySection.Schedule);
            back.Visible=showBack;heading.Left=(int)((showBack?84:0)*z);heading.Width=Math.Max(1,ClientSize.Width-heading.Left-(int)(48*z));
        }
        static FairySection ParentSection(FairySection current)
        {
            switch(current){case FairySection.MiyouOptions:return FairySection.MiyouTasks;case FairySection.HeyboxOptions:return FairySection.HeyboxTasks;case FairySection.MiyouTasks:case FairySection.HeyboxTasks:return FairySection.Checkin;default:return FairySection.Login;}
        }
        public void GoBack()
        {
            if(working)return;
            if(history.Count==0){ShowSection(ParentSection(section),true);return;}
            ShowSection(history.Pop(),true);
        }
        void Push(FairySection target)
        {history.Push(section);ShowSection(target,true);}
        void Row(Control control,int height)
        {
            float z=LayoutScale;int row=body.RowCount++;body.RowStyles.Add(new RowStyle(SizeType.Absolute,height*z));control.Dock=DockStyle.Fill;control.Margin=new Padding(0,0,0,(int)(10*z));body.Controls.Add(control,0,row);
        }
        RoundButton MakeAction(string text,Func<Task> action,bool primary=false,string image=null)
        {
            var button=new RoundButton{Text=text,Primary=primary,AccessibleName=text};if(image!=null)button.Image=AppAssets.Image(image);
            button.Click+=async(s,e)=>await Work(action);buttons.Add(button);return button;
        }
        RoundButton Action(string text,Func<Task> action,bool primary=false,string image=null)
        {var button=MakeAction(text,action,primary,image);Row(button,image==null?52:66);return button;}
        void Navigation(string text,FairySection target,string image=null)
        {Action(text,()=>{Push(target);return Task.FromResult(0);},false,image);}
        SmoothLabel Status(string text,int height=56)
        {var label=new SmoothLabel{Text=text,BackColor=Color.Transparent,ForeColor=Theme.Muted};Row(label,height);return label;}
        async Task Work(Func<Task> action)
        {
            if(working)return;working=true;foreach(var button in buttons)button.Enabled=false;back.Enabled=false;
            try{await action();}catch(OperationCanceledException){}catch(Exception ex){if(!IsDisposed){description.Text=ex.Message;panel.Tell("主人，这项操作还需要处理："+ex.Message);}}
            finally{working=false;if(!IsDisposed){foreach(var button in buttons)if(!button.IsDisposed)button.Enabled=true;back.Enabled=true;}}
        }
        public void Navigate(FairySection target,bool announce=true)
        {history.Clear();ShowSection(target,announce);}
        void ShowSection(FairySection target,bool announce)
        {
            section=target;SuspendLayout();
            var host=(TableLayoutPanel)body.Parent;host.SuspendLayout();host.Controls.Remove(body);body.Dispose();
            body=new BufferedTableLayout{Dock=DockStyle.Fill,BackColor=Color.Transparent,ColumnCount=1,RowCount=0,AutoScroll=true};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));host.Controls.Add(body,0,2);body.SuspendLayout();buttons.Clear();
            float z=LayoutScale;Size size;
            switch(target){case FairySection.MiyouTasks:size=new Size(860,650);break;case FairySection.HeyboxTasks:size=new Size(600,460);break;case FairySection.MiyouOptions:size=new Size(740,600);break;case FairySection.HeyboxOptions:size=new Size(560,360);break;case FairySection.Schedule:size=new Size(620,560);break;case FairySection.Login:case FairySection.Checkin:size=new Size(580,310);break;case FairySection.MiyouAccount:case FairySection.HeyboxAccount:size=new Size(600,340);break;default:size=new Size(600,370);break;}
            MinimumSize=new Size((int)((target==FairySection.MiyouTasks?820:520)*z),(int)((target==FairySection.MiyouTasks?600:280)*z));
            ClientSize=new Size((int)(size.Width*z),(int)(size.Height*z));
            UpdateHeader();UpdateScrollMetrics();
            switch(target)
            {
                case FairySection.Login:
                    heading.Text="账号接入  /  LOGIN";description.Text="主人，接入通道已就绪。请选择要登录的平台。";
                    Navigation("米游社  ·  "+(panel.HasMiyouLogin?"已接入":"扫码登录"),FairySection.MiyouAccount,"miyoushe");
                    Navigation("小黑盒  ·  "+(panel.HasHeyboxLogin?"已接入":"短信登录"),FairySection.HeyboxAccount,"heybox");break;
                case FairySection.Checkin:
                    heading.Text="签到委托  /  DAILY";description.Text="主人，选择一条签到通道。今天的例行事务，交给 Fairy。";
                    Navigation("米游社  ·  "+(panel.HasMiyouLogin?"管理签到":"等待登录"),FairySection.MiyouTasks,"miyoushe");
                    Navigation("小黑盒  ·  "+(panel.HasHeyboxLogin?"管理签到":"等待登录"),FairySection.HeyboxTasks,"heybox");break;
                case FairySection.MiyouAccount:
                    heading.Text="米游社  /  账号接入";description.Text="主人，请在手机上完成授权。我会为您守好接入通道。";
                    var miyouStatus=Status(panel.AccountDescription(FairyPlatform.Miyou));
                    Action("米游社扫码登录",async()=>{await panel.LoginMiyou(this);miyouStatus.Text=panel.AccountDescription(FairyPlatform.Miyou);},true,"miyoushe");
                    Action("导入米游社凭证",()=>{panel.ImportMiyou(this);miyouStatus.Text=panel.AccountDescription(FairyPlatform.Miyou);return Task.FromResult(0);});break;
                case FairySection.HeyboxAccount:
                    heading.Text="小黑盒  /  账号接入";description.Text="主人，小黑盒通道待命。请输入手机号与验证码完成接入。";
                    var heyboxStatus=Status(panel.AccountDescription(FairyPlatform.Heybox));
                    Action("小黑盒短信登录",()=>{panel.LoginHeybox(this);heyboxStatus.Text=panel.AccountDescription(FairyPlatform.Heybox);return Task.FromResult(0);},true,"heybox");
                    Action("导入小黑盒配置",async()=>{await panel.ImportHeyboxAccount(this);heyboxStatus.Text=panel.AccountDescription(FairyPlatform.Heybox);});break;
                case FairySection.MiyouTasks: BuildTasks(FairyPlatform.Miyou);break;
                case FairySection.HeyboxTasks: BuildTasks(FairyPlatform.Heybox);break;
                case FairySection.MiyouOptions: BuildMiyouOptions();break;
                case FairySection.HeyboxOptions: BuildHeyboxOptions();break;
                case FairySection.Schedule: BuildSchedule();break;
            }
            body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.RowCount++;
            Text="Fairy · "+heading.Text;body.ResumeLayout(true);host.ResumeLayout(true);ResumeLayout(true);body.PerformLayout();
            if(Visible)WindowLayout.Fit(this,MinimumLogical());
            if(announce)panel.Tell(description.Text);
        }
        void BuildTasks(FairyPlatform platform)
        {
            string name=platform==FairyPlatform.Miyou?"米游社":"小黑盒";heading.Text=name+"  /  签到管理";description.Text="主人，"+name+"委托已展开。在这里调整签到项目，确认后交给 Fairy。";
            var result=Status(panel.AccountDescription(platform),36);
            Action("立即执行"+name+"签到",async()=>{result.Text=await panel.RunPlatform(platform,lifetime.Token);},true,platform==FairyPlatform.Miyou?"miyoushe":"heybox");
            if(platform==FairyPlatform.Miyou)
            {
                Status("每日签到项目  /  DAILY SETTINGS",34);
                var selected=AddMiyouSelection(4,2,300);
                var save=MakeAction("确认米游社配置",()=>{SaveMiyouSelection(selected);return Task.FromResult(0);},true);
                var account=MakeAction("米游社登录管理",()=>{Push(FairySection.MiyouAccount);return Task.FromResult(0);});CommandRow(save,account);
            }
            else
            {
                Status("每日委托配置  /  DAILY SETTINGS",34);
                var tile=new HeyboxTile();tile.SignSwitch.Checked=panel.ModuleSettings.HeyboxEnabled;Row(tile,124);
                var save=MakeAction("确认小黑盒配置",()=>{panel.SaveHeyboxOptions(tile.SignSwitch.Checked);return Task.FromResult(0);},true);
                var account=MakeAction("小黑盒登录管理",()=>{Push(FairySection.HeyboxAccount);return Task.FromResult(0);});
                CommandRow(save,account);
            }
        }
        void CommandRow(params RoundButton[] choices)
        {
            var commands=new BufferedTableLayout{ColumnCount=choices.Length,RowCount=1,BackColor=Color.Transparent};commands.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            for(int i=0;i<choices.Length;i++){commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/choices.Length));choices[i].Dock=DockStyle.Fill;choices[i].Margin=new Padding(0,0,(int)((i<choices.Length-1?10:0)*LayoutScale),0);commands.Controls.Add(choices[i],i,0);}Row(commands,54);
        }
        void BuildMiyouOptions()
        {
            heading.Text="米游社  /  签到配置";description.Text="主人，选择要托管的游戏福利与社区板块。确认后，我会按您的安排执行。";
            var selected=AddMiyouSelection(3,3,390);
            Action("确认米游社配置",()=>{SaveMiyouSelection(selected);return Task.FromResult(0);},true);
        }
        List<GameTile> AddMiyouSelection(int columns,int rows,int height)
        {
            var config=panel.ModuleSettings;var grid=new BufferedTableLayout{ColumnCount=columns,RowCount=rows,BackColor=Color.Transparent};
            for(int i=0;i<columns;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/columns));for(int i=0;i<rows;i++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/rows));
            var selected=new List<GameTile>();string[] gids={"2","6","8","1","4","3"};
            for(int i=0;i<Catalog.Games.Length;i++){var game=Catalog.Games[i];var tile=new GameTile(game.Key,gids[i],game.Name,config.Games.Contains(game.Key),config.Forums.Contains(gids[i])){Margin=new Padding((int)(6*LayoutScale))};selected.Add(tile);grid.Controls.Add(tile,i%columns,i/columns);}
            int last=Catalog.Games.Length;var bbs=new GameTile("miyoushe","5","大别野",false,config.Forums.Contains("5")){Margin=new Padding((int)(6*LayoutScale))};selected.Add(bbs);grid.Controls.Add(bbs,last%columns,last/columns);Row(grid,height);return selected;
        }
        void SaveMiyouSelection(IEnumerable<GameTile> selected)
        {panel.SaveMiyouOptions(selected.Where(t=>t.GameSwitch!=null&&t.GameSwitch.Checked).Select(t=>t.Key).ToArray(),selected.Where(t=>t.ForumSwitch.Checked).Select(t=>t.ForumId).ToArray());}
        void BuildHeyboxOptions()
        {
            heading.Text="小黑盒  /  签到配置";description.Text="主人，请确认小黑盒的每日委托。开启后，我会按共同的定时时间执行。";
            var tile=new HeyboxTile();tile.SignSwitch.Checked=panel.ModuleSettings.HeyboxEnabled;Row(tile,144);
            Action("确认小黑盒配置",()=>{panel.SaveHeyboxOptions(tile.SignSwitch.Checked);return Task.FromResult(0);},true);
        }
        void BuildSchedule()
        {
            heading.Text="定时托管  /  TIMER";description.Text="主人，设定每日时间，准点执行交给 Fairy。";
            var config=panel.ModuleSettings;var clock=new DigitalTimePicker{Value=DateTime.Today+Clock.ParseTime(config.DailyTime)};Row(clock,166);
            Status("点击时 / 分输入数字 · 方向键或滚轮调整",34);
            var status=Status(Scheduler.Exists()?"● 每日定时已开启 · "+config.DailyTime:"○ 每日定时尚未开启",36);
            var startup=new ToggleButton{Text="登录 Windows 后启动 Fairy",AccessibleName="定时页开机自启",Checked=StartupManager.IsEnabled()};Row(startup,42);
            bool syncing=false;startup.CheckedChanged+=(s,e)=>{if(syncing)return;try{panel.SetModuleStartup(startup.Checked);}catch(Exception ex){panel.Tell("主人，自启设置需要处理："+ex.Message);}finally{syncing=true;startup.Checked=StartupManager.IsEnabled();syncing=false;}};
            var autoExit=new ToggleButton{Text="全部签到完成后自动退出挂件",AccessibleName="签到完成后自动退出挂件",Checked=config.ExitWidgetAfterCheckin};Row(autoExit,42);
            bool syncingExit=false;autoExit.CheckedChanged+=(s,e)=>{if(syncingExit)return;try{panel.SetModuleAutoExit(autoExit.Checked);}catch(Exception ex){panel.Tell("主人，退出设置需要处理："+ex.Message);}finally{syncingExit=true;autoExit.Checked=panel.ModuleSettings.ExitWidgetAfterCheckin;syncingExit=false;}};
            Status("先显示完成气泡，8 秒后退出 · 每日定时继续保留",34);
            var confirm=MakeAction("确认时间",()=>{string value=clock.Value.ToString("HH:mm");panel.SaveModuleTime(value);status.Text="每日时间已确认 · "+value;return Task.FromResult(0);},true);
            var enable=MakeAction("开启定时",()=>{string value=clock.Value.ToString("HH:mm");panel.InstallModuleSchedule(value);status.Text="● 每日定时已开启 · "+value;return Task.FromResult(0);});
            var pause=MakeAction("暂停定时",()=>{panel.RemoveModuleSchedule();status.Text="○ 每日定时已暂停";return Task.FromResult(0);});
            CommandRow(confirm,enable,pause);
        }
        protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;lifetime.Cancel();lifetime.Dispose();}base.Dispose(disposing);}
    }
}
