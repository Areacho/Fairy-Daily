using System;
using System.IO;
using System.Linq;

namespace MiyouDaily
{
    public static class CheckinStatus
    {
        public const string AlreadyCompleteMessage = "主人今日已完成签到";

        // Only the current account, Beijing date and currently selected targets count.
        public static bool IsTodayComplete(Store store, Settings settings)
        {
            bool miyouSelected=settings.Games.Length+settings.Forums.Length>0;
            string cookie="";if(miyouSelected){try{cookie=store.Decrypt(settings);}catch(InvalidOperationException){return false;}}
            if ((miyouSelected&&cookie.Length==0)||(!miyouSelected&&!settings.HeyboxEnabled)) return false;
            var state = store.LoadState();
            if (state == null || state.Targets == null) throw new InvalidDataException("签到记录损坏，请在数据目录中移走 state.json 后重试。");
            if (state.Day != Clock.Day(Clock.ChinaNow)) return false;
            bool migrated;string prefix = miyouSelected?AccountScope.Prepare(state,cookie,out migrated):"";
            bool signedTarget = false;
            foreach (string game in settings.Games)
            {
                string key = prefix + "game/" + game;
                Attempt group;
                if (!state.Targets.TryGetValue(key, out group) || group == null || !group.Complete) return false;
                var roles = state.Targets.Where(x => x.Key.StartsWith(key + "/", StringComparison.Ordinal)).ToArray();
                if (roles.Any(x => x.Value == null || !x.Value.Complete)) return false;
                if (roles.Length > 0) signedTarget = true;
                else if (group.Message != "没有绑定角色") return false;
            }
            foreach (string forum in settings.Forums)
            {
                Attempt target;
                if (!state.Targets.TryGetValue(prefix + "forum/" + forum, out target) || target == null || !target.Complete) return false;
                signedTarget = true;
            }
            if(settings.HeyboxEnabled)
            {
                HeyboxConfig config;try{config=HeyboxAccount.Load(settings);}catch(InvalidOperationException){return false;}Attempt target;
                if(config==null||!state.Targets.TryGetValue(config.Key,out target)||target==null||!target.Complete)return false;
                signedTarget=true;
            }
            // Selecting only games with no bound roles is not a completed sign-in.
            return signedTarget;
        }

        public static string CompletionMessage(Store store, Settings settings, RunSummary result)
        {
            if (result.Failed > 0) return "主人，签到已执行，有 " + result.Failed + " 项需要您处理，详细情况已记在日志里。";
            if (!IsTodayComplete(store, settings)) return "主人，本次签到检查已结束，尚未确认全部完成，请打开面板查看日志。";
            if (result.NewlySigned == 0) return AlreadyCompleteMessage;
            string platform=settings.HeyboxEnabled?(settings.Games.Length+settings.Forums.Length>0?"米游社与小黑盒":"小黑盒"):"米游社";
            return "主人，今日的"+platform+"签到已完成。愿您今天也有好心情。";
        }
    }
}
