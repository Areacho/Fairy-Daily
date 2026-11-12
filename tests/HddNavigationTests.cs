using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Threading.Tasks;
using MiyouDaily;

partial class Tests
{
    static void HddNavigationChecks()
    {
        var store=TestStore();var settings=new Settings();int executions=0;
        using(var panel=new MainForm(store,settings))
        using(var bubble=new FairyDialogue(panel,()=>{},()=>{executions++;return Task.FromResult(0);}))
        {
            FairySection? requested=null;panel.ModuleRequested+=section=>requested=section;
            bubble.OpenShortcuts(false);bubble.ShowFor(panel);
            var actions=bubble.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<Button>().ToArray();
            actions[0].PerformClick();Assert(requested==FairySection.Login&&!panel.Visible,"login shortcut routes to platform selection without opening the dashboard");
            bubble.OpenShortcuts(false);requested=null;actions[1].PerformClick();Assert(executions==1&&requested==null&&!panel.Visible,"check-in shortcut directly executes the check-in action without opening configuration");
            bubble.OpenShortcuts(false);actions[2].PerformClick();Assert(requested==FairySection.Checkin,"small configuration shortcut opens platform and project settings");
            bubble.OpenShortcuts(false);actions[3].PerformClick();Assert(requested==FairySection.Schedule&&!panel.Visible,"timer shortcut opens only the timing route");
            HeyboxAccount.Set(settings,BoxConfig());bubble.OpenShortcuts(false);Assert(actions[0].Text=="已登录"&&!actions[0].Enabled&&actions[1].Enabled,"Heybox login dims only the login row even before daily automation is enabled");
            requested=null;actions[1].PerformClick();Assert(executions==2&&requested==null,"authenticated check-in also executes directly without navigating");
            settings.ProtectedHeyboxConfig="";bubble.OpenShortcuts(false);Assert(actions[0].Enabled&&actions[0].Text=="登录"&&actions[1].Text=="签到","logging out restores the login row without changing the check-in entry");bubble.Hide();
            using(var module=new FairyModuleForm(panel,FairySection.Login))
            {
                var providerButtons=FindControls(module).OfType<RoundButton>().Where(b=>b.Image!=null).ToArray();
                Assert(providerButtons.Length==2&&providerButtons.Any(b=>b.Text.Contains("米游社"))&&providerButtons.Any(b=>b.Text.Contains("小黑盒")),"second-level login selection shows both platforms with icons");
                module.Navigate(FairySection.MiyouOptions,false);Assert(FindControls(module).OfType<GameTile>().Count()==7&&!FindControls(module).OfType<HeyboxTile>().Any(),"Miyou configuration contains only Miyou projects");
                module.Navigate(FairySection.HeyboxOptions,false);Assert(FindControls(module).OfType<HeyboxTile>().Count()==1&&!FindControls(module).OfType<GameTile>().Any(),"Heybox configuration contains only Heybox controls");
                module.Navigate(FairySection.Schedule,false);Assert(FindControls(module).OfType<DigitalTimePicker>().Count()==1&&!FindControls(module).OfType<GameTile>().Any()&&!FindControls(module).OfType<HeyboxTile>().Any(),"timer module excludes login and platform configuration controls");
                var moduleHandle=module.Handle;foreach(Control control in FindControls(module)){var controlHandle=control.Handle;}module.PerformLayout();module.Controls[0].PerformLayout();
                var clock=FindControls(module).OfType<DigitalTimePicker>().Single();var commands=FindControls(module).OfType<RoundButton>().Where(b=>b.Text=="确认时间"||b.Text=="开启定时"||b.Text=="暂停定时").ToArray();
                Assert(commands.Length==3&&commands.All(b=>b.Parent==commands[0].Parent&&b.Top==commands[0].Top),"timer commands share a single aligned row beneath the clock");
                Assert(clock.Width>=400*module.DeviceDpi/96f&&clock.Height>commands[0].Height*3,"timer page gives the digital clock the main visual area");
                module.Navigate(FairySection.MiyouTasks,false);Assert(FindControls(module).OfType<RoundButton>().Any(b=>b.Text=="确认米游社配置")&&!FindControls(module).OfType<RoundButton>().Any(b=>b.Text.Contains("小黑盒")),"Miyou management provides direct configuration confirmation and account actions");
                Assert(FindControls(module).OfType<GameTile>().Count()==7&&!FindControls(module).OfType<RoundButton>().Any(b=>b.Text=="全部项目配置"||b.Text.EndsWith(" / 配置")),"Miyou management shows all game and community switches without nested configuration entries");
                using(var auth=new HeyboxForm(store,settings))using(var qr=new QrLoginForm())using(var verification=new HeyboxBrowserDialog(store))
                    Assert(panel.Opacity==1&&new[]{module.Opacity,auth.Opacity,qr.Opacity}.All(x=>Math.Abs(x-PopupSurface.InputOpacity)<.0001)&&verification.Opacity==1,"native popup inputs are separated from per-pixel visuals and the embedded browser stays opaque");
                module.Opacity=0;module.Show(panel);module.Navigate(FairySection.Checkin,false);
                FindControls(module).OfType<RoundButton>().Single(b=>b.Text.StartsWith("米游社")).PerformClick();
                FindControls(module).OfType<RoundButton>().Single(b=>b.Text=="米游社登录管理").PerformClick();module.GoBack();
                Assert(module.Section==FairySection.MiyouTasks,"returning from Miyou login management restores the check-in management page");
                var back=FindControls(module).OfType<RoundButton>().Single(b=>b.Text=="返回");Assert(back.Visible&&back.Enabled,"management keeps a visible enabled return button");back.PerformClick();Assert(module.Section==FairySection.Checkin,"platform management returns to check-in platform selection");
                module.Navigate(FairySection.Login,false);FindControls(module).OfType<RoundButton>().Single(b=>b.Text.StartsWith("米游社")).PerformClick();module.GoBack();
                Assert(module.Section==FairySection.Login,"login flow keeps its own return path");
                module.Navigate(FairySection.HeyboxTasks,false);Assert(FindControls(module).OfType<HeyboxTile>().Count()==1&&FindControls(module).OfType<RoundButton>().Any(b=>b.Text=="确认小黑盒配置")&&!FindControls(module).OfType<RoundButton>().Any(b=>b.Text.Contains("米游社")),"Heybox management includes its own daily switch and confirmation action");
                FindControls(module).OfType<RoundButton>().Single(b=>b.Text=="小黑盒登录管理").PerformClick();module.GoBack();Assert(module.Section==FairySection.HeyboxTasks,"Heybox login management returns to Heybox check-in management");
            }
            using(var widget=new FairyWidget(store,panel))
            {
                typeof(Control).GetMethod("OnMouseDoubleClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(widget,new object[]{new MouseEventArgs(MouseButtons.Left,2,100,100,0)});
                Assert(!panel.Visible,"double-clicking the widget never opens the full dashboard");
                widget.OpenModule(FairySection.Checkin);
                var active=(FairyModuleForm)typeof(FairyWidget).GetField("module",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(widget);
                var notice=(FairyDialogue)typeof(FairyWidget).GetField("dialogue",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(widget);
                Assert(notice.Visible,"opening a page displays its Fairy narration");active.Close();Assert(!notice.Visible,"closing a module immediately hides its narration bubble");
                widget.OpenPanel();panel.Close();Assert(!panel.Visible&&!notice.Visible,"closing the dashboard hides both dashboard and narration");
                widget.Say("主人，小黑盒登录通道已就绪。");using(var auth=new HeyboxForm(store,settings,null,null,null,panel.DismissPageDialogue)){auth.Opacity=0;auth.Show();auth.Close();}Assert(!notice.Visible,"closing the Heybox login dialog also dismisses its narration");
            }
            Assert(AppAssets.Image("heybox").Width>0&&Theme.Background.B>Theme.Background.R&&Theme.Cyan.B>Theme.Cyan.R,"provided Heybox icon and unified HDD blue palette are available");
            panel.AllowExit=true;
        }
    }
}
