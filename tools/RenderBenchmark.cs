using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MiyouDaily;

// Offline renderer benchmark: isolated settings, no login or sign-in actions.
class RenderBenchmark
{
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string directory=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","build","ui-preview"));
        Directory.CreateDirectory(directory);
        var watch=Stopwatch.StartNew();
        using(var form=new MainForm(new Store(Path.Combine(directory,"empty-store")),new Settings()))
        {
            var handle=form.Handle;
            Console.WriteLine("Construct + handles: "+watch.ElapsedMilliseconds+" ms");
            foreach(var size in new[]{new Size(1590,1238),new Size(3840,2160),new Size(1590,1238)})
            {
                watch.Restart();form.Size=size;form.PerformLayout();
                Control root=form.Controls[0];root.Font=form.Font;root.ForeColor=form.ForeColor;
                Size client=form.ClientSize;form.Controls.Remove(root);root.Dock=DockStyle.None;
                root.Size=client;root.CreateControl();root.PerformLayout();
                using(var bitmap=new Bitmap(form.ClientSize.Width,form.ClientSize.Height))
                {
                    root.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
                    Console.WriteLine(size+" layout + first frame: "+watch.ElapsedMilliseconds+" ms");
                    watch.Restart();root.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
                    Console.WriteLine(size+" repeated frame: "+watch.ElapsedMilliseconds+" ms");
                    if(args.Length>0)bitmap.Save(Path.Combine(directory,args[0]+"-"+size.Width+".png"));
                }
                form.Controls.Add(root);root.Dock=DockStyle.Fill;
            }
        }
    }
}
