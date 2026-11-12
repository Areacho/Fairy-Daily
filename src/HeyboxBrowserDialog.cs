using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MiyouDaily
{
    // Use the installed Edge runtime to run the official device-token SDK.
    // No phone number, SMS code or account cookie enters this document.
    public sealed class HeyboxBrowserDialog : Form
    {
        const string Origin="https://www.xiaoheihe.cn/fairy-login.html";
        readonly Store store;readonly WebView2 browser=new WebView2();
        readonly TaskCompletionSource<Dictionary<string,string>> result=new TaskCompletionSource<Dictionary<string,string>>();
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        static IntPtr loader;
        bool disposed;
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Theme.DarkTitlebar(Handle);}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibrary(string path);
        public HeyboxBrowserDialog(Store store)
        {
            SuspendLayout();
            this.store=store;Text="小黑盒 · 登录验证";Icon=AppAssets.Icon;Font=AppFonts.Create(10);BackColor=Theme.Background;ForeColor=Theme.Text;Opacity=Theme.PopupOpacity;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(460,360);MinimumSize=new Size(420,320);StartPosition=FormStartPosition.Manual;WindowLayout.Watch(this,()=>new Size(420,320),true);
            browser.Dock=DockStyle.Fill;Controls.Add(browser);FormClosing+=(s,e)=>{lifetime.Cancel();result.TrySetCanceled();};
            ResumeLayout(true);
        }
        protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;lifetime.Cancel();lifetime.Dispose();browser.Dispose();}base.Dispose(disposing);}
        async Task Initialize(CancellationToken cancel)
        {
            if(loader==IntPtr.Zero)
            {
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtimes",Environment.Is64BitProcess?"win-x64":"win-x86","native","WebView2Loader.dll");
                loader=LoadLibrary(path);if(loader==IntPtr.Zero)throw new IOException("登录组件缺失，请完整解压新版发布包后重试。");
            }
            CoreWebView2Environment environment=null;
            try{environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(store.Root,"webview2"));}
            catch(WebView2RuntimeNotFoundException){throw new InvalidOperationException("本机缺少 Microsoft Edge WebView2 Runtime，请安装后重试，或使用已有账号配置导入。");}
            cancel.ThrowIfCancellationRequested();
            await browser.EnsureCoreWebView2Async(environment);cancel.ThrowIfCancellationRequested();
            var core=browser.CoreWebView2;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;
            core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
            core.Settings.AreHostObjectsAllowed=false;core.DownloadStarting+=(s,e)=>e.Cancel=true;
            core.NewWindowRequested+=(s,e)=>e.Handled=true;
            core.NavigationStarting+=(s,e)=>{if(e.Uri!=Origin)e.Cancel=true;};
            core.PermissionRequested+=(s,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.WebMessageReceived+=(s,e)=>{
                if(e.Source!=Origin)return;
                try
                {
                    var data=Json.Read<Dictionary<string,string>>(e.WebMessageAsJson);string kind;
                    if(data==null||!data.TryGetValue("kind",out kind))return;
                    if(kind=="token")
                    {string token;if(data.TryGetValue("value",out token)&&Regex.IsMatch(token,@"^B[A-Za-z0-9+/=_-]{88}$"))result.TrySetResult(data);}
                    else if(kind=="captcha")
                    {
                        string ticket,randstr;
                        if(data.TryGetValue("ticket",out ticket)&&data.TryGetValue("randstr",out randstr)&&ticket.Length>0&&ticket.Length<=2048&&randstr.Length>0&&randstr.Length<=256
                            &&!Regex.IsMatch(ticket+randstr,@"[^\x21-\x7e]")&&!ticket.StartsWith("trerror_",StringComparison.OrdinalIgnoreCase)&&!randstr.StartsWith("trerror_",StringComparison.OrdinalIgnoreCase))result.TrySetResult(data);
                        else result.TrySetException(new IOException("人机验证未完成，请重试。"));
                    }
                    else if(kind=="error")result.TrySetException(new IOException("登录验证未完成，请检查网络后重试。"));
                    else if(kind=="cancel")result.TrySetCanceled();
                }
                catch(ArgumentException){result.TrySetException(new IOException("登录验证响应无效。"));}
            };
            string content=Path.Combine(store.Root,"components","heybox-login");Directory.CreateDirectory(content);
            Store.AtomicWrite(Path.Combine(content,"fairy-login.html"),"<!doctype html><html lang='zh-CN'><meta charset='utf-8'><style>html{min-height:100%;background:radial-gradient(ellipse at 60% 40%,#123c7b,#040c1e 80%)}body{color:#e1f2ff;font:16px 'Microsoft YaHei',sans-serif;padding:30px;line-height:1.8}h2{color:#79daff;font-size:22px}</style><h2 id='title'>Fairy 正在准备接入…</h2><p id='hint'>主人，请稍候。通道就绪后，我会带您返回短信登录窗口。</p><script>function send(x){chrome.webview.postMessage(x)}</script></html>");
            core.SetVirtualHostNameToFolderMapping("www.xiaoheihe.cn",content,CoreWebView2HostResourceAccessKind.DenyCors);
            var ready=new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> complete=null;
            complete=(s,e)=>{core.NavigationCompleted-=complete;if(e.IsSuccess)ready.TrySetResult(true);else ready.TrySetException(new IOException("无法打开登录验证窗口。"));};
            core.NavigationCompleted+=complete;core.Navigate(Origin);
            await Await(ready.Task,cancel,15000);
        }
        static async Task<T> Await<T>(Task<T> task,CancellationToken cancel,int milliseconds)
        {
            using(var timeout=new CancellationTokenSource())
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancel,timeout.Token))
            {
                var delay=Task.Delay(milliseconds,linked.Token);
                if(await Task.WhenAny(task,delay)!=task){cancel.ThrowIfCancellationRequested();throw new IOException("等待登录验证超时，请重试。");}
                timeout.Cancel();return await task;
            }
        }
        public async Task<string> DeviceToken(CancellationToken cancel)
        {
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancel,lifetime.Token))
            {
                await Initialize(linked.Token);
                const string setup=@"window._smConf={organization:'0yD85BjYvGFAvHaSQ1mc',appId:'heybox_website',publicKey:'MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCXj9exmI4nQjmT52iwr+yf7hAQ06bfSZHTAHUfRBYiagCf/whhd8es0R79wBigpiHLd28TKA8b8mGR8OiiI1hV+qfynCWihvp3mdj8MiiH6SU3lhro2hkfYzImZB0RmWr2zE4Xt1+A6Oyp6bf+W7JSxYUXHw3nNv7Td4jw4jEFKQIDAQAB',staticHost:'static.portal101.cn',protocol:'https',noNeedRetry:true,onError:function(){send({kind:'error'})}};window._smReadyFuncs=[function(){var t=SMSdk.getDeviceId();if(/^B[A-Za-z0-9+/=_-]{88}$/.test(t))send({kind:'token',value:t})}];";
                // Unlike Node's VM, this is a normal browser sandbox with no host
                // objects or account credentials. Load the official HTTPS SDK as
                // a web page would, allowing routine SDK updates without a rebuild.
                await browser.CoreWebView2.ExecuteScriptAsync(setup+"\nvar sdk=document.createElement('script');sdk.src='https://static.portal101.cn/dist/web/v3.0.0/fp.min.js';sdk.onerror=function(){send({kind:'error'})};document.head.appendChild(sdk);setInterval(function(){if(window.SMSdk&&SMSdk.getDeviceId){var t=SMSdk.getDeviceId();if(/^B[A-Za-z0-9+/=_-]{88}$/.test(t))send({kind:'token',value:t})}},200);");
                var data=await Await(result.Task,linked.Token,25000);return data["value"];
            }
        }
        public async Task<Dictionary<string,string>> Captcha(string appId,CancellationToken cancel)
        {
            if(!Regex.IsMatch(appId??"",@"^\d{1,32}$"))throw new IOException("小黑盒要求验证，请稍后重试或先在 App 中完成验证。");
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancel,lifetime.Token))
            {
                await Initialize(linked.Token);
                await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('title').textContent='请完成人机验证';document.getElementById('hint').textContent='验证成功后会自动返回登录窗口。';var s=document.createElement('script');s.src='https://turing.captcha.qcloud.com/TCaptcha.js';s.onload=function(){new TencentCaptcha("+Json.Write(appId)+",function(r){if(r.ret===0)send({kind:'captcha',ticket:r.ticket,randstr:r.randstr});else send({kind:'cancel'})}).show()};s.onerror=function(){send({kind:'error'})};document.head.appendChild(s);");
                var data=await Await(result.Task,linked.Token,180000);return new Dictionary<string,string>{{"captcha_type","confirm"},{"ticket",data["ticket"]},{"randstr",data["randstr"]}};
            }
        }
    }
}
