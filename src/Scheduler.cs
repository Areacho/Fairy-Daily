using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;

namespace MiyouDaily
{
    public static class Scheduler
    {
        public static string TaskName { get { return "MiyouDaily-" + WindowsIdentity.GetCurrent().User.Value; } }
        public static string MakeXml(string exePath, string sid, string time, DateTimeOffset now)
        {
            var target = now.ToOffset(TimeSpan.FromHours(8)).Date + Clock.ParseTime(time);
            var start = new DateTimeOffset(target, TimeSpan.FromHours(8));
            if (start > now) start = start.AddDays(-1); // Yesterday ensures today's missed run is eligible.
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            Func<string, object, XElement> e = (name, content) => new XElement(ns + name, content);
            return new XDocument(new XDeclaration("1.0", "utf-16", null),
                new XElement(ns + "Task", new XAttribute("version", "1.2"),
                    e("RegistrationInfo", e("Description", "米游社与小黑盒每日签到；北京时间 " + time + "。登录后补跑，失败每 30 分钟重试，每项目每日最多 3 次。")),
                    new XElement(ns + "Triggers",
                        new XElement(ns + "CalendarTrigger",
                            new XElement(ns + "Repetition", e("Interval", "PT30M"), e("Duration", "P1D"), e("StopAtDurationEnd", "false")),
                            e("StartBoundary", start.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)), e("Enabled", "true"),
                            e("ScheduleByDay", e("DaysInterval", 1))),
                        new XElement(ns + "LogonTrigger", e("Enabled", "true"), e("UserId", sid), e("Delay", "PT1M"))),
                    e("Principals", new XElement(ns + "Principal", new XAttribute("id", "Author"), e("UserId", sid), e("LogonType", "InteractiveToken"), e("RunLevel", "LeastPrivilege"))),
                    new XElement(ns + "Settings", e("MultipleInstancesPolicy", "IgnoreNew"), e("DisallowStartIfOnBatteries", "false"), e("StopIfGoingOnBatteries", "false"),
                        e("AllowHardTerminate", "true"), e("StartWhenAvailable", "true"), e("RunOnlyIfNetworkAvailable", "false"),
                        e("Enabled", "true"), e("Hidden", "false"), e("WakeToRun", "false"), e("ExecutionTimeLimit", "PT20M"), e("Priority", 7)),
                    new XElement(ns + "Actions", new XAttribute("Context", "Author"), new XElement(ns + "Exec", e("Command", exePath), e("Arguments", "--run --scheduled"), e("WorkingDirectory", Path.GetDirectoryName(exePath)))))).ToString();
        }
        static string Quote(string text) { if (text.Contains("\"") || text.Contains("\r") || text.Contains("\n")) throw new ArgumentException("路径格式无效。"); return "\"" + text + "\""; }
        static int Command(string arguments, out string output)
        {
            using (var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe"), arguments)
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(15000)) { process.Kill(); throw new IOException("Windows 任务计划响应超时。"); }
                output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult(); return process.ExitCode;
            }
        }
        public static string Install(Store store, Settings settings, string sourceExe)
        {
            settings.Validate();string exe=AppInstallation.Ensure(store,sourceExe);
            string xmlPath = Path.Combine(store.Root, "task.xml");
            File.WriteAllText(xmlPath, MakeXml(exe, WindowsIdentity.GetCurrent().User.Value, settings.DailyTime, Clock.ChinaNow), Encoding.Unicode);
            string output;
            if (Command("/Create /TN " + Quote(TaskName) + " /XML " + Quote(xmlPath) + " /F", out output) != 0)
                throw new IOException("创建任务失败：" + output.Trim());
            return exe;
        }
        public static bool Exists() { string output; return Command("/Query /TN " + Quote(TaskName), out output) == 0; }
        public static void Remove()
        {
            string output; if (Command("/Delete /TN " + Quote(TaskName) + " /F", out output) != 0) throw new IOException("删除任务失败：" + output.Trim());
        }
    }
}
