using ActionGame;
using KKAITalk.Context;
using KKAITalk.LLM;
using KKAITalk.Memory;
using KKAITalk.UI;
using KKAPI.MainGame;
using System.IO;
using UnityEngine;

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
                    // 日志保留原始标签，方便调试
                    AITalkPlugin.Log.LogInfo($"[原始回复] {reply}");

                    // 清理标签
                    string cleanReply = System.Text.RegularExpressions.Regex.Replace(
                        reply, @"\[APOLOGY:[^\]]+\]|\[FAVOR:[^\]]+\]|\[INTIMACY:[^\]]+\]", "").Trim();
                    cleanReply = cleanReply.Replace("\n", "").Replace("\r", "");

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
                            heroine.favor = Mathf.Clamp(heroine.favor - 5, 0, 100);
                            AITalkPlugin.Log.LogInfo($"激怒角色，好感度-5，当前: {heroine.favor}");
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
                },
                err => AITalkPlugin.Log.LogError("请求失败: " + err)
            );
        }
        private int ParseIntimacyDelta(string reply)
        {
            if (reply.Contains("[INTIMACY:UP]")) return +5;
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