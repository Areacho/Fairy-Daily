// Protocol adapted from Cansiny0320/heybox-daily, MIT. See HEYBOX-LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MiyouDaily
{
    // These property names also accept heybox-daily's exported config.json.
    public sealed class HeyboxSession
    {
        public string heybox_id {get;set;} public string pkey {get;set;}
        public string device_id {get;set;} public string x_xhh_tokenid {get;set;}
    }
    public sealed class HeyboxDevice {public string model {get;set;} public string osVersion {get;set;}}
    public sealed class HeyboxApp {public string version {get;set;} public string build {get;set;} public string shaderVersion {get;set;}}
    public sealed class HeyboxProfile {public string nickname {get;set;}}
    public sealed class HeyboxConfig
    {
        public HeyboxSession session {get;set;} public HeyboxDevice device {get;set;}
        public HeyboxApp app {get;set;} public HeyboxProfile profile {get;set;}
        public static HeyboxConfig Login(string token)
        {
            if(!Regex.IsMatch(token??"",@"^B[A-Za-z0-9+/=_-]{88}$"))throw new ArgumentException("设备令牌无效，请重新准备登录。");
            return new HeyboxConfig{session=new HeyboxSession{heybox_id="-1",pkey="",device_id=Guid.NewGuid().ToString(),x_xhh_tokenid=token},device=new HeyboxDevice{model="PKJ110",osVersion="17"},app=new HeyboxApp{version="1.3.398",build="1142",shaderVersion="14"}};
        }
        public void Validate()
        {
            if(session==null||device==null||app==null)throw new ArgumentException("小黑盒配置缺少账号、设备或版本信息。");
            foreach(string value in new[]{session.heybox_id,session.pkey,session.device_id,session.x_xhh_tokenid,device.model,device.osVersion,app.version,app.build,app.shaderVersion})
                if(string.IsNullOrWhiteSpace(value)||value.Length>8192||value.Any(c=>char.IsControl(c)||c==';'))throw new ArgumentException("小黑盒登录配置不完整或包含无效字符。");
            if(!Regex.IsMatch(session.heybox_id,@"^\d{1,24}$"))throw new ArgumentException("小黑盒配置缺少有效盒友 ID。");
            if(app.version!="1.3.398"||app.build!="1142"||app.shaderVersion!="14")throw new ArgumentException("该小黑盒配置的接口版本尚未适配，请使用当前登录入口重新登录。");
        }
        public string Key {get{return "heybox/"+Hashes.Sha256("uid:"+session.heybox_id).Substring(0,16)+"/sign";}}
    }
    public static class HeyboxAccount
    {
        public static HeyboxConfig Load(Settings settings)
        {
            if(string.IsNullOrEmpty(settings.ProtectedHeyboxConfig))return null;
            try
            {
                string raw=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(settings.ProtectedHeyboxConfig),null,DataProtectionScope.CurrentUser));
                var config=Json.Read<HeyboxConfig>(raw);if(config==null)throw new ArgumentException();config.Validate();return config;
            }
            catch(Exception ex){if(ex is CryptographicException||ex is FormatException||ex is ArgumentException||ex is InvalidOperationException)throw new InvalidOperationException("无法读取小黑盒登录状态，请在此 Windows 账号下重新登录。");throw;}
        }
        public static void Set(Settings settings,HeyboxConfig config)
        {
            config.Validate();settings.ProtectedHeyboxConfig=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Write(config)),null,DataProtectionScope.CurrentUser));
        }
        public static string Redact(string message,HeyboxConfig config)
        {
            if(config==null||config.session==null)return Store.Redact(message,"");
            return Store.Redact(message,"pkey="+config.session.pkey+"; x_xhh_tokenid="+config.session.x_xhh_tokenid+"; device_id="+config.session.device_id);
        }
        public static Settings Only(Settings settings)
        {
            var scoped=Json.Read<Settings>(Json.Write(settings));scoped.Games=new string[0];scoped.Forums=new string[0];scoped.HeyboxEnabled=true;return scoped;
        }
    }
    public sealed class HeyboxCaptchaException : ApiException
    {
        public readonly string AppId;
        public HeyboxCaptchaException(string appId):base("请在验证窗口中完成小黑盒人机验证。",true){AppId=Regex.IsMatch(appId??"",@"^\d{1,32}$")?appId:"";}
    }
    public sealed class HeyboxTransport : ITransport,IDisposable
    {
        readonly HttpClient http;
        public HeyboxTransport()
        {
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate});http.Timeout=TimeSpan.FromSeconds(25);
        }
        public async Task<Dictionary<string,object>> Send(string method,string url,Dictionary<string,string> headers,string body,CancellationToken cancel)
        {
            var uri=new Uri(url);if(uri.Scheme!="https"||uri.Host!="api.xiaoheihe.cn")throw new InvalidOperationException("小黑盒接口地址无效。");
            // The sign endpoint is a GET that changes state. Never replay a request.
            try
            {
                using(var request=new HttpRequestMessage(new HttpMethod(method),uri))
                {
                    foreach(var header in headers)request.Headers.TryAddWithoutValidation(header.Key,header.Value);
                    if(body!=null)request.Content=new StringContent(body,Encoding.UTF8,"application/x-www-form-urlencoded");
                    using(var response=await http.SendAsync(request,cancel).ConfigureAwait(false))
                    {
                        int status=(int)response.StatusCode;
                        if(status==429)throw new ApiException("小黑盒请求过于频繁，请稍后手动重试。",true);
                        if(!response.IsSuccessStatusCode)throw new IOException("小黑盒返回 HTTP "+status+"，请稍后重试。");
                        string text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);if(text.Length>1024*1024)throw new IOException("小黑盒响应超出大小限制。");
                        try{var result=Json.Read<Dictionary<string,object>>(text);if(result==null||!result.ContainsKey("status"))throw new IOException("小黑盒响应缺少状态，无法确认结果。");return result;}
                        catch(ArgumentException){throw new IOException("小黑盒响应不是有效 JSON。");}catch(InvalidOperationException){throw new IOException("小黑盒响应不是有效 JSON。");}
                    }
                }
            }
            catch(HttpRequestException){throw new IOException("无法连接小黑盒，请检查网络或系统代理。");}
            catch(TaskCanceledException){cancel.ThrowIfCancellationRequested();throw new IOException("小黑盒请求超时，请稍后检查状态。");}
        }
        public void Dispose(){http.Dispose();}
    }
    public sealed class HeyboxApi
    {
        readonly HeyboxConfig config;readonly ITransport transport;
        public HeyboxApi(HeyboxConfig config,ITransport transport){this.config=config;this.transport=transport;}
        public static string NormalizePhone(string value)
        {
            string phone=(value??"").Trim();if(Regex.IsMatch(phone,@"^1[3-9]\d{9}$"))return "+86"+phone;
            if(Regex.IsMatch(phone,@"^\+[1-9]\d{7,14}$"))return phone;throw new ArgumentException("请输入正确的手机号；海外号码请包含 + 和国家或地区区号。");
        }
        public static string EncryptPhone(string phone)
        {
            const string modulus="2YI1cAIik42eeZxvpluq900lOBArzWKgzCEv24Qo9kGh+RiPymW9kwafggb1I/UixZ5iUNJMx8x6BmQkPPFXrwn3xeyXjxhfXJikKqLZX0ym1eKMVeYi8+yUhum/1YOwF7a06JTbVrGvB4gdLSNGApWGkJTl5uFMS3ksUMEhRE8=";
            using(var rsa=new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp=false;rsa.ImportParameters(new RSAParameters{Modulus=Convert.FromBase64String(modulus),Exponent=new byte[]{1,0,1}});
                string raw=Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(NormalizePhone(phone)),false));
                return string.Join("\n",Enumerable.Range(0,(raw.Length+75)/76).Select(i=>raw.Substring(i*76,Math.Min(76,raw.Length-i*76))))+"\n";
            }
        }
        static string Encode(IEnumerable<KeyValuePair<string,string>> values){return string.Join("&",values.Select(x=>Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value)));}
        public async Task<Dictionary<string,object>> Native(string endpoint,Dictionary<string,string> extra,string body,CancellationToken cancel)
        {
            var session=config.session;var device=config.device;var app=config.app;
            var query=new Dictionary<string,string>{{"heybox_id",session.heybox_id},{"imei",session.device_id},{"device_info",device.model},{"os_type","Android"},{"x_os_type","Android"},{"x_client_type","mobile"},{"os_version",device.osVersion},{"version",app.version},{"build",app.build},{"dw","360"},{"channel","heybox"},{"x_app","heybox"},{"time_zone","Asia/Shanghai"}};
            if(extra!=null)foreach(var item in extra)query[item.Key]=item.Value;
            string time=((long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds).ToString(CultureInfo.InvariantCulture);
            foreach(var item in HeyboxSignature.Sign(HeyboxSignature.NormalizePath(endpoint),time,session.device_id+session.heybox_id,device.osVersion,app.version))query[item.Key]=item.Value;
            var headers=new Dictionary<string,string>{{"User-Agent","Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2272.118 Safari/537.36 ApiMaxJia/1.0"},{"Accept","application/json"},{"Referer","http://api.maxjia.com/"},{"Cookie","pkey="+session.pkey+"; x_xhh_tokenid="+session.x_xhh_tokenid}};
            var response=await transport.Send(body==null?"GET":"POST","https://api.xiaoheihe.cn"+endpoint+"?"+Encode(query),headers,body,cancel).ConfigureAwait(false);
            string status=Json.Str(response,"status"),message=Json.Str(response,"msg");
            if(status=="show_captcha")throw new HeyboxCaptchaException(Json.Str(Json.Get(response,"result"),"appid"));
            if(status=="relogin"||status=="lack_token")throw new ApiException("小黑盒登录已过期或设备令牌失效，请重新登录。",true);
            if(status!="ok")
            {
                if(message.Contains("频繁")||message.Contains("限制"))throw new ApiException("小黑盒请求过于频繁，请稍后重试。",true);
                if(message.Contains("验证码"))throw new ApiException(message.Contains("过期")||message.Contains("失效")?"短信验证码已过期，请重新获取。":"短信验证码不正确，请检查后重试。",true);
                throw new ApiException("小黑盒未接受本次操作，请稍后重试或重新登录。",true);
            }
            if(endpoint=="/account/get_login_code/")return Json.Obj(Json.Get(response,"result"));
            if(!response.ContainsKey("result")||!(response["result"] is Dictionary<string,object>))throw new IOException("小黑盒响应缺少结果，无法确认操作成功。");
            return Json.Obj(response["result"]);
        }
        public Task<Dictionary<string,object>> SendCode(string phone,Dictionary<string,string> captcha,CancellationToken cancel)
        {return Native("/account/get_login_code/",captcha,"phone_num="+Uri.EscapeDataString(EncryptPhone(phone)),cancel);}
        public async Task<HeyboxConfig> VerifyCode(string phone,string code,Dictionary<string,string> captcha,CancellationToken cancel)
        {
            if(!Regex.IsMatch(code??"",@"^\d{4,8}$"))throw new ArgumentException("验证码应为 4 至 8 位数字。");
            var query=new Dictionary<string,string>{{"code",code},{"is_new_device","1"}};if(captcha!=null)foreach(var item in captcha)query[item.Key]=item.Value;
            var result=await Native("/account/login_code/",query,"phone_num="+Uri.EscapeDataString(EncryptPhone(phone)),cancel).ConfigureAwait(false);
            string id=Json.Str(Json.Get(result,"profile"),"heybox_id"),pkey=Json.Str(result,"pkey");
            if(!Regex.IsMatch(id,@"^\d{1,24}$")||id!=Json.Str(Json.Get(result,"account_detail"),"userid")||pkey.Length==0)throw new IOException("小黑盒未返回有效登录凭证，原登录状态已保留。");
            var authenticated=Json.Read<HeyboxConfig>(Json.Write(config));authenticated.session.heybox_id=id;authenticated.session.pkey=pkey;authenticated.profile=new HeyboxProfile{nickname=Json.Str(Json.Get(result,"profile"),"nickname")};authenticated.Validate();
            await new HeyboxApi(authenticated,transport).State(cancel).ConfigureAwait(false);return authenticated;
        }
        public async Task<Dictionary<string,object>> State(CancellationToken cancel)
        {
            var state=await Native("/task/sign_v4/get_sign_state",null,null,cancel).ConfigureAwait(false);
            if(Json.Str(state,"state").Length==0)throw new IOException("小黑盒未返回签到状态，无法确认账号或签到结果。");return state;
        }
        public static bool SignedToday(object state,DateTimeOffset now)
        {
            long date;long day=(long)Math.Floor(((now.ToUniversalTime()-new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero)).TotalSeconds+8*3600)/86400);
            return new[]{"ok","ignore"}.Contains(Json.Str(state,"state"))&&long.TryParse(Json.Str(state,"signed_date"),out date)&&date==day;
        }
        public async Task<string> Sign(CancellationToken cancel)
        {
            if(SignedToday(await State(cancel).ConfigureAwait(false),Clock.ChinaNow))return "今日已签到";
            Exception uncertain=null;
            try{await Native("/task/sign_v3/sign",null,null,cancel).ConfigureAwait(false);}
            catch(IOException ex){uncertain=ex;}
            // Confirm a potentially successful submission rather than replaying it.
            foreach(int delay in new[]{300,1000,2000})
            {
                await Task.Delay(delay,cancel).ConfigureAwait(false);
                if(SignedToday(await State(cancel).ConfigureAwait(false),Clock.ChinaNow))return "小黑盒签到成功";
            }
            if(uncertain!=null)throw uncertain;throw new IOException("小黑盒签到尚未确认完成，请稍后检查或在 App 中确认。");
        }
    }
}
