using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace MiyouDaily
{
    public static class Json
    {
        public static string Write(object value) { return new JavaScriptSerializer().Serialize(value); }
        public static T Read<T>(string text) { return new JavaScriptSerializer().Deserialize<T>(text); }
        public static Dictionary<string, object> Obj(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        public static object Get(object value, string key) { object found; return Obj(value).TryGetValue(key, out found) ? found : null; }
        public static string Str(object value, string key) { return Convert.ToString(Get(value, key), CultureInfo.InvariantCulture); }
        public static int Num(object value, string key) { int n; return int.TryParse(Str(value, key), out n) ? n : 0; }
        public static bool Bool(object value, string key) { bool b; return bool.TryParse(Str(value, key), out b) && b; }
        public static IEnumerable<object> Items(object value) { return (value as IEnumerable ?? new object[0]).Cast<object>(); }
    }

    public static class Clock
    {
        public static DateTimeOffset ChinaNow { get { return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)); } }
        public static string Day(DateTimeOffset now) { return now.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static TimeSpan ParseTime(string value)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                throw new ArgumentException("签到时间应为 24 小时制 HH:mm，例如 09:00。");
            return parsed.TimeOfDay;
        }
        public static bool IsDue(string value, DateTimeOffset now) { return now.ToOffset(TimeSpan.FromHours(8)).TimeOfDay >= ParseTime(value); }
    }

    public static class Cookies
    {
        public static Dictionary<string, string> Parse(string raw)
        {
            raw = (raw ?? "").Trim().Trim('"', '\'');
            if (raw.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(7);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in raw.Split(';'))
            {
                int equals = part.IndexOf('=');
                if (equals <= 0) continue;
                string name = part.Substring(0, equals).Trim();
                string value = part.Substring(equals + 1).Trim();
                if (!Regex.IsMatch(name, "^[a-zA-Z0-9_]+$") || Regex.IsMatch(value, "[\\r\\n\\x00-\\x1f]"))
                    throw new ArgumentException("Cookie 格式不正确，请只复制请求头中的 Cookie 内容。");
                if (value.Length > 0) result[name] = value;
            }
            return result;
        }
        public static string Pick(Dictionary<string, string> values, params string[] names)
        {
            foreach (string name in names) { string value; if (values.TryGetValue(name, out value) && value.Length > 0) return value; }
            return "";
        }
        public static string Join(Dictionary<string, string> values) { return string.Join("; ", values.Select(x => x.Key + "=" + x.Value)); }
        public static string Normalize(string raw) { return Join(Parse(raw)); }
        public static string Community(string raw)
        {
            var values = Parse(raw);
            string uid = Pick(values, "stuid", "stuid_v2", "account_id_v2", "account_id", "ltuid_v2", "ltuid");
            string token = Pick(values, "stoken", "stoken_v2");
            string mid = Pick(values, "mid", "mid_v2", "account_mid_v2", "ltmid_v2");
            if (uid == "" || token == "") throw new InvalidOperationException("社区签到缺少 stuid/stoken，请扫码登录，或导入含 stoken 的 Cookie。");
            if (token.StartsWith("v2_", StringComparison.Ordinal) && mid == "")
                throw new InvalidOperationException("stoken v2 缺少 mid，请重新扫码登录。");
            return "stuid=" + uid + "; stoken=" + token + (mid == "" ? "" : "; mid=" + mid);
        }
        public static string Account(string raw) { return Pick(Parse(raw), "stuid", "stuid_v2", "account_id_v2", "account_id", "ltuid_v2", "ltuid"); }
    }

    public static class Hashes
    {
        public static string Md5(string text)
        {
            using (var hash = MD5.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        public static string Sha256(string text)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        public static string Ds1(string salt, string time, string random) { return time + "," + random + "," + Md5("salt=" + salt + "&t=" + time + "&r=" + random); }
        public static string Ds2(string salt, string time, string random, string body, string query)
        { return time + "," + random + "," + Md5("salt=" + salt + "&t=" + time + "&r=" + random + "&b=" + body + "&q=" + query); }
        public static string Ds(string salt, bool version2, string body, string query)
        {
            string time = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(CultureInfo.InvariantCulture);
            byte[] bytes = new byte[4]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            string random = version2 ? (100001 + BitConverter.ToUInt32(bytes, 0) % 100000).ToString(CultureInfo.InvariantCulture) : Guid.NewGuid().ToString("N").Substring(0, 6);
            return version2 ? Ds2(salt, time, random, body, query) : Ds1(salt, time, random);
        }
    }

    public class Settings
    {
        public int Version { get; set; }
        public string ProtectedCookie { get; set; }
        public bool HeyboxEnabled { get; set; }
        public string ProtectedHeyboxConfig { get; set; }
        public string DeviceId { get; set; }
        public string DeviceFp { get; set; }
        public string DailyTime { get; set; }
        public bool ExitWidgetAfterCheckin { get; set; }
        public string[] Games { get; set; }
        public string[] Forums { get; set; }
        public string AppVersion { get; set; }
        public string WebSalt { get; set; }
        public string X6Salt { get; set; }
        public string PassportVersion { get; set; }
        public string X4Salt { get; set; }
        public Settings()
        {
            Version = 1; ProtectedCookie = ""; DeviceId = Guid.NewGuid().ToString().ToUpperInvariant(); DeviceFp = "";
            DailyTime = "09:00"; Games = new[] { "genshin", "starrail", "zzz", "honkai3rd" }; Forums = new[] { "5", "2", "6", "8", "1" };
            AppVersion = "2.117.0"; WebSalt = "4751cb5557bfad5e249e58462c39fb0a";
            X6Salt = "t0qEgfub6cvueAPgR5m9aQWWVciEer7v"; X4Salt = "xV8v4Qu54lUKrEYFZkJhB8cuOh9Asafs"; PassportVersion = "2.90.1";
        }
        public void Validate()
        {
            Clock.ParseTime(DailyTime);
            if (Games == null || Forums == null || Games.Any(x => !Catalog.Games.Any(g => g.Key == x)) || Forums.Any(x => !Catalog.Forums.ContainsKey(x)))
                throw new ArgumentException("配置中的游戏或社区板块无效。");
            if (Games.Length + Forums.Length == 0 && !HeyboxEnabled) throw new ArgumentException("至少选择一个游戏、社区板块或小黑盒签到。");
            if (string.IsNullOrWhiteSpace(DeviceId) || string.IsNullOrWhiteSpace(AppVersion) || string.IsNullOrWhiteSpace(WebSalt) || string.IsNullOrWhiteSpace(X6Salt) || string.IsNullOrWhiteSpace(X4Salt))
                throw new ArgumentException("接口配置不完整。");
        }
    }

    public class Game
    {
        public string Key, Name, Biz, Act, Base, SignGame;
        public Game(string key, string name, string biz, string act, string signGame)
        { Key = key; Name = name; Biz = biz; Act = act; SignGame = signGame; Base = key == "zzz" ? "https://act-nap-api.mihoyo.com/event/luna/zzz" : "https://api-takumi.mihoyo.com/event/luna"; }
        public override string ToString() { return Name; }
    }
    public static class Catalog
    {
        public static readonly Game[] Games = {
            new Game("genshin", "原神", "hk4e_cn", "e202311201442471", "hk4e"),
            new Game("starrail", "崩坏：星穹铁道", "hkrpg_cn", "e202304121516551", ""),
            new Game("zzz", "绝区零", "nap_cn", "e202406242138391", "zzz"),
            new Game("honkai3rd", "崩坏3", "bh3_cn", "e202306201626331", ""),
            new Game("tears", "未定事件簿", "nxx_cn", "e202202251749321", ""),
            new Game("honkai2", "崩坏学园2", "bh2_cn", "e202203291431091", "")
        };
        public static readonly Dictionary<string, string> Forums = new Dictionary<string, string> {
            {"5", "大别野"}, {"2", "原神"}, {"6", "崩坏：星穹铁道"}, {"8", "绝区零"}, {"1", "崩坏3"}, {"4", "未定事件簿"}, {"3", "崩坏学园2"}
        };
    }

    public class Attempt
    {
        public int Count { get; set; }
        public bool Complete { get; set; }
        public bool NeedsAction { get; set; }
        public string Message { get; set; }
    }
    public class RunState
    {
        public string Day { get; set; }
        public Dictionary<string, Attempt> Targets { get; set; }
        public RunState() { Day = ""; Targets = new Dictionary<string, Attempt>(); }
        public void ForDay(string day) { if (Day != day) { Day = day; Targets.Clear(); } }
        public bool Skip(string key, bool scheduled)
        { Attempt a; return Targets.TryGetValue(key, out a) && (a.Complete || (scheduled && (a.NeedsAction || a.Count >= 3))); }
    }

    public sealed class Store
    {
        public readonly string Root;
        public string ConfigPath { get { return Path.Combine(Root, "config.json"); } }
        public string StatePath { get { return Path.Combine(Root, "state.json"); } }
        public Store(string root) { Root = Path.GetFullPath(root); Directory.CreateDirectory(Root); }
        public static Store Default() { return DataLocation.Open(); }
        public static void AtomicWrite(string path, string text)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary, text, new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public Settings Load()
        {
            if (!File.Exists(ConfigPath)) return new Settings();
            var result = Json.Read<Settings>(File.ReadAllText(ConfigPath));
            if (result == null || result.Version != 1) throw new InvalidDataException("配置文件版本无效。");
            result.Validate(); return result;
        }
        public void Save(Settings settings) { settings.Validate(); AtomicWrite(ConfigPath, Json.Write(settings)); }
        public string Decrypt(Settings settings)
        {
            if (string.IsNullOrEmpty(settings.ProtectedCookie)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(settings.ProtectedCookie), null, DataProtectionScope.CurrentUser)); }
            catch (CryptographicException) { throw new InvalidOperationException("无法解密登录凭证，请在当前 Windows 账号下重新扫码登录。"); }
            catch (FormatException) { throw new InvalidOperationException("登录凭证格式无效，请重新扫码登录。"); }
        }
        public void SetCookie(Settings settings, string raw)
        {
            string cookie = Cookies.Normalize(raw);
            if (cookie.Length == 0 || Cookies.Account(cookie).Length == 0) throw new ArgumentException("Cookie 为空或缺少账号 UID。");
            settings.ProtectedCookie = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(cookie), null, DataProtectionScope.CurrentUser));
        }
        public RunState LoadState() { return File.Exists(StatePath) ? Json.Read<RunState>(File.ReadAllText(StatePath)) : new RunState(); }
        public void SaveState(RunState state) { AtomicWrite(StatePath, Json.Write(state)); }
        public FileStream AcquireRunLock()
        {
            try { return new FileStream(Path.Combine(Root, "run.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new InvalidOperationException("另一项签到正在运行，请稍后查看日志。"); }
        }
        public string LatestLog { get { return Path.Combine(Root, "logs", Clock.Day(Clock.ChinaNow) + ".log"); } }
        public void Log(string message)
        {
            Directory.CreateDirectory(Path.Combine(Root, "logs"));
            File.AppendAllText(LatestLog, Clock.ChinaNow.ToString("HH:mm:ss") + "  " + message.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8);
        }
        public static string Redact(string message, string cookie)
        {
            foreach (var pair in Cookies.Parse(cookie).Where(x => x.Value.Length >= 4).OrderByDescending(x => x.Value.Length)) message = message.Replace(pair.Value, "[已隐藏]");
            return Regex.Replace(message, "(?i)(stoken|ltoken|cookie_token|ticket|mid|pkey|x_xhh_tokenid|phone_num|code)(?:_v2)?[=:]\\s*[^\\s;,]+", "$1=[已隐藏]");
        }
    }
}
