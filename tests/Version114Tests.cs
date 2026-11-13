using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void Version114Checks()
    {
        using(var panel=new MainForm(TestStore(),new Settings()))
        using(var widget=new FairyWidget(TestStore(),panel))
        {
            var bubble=WidgetDialogue(widget);int dismissed=0;bubble.Dismissed+=()=>dismissed++;
            widget.Say("offline short notice");bubble.ProcessIdle(3999,false);Assert(dismissed==0,"a normal notice remains readable before four seconds");
            bubble.ProcessIdle(4000,true);Assert(dismissed==0,"hovering a short notice still pauses its dismissal");
            bubble.KeepNoticeOpen=true;bubble.ProcessIdle(4000,false);Assert(dismissed==0,"progress and automatic-exit notices retain their explicit keep-open behaviour");
            bubble.KeepNoticeOpen=false;bubble.ProcessIdle(4000,false);Assert(dismissed==1,"a normal notice starts disappearing after four seconds");
            Thread.Sleep(120);Application.DoEvents();Assert(!bubble.Visible&&!PopupSurface.Attach(bubble).Background.Visible,"the shorter fade hides both notification windows promptly");
            bubble.OpenShortcuts(false);bubble.ShowFor(widget);int before=dismissed;bubble.ProcessIdle(5999,false);Assert(dismissed==before,"shortcut controls keep six seconds of idle reading time");bubble.ProcessIdle(6000,false);Assert(dismissed==before+1,"idle shortcuts dismiss after six seconds");
            panel.Opacity=0;int speech=0;panel.SpeechRequested+=text=>speech++;widget.OpenPanel();
            var heading=FindControls(panel).OfType<FairyHeader>().Single();var entry=FindControls(panel).OfType<RoundButton>().Single(b=>b.AccessibleName=="打开显示设置");
            Assert(!bubble.Visible&&!PopupSurface.Attach(bubble).Background.Visible&&speech==0,"opening the total panel stops the desktop bubble instead of leaving an obscured announcement");
            Assert(heading.HasEntryHint,"the total-panel announcement appears inside its own header");
            using(var image=new Bitmap(heading.Width,heading.Height)){image.SetResolution(heading.DeviceDpi,heading.DeviceDpi);heading.DrawToBitmap(image,heading.ClientRectangle);image.Save(System.IO.Path.Combine(DisplayReviewDirectory(),"entry-hint-v114.png"));}
            foreach(int dpi in new[]{96,120,144,168,192,240,288})
            {
                SetTestDpi(panel,dpi);panel.ApplyDisplayLayout(false);panel.ShowEntryHint();
                Assert(heading.ClientRectangle.Contains(heading.EntryHintBounds)&&!heading.EntryHintBounds.IntersectsWith(entry.Bounds),"the in-page hint stays clear of settings controls at "+dpi+" DPI");
            }
            var timer=(System.Windows.Forms.Timer)typeof(FairyHeader).GetField("hintTimer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(heading);
            Assert(timer.Interval==2500,"the total-panel entry hint has a two-and-a-half-second lifetime");
            typeof(System.Windows.Forms.Timer).GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(timer,new object[]{EventArgs.Empty});
            Assert(!heading.HasEntryHint&&!timer.Enabled,"the entry-hint timer clears the text and stops itself");
            panel.ShowEntryHint();typeof(Form).GetMethod("OnDeactivate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,new object[]{EventArgs.Empty});Assert(!heading.HasEntryHint,"switching to another page clears the in-page announcement");
            widget.OpenModule(FairySection.Checkin);Assert(!bubble.Visible,"opening the configuration page uses its existing inline guide without an overlapping desktop bubble");
            panel.AllowExit=true;
        }
    }
}
