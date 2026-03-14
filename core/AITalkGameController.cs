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
                    // 解析anger变化
                    if (heroine.isAnger)
                    {
                        int delta = ParseAngerDelta(reply);
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
                            AITalkPlugin.Log.LogInfo($"愤怒加剧，当前: {heroine.anger}");
                        }
                        else
                        {
                            AITalkPlugin.Log.LogInfo($"愤怒变化后: {heroine.anger}");
                        }
                    }

                    // 去掉标记再显示
                    string cleanReply = System.Text.RegularExpressions.Regex.Replace(reply, @"\[ANGER:[^\]]+\]", "").Trim();
                    AIDialogueUI.Instance?.ShowReply(cleanReply);

                    // 保存历史也用cleanReply
                    history.Add(new ChatMessage { role = "user", content = playerInput });
                    history.Add(new ChatMessage { role = "assistant", content = cleanReply });
                    MemoryManager.SaveHistory(saveId, _currentChara.CharaId, history);
                },
                err => AITalkPlugin.Log.LogError("请求失败: " + err)
            );
        }

        private int ParseAngerDelta(string reply)
        {
            if (reply.Contains("[APOLOGY:WORSE]")) return +30;
            if (reply.Contains("[APOLOGY:NONE]")) return 0;
            if (reply.Contains("[APOLOGY:WEAK]")) return -10;
            if (reply.Contains("[APOLOGY:SINCERE]")) return -30;
            if (reply.Contains("[APOLOGY:FORGIVEN]")) return -100;
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