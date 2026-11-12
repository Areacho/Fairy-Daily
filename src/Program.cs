using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MiyouDaily
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
                if (new AssemblyName(e.Name).Name != "QRCoder") return null;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QRCoder.dll"))
                { if (stream == null) return null; byte[] bytes = new byte[stream.Length]; int offset = 0, read; while ((read = stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += read; return Assembly.Load(bytes); }
            };
            bool selfCheck = args.Contains("--self-check");
            bool headless = args.Contains("--run") || selfCheck; Store store = null;Settings settings=null;
            try
            {
                if (selfCheck) { QrTools.SelfCheck();foreach(var game in Catalog.Games)AppAssets.Image(game.Key);AppAssets.Image("miyoushe");AppAssets.Image("heybox");if(AppAssets.Icon.Width<1)throw new InvalidDataException("Fairy 图标加载失败。");return 0; }
                store = Store.Default(); settings = store.Load();
                if (headless)
                {
                    bool scheduled=args.Contains("--scheduled");
                    if(!scheduled && CheckinStatus.IsTodayComplete(store,settings))
                    {
                        store.Log(CheckinStatus.AlreadyCompleteMessage);var notice=CheckinNotifications.Publish(store,settings,new RunSummary(),false);CheckinNotifications.EnsureWidget(store,notice,Application.ExecutablePath);return 0;
                    }
                    bool announce=!scheduled || SpeechBus.HasDueWork(store,settings,scheduled);
                    if(announce&&!scheduled)SpeechBus.Publish(store,"主人，签到委托已接收，正在为您处理。");
                    using (var transport = new Transport())
                    {
                        var result=new Runner(store, transport, null).Run(settings, scheduled, CancellationToken.None).GetAwaiter().GetResult();
                        if(announce||(scheduled&&CheckinNotifications.ShouldReportScheduled(store,settings,false)))
                        {
                            var notice=CheckinNotifications.Publish(store,settings,result,scheduled);CheckinNotifications.EnsureWidget(store,notice,Application.ExecutablePath);
                        }
                        return result.ExitCode;
                    }
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                using(var singleton=new Mutex(false,CheckinNotifications.WidgetMutexName))
                {
                    bool owns=false;try{owns=singleton.WaitOne(0);}catch(AbandonedMutexException){owns=true;}
                    if(!owns)return 0;
                    try{var panel=new MainForm(store,settings);var widget=new FairyWidget(store,panel);Application.Run(widget);}
                    finally{singleton.ReleaseMutex();}
                }
                return 0;
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                if (store != null) { try { message = Store.Redact(message, store.Decrypt(store.Load())); } catch { } try { store.Log("执行失败：" + message); } catch { } }
                if(headless&&store!=null){try{bool scheduled=args.Contains("--scheduled");string text=scheduled?"主人，定时签到未完成，请查看日志："+message:"主人，签到未完成，请查看日志："+message;var notice=CheckinNotifications.PublishText(store,settings??new Settings(),text,scheduled);CheckinNotifications.EnsureWidget(store,notice,Application.ExecutablePath);}catch{}}
                if (!headless) MessageBox.Show(message, "米游日签", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
