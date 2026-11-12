using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void CompletionNoticeChecks()
    {
        var store=TestStore();var settings=Setup(store,new string[0],new[]{"5"});store.SaveState(CompletedState(settings,FakeCookie));
        var scheduled=CheckinNotifications.Publish(store,settings,new RunSummary{Success=1},true);
        Assert(scheduled!=null&&scheduled.IsCompletion&&scheduled.Text.Contains("定时委托已完成")&&scheduled.Text.Contains("米游社"),"scheduled completion produces an explicit Fairy dialogue message");
        Assert(CheckinNotifications.ShouldReportScheduled(store,settings,false)&&!SpeechBus.HasDueWork(store,settings,true),"the first scheduled completed check still reports completion when no new network work is needed");
        Assert(CheckinNotifications.Publish(store,settings,new RunSummary{Skipped=1},true)==null,"scheduled fresh completion and subsequent cached checks share one daily result notification");
        var manual=CheckinNotifications.Publish(store,settings,new RunSummary{Skipped=1},false);
        Assert(manual!=null&&manual.Text==CheckinStatus.AlreadyCompleteMessage&&manual.Id!=scheduled.Id,"manual repeated completion gives immediate feedback with a fresh message ID");
        var noon=new DateTimeOffset(Clock.ChinaNow.Date.AddHours(12),TimeSpan.FromHours(8));
        var delayed=new FairyMessage{Id="delayed-completion",Text=manual.Text,IsCompletion=true,At=noon.AddMinutes(-30).ToString("o")};
        Assert(SpeechBus.IsPending(store,delayed,noon),"unread completion survives beyond the old two-minute transient notification window");
        Assert(!SpeechBus.IsPending(store,delayed,noon.AddDays(1)),"yesterday's completion is not displayed as today's result");
        SpeechBus.MarkDelivered(store,manual);Assert(!SpeechBus.IsPending(store,manual,DateTimeOffset.UtcNow),"a displayed result is acknowledged and not repeated after restart");
        Assert(SpeechBus.IsPending(store,scheduled,DateTimeOffset.UtcNow),"acknowledging one result does not consume another pending result");
        Assert(CheckinNotifications.ShouldStartWidget(scheduled,false)&&!CheckinNotifications.ShouldStartWidget(scheduled,true)&&!CheckinNotifications.ShouldStartWidget(null,false),"background completion wakes a missing widget without starting duplicates or empty notifications");
        var failure=CheckinNotifications.Publish(store,settings,new RunSummary{Failed=2},true);
        Assert(failure.Text.Contains("2 项需要")&&!failure.Text.Contains("已全部确认"),"scheduled failures are reported without claiming successful completion");
        using(var panel=new MainForm(store,settings))using(var widget=new FairyWidget(store,panel))
        {
            var dialogue=(FairyDialogue)typeof(FairyWidget).GetField("dialogue",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(widget);
            Assert(!widget.DeliverCompletion(failure,false)&&!dialogue.Visible&&SpeechBus.IsPending(store,failure,DateTimeOffset.UtcNow),"locked or unavailable desktop defers the dialogue without consuming its notification");
            widget.PollNotifications(true);
            Assert(dialogue.Visible&&dialogue.TopMost&&!dialogue.Controls.OfType<TableLayoutPanel>().Single().Visible,"pending completion is displayed above other windows as a message-only dialogue");
            Assert(dialogue.Controls.OfType<Label>().Single().Text==failure.Text&&!SpeechBus.IsPending(store,failure,DateTimeOffset.UtcNow),"the displayed completion contains the persisted result and records delivery");
            dialogue.HideNow();widget.PollNotifications(true);Assert(!dialogue.Visible,"polling again does not reopen an acknowledged completion dialogue");
            var success=CheckinNotifications.Publish(store,settings,new RunSummary(),false);widget.DeliverCompletion(success,true);
            Assert(dialogue.Visible&&dialogue.Controls.OfType<Label>().Single().Text==CheckinStatus.AlreadyCompleteMessage,"a later manual completion displays a new dialogue after an earlier result was dismissed");
            panel.AllowExit=true;
        }
    }
}
