using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiyouDaily
{
    public class RunSummary
    {
        public int Success, Failed, Skipped, AlreadySigned;
        public int NewlySigned { get { return Success - AlreadySigned; } }
        public int ExitCode { get { return Failed > 0 ? 1 : 0; } }
        public override string ToString() { return "本次完成：成功 " + Success + " 项，失败/待处理 " + Failed + " 项，跳过 " + Skipped + " 项。"; }
        public void Record(Attempt result)
        {
            if (!result.Complete) { Failed++; return; }
            Success++;
            if ((result.Message ?? "").StartsWith("今日已签到", StringComparison.Ordinal)) AlreadySigned++;
        }
    }
    public sealed class Runner
    {
        readonly Store store;
        readonly ITransport transport;
        readonly Action<string> output;
        readonly ITransport heyboxTransport;
        string cookie = "";
        public Runner(Store store, ITransport transport, Action<string> output, ITransport heyboxTransport = null) { this.store = store; this.transport = transport; this.output = output; this.heyboxTransport=heyboxTransport; }
        void Log(string message)
        {
            message = Store.Redact(message, cookie); store.Log(message); if (output != null) output(message);
        }
        public async Task<RunSummary> Run(Settings settings, bool scheduled, CancellationToken cancel)
        {
            using (store.AcquireRunLock())
            {
                settings.Validate();
                var summary = new RunSummary();
                if (scheduled && !Clock.IsDue(settings.DailyTime, Clock.ChinaNow)) { Log("尚未到北京时间 " + settings.DailyTime + "，本次无需执行。"); return summary; }
                bool miyouSelected=settings.Games.Length+settings.Forums.Length>0;
                cookie="";if(miyouSelected){try{cookie=store.Decrypt(settings);}catch(InvalidOperationException){Log("米游社登录状态无法解密，请重新登录。");}}
                if(cookie.Length==0&&miyouSelected){summary.Failed++;Log("米游社尚未登录，本次跳过米游社项目。小黑盒将独立检查。");}
                string day = Clock.Day(Clock.ChinaNow);
                var state = store.LoadState();
                if (state == null || state.Targets == null) throw new InvalidDataException("签到记录损坏，请在数据目录中移走 state.json 后重试。");
                state.ForDay(day);
                bool migrated=false;string account = cookie.Length==0?"":AccountScope.Prepare(state,cookie,out migrated);
                if(migrated)store.SaveState(state);
                var api = new MiyouApi(settings, cookie, transport);
                Log((scheduled ? "定时" : "手动") + "签到开始（北京时间 " + day + "）。");
                foreach (var game in Catalog.Games.Where(x => cookie.Length>0 && settings.Games.Contains(x.Key)))
                {
                    cancel.ThrowIfCancellationRequested();
                    string key = account + "game/" + game.Key;
                    if (state.Skip(key, scheduled)) { summary.Skipped++; Log(game.Name + "：今日已完成，或已达自动重试上限；可手动重试待处理项。"); continue; }
                    Attempt group; if (!state.Targets.TryGetValue(key, out group)) group = new Attempt();
                    group.Count++; state.Targets[key] = group;
                    // Persist before sending requests so a crash cannot cause unlimited retries.
                    store.SaveState(state);
                    try
                    {
                        var roles = await api.Roles(game, cancel).ConfigureAwait(false);
                        if (roles.Count == 0) { group.Complete = true; group.Message = "没有绑定角色"; summary.Skipped++; Log(game.Name + "：没有绑定角色，跳过。"); }
                        else
                        {
                            bool allComplete = true, hasRetryable = false;
                            foreach (var role in roles)
                            {
                                string roleKey = key + "/" + Json.Str(role, "region") + "/" + Json.Str(role, "game_uid");
                                string label = game.Name + " · " + Json.Str(role, "nickname") + "（" + Json.Str(role, "game_uid") + "）";
                                if (state.Skip(roleKey, scheduled))
                                {
                                    var previous = state.Targets[roleKey];
                                    allComplete &= previous.Complete;
                                    hasRetryable |= !previous.Complete && !previous.NeedsAction && previous.Count < 3;
                                    summary.Skipped++; continue;
                                }
                                var result = await Execute(state, roleKey, label, () => api.SignGame(game, role, cancel), cancel).ConfigureAwait(false);
                                allComplete &= result.Complete;
                                hasRetryable |= !result.Complete && !result.NeedsAction && result.Count < 3;
                                summary.Record(result);
                                await Task.Delay(1200, cancel).ConfigureAwait(false);
                            }
                            group.Complete = allComplete; group.NeedsAction = !allComplete && !hasRetryable;
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        group.Complete = false; group.NeedsAction = ex is ApiException && ((ApiException)ex).NeedsAction;
                        group.Message = Store.Redact(ex.Message, cookie); summary.Failed++; Log(game.Name + "：" + group.Message);
                    }
                    store.SaveState(state);
                }
                foreach (string gid in cookie.Length==0?new string[0]:settings.Forums)
                {
                    cancel.ThrowIfCancellationRequested(); string key = account + "forum/" + gid;
                    if (state.Skip(key, scheduled)) { summary.Skipped++; continue; }
                    var result = await Execute(state, key, "社区 · " + Catalog.Forums[gid], () => api.SignForum(gid, cancel), cancel).ConfigureAwait(false);
                    summary.Record(result);
                    await Task.Delay(1200, cancel).ConfigureAwait(false);
                }
                if(settings.HeyboxEnabled)
                {
                    cancel.ThrowIfCancellationRequested();HeyboxConfig heybox=null;
                    try
                    {
                        heybox=HeyboxAccount.Load(settings);
                        if(heybox==null)throw new ApiException("小黑盒尚未登录，请打开“小黑盒签到”完成短信登录或导入配置。",true);
                        if(state.Skip(heybox.Key,scheduled)){summary.Skipped++;Log("小黑盒：今日已完成，或需要手动处理。");}
                        else
                        {
                            // Keep this provider's errors and credentials isolated from Miyou.
                            Func<Task<string>> sign=async()=>{
                                try
                                {
                                    if(heyboxTransport!=null)return await new HeyboxApi(heybox,heyboxTransport).Sign(cancel).ConfigureAwait(false);
                                    using(var client=new HeyboxTransport())return await new HeyboxApi(heybox,client).Sign(cancel).ConfigureAwait(false);
                                }
                                catch(OperationCanceledException){throw;}
                                catch(Exception ex){throw new ApiException(HeyboxAccount.Redact(ex.Message,heybox),ex is ApiException&&((ApiException)ex).NeedsAction);}
                            };
                            summary.Record(await Execute(state,heybox.Key,"小黑盒",sign,cancel).ConfigureAwait(false));
                        }
                    }
                    catch(OperationCanceledException){throw;}
                    catch(Exception ex){summary.Failed++;Log("小黑盒："+HeyboxAccount.Redact(ex.Message,heybox));}
                }
                // A cancelled or partial run never marks the entire day complete.
                Log(summary.ToString());
                return summary;
            }
        }
        async Task<Attempt> Execute(RunState state, string key, string label, Func<Task<string>> action, CancellationToken cancel)
        {
            Attempt a; if (!state.Targets.TryGetValue(key, out a)) a = new Attempt();
            a.Count++; state.Targets[key] = a; store.SaveState(state);
            try { a.Message = await action().ConfigureAwait(false); a.Complete = true; a.NeedsAction = false; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                a.Complete = false;
                a.NeedsAction = ex is InvalidOperationException || (ex is ApiException && ((ApiException)ex).NeedsAction);
                a.Message = Store.Redact(ex.Message, cookie);
            }
            store.SaveState(state); Log(label + "：" + a.Message); return a;
        }
    }
}
