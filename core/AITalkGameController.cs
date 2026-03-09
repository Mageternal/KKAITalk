using ActionGame;
using KKAITalk.Context;
using KKAITalk.LLM;
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

            var messages = GameContextBuilder.BuildMessages(_currentChara, playerInput);
            AITalkPlugin.Client.SendMessage(
                messages,
                reply => AIDialogueUI.Instance?.ShowReply(reply),
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