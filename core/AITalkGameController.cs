using ActionGame;
using KKAITalk;
using KKAITalk.Context;
using KKAITalk.LLM;
using KKAITalk.Memory;
using KKAITalk.UI;
using KKAPI.MainGame;
using System.IO;
using UnityEngine;
using static SaveData;

namespace KKAITalk
{
    public class AITalkGameController : GameCustomFunctionController
    {
        private CharacterContext _currentChara;

        protected override void OnDayChange(Cycle.Week day)
        {
        }

        protected override void OnPeriodChange(Cycle.Type period)
        {
            _currentPeriod = period;
            AITalkPlugin.Log.LogInfo("时间段变化: " + GameContextBuilder.TranslatePeriod(period));
        }
        private int ParseFavorDelta(string reply)
        {
            int upIdx = reply.LastIndexOf("[FAVOR:UP]");
            int downIdx = reply.LastIndexOf("[FAVOR:DOWN]");
            int noneIdx = reply.LastIndexOf("[FAVOR:NONE]");

            int maxIdx = Mathf.Max(upIdx, downIdx, noneIdx);
            if (maxIdx < 0) return 0;

            if (maxIdx == upIdx) return +5;
            if (maxIdx == downIdx) return -5;
            return 0;
        }
        public void OnTalkStart(SaveData.Heroine heroine, string playerInput)
        {
            _currentChara = CharacterDataReader.ReadFromHeroine(heroine);
            if (_currentChara == null) return;
            _currentChara.CurrentPeriod = _currentPeriod;

            AIDialogueUI.Instance?.ShowWaiting(_currentChara.Name);
            // 读取记忆
            string saveId = MemoryManager.GetSaveId();
            var history = MemoryManager.LoadHistory(saveId, _currentChara.CharaId);

            var messages = GameContextBuilder.BuildMessages(_currentChara, playerInput, history);
            AITalkPlugin.Client.SendMessage(
                messages,
                reply =>
                {
                    // 日志保留原始标签
                    AITalkPlugin.Log.LogInfo($"[原始回复] {reply}");

                    // 清理标签
                    string cleanReply = System.Text.RegularExpressions.Regex.Replace(
                        reply, @"\[APOLOGY:[^\]]+\]|\[FAVOR:[^\]]+\]|\[INTIMACY:[^\]]+\]|\[LEWD:[^\]]+\]|\[EVENT:[^\]]+\]", "").Trim();
                    cleanReply = cleanReply.Replace("\n", "").Replace("\r", "");

                    // 触发游戏事件 ← 放这里
                    var talkScene = UnityEngine.Object.FindObjectOfType<TalkScene>();
                    if (talkScene != null)
                        ParseAndTriggerEvent(reply, talkScene, heroine);

                    // 解析anger用原始reply
                    if (heroine.isAnger)
                    {
                        int delta = ParseAngerDelta(reply);
                        if (!reply.Contains("[APOLOGY:"))
                            AITalkPlugin.Log.LogWarning("未检测到APOLOGY标签，anger不变");
                        AITalkPlugin.Log.LogInfo($"道歉解析结果: delta={delta}");
                        heroine.anger = Mathf.Clamp(heroine.anger + delta, 0, 100);
                        if (heroine.anger <= 0)
                        {
                            heroine.anger = 0;
                            heroine.isAnger = false;
                            AITalkPlugin.Log.LogInfo("角色已原谅玩家");
                        }
                        else if (delta > 0)
                        {
                            heroine.isAnger = true;
                            // 激怒角色时扣好感
                            heroine.favor = Mathf.Clamp(heroine.favor - 10, 0, 100);
                            AITalkPlugin.Log.LogInfo($"激怒角色，好感度-10，当前: {heroine.favor}");
                            AITalkPlugin.Log.LogInfo($"愤怒加剧，当前: {heroine.anger}");
                        }
                        else
                            AITalkPlugin.Log.LogInfo($"愤怒变化后: {heroine.anger}");
                    }
                    // 好感度变化
                    else
                    {
                        int favorDelta = ParseFavorDelta(reply);
                        int intimacyDelta = ParseIntimacyDelta(reply);
                        if (!reply.Contains("[FAVOR:"))
                            AITalkPlugin.Log.LogWarning("未检测到FAVOR标签");
                        if (favorDelta != 0)
                        {
                            heroine.favor = Mathf.Clamp(heroine.favor + favorDelta, 0, 100);
                            AITalkPlugin.Log.LogInfo($"好感度变化: {favorDelta}, 当前: {heroine.favor}");
                        }
                        if (!reply.Contains("[INTIMACY:"))
                            AITalkPlugin.Log.LogWarning("未检测到INTIMACY标签");
                        if (intimacyDelta != 0)
                        {
                            heroine.intimacy = Mathf.Clamp(heroine.intimacy + intimacyDelta, 0, 100);
                            AITalkPlugin.Log.LogInfo($"亲密度变化: +{intimacyDelta}, 当前: {heroine.intimacy}");
                        }
                    }


                    // UI和历史记录用cleanReply
                    AIDialogueUI.Instance?.ShowReply(cleanReply);
                    history.Add(new ChatMessage { role = "user", content = playerInput });
                    history.Add(new ChatMessage { role = "assistant", content = cleanReply });
                    MemoryManager.SaveHistory(saveId, _currentChara.CharaId, history);

                    AITalkPlugin.OnReplyReceived?.Invoke();
                    AITalkPlugin.Log.LogInfo($"OnReplyReceived调用完成，是否为null: {AITalkPlugin.OnReplyReceived == null}");
                    AITalkPlugin.OnReplyReceived = null;
                },
                err => AITalkPlugin.Log.LogError("请求失败: " + err)
            );
        }
        private int ParseIntimacyDelta(string reply)
        {
            if (reply.Contains("[INTIMACY:UP]")) return +10;
            return 0;
        }
        private int ParseAngerDelta(string reply)
        {
            // 从后往前匹配，只取最后出现的标签
            int worseIdx = reply.LastIndexOf("[APOLOGY:WORSE]");
            int sorryIdx = reply.LastIndexOf("[APOLOGY:SORRY]");
            int noneIdx = reply.LastIndexOf("[APOLOGY:NONE]");

            int maxIdx = Mathf.Max(worseIdx, sorryIdx, noneIdx);
            if (maxIdx < 0) return 0;

            if (maxIdx == worseIdx) return +30;
            if (maxIdx == sorryIdx) return -100;
            return 0;
        }
        private void ParseAndTriggerEvent(string reply, TalkScene talkScene, SaveData.Heroine heroine)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Talk")
            {
                AITalkPlugin.Log.LogInfo("非Talk场景，跳过事件触发");
                return;
            }

