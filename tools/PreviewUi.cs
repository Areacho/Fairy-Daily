using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.Versioning;
using MiyouDaily;

[assembly: TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

// Offline visual review. Uses an empty, isolated store and never invokes login,
// sign-in, startup registration, or task installation.
class PreviewUi
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string output=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build","ui-preview"));
        Directory.CreateDirectory(output);
        var previewStore=new Store(Path.Combine(output,"empty-store"));var previewSettings=new Settings();
        using(var form=new MainForm(previewStore,previewSettings))
        {
            if(args.Length>0&&args[0]=="--modules"){using(var module=new FairyModuleForm(form,FairySection.Checkin))Application.Run(module);return;}
            if(args.Length==0 || args[0]!="--render"){form.AllowExit=true;Application.Run(form);return;}
            var handle=form.Handle;
            var standard=WindowLayout.Pixels(new Size(1080,900),form.DeviceDpi);var small=WindowLayout.Pixels(new Size(980,810),form.DeviceDpi);
            Render(form,standard.Width,standard.Height,Path.Combine(output,"dashboard.png"));
            Render(form,small.Width,small.Height,Path.Combine(output,"dashboard-small.png"));
            using(var module=new FairyModuleForm(form,FairySection.Login))
            {
                var moduleHandle=module.Handle;
                foreach(var section in new[]{FairySection.Login,FairySection.Checkin,FairySection.MiyouAccount,FairySection.HeyboxAccount,FairySection.MiyouTasks,FairySection.HeyboxTasks,FairySection.MiyouOptions,FairySection.HeyboxOptions,FairySection.Schedule})
                {module.Navigate(section,false);Render(module,module.Width,module.Height,Path.Combine(output,"module-"+section+".png"));}
            }
            using(var bubble=new FairyDialogue(form,()=>{}))
            {
                bubble.OpenShortcuts(false);bubble.ShowFor(form);float z=bubble.DeviceDpi/96f;bubble.ClientSize=new Size((int)(480*z),(int)(116*z));
                using(var bitmap=new Bitmap(bubble.Width,bubble.Height)){bubble.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"shortcuts.png"));}
                previewStore.SetCookie(previewSettings,"stuid=10000000; stoken=SYNTHETIC_PREVIEW_TOKEN");bubble.OpenShortcuts(false);bubble.ClientSize=new Size((int)(480*z),(int)(116*z));
                using(var bitmap=new Bitmap(bubble.Width,bubble.Height)){bubble.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"shortcuts-logged-in.png"));}previewSettings.ProtectedCookie="";
                bubble.Say("主人，定时委托已完成。今日的米游社与小黑盒签到已全部确认。");bubble.ClientSize=new Size((int)(480*z),(int)(126*z));
                using(var bitmap=new Bitmap(bubble.Width,bubble.Height)){bubble.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"notice.png"));}
                bubble.Say("主人，定时委托已完成。今日的米游社与小黑盒签到已全部确认。\n8 秒后退出挂件，点击眼睛或关闭气泡可取消。",true);bubble.ClientSize=new Size((int)(480*z),(int)(164*z));
                using(var bitmap=new Bitmap(bubble.Width,bubble.Height)){bubble.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"notice-auto-exit.png"));}bubble.Hide();
            }
            using(var widget=new FairyWidget(previewStore,form))
            {
                var menu=widget.ContextMenuStrip;menu.Opacity=0;menu.Show(new Point(40,40));menu.PerformLayout();
                using(var bitmap=new Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"context-menu.png"));}menu.Close();
            }
        }
        using(var heybox=new HeyboxForm(new Store(Path.Combine(output,"empty-store")),new Settings()))
        {
            var handle=heybox.Handle;Render(heybox,heybox.Width,heybox.Height,Path.Combine(output,"heybox.png"));
        }
        using(var qr=new QrLoginForm())
        {
            var handle=qr.Handle;
            Render(qr,qr.Width,qr.Height,Path.Combine(output,"login.png"));
        }
        Console.WriteLine(output);
    }
    static void Render(Form form,int width,int height,string path)
    {
        form.Size=new Size(width,height);form.PerformLayout();
        Control root=form.Controls[0];root.Font=form.Font;root.ForeColor=form.ForeColor;
        Size size=form.ClientSize;
        form.Controls.Remove(root);root.Dock=DockStyle.None;root.Size=size;CreateHandles(root);root.PerformLayout();
        using(var bitmap=new Bitmap(size.Width,size.Height))
        {root.DrawToBitmap(bitmap,new Rectangle(Point.Empty,size));bitmap.Save(path);}
        form.Controls.Add(root);root.Dock=DockStyle.Fill;
    }
    static void CreateHandles(Control control)
    {var handle=control.Handle;foreach(Control child in control.Controls)CreateHandles(child);}
}
