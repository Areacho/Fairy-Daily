using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MiyouDaily;

partial class Tests
{
    static int passed;
    static readonly CancellationToken None = CancellationToken.None;
    static void Assert(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); passed++; Console.WriteLine("PASS: " + label); }
    static void Throws<T>(Action action, string label) where T : Exception
    { try { action(); } catch (T) { Assert(true, label); return; } throw new Exception("FAIL: " + label + " did not throw"); }
    static Dictionary<string, object> Response(string data) { return Json.Read<Dictionary<string, object>>(data); }
    const string Ok = "{\"retcode\":0,\"data\":{}}";
    const string Unsigned = "{\"retcode\":0,\"data\":{\"is_sign\":false,\"total_sign_day\":0}}";
    const string Signed = "{\"retcode\":0,\"data\":{\"is_sign\":true,\"total_sign_day\":1}}";
    const string FakeCookie = "stuid=12345678; stoken=v2_SYNTHETIC_TEST_TOKEN; mid=FAKE_MID_123; ltoken=FAKE_LTOKEN_123";

    sealed class Mock : ITransport
    {
        public readonly Queue<Func<string, string, Dictionary<string, string>, string, Dictionary<string, object>>> Steps = new Queue<Func<string, string, Dictionary<string, string>, string, Dictionary<string, object>>>();
        public int Count;
        public Mock Add(string method, string path, string response)
        {
            Steps.Enqueue((m, u, h, b) => { if (method != m || !u.Contains(path)) throw new Exception("Unexpected API call: " + m + " " + u); return Response(response); }); return this;
        }
        public Mock Fail(string path)
        { Steps.Enqueue((m, u, h, b) => { if (!u.Contains(path)) throw new Exception("Wrong failure URL"); throw new IOException("Synthetic network failure"); }); return this; }
        public Task<Dictionary<string, object>> Send(string m, string u, Dictionary<string, string> h, string b, CancellationToken token)
        { Count++; token.ThrowIfCancellationRequested(); if (Steps.Count == 0) throw new Exception("Unexpected extra API call"); return Task.FromResult(Steps.Dequeue()(m, u, h, b)); }
    }
    static Store TestStore()
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "build", "tests", Guid.NewGuid().ToString("N")));
        return new Store(root);
    }
    static Settings Setup(Store store, string[] games, string[] forums)
    {
        var config = new Settings { Games = games, Forums = forums, DailyTime = "00:00" }; store.SetCookie(config, FakeCookie); store.Save(config); return config;
    }
    static void Basic()
    {
        Assert(Hashes.Md5("abc") == "900150983cd24fb0d6963f7d28e17f72", "MD5 known vector");
        Assert(Hashes.Ds1("test", "1700000000", "abcdef") == "1700000000,abcdef,7154775712c3c6d6dc601216cce46950", "DS1 known vector");
        Assert(Hashes.Ds2("test", "1700000000", "123456", "{\"gids\":\"5\"}", "") == "1700000000,123456,16cee2071bf23baf346fb7dbe85ef49d", "DS2 signs exact JSON bytes");
        Assert(Cookies.Parse(" Cookie: stuid=1; stoken=a=b=c; ")["stoken"] == "a=b=c", "cookie preserves equals signs");
        Assert(Cookies.Community(FakeCookie) == "stuid=12345678; stoken=v2_SYNTHETIC_TEST_TOKEN; mid=FAKE_MID_123", "community auth excludes web credentials");
        Throws<InvalidOperationException>(() => Cookies.Community("stuid=1; stoken=v2_missingmid"), "v2 token requires mid");
        Throws<ArgumentException>(() => Cookies.Parse("stuid=1; stoken=a\r\nHeader=bad"), "reject header injection");
        Assert(Clock.Day(DateTimeOffset.Parse("2026-11-11T17:01:00Z")) == "2026-11-12", "day boundary uses Beijing time");
        Assert(!Clock.IsDue("09:00", DateTimeOffset.Parse("2026-11-11T00:59:00Z")) && Clock.IsDue("09:00", DateTimeOffset.Parse("2026-11-11T01:00:00Z")), "daily cutoff uses Beijing time");
        Throws<ArgumentException>(() => Clock.ParseTime("24:00"), "reject invalid time");
        var state = new RunState(); state.ForDay("2026-11-11"); state.Targets["one"] = new Attempt { NeedsAction = true, Count = 1 };
        Assert(state.Skip("one", true) && !state.Skip("one", false), "manual retry can revisit captcha or expired auth");
        state.Targets["one"] = new Attempt { Count = 3 }; Assert(state.Skip("one", true), "automatic attempts are capped");
        state.ForDay("2026-11-12"); Assert(state.Targets.Count == 0, "new day resets successful and failed states");
        var store = TestStore(); var config = Setup(store, new[] {"genshin"}, new string[0]);
        Assert(store.Decrypt(store.Load()) == Cookies.Normalize(FakeCookie), "DPAPI round trip");
        Assert(!File.ReadAllText(store.ConfigPath).Contains("SYNTHETIC_TEST_TOKEN"), "config never stores plaintext cookie");
        using (store.AcquireRunLock()) Throws<InvalidOperationException>(() => store.AcquireRunLock(), "cross-process run lock");
        string redacted = Store.Redact("stoken=v2_SYNTHETIC_TEST_TOKEN cookie_token=secret ltoken=FAKE_LTOKEN_123", FakeCookie);
        Assert(!redacted.Contains("SYNTHETIC") && !redacted.Contains("secret") && !redacted.Contains("FAKE_LTOKEN"), "redact tokens from diagnostics");
        Throws<ApiException>(() => ApiResult.Require(Response("{\"retcode\":0,\"data\":{\"success\":1,\"challenge\":\"test\"}}")), "retcode zero with captcha is not success");
        Throws<ApiException>(() => ApiResult.Require(Response("{\"retcode\":1034}")), "captcha is actionable");
        Throws<ApiException>(() => ApiResult.Require(Response("{\"retcode\":-100}")), "expired credentials are actionable");
        Throws<IOException>(() => ApiResult.Require(Response("{\"data\":{}}")), "malformed envelope is not success");
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var xml = XDocument.Parse(Scheduler.MakeXml("C:\\中文 & spaces\\MiyouDaily.exe", "S-1-5-21-test", "09:00", DateTimeOffset.Parse("2026-11-11T00:00:00Z")));
        Assert(xml.Descendants(ns + "Command").Single().Value == "C:\\中文 & spaces\\MiyouDaily.exe", "task paths preserve Unicode and XML escaping");
        Assert(xml.Descendants(ns + "StartBoundary").Single().Value.EndsWith("+08:00"), "task trigger is pinned to UTC+8");
        Assert(xml.Descendants(ns + "LogonType").Single().Value == "InteractiveToken" && xml.Descendants(ns + "StartWhenAvailable").Single().Value == "true", "task uses logged-on user and missed-run recovery");
        Assert(xml.Descendants(ns + "Arguments").Single().Value == "--run --scheduled", "scheduler starts headless path");
    }
    static async Task ApiTests()
    {
        var game = Catalog.Games[0]; var role = Response("{\"game_uid\":\"100000001\",\"region\":\"cn_gf01\"}");
        var mock = new Mock().Add("GET", "/info?", Signed);
        Assert((await new MiyouApi(new Settings(), FakeCookie, mock).SignGame(game, role, None)).Contains("已签到") && mock.Count == 1, "already signed role sends no POST");
        mock = new Mock().Add("GET", "/info?", Unsigned).Add("POST", "/sign", Ok).Add("GET", "/info?", Signed);
        Assert((await new MiyouApi(new Settings(), FakeCookie, mock).SignGame(game, role, None)).Contains("成功") && mock.Count == 3, "successful POST requires status confirmation");
        mock = new Mock().Add("GET", "/info?", Unsigned).Add("POST", "/sign", "{\"retcode\":-5003}").Add("GET", "/info?", Signed);
        Assert((await new MiyouApi(new Settings(), FakeCookie, mock).SignGame(game, role, None)).StartsWith("今日已签到") && mock.Count == 3, "concurrent sign-in reported by server remains already signed after verification");
        mock = new Mock().Add("GET", "/info?", Unsigned).Add("POST", "/sign", "{\"retcode\":0,\"data\":{\"success\":1}}");
        try { await new MiyouApi(new Settings(), FakeCookie, mock).SignGame(game, role, None); throw new Exception("captcha falsely accepted"); } catch (ApiException e) { Assert(e.NeedsAction && mock.Count == 2, "captcha POST never becomes successful result"); }
        mock = new Mock();
        mock.Steps.Enqueue((m, u, h, b) => {
            Assert(m == "POST" && b == "{\"gids\":\"5\"}", "community endpoint receives exact gids string");
            Assert(h["Cookie"].Contains("stoken=") && !h["Cookie"].Contains("ltoken="), "community endpoint receives app credentials");
            string[] ds = h["DS"].Split(','); Assert(h["DS"] == Hashes.Ds2(new Settings().X6Salt, ds[0], ds[1], b, ""), "community signature matches transmitted body"); return Response(Ok);
        });
        await new MiyouApi(new Settings(), FakeCookie, mock).SignForum("5", None);
        mock=new Mock().Add("POST","signIn","{\"retcode\":1008,\"message\":\"打卡失败或重复打卡\"}");
        try{await new MiyouApi(new Settings(),FakeCookie,mock).SignForum("5",None);throw new Exception("ambiguous community status falsely succeeded");}
        catch(ApiException ex){Assert(!ex.NeedsAction&&ex.Message.Contains("打卡失败或重复打卡")&&!ex.Message.Contains("登录"),"community 1008 preserves the real duplicate-or-failed response without falsely requesting login");}
    }
    static async Task PartialForumRetries()
    {
        var store = TestStore(); var settings = Setup(store, new string[0], new[] { "5", "2" });
        var mock = new Mock().Add("POST", "signIn", Ok).Fail("signIn").Add("POST", "signIn", Ok);
        var runner = new Runner(store, mock, null); var first = await runner.Run(settings, true, None); var second = await runner.Run(settings, true, None);
        Assert(first.Success == 1 && first.Failed == 1 && second.Success == 1 && second.Skipped == 1 && mock.Count == 3, "partial community failure retries only failed target");
        Assert(first.NewlySigned == 1 && !CheckinStatus.CompletionMessage(store, settings, first).Equals(CheckinStatus.AlreadyCompleteMessage), "partial failure never announces already completed");
        await runner.Run(settings, true, None); Assert(mock.Count == 3, "completed day sends no further requests");
        Assert(!File.ReadAllText(store.LatestLog).Contains("SYNTHETIC"), "run logs contain no login token");
    }
    static async Task PartialRoleRetries()
    {
        const string roles = "{\"retcode\":0,\"data\":{\"list\":[{\"game_uid\":\"1001\",\"region\":\"cn_gf01\",\"nickname\":\"A\"},{\"game_uid\":\"1002\",\"region\":\"cn_qd01\",\"nickname\":\"B\"}]}}";
        var store = TestStore(); var settings = Setup(store, new[] {"genshin"}, new string[0]);
        var mock = new Mock().Add("GET", "getUserGameRoles", roles).Add("GET", "uid=1001", Unsigned).Add("POST", "/sign", "{\"retcode\":1034}").Fail("uid=1002")
            .Add("GET", "getUserGameRoles", roles).Add("GET", "uid=1002", Signed);
        var runner = new Runner(store, mock, null); var first = await runner.Run(settings, true, None); var second = await runner.Run(settings, true, None);
        Assert(first.Failed == 2 && second.Success == 1 && mock.Count == 6, "captcha on one role does not suppress retry for another role");
        Assert(!CheckinStatus.IsTodayComplete(store, settings), "unfinished role prevents a completed-day announcement");
    }
    static RunState CompletedState(Settings settings, string cookie)
    {
        var state = new RunState(); state.ForDay(Clock.Day(Clock.ChinaNow));
        string prefix = AccountScope.Prefix(Cookies.Normalize(cookie));
        foreach (string game in settings.Games)
        {
            state.Targets[prefix + "game/" + game] = new Attempt { Complete = true };
            state.Targets[prefix + "game/" + game + "/cn_gf01/1001"] = new Attempt { Complete = true, Message = "今日已签到" };
        }
        foreach (string forum in settings.Forums) state.Targets[prefix + "forum/" + forum] = new Attempt { Complete = true, Message = "今日已签到" };
        return state;
    }
    static async Task CompletionChecks()
    {
        var store = TestStore(); var settings = Setup(store, new[] { "genshin" }, new[] { "5" });
        Assert(!CheckinStatus.IsTodayComplete(store, settings), "no local record requires checking the server");
        const string roles = "{\"retcode\":0,\"data\":{\"list\":[{\"game_uid\":\"1001\",\"region\":\"cn_gf01\",\"nickname\":\"A\"}]}}";
        var mock = new Mock().Add("GET", "getUserGameRoles", roles).Add("GET", "/info?", Signed).Add("POST", "signIn", "{\"retcode\":-5003}");
        var runner = new Runner(store, mock, null); var result = await runner.Run(settings, false, None);
        Assert(result.Success == 2 && result.AlreadySigned == 2 && result.NewlySigned == 0 && mock.Count == 3, "server-completed games and communities are recognized without a game sign POST");
        Assert(CheckinStatus.IsTodayComplete(store, settings) && CheckinStatus.CompletionMessage(store, settings, result) == "主人今日已完成签到", "server-completed selection produces the exact Fairy dialogue");
        result = await runner.Run(settings, false, None);
        Assert(result.Skipped == 2 && mock.Count == 3 && CheckinStatus.CompletionMessage(store, settings, result) == "主人今日已完成签到", "repeated completed execution makes no network requests");
        settings.Forums = new[] { "5", "2" };
        Assert(!CheckinStatus.IsTodayComplete(store, settings), "newly selected target must be checked even after other targets completed");
        mock.Add("POST", "signIn", Ok); result = await runner.Run(settings, false, None);
        Assert(result.NewlySigned == 1 && result.Skipped == 2 && mock.Count == 4 && CheckinStatus.CompletionMessage(store, settings, result).Contains("今日的米游社签到已完成"), "mixed cached and newly signed targets report fresh completion");
        var state = CompletedState(settings, FakeCookie); state.Day = Clock.Day(Clock.ChinaNow.AddDays(-1)); store.SaveState(state);
        Assert(!CheckinStatus.IsTodayComplete(store, settings), "yesterday's state never suppresses today's sign-in");
        state = CompletedState(settings, "stuid=87654321; stoken=OTHER_SYNTHETIC_TEST_TOKEN"); store.SaveState(state);
        Assert(!CheckinStatus.IsTodayComplete(store, settings), "another account's state never suppresses sign-in");
        settings.Games = new[] { "genshin" }; settings.Forums = new string[0];
        string key = AccountScope.Prefix(store.Decrypt(settings)) + "game/genshin";
        state = new RunState(); state.ForDay(Clock.Day(Clock.ChinaNow)); state.Targets[key] = new Attempt { Complete = true, Message = "没有绑定角色" }; store.SaveState(state);
        Assert(!CheckinStatus.IsTodayComplete(store, settings) && CheckinStatus.CompletionMessage(store, settings, new RunSummary()) != "主人今日已完成签到", "unbound games are not presented as successful sign-ins");
        state = CompletedState(settings, FakeCookie); state.Targets[key + "/cn_gf01/1001"].Complete = false; store.SaveState(state);
        Assert(!CheckinStatus.IsTodayComplete(store, settings), "a partial role record cannot be mistaken for a complete group");
    }
    static async Task AccountScopeChecks()
    {
        const string refreshed="stuid=12345678; stoken=v2_ROTATED_TEST_TOKEN; mid=FAKE_MID_123; ltoken=ROTATED_TEST_LTOKEN";
        const string earlier="stuid=12345678; stoken=v2_EARLIER_TEST_TOKEN; mid=FAKE_MID_123";
        const string unrelated="stuid=87654321; stoken=OTHER_TEST_TOKEN";
        Assert(AccountScope.Prefix(FakeCookie)==AccountScope.Prefix(refreshed)&&AccountScope.Prefix(FakeCookie)!=AccountScope.Prefix(unrelated),"QR token rotation keeps the same account scope while other accounts stay separate");
        var store=TestStore();var config=Setup(store,new[]{"genshin"},new[]{"5"});
        var state=new RunState();state.ForDay(Clock.Day(Clock.ChinaNow));
        string current=Hashes.Sha256(Cookies.Normalize(FakeCookie)).Substring(0,16)+"/",old=Hashes.Sha256(Cookies.Normalize(earlier)).Substring(0,16)+"/",other=Hashes.Sha256(Cookies.Normalize(unrelated)).Substring(0,16)+"/";
        foreach(string prefix in new[]{current,old})
        {
            state.Targets[prefix+"game/genshin"]=new Attempt{Complete=true};
            state.Targets[prefix+"game/genshin/cn_gf01/1001"]=new Attempt{Complete=true,Message="今日已签到"};
        }
        state.Targets[current+"forum/5"]=new Attempt{Count=2,NeedsAction=true,Message="登录凭证已失效，请重新扫码登录（1008）。"};
        state.Targets[old+"forum/5"]=new Attempt{Complete=true,Message="社区签到成功"};
        state.Targets[other+"game/genshin/cn_gf01/9999"]=new Attempt{Complete=true};state.Targets[other+"forum/2"]=new Attempt{Complete=true};
        bool changed;string account=AccountScope.Prepare(state,store.Decrypt(config),out changed);
        Assert(changed&&state.Targets[account+"forum/5"].Complete&&!state.Targets[account+"forum/5"].NeedsAction,"verified matching role bindings recover today's success across old credential fingerprints");
        Assert(!state.Targets.ContainsKey(account+"forum/2"),"migration never imports unrelated account records without matching role evidence");
        store.SaveState(state);store.SetCookie(config,refreshed);store.Save(config);
        var mock=new Mock();var result=await new Runner(store,mock,null).Run(config,false,None);
        Assert(mock.Count==0&&result.Skipped==2&&CheckinStatus.IsTodayComplete(store,config),"relogin preserves today's completed targets and makes no duplicate sign-in requests");
        Assert(!SpeechBus.HasDueWork(store,config,true),"recovered completion also suppresses unnecessary background notifications");
    }
    static void DataLocationChecks()
    {
        var fixture=TestStore();string profile=Path.Combine(fixture.Root,"profile"),local=Path.Combine(fixture.Root,"local");
        var emptyNative=new Store(Path.Combine(local,"MiyouDaily"));emptyNative.Save(new Settings());
        var packaged=new Store(Path.Combine(local,@"Packages\OpenAI.Codex_test\LocalCache\Local\MiyouDaily"));
        var settings=Setup(packaged,new[]{"genshin"},new[]{"5"});packaged.SaveState(CompletedState(settings,FakeCookie));packaged.Log("synthetic migration log");
        File.WriteAllText(Path.Combine(packaged.Root,"widget.json"),"{\"Size\":250}");
        var shared=DataLocation.Open(profile,local);
        Assert(shared.Root==Path.Combine(profile,".miyou-daily")&&shared.Decrypt(shared.Load())==Cookies.Normalize(FakeCookie),"packaged credentials migrate to a shared profile directory even when native config is empty");
        Assert(CheckinStatus.IsTodayComplete(shared,shared.Load())&&File.Exists(Path.Combine(shared.Root,"widget.json"))&&File.ReadAllText(shared.LatestLog).Contains("synthetic migration log"),"migration keeps completed records, widget preferences and logs");
        var scheduled=DataLocation.Open(profile,Path.Combine(fixture.Root,"unredirected-local"));
        Assert(scheduled.Root==shared.Root&&scheduled.Decrypt(scheduled.Load())==Cookies.Normalize(FakeCookie),"scheduled and desktop contexts load the same credentials without depending on AppData redirection");
        Assert(File.Exists(packaged.ConfigPath)&&packaged.Decrypt(packaged.Load())==Cookies.Normalize(FakeCookie),"migration leaves the original encrypted data intact");
        settings=shared.Load();settings.DailyTime="14:25";shared.Save(settings);
        Assert(DataLocation.Open(profile,local).Load().DailyTime=="14:25","subsequent launches preserve shared settings instead of reimporting stale data");
    }
    static void ScheduledNoticeChecks()
    {
        var store=TestStore();var settings=Setup(store,new[]{"genshin"},new[]{"5"});
        Assert(SpeechBus.PublishScheduled(store,settings,"主人，社区签到状态待确认。"),"the first scheduled outcome is announced");string first=SpeechBus.Read(store).Id;
        Assert(!SpeechBus.PublishScheduled(store,settings,"主人，社区签到状态待确认。")&&SpeechBus.Read(store).Id==first,"identical consecutive scheduled errors do not reopen the dialogue");
        Assert(SpeechBus.PublishScheduled(store,settings,CheckinStatus.AlreadyCompleteMessage)&&SpeechBus.Read(store).Id!=first,"a changed scheduled outcome is announced immediately");
        string before=SpeechBus.Read(store).Id;SpeechBus.Publish(store,CheckinStatus.AlreadyCompleteMessage);
        Assert(SpeechBus.Read(store).Id!=before,"manual actions still receive feedback even when the text repeats");
        settings.Forums=new[]{"5","2"};
        Assert(SpeechBus.PublishScheduled(store,settings,CheckinStatus.AlreadyCompleteMessage),"changing the selected targets allows a fresh scheduled notification");
        string noticePath=Path.Combine(store.Root,"scheduled-notice.json");var prior=Json.Read<ScheduledNotice>(File.ReadAllText(noticePath));prior.Day=Clock.Day(Clock.ChinaNow.AddDays(-1));Store.AtomicWrite(noticePath,Json.Write(prior));
        Assert(SpeechBus.PublishScheduled(store,settings,CheckinStatus.AlreadyCompleteMessage),"a new Beijing date permits a new scheduled notification");
    }
    static async Task ProbeQr()
    {
        using (var transport = new Transport())
        {
            var qr = await new MiyouApi(new Settings(), "", transport).CreateQr(None);
            Console.WriteLine("Official QR endpoint: returned valid URL and ticket (values withheld). No login performed.");
        }
    }
    static void EyeRepaintCheck(FairyPanelButton icon,string preview)
    {
        using(var bitmap=new System.Drawing.Bitmap(icon.Width,icon.Height))
        {
            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
            {
                // Reproduce a dirty native-button buffer: the icon's own paint
                // must erase the blue layer without relying on background events.
                graphics.Clear(Theme.Cyan);
                var paint=typeof(FairyPanelButton).GetMethod("OnPaint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                paint.Invoke(icon,new object[]{new System.Windows.Forms.PaintEventArgs(graphics,new System.Drawing.Rectangle(System.Drawing.Point.Empty,bitmap.Size))});
            }
            int blue=0;
            for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)
            {
                double dx=(x-bitmap.Width/2.0)/bitmap.Width,dy=(y-bitmap.Height/2.0)/bitmap.Height;
                if(dx*dx+dy*dy>.42*.42 && bitmap.GetPixel(x,y).ToArgb()==Theme.Cyan.ToArgb())blue++;
            }
            Assert(blue==0,preview+": repaint completely removes the inherited rectangular blue layer");
            bitmap.Save(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build",preview+".png")));
        }
    }
    static void InterfaceChecks()
    {
        var store=TestStore();var config=Setup(store,new[]{"genshin","zzz"},new[]{"5","2"});
        using(var form=new MainForm(store,config))
        {
            Assert(form.Font.FontFamily.Name=="Miyou UI","application loads embedded Chinese and Latin UI font");
            form.CaptureSelection();
            Assert(config.Games.SequenceEqual(new[]{"genshin","zzz"}) && config.Forums.OrderBy(x=>x).SequenceEqual(new[]{"2","5"}),"unified game switches preserve existing configuration");
            var timer=form.Controls[0].Controls.Cast<System.Windows.Forms.Control>().SelectMany(x=>x.Controls.Cast<System.Windows.Forms.Control>()).OfType<DigitalTimePicker>().Single();
            timer.Value=DateTime.Today.AddHours(18).AddMinutes(45);form.CaptureSelection();Assert(config.DailyTime=="18:45","digital timer selection is saved as the scheduled HH:mm time");
            timer.Value=DateTime.Today;form.CaptureSelection();
            var grid=form.Controls[0].Controls.Cast<System.Windows.Forms.Control>().SelectMany(x=>x.Controls.Cast<System.Windows.Forms.Control>()).OfType<System.Windows.Forms.TableLayoutPanel>().Single(g=>g.Controls.OfType<GameTile>().Any());
            var tiles=grid.Controls.OfType<GameTile>().ToArray();
            Assert(tiles.Length==7 && tiles.Sum(x=>x.Controls.Count)==13,"seven icon categories expose six game and seven community switches");
            var tile=tiles.Single(x=>x.Key=="genshin");tile.GameSwitch.Checked=false;form.CaptureSelection();
            Assert(!config.Games.Contains("genshin")&&config.Forums.Contains("2"),"game and community switches remain independent");
            Assert(form.Icon!=null && AppAssets.Image("honkai2").Width>=128,"Fairy app icon and game assets load from EXE");
            int panelOpens=0;
            using(var bubble=new FairyDialogue(form,()=>panelOpens++))
            {
                bubble.Say("主人，设定已确认。");Assert(bubble.Height>100,"Fairy notification initializes with its message layout");
                var actions=bubble.Controls.OfType<System.Windows.Forms.TableLayoutPanel>().Single();
                var shortcuts=actions.Controls.OfType<System.Windows.Forms.Button>().ToArray();
                Assert(actions.RowCount==2&&shortcuts.Length==4&&shortcuts[1].Text=="签到"&&shortcuts[2].Text=="配置"&&shortcuts[3].Text=="定时"&&!shortcuts.Any(x=>x.Text.Contains("米游社")||x.Text.Contains("小黑盒")),"first-level shortcuts keep direct check-in beside smaller configuration and timer buttons");
                string savedCookie=config.ProtectedCookie;config.ProtectedCookie="";bubble.OpenShortcuts(false);
                Assert(shortcuts[0].Text=="登录","logged-out widget offers a platform-neutral login entry");
                config.ProtectedCookie=savedCookie;bubble.Say("主人，登录成功。");
                Assert(shortcuts[0].Text=="已登录"&&!shortcuts[0].Enabled&&shortcuts[1].Text=="签到"&&shortcuts[1].Enabled,"login success dims the login row without replacing or hiding check-in");
                var panelIcon=bubble.Controls.OfType<FairyPanelButton>().Single();
                Assert(panelIcon.AccessibleName=="打开签到面板"&&panelIcon.TabStop,"small Fairy icon is an accessible dashboard entry");
                bubble.ShowFor(form);var bubbleHandle=bubble.Handle;
                bubble.Say("主人，正在检查签到状态。");bubble.ShowFor(form);
                bubble.Say("主人今日已完成签到");bubble.ShowFor(form);
                Assert(bubble.Visible&&!bubble.Modal&&bubble.Handle==bubbleHandle&&!form.Visible&&!actions.Visible,"successive notifications reuse the non-modal dialogue with no shortcut row");
                int normalWidth=bubble.Width;float z=bubble.DeviceDpi/96f;
                bubble.OpenShortcuts(false);bubble.Width=(int)(384*z);
                Assert(actions.Right+(int)(8*z)<=panelIcon.Left,"resizing a shortcut bubble never lets the action button overlap the Fairy icon");
                Assert(shortcuts[0].Top<shortcuts[1].Top&&shortcuts.Skip(2).All(b=>b.Width<shortcuts[1].Width&&b.Height<shortcuts[1].Height),"login and check-in occupy separate rows while configuration and timer remain smaller");
                var dialogueText=bubble.Controls.OfType<System.Windows.Forms.Label>().Single();
                bubble.OpenShortcuts(true);
                Assert(dialogueText.Right+(int)(8*z)<=panelIcon.Left&&dialogueText.TextAlign==System.Drawing.ContentAlignment.MiddleCenter,"greeting stays centered in its own area beside the icon after resizing");
                bubble.Say("主人今日已完成签到");
                Assert(dialogueText.Right+(int)(8*z)<=panelIcon.Left&&!actions.Visible,"resizing a notification preserves text spacing without restoring shortcut entries");
                bubble.Width=normalWidth;bubble.OpenShortcuts(false);
                Assert(actions.Right+(int)(8*z)<=panelIcon.Left,"expanding the bubble recalculates the shortcut layout together");
                Assert(panelIcon.Region!=null&&!panelIcon.Region.IsVisible(1,1)&&panelIcon.Region.IsVisible(panelIcon.Width/2,panelIcon.Height/2),"Fairy icon has a circular visible and clickable area without rectangular corners");
                EyeRepaintCheck(panelIcon,"eye-options-repaint");bubble.Say("主人今日已完成签到");EyeRepaintCheck(panelIcon,"eye-notice-repaint");
                panelIcon.PerformClick();Assert(panelOpens==1,"clicking the small Fairy icon opens the dashboard callback");
                panelIcon.AccessibilityObject.DoDefaultAction();Assert(panelOpens==2,"Fairy icon keeps its accessible open-panel action");bubble.Hide();
            }
            using(var qr=new QrLoginForm())
            {
                var qrHandle=qr.Handle;qr.SetStatus("正在生成二维码…");
                Assert(!form.Visible&&!qr.ShowInTaskbar&&qrHandle!=IntPtr.Zero,"standalone QR popup initializes while dashboard stays hidden");
                using(var bitmap=new System.Drawing.Bitmap(qr.Width,qr.Height))
                {
                    // Render the detached content without opening any login window.
                    var root=qr.Controls[0];root.Font=qr.Font;qr.Controls.Remove(root);
                    root.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height));qr.Controls.Add(root);
                    bitmap.Save(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build","qr-popup-preview.png")));
                }
            }
            using(var widget=new FairyWidget(store,form))
            {
                Assert(!widget.ShowInTaskbar,"desktop eye never creates a taskbar entry");
                Assert(widget.ContextMenuStrip.Font.FontFamily.Name=="Miyou UI","widget and tray menu use the application font");
                Assert(widget.Height==widget.Width,"desktop eye has no extra status-plaque area");
            }
            store.SaveState(CompletedState(config,FakeCookie));string spoken=null;int notices=0;
            form.SpeechRequested+=text=>{spoken=text;notices++;};bool alreadyComplete=form.TryReportAlreadyComplete();
            Assert(alreadyComplete&&spoken=="主人今日已完成签到"&&notices==1&&!form.IsWorking&&!form.Visible,"widget completion check reports exact dialogue without starting a network operation");
            form.AllowExit=true;
        }
        config.Games=new[]{"genshin"};config.Forums=new[]{"5"};
        Assert(SpeechBus.HasDueWork(store,config,true),"unfinished scheduled targets request a notification");
        var state=new RunState();state.ForDay(Clock.Day(Clock.ChinaNow));string prefix=Hashes.Sha256(Cookies.Normalize(FakeCookie)).Substring(0,16)+"/";
        state.Targets[prefix+"game/genshin"]=new Attempt{Complete=true};state.Targets[prefix+"forum/5"]=new Attempt{Complete=true};store.SaveState(state);
        Assert(!SpeechBus.HasDueWork(store,config,true),"completed periodic checks do not repeat notification");
        SpeechBus.Publish(store,"主人，签到开始。");var first=SpeechBus.Read(store);SpeechBus.Publish(store,"主人，今日的米游社签到已完成。");var last=SpeechBus.Read(store);
        Assert(first.Id!=last.Id && last.Text.Contains("已完成"),"headless completion reaches widget through notification file");
        var greeting=new GreetingSession();Assert(greeting.Begin()&&!greeting.Begin(),"greeting plays only once per application session");
        Assert(new GreetingSession().Begin(),"a fresh application session restores the greeting");
        Assert(GreetingSession.VisibleTextAt(900)=="主人，我在","first greeting line is presented by itself");
        Assert(GreetingSession.VisibleTextAt(2800)=="今天有什么可以为您效劳","second greeting line follows the first");
        Assert(GreetingSession.StageAt(3350)==2&&GreetingSession.LineAt(3350)=="","greeting transitions into text-free shortcut options");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr GetThreadDpiAwarenessContext();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
    static int TextInkHeight(int dpi,out int colors)
    {
        float z=dpi/96f;
        using(var bitmap=new System.Drawing.Bitmap((int)(360*z),(int)(70*z)))
        {
            bitmap.SetResolution(dpi,dpi);
            using(var g=System.Drawing.Graphics.FromImage(bitmap))using(var f=AppFonts.Create(18))
            {g.Clear(System.Drawing.Color.Black);UiRendering.Text(g,"FAIRY · 签到已完成",f,new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Color.White,true);}
            var shades=new HashSet<int>();int top=bitmap.Height,bottom=0;
            for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)
            {int c=bitmap.GetPixel(x,y).ToArgb();if(c!=System.Drawing.Color.Black.ToArgb()){top=Math.Min(top,y);bottom=Math.Max(bottom,y);shades.Add(c);}}
            colors=shades.Count;
            string output=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build","rendering-"+dpi+"dpi.png"));System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));bitmap.Save(output);
            return bottom-top+1;
        }
    }
    static void RenderingChecks()
    {
        Assert(AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),new IntPtr(-4)),"runtime enables per-monitor V2 DPI awareness");
        int lowColors,highColors;int low=TextInkHeight(96,out lowColors),high=TextInkHeight(192,out highColors);
        Assert(lowColors>64&&highColors>64,"supersampled text has smooth grayscale coverage beyond the old 16-level rasterizer");
        Assert(high>=low*1.7,"text is rasterized at native high-DPI pixel density");
        foreach(int dpi in new[]{120,144,168,240,288})
        {
            int colors;int height=TextInkHeight(dpi,out colors);
            Console.WriteLine("Text raster "+dpi+" dpi: height="+height+", base="+low+", shades="+colors);
            Assert(colors>64&&Math.Abs(height/(double)low-dpi/96.0)<0.2,"text preserves smooth edges and physical size at "+dpi+" dpi");
        }
        using(var backdrop=new FairyBackdrop{Size=new System.Drawing.Size(320,240)})
        using(var image=new System.Drawing.Bitmap(320,240))
        {
            backdrop.CreateControl();backdrop.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,320,240));
            bool opaque=true;
            for(int y=0;y<240;y++)for(int x=0;x<320;x++)if(image.GetPixel(x,y).A!=255)opaque=false;
            Assert(opaque,"first backdrop frame paints every pixel including outer margins");
            var center=image.GetPixel(160,95);var edge=image.GetPixel(319,239);
            Assert(center.B>center.R+20&&center.B>edge.B,"HDD backdrop uses a blue light field above a darker navy edge");
        }
        using(var regular=AppFonts.Create(10))using(var heavy=AppFonts.Create(10,System.Drawing.FontStyle.Bold))
            Assert(regular.FontFamily.Name==heavy.FontFamily.Name && heavy.Bold,"Chinese and Latin share the embedded family with a real heavy face");
        foreach(int dpi in new[]{96,120,144,168,192,240,288})
        using(var parent=new System.Windows.Forms.Panel{BackColor=Theme.Surface})
        using(var button=new ButtonProbe())
        {
            float z=dpi/96f;button.Size=new System.Drawing.Size((int)(120*z),(int)(34*z));parent.Size=new System.Drawing.Size(button.Width+40,button.Height+40);parent.Controls.Add(button);button.Location=new System.Drawing.Point(20,20);
            button.Text="导入 Cookie";
            foreach(bool hovered in new[]{false,true,false})
            using(var bitmap=new System.Drawing.Bitmap(button.Width,button.Height))
            {
                bitmap.SetResolution(dpi,dpi);button.Hover(hovered);
                using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.Clear(System.Drawing.Color.Magenta);button.Render(g);}
                bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build","button-"+dpi+"-"+hovered+".png"));
                Assert(bitmap.GetPixel(0,0).ToArgb()==Theme.Surface.ToArgb() && bitmap.GetPixel(bitmap.Width-1,bitmap.Height-1).ToArgb()==Theme.Surface.ToArgb(),"button corners repaint parent without a native rectangle at "+dpi+" dpi, hover="+hovered);

            }
        }
        // Reproduce the real hierarchy: a button in a transparent layout panel.
        // A parent's foreground Paint handler must never run on a child's DC.
        foreach(bool patterned in new[]{false,true})
        using(var surface=patterned?(System.Windows.Forms.Control)new FairyBackdrop():new RoundedPanel())
        using(var layout=new System.Windows.Forms.Panel{BackColor=System.Drawing.Color.Transparent})
        using(var button=new ButtonProbe())
        {
            surface.Size=new System.Drawing.Size(420,180);layout.Bounds=new System.Drawing.Rectangle(25,30,350,120);
            surface.Controls.Add(layout);layout.Controls.Add(button);button.Bounds=new System.Drawing.Rectangle(35,20,180,51);button.Text="扫码登录";
            int foregroundPaints=0;
            surface.Paint+=(s,e)=>{foregroundPaints++;e.Graphics.DrawString("DUPLICATE TEXT",button.Font,System.Drawing.Brushes.Magenta,0,0);};
            layout.Paint+=(s,e)=>{foregroundPaints++;e.Graphics.Clear(System.Drawing.Color.Magenta);};
            using(var actual=new System.Drawing.Bitmap(button.Width,button.Height))
            using(var expected=new System.Drawing.Bitmap(button.Width,button.Height))
            {
                actual.SetResolution(144,144);expected.SetResolution(144,144);
                using(var g=System.Drawing.Graphics.FromImage(actual))button.Render(g);
                for(int i=0;i<8;i++)
                {
                    button.Hover(i%2==0);button.Press(i%3==0);button.Enabled=i%4!=0;
                    using(var g=System.Drawing.Graphics.FromImage(actual))button.Render(g);
                }
                button.Enabled=true;button.Press(false);button.Hover(false);
                using(var g=System.Drawing.Graphics.FromImage(actual))button.Render(g);
                using(var g=System.Drawing.Graphics.FromImage(expected))button.Render(g);
                bool same=true;for(int y=0;y<button.Height;y++)for(int x=0;x<button.Width;x++)if(actual.GetPixel(x,y)!=expected.GetPixel(x,y))same=false;
                Assert(same,"repeated hover, press and disable paints match a fresh full frame, patterned="+patterned);
                Assert(foregroundPaints==0,"button never replays parent labels or native paint layers, patterned="+patterned);
                actual.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build","button-nested-"+patterned+".png"));
            }
        }
    }
    sealed class ButtonProbe : RoundButton
    {
        public void Hover(bool active){if(active)OnMouseEnter(EventArgs.Empty);else OnMouseLeave(EventArgs.Empty);}
        public void Press(bool active){var e=new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,1,10,10,0);if(active)OnMouseDown(e);else OnMouseUp(e);}
        // WM_PAINT may arrive without a background erase; exercise that path.
        public void Render(System.Drawing.Graphics g){using(var e=new System.Windows.Forms.PaintEventArgs(g,ClientRectangle)){OnPaint(e);}}
    }
    static void DigitalClockChecks()
    {
        using(var timer=new DigitalTimePicker())
        {
            timer.Value=DateTime.Today.AddHours(23).AddMinutes(59);timer.SelectedPart=0;timer.Step(1);
            Assert(timer.Value.Hour==0&&timer.Value.Minute==59,"digital timer hour adjustment wraps safely at midnight");
            timer.SelectedPart=1;timer.Step(1);Assert(timer.Value.Hour==0&&timer.Value.Minute==0,"digital timer minute adjustment stays within 00-59");
            timer.SelectedPart=0;foreach(int digit in new[]{1,2,3,0})timer.EnterDigit(digit);
            Assert(timer.Value.Hour==12&&timer.Value.Minute==30,"four-digit keyboard entry sets hours and minutes");
            timer.Value=DateTime.Today.AddHours(12);
            using(var bitmap=new System.Drawing.Bitmap(timer.Width,timer.Height))
            {
                timer.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height));
                bitmap.Save(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build","digital-timer.png")));
            }
        }
    }
    static void StartupChecks()
    {
        string testKey=@"Software\MiyouDaily\Tests\"+Guid.NewGuid().ToString("N");
        try
        {
            using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testKey))
            {
                key.SetValue("KeepOtherSetting","unchanged");
                Assert(!StartupManager.IsEnabled(key),"startup option is off when no startup entry exists");
                StartupManager.Apply(key,true,@"C:\测试 空间\Fairy.exe");
                Assert(StartupManager.IsEnabled(key)&&Convert.ToString(key.GetValue(StartupManager.ValueName))=="\"C:\\测试 空间\\Fairy.exe\" --autostart","startup command quotes Unicode paths and launches widget mode");
                StartupManager.Apply(key,false,null);StartupManager.Apply(key,false,null);
                Assert(!StartupManager.IsEnabled(key)&&Convert.ToString(key.GetValue("KeepOtherSetting"))=="unchanged","disabling startup removes only Fairy's entry and is repeatable");
            }
        }
        finally{Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(testKey,false);}
        var store=TestStore();string release=Path.Combine(store.Root,"release files");Directory.CreateDirectory(release);
        string source=Path.Combine(release,"Fairy.exe");File.WriteAllText(source,"synthetic executable fixture");File.WriteAllText(source+".config","configuration fixture");
        string installed=AppInstallation.Ensure(store,source);
        Assert(File.ReadAllText(installed)==File.ReadAllText(source)&&File.ReadAllText(installed+".config")==File.ReadAllText(source+".config"),"startup deploys executable and DPI configuration to stable app folder");
        using(var held=File.Open(installed,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert(AppInstallation.Ensure(store,source)==installed,"unchanged installed program is not overwritten while in use");
        Assert(AppInstallation.Ensure(store,installed)==installed,"startup also works when launched from installed app folder");
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            System.Windows.Forms.Application.EnableVisualStyles();System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.ThreadException+=(s,e)=>{Console.Error.WriteLine(e.Exception);Environment.Exit(1);};
            if (args.Contains("--render-check")) { using(var host=new System.Windows.Forms.Form()){var handle=host.Handle;RenderingChecks();}return 0; }
            if (args.Contains("--menu-check")) { ContextMenuChecks();return 0; }
            if (args.Contains("--scroll-check")) { ScrollbarChecks();return 0; }
            if (args.Contains("--motion-check")) { MotionNoticeChecks();AutoExitChecks();return 0; }
            if (args.Contains("--input-check")) { InputAlignmentChecks();return 0; }
            if (args.Contains("--input-focus-check")) { InputFocusChecks();return 0; }
            if (args.Contains("--main-panel-check")) { MainPanelPerformanceChecks();return 0; }
            if (args.Contains("--main-display-check")) { MainDisplayMatrixChecks();return 0; }
            if (args.Contains("--v110-check")) { Version110Checks();return 0; }
            if (args.Contains("--window-behavior-check")) { WindowBehaviorChecks();return 0; }
            if (args.Contains("--drag-benchmark")) { DragPerformanceChecks(false);return 0; }
            if (args.Contains("--drag-check")) { DragPerformanceChecks();return 0; }
            if (args.Contains("--display-check")) { HighDpiChecks();DisplayMatrixChecks();return 0; }
            if (args.Contains("--popup-check")) { PopupSurfaceChecks();InterfaceChecks();HddNavigationChecks();CenteredWindowChecks();HighDpiChecks();RenderingChecks();ScrollbarChecks();DigitalClockChecks();return 0; }
            if (args.Contains("--surface-check")) { PopupSurfaceChecks();return 0; }
            if (args.Contains("--layer-check")) { DialogueLayerChecks();return 0; }
            if (args.Contains("--anchor-menu-check")) { AnchorMenuChecks();ContextMenuChecks();return 0; }
            if (args.Contains("--center-check")) { CenteredWindowChecks();return 0; }
            if (args.Contains("--probe-qr")) { ProbeQr().GetAwaiter().GetResult(); return 0; }
            if (args.Contains("--probe-heybox-token")) return ProbeHeyboxToken();
            Basic(); ApiTests().GetAwaiter().GetResult(); PartialForumRetries().GetAwaiter().GetResult(); PartialRoleRetries().GetAwaiter().GetResult(); CompletionChecks().GetAwaiter().GetResult(); AccountScopeChecks().GetAwaiter().GetResult(); DataLocationChecks(); ScheduledNoticeChecks(); HeyboxChecks().GetAwaiter().GetResult(); CompletionNoticeChecks(); MotionNoticeChecks(); PopupSurfaceChecks(); DragPerformanceChecks(); MainPanelPerformanceChecks(); Version110Checks(); MainDisplayMatrixChecks(); WindowBehaviorChecks(); InputAlignmentChecks(); InputFocusChecks(); DialogueLayerChecks(); AutoExitChecks(); InterfaceChecks(); HddNavigationChecks(); CenteredWindowChecks(); AnchorMenuChecks(); ContextMenuChecks(); ScrollbarChecks(); HighDpiChecks(); DisplayMatrixChecks(); RenderingChecks(); DigitalClockChecks(); StartupChecks();
            string smoke = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "build", "scheduler-smoke.xml"));
            var xml = XDocument.Parse(Scheduler.MakeXml(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MiyouDaily.exe"), System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, "09:00", Clock.ChinaNow));
            foreach (var enabled in xml.Descendants().Where(x => x.Name.LocalName == "Enabled")) enabled.Value = "false";
            Directory.CreateDirectory(Path.GetDirectoryName(smoke)); File.WriteAllText(smoke, xml.ToString(), System.Text.Encoding.Unicode);
            Console.WriteLine("All " + passed + " assertions passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
