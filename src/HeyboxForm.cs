using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed class HeyboxForm : Form
    {
        readonly Store store;readonly Settings settings;readonly Action<string> output,speak;readonly Action changed,dismiss;
        readonly TextBox phone=new TextBox(),code=new TextBox();
        readonly RoundButton send=new RoundButton(),verify=new RoundButton(),import=new RoundButton(),logout=new RoundButton();
        readonly SmoothLabel account=new SmoothLabel();readonly RichTextBox log=new RichTextBox();
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();readonly System.Windows.Forms.Timer cooldown=new System.Windows.Forms.Timer();
        HeyboxConfig pending;string pendingPhone;int attempts;DateTime nextSms=DateTime.MinValue;bool busy,closing,disposed,completed;
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Theme.DarkTitlebar(Handle);}
        public HeyboxForm(Store store,Settings settings,Action<string> output=null,Action<string> speak=null,Action changed=null,Action dismiss=null)
        {
            SuspendLayout();
            this.store=store;this.settings=settings;this.output=output;this.speak=speak;this.changed=changed;this.dismiss=dismiss;
            Text="FAIRY / 小黑盒登录";Icon=AppAssets.Icon;Font=AppFonts.Create(10);ForeColor=Theme.Text;BackColor=Theme.Background;PopupSurface.Attach(this);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(650,448);MinimumSize=new Size(600,440);StartPosition=FormStartPosition.CenterParent;
            var root=new FairyBackdrop{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(22)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,55));root.RowStyles.Add(new RowStyle(SizeType.Absolute,175));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
            root.Controls.Add(new SmoothLabel{Text="小黑盒  /  短信登录",Dock=DockStyle.Fill,Font=AppFonts.Create(21,FontStyle.Bold),ForeColor=Theme.Lime,BackColor=Color.Transparent},0,0);
            var login=new RoundedPanel{Dock=DockStyle.Fill,Padding=new Padding(18,12,18,12),Margin=new Padding(0,5,0,6)};
            var fields=new BufferedTableLayout{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,BackColor=Color.Transparent};
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,75));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,146));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute,47));fields.RowStyles.Add(new RowStyle(SizeType.Absolute,47));fields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            fields.Controls.Add(Label("手机号"),0,0);fields.Controls.Add(Label("验证码"),0,1);
            foreach(var field in new[]{phone,code}){field.BackColor=Theme.Tile;field.ForeColor=Theme.Text;field.BorderStyle=BorderStyle.FixedSingle;field.Dock=DockStyle.Top;field.Margin=new Padding(3,8,14,0);field.ImeMode=ImeMode.Disable;}
            phone.MaxLength=18;phone.AccessibleName="小黑盒手机号";code.MaxLength=8;code.UseSystemPasswordChar=true;code.AccessibleName="小黑盒短信验证码";
            fields.Controls.Add(phone,1,0);fields.Controls.Add(code,1,1);
            Configure(send,"获取验证码",false);Configure(verify,"登录",true);fields.Controls.Add(send,2,0);fields.Controls.Add(verify,2,1);
            account.Dock=DockStyle.Fill;account.BackColor=Color.Transparent;account.ForeColor=Theme.Muted;fields.Controls.Add(account,0,2);fields.SetColumnSpan(account,3);
            login.Controls.Add(fields);root.Controls.Add(login,0,1);
            var actions=new BufferedFlowLayout{Dock=DockStyle.Fill,BackColor=Color.Transparent,WrapContents=false,Padding=new Padding(0,5,0,0)};
            Configure(import,"导入配置",false);Configure(logout,"退出登录",false);foreach(var button in new[]{import,logout}){button.Size=new Size(135,36);button.Dock=DockStyle.None;button.Margin=new Padding(0,0,10,0);actions.Controls.Add(button);}root.Controls.Add(actions,0,2);
            var logs=new RoundedPanel{Dock=DockStyle.Fill,Padding=new Padding(12),Margin=new Padding(0,4,0,0)};log.Dock=DockStyle.Fill;log.ReadOnly=true;log.BorderStyle=BorderStyle.None;log.BackColor=Theme.Surface;log.ForeColor=Theme.Muted;log.Font=AppFonts.Create(9.5f);log.DetectUrls=false;logs.Controls.Add(log);root.Controls.Add(logs,0,3);ScrollbarTheme.Attach(log);
            send.Click+=async(s,e)=>await Work(SendCode);verify.Click+=async(s,e)=>await Work(Login);import.Click+=async(s,e)=>await Work(Import);
            logout.Click+=(s,e)=>{string previous=settings.ProtectedHeyboxConfig;bool wasEnabled=settings.HeyboxEnabled;try{settings.ProtectedHeyboxConfig="";settings.HeyboxEnabled=false;Persist();RefreshAccount();Append("小黑盒本机登录已清除，自动签到已关闭。");if(speak!=null)speak("主人，小黑盒通道已断开，每日委托也已暂停。需要时再叫我接入。");}catch(Exception ex){settings.ProtectedHeyboxConfig=previous;settings.HeyboxEnabled=wasEnabled;RefreshAccount();Report(ex);}};
            cooldown.Interval=500;cooldown.Tick+=(s,e)=>RefreshButtons();
            FormClosing+=(s,e)=>{closing=true;lifetime.Cancel();};FormClosed+=(s,e)=>{if(!completed&&dismiss!=null)dismiss();};RefreshAccount();
            Append("支持短信验证码登录，也可导入已有账号配置。接入后返回小黑盒签到页面，即可下达每日委托。");
            WindowLayout.Watch(this,()=>new Size(600,440),true);
            ResumeLayout(true);
        }
        static SmoothLabel Label(string text){return new SmoothLabel{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.Transparent};}
        static void Configure(RoundButton button,string text,bool primary){button.Text=text;button.AccessibleName="小黑盒"+text;button.Primary=primary;button.Dock=DockStyle.Fill;button.Margin=new Padding(0,4,0,7);}
        void Append(string message)
        {
            if(closing||IsDisposed)return;log.AppendText(Clock.ChinaNow.ToString("HH:mm:ss")+"  "+message+Environment.NewLine);log.SelectionStart=log.TextLength;log.ScrollToCaret();if(output!=null)output("小黑盒："+message);
        }
        void Report(Exception ex)
        {
            string text=HeyboxAccount.Redact(ex.Message,pending);try{text=HeyboxAccount.Redact(text,HeyboxAccount.Load(settings));}catch{}
            Append(text);if(speak!=null)speak("主人，小黑盒需要处理："+text);
        }
        void NotifyChanged(){try{if(changed!=null)changed();}catch(Exception ex){Append("设置已保存，更新每日定时需要处理："+ex.Message);}}
        void Persist(){store.Save(settings);NotifyChanged();}
        void RefreshAccount()
        {
            try{var config=HeyboxAccount.Load(settings);account.Text=config==null?"尚未登录 · 验证码仅用于本次登录":"已登录 · 盒友 ID "+config.session.heybox_id;}
            catch(InvalidOperationException){account.Text="登录状态需要更新，请重新登录。";}
            RefreshButtons();
        }
        void RefreshButtons()
        {
            if(closing)return;int remaining=Math.Max(0,(int)Math.Ceiling((nextSms-DateTime.UtcNow).TotalSeconds));
            send.Text=remaining==0?"获取验证码":remaining+" 秒后重试";send.Enabled=!busy&&remaining==0;
            verify.Enabled=!busy&&pending!=null&&pendingPhone!=null&&attempts<3;import.Enabled=logout.Enabled=phone.Enabled=code.Enabled=!busy;
            cooldown.Enabled=remaining>0;
        }
        async Task Work(Func<Task> action)
        {
            if(busy)return;busy=true;RefreshButtons();
            try{await action();}catch(OperationCanceledException){Append("操作已取消。");}catch(Exception ex){if(!closing)Report(ex);}
            finally{busy=false;if(!closing){RefreshAccount();}}
        }
        async Task<Dictionary<string,string>> Challenge(HeyboxCaptchaException ex)
        {
            using(var dialog=new HeyboxBrowserDialog(store)){dialog.Show(this);return await dialog.Captcha(ex.AppId,lifetime.Token);}
        }
        async Task SendCode()
        {
            string number=HeyboxApi.NormalizePhone(phone.Text);pendingPhone=null;attempts=0;code.Clear();
            if(speak!=null)speak("主人，正在准备小黑盒短信接入。若出现验证窗口，请协助我完成验证。");
            if(pending==null)
            {
                Append("正在准备登录，请在验证窗口中稍候。");
                using(var dialog=new HeyboxBrowserDialog(store)){dialog.Show(this);pending=HeyboxConfig.Login(await dialog.DeviceToken(lifetime.Token));}
            }
            using(var transport=new HeyboxTransport())
            {
                var api=new HeyboxApi(pending,transport);
                HeyboxCaptchaException challenge=null;
                try{await api.SendCode(number,null,lifetime.Token);}
                catch(HeyboxCaptchaException ex){challenge=ex;}
                if(challenge!=null){var proof=await Challenge(challenge);await api.SendCode(number,proof,lifetime.Token);}
            }
            pendingPhone=number;nextSms=DateTime.UtcNow.AddSeconds(60);Append("验证码已发送，请输入后点击“登录”。");if(speak!=null)speak("主人，验证码已发出。请在小黑盒登录窗口输入，我在这里等您。");
        }
        async Task Login()
        {
            if(pending==null||pendingPhone==null)throw new InvalidOperationException("请先获取短信验证码。");
            if(HeyboxApi.NormalizePhone(phone.Text)!=pendingPhone)throw new InvalidOperationException("手机号已改变，请重新获取验证码。");
            if(attempts>=3)throw new InvalidOperationException("本次验证码已尝试三次，请重新获取。");
            string value=code.Text.Trim();if(!System.Text.RegularExpressions.Regex.IsMatch(value,@"^\d{4,8}$"))throw new ArgumentException("验证码应为 4 至 8 位数字。");attempts++;
            HeyboxConfig authenticated=null;
            using(var transport=new HeyboxTransport())
            {
                var api=new HeyboxApi(pending,transport);
                HeyboxCaptchaException challenge=null;
                try{authenticated=await api.VerifyCode(pendingPhone,value,null,lifetime.Token);}
                catch(HeyboxCaptchaException ex){challenge=ex;}
                if(challenge!=null){var proof=await Challenge(challenge);authenticated=await api.VerifyCode(pendingPhone,value,proof,lifetime.Token);}
            }
            lifetime.Token.ThrowIfCancellationRequested();string previous=settings.ProtectedHeyboxConfig;
            try{HeyboxAccount.Set(settings,authenticated);store.Save(settings);}catch{settings.ProtectedHeyboxConfig=previous;throw;}
            code.Clear();phone.Clear();pending=null;pendingPhone=null;NotifyChanged();Append("小黑盒登录成功，凭证已加密保存在本机。");if(speak!=null)speak("主人，小黑盒已接入。Fairy 为您守好这条通道，随时可以下达签到委托。");completed=true;Close();
        }
        async Task Import()
        {
            if(await ImportConfiguration(this,store,settings,lifetime.Token)){NotifyChanged();Append("小黑盒配置已校验并加密导入。");if(speak!=null)speak("主人，小黑盒配置已校验，接入成功。今天的委托随时可以开始。");completed=true;Close();}
        }
        public static async Task<bool> ImportConfiguration(IWin32Window owner,Store store,Settings settings,CancellationToken cancel)
        {
            using(var picker=new OpenFileDialog{Title="选择小黑盒账号配置",Filter="JSON 配置 (*.json)|*.json",CheckFileExists=true})
            {
                if(picker.ShowDialog(owner)!=DialogResult.OK)return false;
                if(new FileInfo(picker.FileName).Length>65536)throw new ArgumentException("配置文件过大，请选择 heybox-daily 导出的账号配置。");
                var candidate=Json.Read<HeyboxConfig>(File.ReadAllText(picker.FileName));if(candidate==null)throw new ArgumentException("账号配置无效。");candidate.Validate();
                try{using(var transport=new HeyboxTransport())await new HeyboxApi(candidate,transport).State(cancel);}
                catch(OperationCanceledException){throw;}
                catch(Exception ex){throw new IOException(HeyboxAccount.Redact(ex.Message,candidate));}
                cancel.ThrowIfCancellationRequested();string previous=settings.ProtectedHeyboxConfig;
                try{HeyboxAccount.Set(settings,candidate);store.Save(settings);}catch{settings.ProtectedHeyboxConfig=previous;throw;}
                return true;
            }
        }
        protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;cooldown.Dispose();lifetime.Cancel();lifetime.Dispose();}base.Dispose(disposing);}
    }
}
