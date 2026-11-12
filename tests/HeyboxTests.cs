using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static HeyboxConfig BoxConfig(string id="12345678")
    {
        var config=HeyboxConfig.Login("B"+new string('A',88));config.session.heybox_id=id;config.session.pkey="SYNTHETIC_HEYBOX_PKEY";return config;
    }
    static string BoxState(bool signed)
    {
        long day=(long)Math.Floor(((DateTimeOffset.UtcNow-new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero)).TotalSeconds+8*3600)/86400);
        return Json.Write(new{status="ok",result=new{state=signed?"ok":"waiting",signed_date=signed?day:day-1}});
    }
    const string BoxOk="{\"status\":\"ok\",\"result\":{}}";
    static async Task HeyboxChecks()
    {
        Assert(!new Settings().HeyboxEnabled,"existing settings keep Heybox disabled by default");
        Dictionary<string,object> fixture;
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MiyouDaily.heybox-native-v14.json"))
        using(var reader=new StreamReader(stream))fixture=Response(reader.ReadToEnd());
        foreach(var record in Json.Items(Json.Get(fixture,"records")))
        {
            var input=Json.Get(record,"input");var expected=Json.Get(record,"signature");
            var actual=HeyboxSignature.Sign(Json.Str(input,"path"),Json.Str(input,"time"),Json.Str(input,"debugInfo"),Json.Str(input,"osVersion"),Json.Str(input,"version"));
            Assert(new[]{"_time","hkey","_rnd"}.All(key=>actual[key]==Json.Str(expected,key)),"Heybox native reference vector: "+Json.Str(record,"id"));
        }
        var replay=Json.Get(fixture,"nonceReplay");int cursor=0;int[] indices=Json.Items(Json.Get(replay,"indices")).Select(Convert.ToInt32).ToArray();
        Assert(HeyboxSignature.Nonce(Json.Str(replay,"timeSeconds"),max=>indices[cursor++])==Json.Str(replay,"actual")&&cursor==32,"Heybox nonce matches native UTC-day replay");
        Assert(Enumerable.Range(0,100).Select(i=>HeyboxSignature.Nonce("1793040000")).Distinct().Count()==100,"Heybox requests use fresh nonces");
        Assert(HeyboxApi.NormalizePhone("13800138000")=="+8613800138000"&&HeyboxApi.NormalizePhone("+14155552671")=="+14155552671","Heybox login normalizes domestic and international phone numbers");
        Throws<ArgumentException>(()=>HeyboxApi.NormalizePhone("13800138000\r\nCookie:x"),"Heybox phone rejects control characters");
        string encrypted=HeyboxApi.EncryptPhone("13800138000");Assert(Convert.FromBase64String(encrypted).Length==128&&encrypted.EndsWith("\n")&&encrypted.Split('\n')[0].Length==76,"Heybox phone uses Android-compatible RSA PKCS1 and wrapped base64");
        var store=TestStore();var settings=new Settings{Games=new string[0],Forums=new string[0],HeyboxEnabled=true,DailyTime="00:00"};
        var config=BoxConfig();HeyboxAccount.Set(settings,config);store.Save(settings);
        Assert(HeyboxAccount.Load(store.Load()).session.pkey==config.session.pkey&&!File.ReadAllText(store.ConfigPath).Contains(config.session.pkey),"Heybox credentials survive DPAPI round trip without plaintext storage");
        var rotated=BoxConfig();rotated.session.pkey="ROTATED_SYNTHETIC_PKEY";Assert(config.Key==rotated.Key&&config.Key!=BoxConfig("87654321").Key,"Heybox completion follows account ID across token rotation and isolates other accounts");
        var bad=BoxConfig();bad.app.shaderVersion="99";Throws<ArgumentException>(()=>bad.Validate(),"Heybox rejects an unverified signing version");
        Assert(!HeyboxAccount.Redact("pkey="+config.session.pkey+" x_xhh_tokenid="+config.session.x_xhh_tokenid,config).Contains("SYNTHETIC")&&!Store.Redact("pkey=secret x_xhh_tokenid=other","").Contains("secret"),"Heybox secrets are removed from provider and generic diagnostics");
        var mock=new Mock().Add("GET","get_sign_state",BoxState(true));
        var result=await new Runner(store,new Mock(),null,mock).Run(settings,false,None);
        Assert(result.AlreadySigned==1&&result.NewlySigned==0&&mock.Count==1&&CheckinStatus.IsTodayComplete(store,settings),"Heybox server completion skips the mutating GET and updates local progress");
        result=await new Runner(store,new Mock(),null,mock).Run(settings,false,None);
        Assert(result.Skipped==1&&mock.Count==1&&!SpeechBus.HasDueWork(store,settings,true),"Heybox completed periodic runs do not request network work or notifications");
        var state=store.LoadState();state.Day=Clock.Day(Clock.ChinaNow.AddDays(-1));store.SaveState(state);
        mock=new Mock().Add("GET","get_sign_state",BoxState(false)).Add("GET","sign_v3/sign",BoxOk).Add("GET","get_sign_state",BoxState(true));
        result=await new Runner(store,new Mock(),null,mock).Run(settings,false,None);
        Assert(result.NewlySigned==1&&mock.Count==3&&CheckinStatus.CompletionMessage(store,settings,result).Contains("今日的小黑盒签到已完成"),"Heybox signs on the next Beijing day and requires server confirmation");
        HeyboxAccount.Set(settings,rotated);result=await new Runner(store,new Mock(),null,mock).Run(settings,false,None);
        Assert(result.Skipped==1&&mock.Count==3,"Heybox relogin with a rotated token preserves today's completion");
        HeyboxAccount.Set(settings,BoxConfig("87654321"));Assert(!CheckinStatus.IsTodayComplete(store,settings),"another Heybox account does not inherit completion");
        mock=new Mock().Add("GET","get_sign_state",BoxState(false)).Fail("sign_v3/sign").Add("GET","get_sign_state",BoxState(true));
        result=await new Runner(store,new Mock(),null,mock).Run(settings,false,None);
        Assert(result.NewlySigned==1&&mock.Count==3,"Heybox verifies an ambiguous submission without sending it twice");
        var unconfirmedStore=TestStore();HeyboxAccount.Set(settings,config);
        mock=new Mock().Add("GET","get_sign_state",BoxState(false)).Add("GET","sign_v3/sign",BoxOk).Add("GET","get_sign_state",BoxState(false)).Add("GET","get_sign_state",BoxState(false)).Add("GET","get_sign_state",BoxState(false));
        result=await new Runner(unconfirmedStore,new Mock(),null,mock).Run(settings,false,None);
        Assert(result.Failed==1&&!CheckinStatus.IsTodayComplete(unconfirmedStore,settings)&&mock.Count==5,"Heybox never treats an unconfirmed success response as a completed sign-in");
        state=unconfirmedStore.LoadState();state.Targets[config.Key].Count=3;unconfirmedStore.SaveState(state);
        result=await new Runner(unconfirmedStore,new Mock(),null,mock).Run(settings,true,None);
        Assert(result.Skipped==1&&mock.Count==5,"Heybox scheduled retries obey the per-account daily cap");
        mock=new Mock().Add("POST","login_code/","{\"status\":\"ok\",\"result\":{\"profile\":{\"heybox_id\":\"1\"},\"account_detail\":{\"userid\":\"2\"},\"pkey\":\"test\"}}");
        try{await new HeyboxApi(config,mock).VerifyCode("13800138000","123456",null,None);throw new Exception("mismatched Heybox login accepted");}
        catch(IOException){Assert(config.session.heybox_id=="12345678"&&config.session.pkey=="SYNTHETIC_HEYBOX_PKEY","a mismatched Heybox login leaves existing credentials intact");}
        mock=new Mock().Add("POST","login_code/","{\"status\":\"ok\",\"result\":{\"profile\":{\"heybox_id\":\"1\"},\"account_detail\":{\"userid\":\"1\"},\"pkey\":\"new-test-pkey\"}}").Add("GET","get_sign_state",BoxState(false));
        var loggedIn=await new HeyboxApi(config,mock).VerifyCode("13800138000","123456",null,None);
        Assert(loggedIn.session.heybox_id=="1"&&config.session.heybox_id=="12345678"&&mock.Count==2,"Heybox SMS login verifies the new session before replacing credentials");
        mock=new Mock();mock.Steps.Enqueue((method,url,headers,body)=>{
            Assert(method=="POST"&&url.Contains("/account/get_login_code/")&&body.StartsWith("phone_num=")&&body.Contains("%0A")&&!body.Contains("13800138000"),"Heybox SMS request sends an encrypted phone in the form body");return Response("{\"status\":\"ok\"}");});
        await new HeyboxApi(config,mock).SendCode("13800138000",null,None);
        mock=new Mock().Add("POST","get_login_code/","{\"status\":\"show_captcha\",\"result\":{\"appid\":\"123456\"}}");
        try{await new HeyboxApi(config,mock).SendCode("13800138000",null,None);throw new Exception("Heybox captcha ignored");}
        catch(HeyboxCaptchaException ex){Assert(ex.AppId=="123456"&&mock.Count==1,"Heybox captcha stops SMS submission and exposes only the public challenge ID");}
        mock=new Mock().Add("GET","get_sign_state","{\"status\":\"lack_token\"}");
        try{await new HeyboxApi(config,mock).State(None);throw new Exception("Heybox expired device accepted");}
        catch(ApiException ex){Assert(ex.NeedsAction&&mock.Count==1,"Heybox expired devices require relogin without retrying");}
        var mixedStore=TestStore();var mixed=Setup(mixedStore,new string[0],new[]{"5"});mixed.HeyboxEnabled=true;HeyboxAccount.Set(mixed,config);mixedStore.Save(mixed);
        var miyou=new Mock().Add("POST","signIn",Ok);mock=new Mock().Fail("get_sign_state");
        result=await new Runner(mixedStore,miyou,null,mock).Run(mixed,false,None);
        Assert(result.Success==1&&result.Failed==1&&mixedStore.LoadState().Targets[AccountScope.Prefix(FakeCookie)+"forum/5"].Complete,"a Heybox failure preserves successful Miyou progress");
        mixed.HeyboxEnabled=false;result=await new Runner(mixedStore,miyou,null,mock).Run(mixed,false,None);
        Assert(result.Failed==0&&mock.Count==1,"disabled Heybox never sends a request or interferes with completed Miyou tasks");mixed.HeyboxEnabled=true;
        mixed.ProtectedCookie="invalid";var independent=TestStore();mock=new Mock().Add("GET","get_sign_state",BoxState(true));
        result=await new Runner(independent,new Mock(),null,mock).Run(mixed,false,None);
        Assert(result.Success==1&&result.Failed==1&&mock.Count==1,"unreadable Miyou credentials do not block Heybox sign-in");
        mixed.ProtectedCookie="";independent=TestStore();mock=new Mock().Add("GET","get_sign_state",BoxState(true));
        result=await new Runner(independent,new Mock(),null,mock).Run(mixed,false,None);
        Assert(result.Success==1&&result.Failed==1,"a missing Miyou login does not block independent Heybox work");
        mixedStore.SetCookie(mixed,FakeCookie);mixed.ProtectedHeyboxConfig="invalid";
        Assert(!CheckinStatus.IsTodayComplete(mixedStore,mixed)&&SpeechBus.HasDueWork(mixedStore,mixed,true)&&SpeechBus.PublishScheduled(mixedStore,mixed,"需要重新登录"),"unreadable Heybox credentials do not break combined completion or scheduled feedback");
        using(var canceled=new CancellationTokenSource())
        {
            var canceledStore=TestStore();mock=new Mock().Add("GET","get_sign_state",BoxState(false));mock.Steps.Enqueue((m,u,h,b)=>{canceled.Cancel();return Response(BoxOk);});
            try{await new Runner(canceledStore,new Mock(),null,mock).Run(settings,false,canceled.Token);throw new Exception("Heybox cancellation ignored");}
            catch(OperationCanceledException){Assert(!canceledStore.LoadState().Targets[config.Key].Complete&&mock.Count==2,"canceling a Heybox submission preserves an incomplete checkpoint");}
        }
        using(var form=new HeyboxForm(TestStore(),new Settings()))
            Assert(form.Text.Contains("小黑盒登录")&&FindControls(form).Any(c=>c.AccessibleName=="小黑盒获取验证码")&&!FindControls(form).OfType<ToggleButton>().Any(),"Heybox popup handles authentication while daily selection remains on the dashboard");
        var dashboardSettings=new Settings();
        using(var form=new MainForm(TestStore(),dashboardSettings))
        {
            var controls=FindControls(form).ToArray();var miyouLogin=controls.Single(c=>c.AccessibleName=="米游社扫码登录");var heyboxLogin=controls.Single(c=>c.AccessibleName=="小黑盒短信登录");
            Assert(miyouLogin.Parent==heyboxLogin.Parent&&controls.Single(c=>c.AccessibleName=="小黑盒登录状态").Parent==miyouLogin.Parent,"both providers share the same account and login area");
            var heyboxTile=controls.OfType<HeyboxTile>().Single();var miyouTile=controls.OfType<GameTile>().First();
            Assert(heyboxTile.Parent==miyouTile.Parent,"Heybox daily check-in appears in the same project grid as Miyou games");
            var beforeGames=dashboardSettings.Games.ToArray();var beforeForums=dashboardSettings.Forums.ToArray();heyboxTile.SignSwitch.Checked=true;form.CaptureSelection();
            Assert(dashboardSettings.HeyboxEnabled&&dashboardSettings.Games.OrderBy(x=>x).SequenceEqual(beforeGames.OrderBy(x=>x))&&dashboardSettings.Forums.OrderBy(x=>x).SequenceEqual(beforeForums.OrderBy(x=>x)),"the shared Heybox switch saves independently of Miyou selections");
            foreach(var tile in controls.OfType<GameTile>()){if(tile.GameSwitch!=null)tile.GameSwitch.Checked=false;tile.ForumSwitch.Checked=false;}form.CaptureSelection();dashboardSettings.Validate();
            Assert(dashboardSettings.HeyboxEnabled&&dashboardSettings.Games.Length==0&&dashboardSettings.Forums.Length==0,"the shared dashboard also supports Heybox-only daily selection");
        }
        var install=TestStore();string release=Path.Combine(install.Root,"release");Directory.CreateDirectory(release);string exe=Path.Combine(release,"Fairy.exe");File.WriteAllText(exe,"fixture");
        File.WriteAllText(Path.Combine(release,"Microsoft.Web.WebView2.Core.dll"),"managed fixture");string loader=Path.Combine(release,"runtimes","win-x64","native","WebView2Loader.dll");Directory.CreateDirectory(Path.GetDirectoryName(loader));File.WriteAllText(loader,"native fixture");
        string installed=AppInstallation.Ensure(install,exe);
        Assert(File.Exists(Path.Combine(Path.GetDirectoryName(installed),"Microsoft.Web.WebView2.Core.dll"))&&File.Exists(Path.Combine(Path.GetDirectoryName(installed),"runtimes","win-x64","native","WebView2Loader.dll")),"installed app retains managed and native Heybox login components");
    }
    static IEnumerable<Control> FindControls(Control root)
    {foreach(Control child in root.Controls){yield return child;foreach(var nested in FindControls(child))yield return nested;}}
    static int ProbeHeyboxToken()
    {
        bool success=false;
        using(var dialog=new HeyboxBrowserDialog(TestStore()))
        {
            dialog.Shown+=async(s,e)=>{
                try{string token=await dialog.DeviceToken(None);success=token.Length==89;Console.WriteLine(success?"Heybox device token probe passed (token hidden; no SMS sent).":"Heybox device token probe failed.");}
                catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);}
                finally{dialog.Close();}
            };
            Application.Run(dialog);
        }
        return success?0:1;
    }
}
