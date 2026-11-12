using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiyouDaily
{
    public interface ITransport
    {
        Task<Dictionary<string, object>> Send(string method, string url, Dictionary<string, string> headers, string body, CancellationToken cancel);
    }
    public sealed class Transport : ITransport, IDisposable
    {
        readonly HttpClient http;
        static readonly string[] Hosts = { "api-takumi.mihoyo.com", "bbs-api.miyoushe.com", "act-nap-api.mihoyo.com", "passport-api.mihoyo.com" };
        public Transport()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            http.Timeout = TimeSpan.FromSeconds(25);
        }
        public async Task<Dictionary<string, object>> Send(string method, string url, Dictionary<string, string> headers, string body, CancellationToken cancel)
        {
            var uri = new Uri(url);
            if (uri.Scheme != "https" || !Hosts.Contains(uri.Host)) throw new InvalidOperationException("接口地址不在米哈游服务列表中。");
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using (var request = new HttpRequestMessage(new HttpMethod(method), uri))
                    {
                        foreach (var item in headers) request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                        using (var response = await http.SendAsync(request, cancel).ConfigureAwait(false))
                        {
                            int status = (int)response.StatusCode;
                            if (status == 429 || status >= 500)
                            {
                                if (method == "GET" && attempt < 2) { await Task.Delay(1500 * (attempt + 1), cancel).ConfigureAwait(false); continue; }
                                throw new IOException("服务暂时不可用（HTTP " + status + "），稍后重试。");
                            }
                            if (!response.IsSuccessStatusCode) throw new IOException("接口返回 HTTP " + status + "。");
                            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (text.Length > 1024 * 1024) throw new IOException("接口响应超出大小限制。");
                            Dictionary<string, object> result;
                            try { result = Json.Read<Dictionary<string, object>>(text); }
                            catch (Exception ex)
                            { if (ex is ArgumentException || ex is InvalidOperationException) throw new IOException("接口返回的内容不是有效 JSON。"); throw; }
                            int code;
                            if (result == null || !result.ContainsKey("retcode") || !int.TryParse(Json.Str(result, "retcode"), out code))
                                throw new IOException("接口响应缺少 retcode，无法确认执行结果。");
                            return result;
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    if (method != "GET" || attempt >= 2) throw new IOException("网络连接失败，请检查网络或系统代理。");
                }
                catch (TaskCanceledException)
                {
                    cancel.ThrowIfCancellationRequested();
                    throw new IOException("请求超时，稍后可重新执行。");
                }
                await Task.Delay(1500 * (attempt + 1), cancel).ConfigureAwait(false);
            }
        }
        public void Dispose() { http.Dispose(); }
    }

    public class ApiException : Exception
    {
        public readonly bool NeedsAction;
        public ApiException(string message, bool needsAction) : base(message) { NeedsAction = needsAction; }
    }
    public static class ApiResult
    {
        public static bool Challenge(object response)
        {
            object data = Json.Get(response, "data");
            return Json.Num(response, "retcode") == 1034 || Json.Num(response, "retcode") == -3101 ||
                Json.Num(data, "success") == 1 || Json.Num(data, "risk_code") != 0 ||
                Json.Str(data, "gt") != "" || Json.Str(data, "challenge") != "";
        }
        public static void Require(object response)
        {
            if (Json.Get(response, "retcode") == null) throw new IOException("接口响应缺少 retcode。");
            if (Challenge(response)) throw new ApiException("触发官方人机验证，请在米游社 App 手动完成验证或签到。", true);
            int code = Json.Num(response, "retcode");
            if (code == 0) return;
            if (new[] { -100, 10103, 10104, -101 }.Contains(code))
                throw new ApiException("登录凭证已失效，请重新扫码登录（" + code + "）。", true);
            string message = Json.Str(response, "message");
            if (message.Length > 180) message = message.Substring(0, 180);
            throw new ApiException("接口返回：" + message + "（" + code + "）。", false);
        }
    }

    public sealed class MiyouApi
    {
        const string Passport = "https://passport-api.mihoyo.com";
        readonly Settings settings;
        readonly ITransport transport;
        readonly string cookie;
        public MiyouApi(Settings settings, string cookie, ITransport transport) { this.settings = settings; this.cookie = cookie; this.transport = transport; }
        Dictionary<string, string> BaseHeaders(string authCookie)
        {
            var h = new Dictionary<string, string> {
                {"User-Agent", "Mozilla/5.0 (Linux; Android 12) AppleWebKit/537.36 Chrome/103.0.5060.129 Mobile Safari/537.36 miHoYoBBS/" + settings.AppVersion},
                {"x-rpc-app_version", settings.AppVersion}, {"x-rpc-device_id", settings.DeviceId},
                {"Accept", "application/json"}, {"x-rpc-channel", "miyousheluodi"}
            };
            if (authCookie.Length > 0) h["Cookie"] = authCookie;
            if (!string.IsNullOrEmpty(settings.DeviceFp)) h["x-rpc-device_fp"] = settings.DeviceFp;
            return h;
        }
        Dictionary<string, string> GameHeaders(Game game)
        {
            var h = BaseHeaders(cookie); h["x-rpc-client_type"] = "5"; h["Referer"] = "https://act.mihoyo.com/";
            h["Origin"] = "https://act.mihoyo.com"; h["X-Requested-With"] = "com.mihoyo.hyperion";
            h["DS"] = Hashes.Ds(settings.WebSalt, false, "", "");
            if (game.SignGame.Length > 0) h["x-rpc-signgame"] = game.SignGame;
            return h;
        }
        static string Query(params string[] parts)
        {
            var pairs = new List<string>();
            for (int i = 0; i < parts.Length; i += 2) pairs.Add(Uri.EscapeDataString(parts[i]) + "=" + Uri.EscapeDataString(parts[i + 1]));
            return string.Join("&", pairs);
        }
        public async Task<List<Dictionary<string, object>>> Roles(Game game, CancellationToken cancel)
        {
            var response = await transport.Send("GET", "https://api-takumi.mihoyo.com/binding/api/getUserGameRolesByCookie?" + Query("game_biz", game.Biz), GameHeaders(game), null, cancel).ConfigureAwait(false);
            ApiResult.Require(response);
            if (Json.Get(Json.Get(response, "data"), "list") == null) throw new IOException("角色接口未返回角色列表。");
            return Json.Items(Json.Get(Json.Get(response, "data"), "list")).Select(Json.Obj).ToList();
        }
        public async Task<Dictionary<string, object>> Info(Game game, string region, string uid, CancellationToken cancel)
        {
            var response = await transport.Send("GET", game.Base + "/info?" + Query("lang", "zh-cn", "act_id", game.Act, "region", region, "uid", uid), GameHeaders(game), null, cancel).ConfigureAwait(false);
            ApiResult.Require(response);
            var data = Json.Obj(Json.Get(response, "data"));
            if (!data.ContainsKey("is_sign")) throw new IOException("签到状态接口缺少 is_sign，无法确认结果。");
            return data;
        }
        public async Task<string> SignGame(Game game, Dictionary<string, object> role, CancellationToken cancel)
        {
            string uid = Json.Str(role, "game_uid"), region = Json.Str(role, "region");
            if (uid == "" || region == "") throw new IOException("角色信息缺少 UID 或服务器。");
            var info = await Info(game, region, uid, cancel).ConfigureAwait(false);
            if (Json.Bool(info, "first_bind")) throw new ApiException("首次绑定，请先在米游社手动签到一次。", true);
            if (Json.Bool(info, "is_sign")) return "今日已签到；本月累计 " + Json.Num(info, "total_sign_day") + " 天";
            string body = Json.Write(new { act_id = game.Act, region = region, uid = uid });
            var response = await transport.Send("POST", game.Base + "/sign", GameHeaders(game), body, cancel).ConfigureAwait(false);
            bool alreadySigned = Json.Num(response, "retcode") == -5003;
            if (!alreadySigned) ApiResult.Require(response);
            await Task.Delay(700, cancel).ConfigureAwait(false);
            info = await Info(game, region, uid, cancel).ConfigureAwait(false);
            if (!Json.Bool(info, "is_sign")) throw new IOException("签到已提交，但状态尚未确认，稍后会再检查。");
            return (alreadySigned ? "今日已签到；本月累计 " : "签到成功；本月累计 ") + Json.Num(info, "total_sign_day") + " 天";
        }
        public async Task<string> SignForum(string gid, CancellationToken cancel)
        {
            var h = BaseHeaders(Cookies.Community(cookie));
            h["x-rpc-client_type"] = "2"; h["x-rpc-sys_version"] = "12"; h["x-rpc-device_name"] = "Xiaomi MI 6";
            h["x-rpc-device_model"] = "Mi 6"; h["x-rpc-h265_supported"] = "1"; h["x-rpc-verify_key"] = "bll8iq97cem8";
            h["x-rpc-csm_source"] = "home"; h["Referer"] = "https://app.mihoyo.com"; h["User-Agent"] = "okhttp/4.9.3";
            string body = Json.Write(new { gids = gid }); h["DS"] = Hashes.Ds(settings.X6Salt, true, body, "");
            var response = await transport.Send("POST", "https://bbs-api.miyoushe.com/apihub/app/api/signIn", h, body, cancel).ConfigureAwait(false);
            if (Json.Num(response, "retcode") == -5003) return "今日已签到";
            if (Json.Num(response, "retcode") == 1008)
            {
                string message=Json.Str(response,"message");if(message.Length>180)message=message.Substring(0,180);
                throw new ApiException("社区返回：" + message + "（1008）。签到状态待确认，请在米游社查看该板块。",false);
            }
            ApiResult.Require(response); return "社区签到成功";
        }
        public async Task<string> Missions(CancellationToken cancel)
        {
            var h = BaseHeaders(cookie); h["Referer"] = "https://webstatic.mihoyo.com";
            var response = await transport.Send("GET", "https://bbs-api.miyoushe.com/apihub/wapi/getUserMissionsState?point_sn=myb", h, null, cancel).ConfigureAwait(false);
            ApiResult.Require(response); object d = Json.Get(response, "data");
            if (Json.Get(d, "total_points") == null) return "米游币接口未返回余额。";
            return "米游币：今日已获得 " + Json.Num(d, "already_received_points") + "，当前余额 " + Json.Num(d, "total_points") + "。";
        }
        Dictionary<string, string> QrHeaders(string body)
        {
            var h = BaseHeaders(""); h["User-Agent"] = "Mozilla/5.0 miHoYoBBS/" + settings.PassportVersion + " Capture/2.2.0";
            h["x-rpc-client_type"] = "3"; h["x-rpc-app_version"] = settings.PassportVersion; h["x-rpc-game_biz"] = "bbs_cn";
            h["x-rpc-app_id"] = "bll8iq97cem8"; h["x-rpc-sdk_version"] = settings.PassportVersion;
            h["x-rpc-device_name"] = "Xiaomi MI 6"; h["x-rpc-device_model"] = "Mi 6"; h["x-rpc-account_version"] = settings.PassportVersion;
            h["DS"] = Hashes.Ds(settings.X4Salt, true, body, ""); return h;
        }
        public async Task<Dictionary<string, object>> CreateQr(CancellationToken cancel)
        {
            var response = await transport.Send("POST", Passport + "/account/ma-cn-passport/app/createQRLogin", QrHeaders("{}"), "{}", cancel).ConfigureAwait(false);
            ApiResult.Require(response); var data = Json.Obj(Json.Get(response, "data"));
            if (Json.Str(data, "url") == "" || Json.Str(data, "ticket") == "") throw new IOException("登录接口未返回二维码，请稍后重试或导入 Cookie。");
            return data;
        }
        public async Task<Dictionary<string, object>> PollQr(string ticket, CancellationToken cancel)
        {
            string body = Json.Write(new { ticket = ticket });
            var response = await transport.Send("POST", Passport + "/account/ma-cn-passport/app/queryQRLoginStatus", QrHeaders(body), body, cancel).ConfigureAwait(false);
            ApiResult.Require(response); return Json.Obj(Json.Get(response, "data"));
        }
        public async Task<string> FinishLogin(Dictionary<string, object> data, CancellationToken cancel)
        {
            object user = Json.Get(data, "user_info"); string uid = Json.Str(user, "aid"), mid = Json.Str(user, "mid");
            var tokens = Json.Items(Json.Get(data, "tokens")).Select(Json.Obj).ToList();
            var chosen = tokens.FirstOrDefault(x => Json.Str(x, "name") == "stoken" || Json.Num(x, "token_type") == 1) ?? tokens.FirstOrDefault();
            string stoken = Json.Str(chosen, "token");
            if (uid == "" || mid == "" || stoken == "") throw new IOException("扫码结果缺少登录凭证，请重试。");
            var values = new Dictionary<string, string> { {"stuid", uid}, {"stoken", stoken}, {"mid", mid}, {"account_id", uid}, {"account_id_v2", uid}, {"ltuid", uid}, {"ltuid_v2", uid}, {"account_mid_v2", mid}, {"ltmid_v2", mid} };
            foreach (string tokenName in new[] { "ltoken", "cookie_token" })
            {
                string query = Query("stoken", stoken);
                var h = BaseHeaders("mid=" + mid + "; stoken=" + stoken); h["x-rpc-client_type"] = tokenName == "ltoken" ? "5" : "2";
                h["Referer"] = "https://webstatic.mihoyo.com"; h["DS"] = Hashes.Ds(settings.X4Salt, true, "", query);
                string path = tokenName == "ltoken" ? "/account/auth/api/getLTokenBySToken" : "/account/auth/api/getCookieAccountInfoBySToken";
                var response = await transport.Send("GET", Passport + path + "?" + query, h, null, cancel).ConfigureAwait(false);
                ApiResult.Require(response); string token = Json.Str(Json.Get(response, "data"), tokenName);
                if (token == "") throw new IOException("扫码成功，但网页登录凭证未返回，请重试。");
                values[tokenName] = token; values[tokenName + "_v2"] = token;
            }
            return Cookies.Join(values);
        }
    }
}
