using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed partial class MainForm
    {
        void BuildDailyControls()
        {
            var card=Card("02   每日定时");card.AccessibleName="每日定时设置";
            var zone=new SmoothLabel{Text="每天 / 北京时间",ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.Transparent};
            var note=new SmoothLabel{Text="联网并保持 Windows 登录，锁屏也可执行",ForeColor=Theme.Muted,BackColor=Color.Transparent};
            schedule.ForeColor=Theme.Cyan;schedule.BackColor=Color.Transparent;
            autoStart.Text="开机自启";autoStart.AccessibleName="开机自启开关";autoStart.AccessibleDescription="登录 Windows 后自动启动 Fairy 桌面挂件";autoStart.Checked=StartupManager.IsEnabled();
            autoStart.CheckedChanged+=(s,e)=>ChangeStartup();
            enableDaily.Text="开启每日定时";disableDaily.Text="关闭每日定时";
            enableDaily.Click+=(s,e)=>{try{Save();RequireLogin();Scheduler.Install(store,settings,Application.ExecutablePath);RefreshStatus();Write("每日定时已开启。");Speak("主人，每日签到已安排妥当。我会在北京时间 "+settings.DailyTime+" 执行签到。");}catch(Exception ex){ShowError(ex);}};
            disableDaily.Click+=(s,e)=>{try{if(Scheduler.Exists())Scheduler.Remove();RefreshStatus();Write("每日定时已关闭。");Speak("主人，每日定时已关闭，需要时可以随时叫我签到。");}catch(Exception ex){ShowError(ex);}};
            card.Controls.AddRange(new Control[]{time,zone,enableDaily,disableDaily,schedule,note,autoStart});
            card.Layout+=(s,e)=>{
                float z=DeviceDpi/96f;
                zone.SetBounds((int)(20*z),(int)(36*z),(int)(206*z),(int)(24*z));
                time.SetBounds((int)(20*z),(int)(65*z),(int)(206*z),(int)(64*z));
                enableDaily.SetBounds((int)(248*z),(int)(73*z),(int)(144*z),(int)(40*z));
                disableDaily.SetBounds((int)(404*z),(int)(73*z),(int)(144*z),(int)(40*z));
                schedule.SetBounds((int)(248*z),(int)(36*z),Math.Max(1,card.Width-(int)(268*z)),(int)(26*z));
                note.SetBounds((int)(248*z),(int)(116*z),Math.Max(1,card.Width-(int)(268*z)),(int)(24*z));
                autoStart.SetBounds(card.Width-(int)(230*z),(int)(76*z),(int)(210*z),(int)(34*z));
            };
            root.Controls.Add(card,0,2);
        }
    }
}
