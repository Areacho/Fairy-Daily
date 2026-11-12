using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MiyouDaily
{
    public static class CheckinNotifications
    {
        public static string WidgetMutexName {get{return "Local\\MiyouDaily-Fairy-"+Environment.UserName;}}
        public static FairyMessage Publish(Store store,Settings settings,RunSummary result,bool scheduled)
        {
            if(scheduled&&!Clock.IsDue(settings.DailyTime,Clock.ChinaNow))return null;
            string text=CheckinStatus.CompletionMessage(store,settings,result);
            if(scheduled)
            {
                if(result.Failed>0)text="主人，定时签到已执行，有 "+result.Failed+" 项需要您处理，请查看签到日志。";
                else if(CheckinStatus.IsTodayComplete(store,settings))
                {string platform=settings.HeyboxEnabled?(settings.Games.Length+settings.Forums.Length>0?"米游社与小黑盒":"小黑盒"):"米游社";text="主人，定时委托已完成。今日的"+platform+"签到已全部确认。";}
                else text="主人，定时检查已结束，部分项目尚未确认完成，请查看日志。";
            }
            return PublishText(store,settings,text,scheduled);
        }
        public static FairyMessage PublishText(Store store,Settings settings,string text,bool scheduled)
        {return scheduled?SpeechBus.PublishScheduledMessage(store,settings,text,true):SpeechBus.Publish(store,text,true);}
        public static bool ShouldReportScheduled(Store store,Settings settings,bool hadWork)
        {return Clock.IsDue(settings.DailyTime,Clock.ChinaNow)&&(hadWork||CheckinStatus.IsTodayComplete(store,settings));}
        public static bool ShouldStartWidget(FairyMessage message,bool widgetRunning)
        {return message!=null&&message.IsCompletion&&!widgetRunning;}
        public static void EnsureWidget(Store store,FairyMessage message,string executable)
        {
            bool running=false;try{using(var existing=Mutex.OpenExisting(WidgetMutexName))running=true;}catch(WaitHandleCannotBeOpenedException){}
            if(!ShouldStartWidget(message,running))return;
            try{using(var process=Process.Start(new ProcessStartInfo(executable,"--notify"){UseShellExecute=false,CreateNoWindow=true})){} }
            catch(Win32Exception ex){store.Log("签到结果已保存，挂件启动需要处理："+ex.Message);}
            catch(IOException ex){store.Log("签到结果已保存，挂件启动需要处理："+ex.Message);}
        }
        [DllImport("user32.dll",SetLastError=true)]static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
        [DllImport("user32.dll")]static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder value,uint length,out uint needed);
        public static bool DesktopAvailable()
        {
            IntPtr desktop=OpenInputDesktop(0,false,1);if(desktop==IntPtr.Zero)return false;
            try{var name=new StringBuilder(256);uint needed;return GetUserObjectInformation(desktop,2,name,(uint)(name.Capacity*2),out needed)&&string.Equals(name.ToString(),"Default",StringComparison.OrdinalIgnoreCase);}
            finally{CloseDesktop(desktop);}
        }
    }
}
