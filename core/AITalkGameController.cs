using ActionGame;
using KKAITalk.Context;
using KKAITalk.LLM;
using KKAITalk.Memory;
using KKAITalk.UI;
using KKAPI.MainGame;
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
                    AIDialogueUI.Instance?.ShowReply(reply);
                    AITalkPlugin.Log.LogInfo("[" + _currentChara.Name + "] " + reply);

                    // 保存这轮对话到历史
                    history.Add(new ChatMessage { role = "user", content = playerInput });
                    history.Add(new ChatMessage { role = "assistant", content = reply });
                    MemoryManager.SaveHistory(saveId, _currentChara.CharaId, history);
                },
                err => AITalkPlugin.Log.LogError("请求失败: " + err),
                ThinkingMode.Normal
            );
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