            // 表白直接改布尔值，不触发按钮
            if (reply.Contains("[EVENT:CONFESS]"))
            {
                heroine.isGirlfriend = true;
                AITalkPlugin.Log.LogInfo("表白成功，isGirlfriend=true");
                return;
            }

            int index = -1;
            if (reply.Contains("[EVENT:DIVORCE]")) index = 2;
            else if(reply.Contains("[EVENT:H]")) index = 3;
            else if (reply.Contains("[EVENT:LUNCH]")) index = 4;
            else if (reply.Contains("[EVENT:CLUB]")) index = 5;
            else if (reply.Contains("[EVENT:GOHOME]")) index = 6;
            else if (reply.Contains("[EVENT:DATE]")) index = 7;
            else if (reply.Contains("[EVENT:STUDY]")) index = 8;
            else if (reply.Contains("[EVENT:EXERCISE]")) index = 9;
            else if (reply.Contains("[EVENT:JOIN]")) index = 10;
            else if (reply.Contains("[EVENT:FOLLOW]")) index = 11;

            if (index < 0) return;

            AITalkPlugin.Log.LogInfo($"触发事件索引: {index}");
            AITalkPlugin.Instance.TriggerTalkEvent(talkScene, index);
        }


        private Cycle.Type _currentPeriod;

        protected override void OnStartH(HSceneProc hSceneProc, bool freeH)
        {
        }

        protected override void OnEndH(HSceneProc hSceneProc, bool freeH)
        {
        }


        protected override void OnGameLoad(GameSaveLoadEventArgs args)
        {
            AITalkPlugin.Log.LogInfo("游戏读档");
        }

        protected override void OnGameSave(GameSaveLoadEventArgs args)
        {
            AITalkPlugin.Log.LogInfo("游戏存档");
        }
    }
}