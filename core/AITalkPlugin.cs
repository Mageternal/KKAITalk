using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAITalk.Context;
using KKAITalk;
using KKAITalk.LLM;
using KKAITalk.UI;
using KKAPI.MainGame;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace KKAITalk
{
    [BepInPlugin("com.Mageternal.kkaitalik", "KKAITalk", "1.0.0")]
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    public class AITalkPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static LlamaClient Client;
        internal static ConfigEntry<bool> UseThinking;
        internal static AITalkPlugin Instance;
        internal static SaveData.Heroine CurrentHeroine;
        internal static ReplyReceivedDelegate OnReplyReceived;
        private TalkScene _pendingTalkScene;
        private int _pendingEventIndex;
        private string _pendingEventScene = "";
        private bool _isFirstEventInput = false;
        private string _sceneBeforeTalk = "";
        public delegate void ReplyReceivedDelegate();
        private bool _talkSceneWasLoaded = false;
        private bool _eventTriggered = false;




        private void Awake()
        {
            Instance = this;
            var uiObj = new GameObject("AIDialogueUI");
            DontDestroyOnLoad(uiObj);
            uiObj.AddComponent<AIDialogueUI>();

            var testObj = new GameObject("TestRunner");
            DontDestroyOnLoad(testObj);
            testObj.AddComponent<TestRunner>();

            UseThinking = Config.Bind(
                "LLM",           // 分组
                "UseThinking",   // key
                false,           // 默认关闭
                "日常对话是否启用思考模式，开启后回复更准确但速度慢"
            );

            Log = Logger;
            Log.LogInfo("KKAITalk 插件已加载！");
            Client = gameObject.AddComponent<LlamaClient>();

            GameAPI.RegisterExtraBehaviour<AITalkGameController>(KKAPI.KoikatuAPI.GameProcessName);

            // 订阅场景加载事件
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            Log.LogInfo("GameController 注册完成");
        }
        public void TriggerTalkEvent(TalkScene talkScene, int index)
        {
            _eventTriggered = true; // 标记是事件触发的
            _pendingTalkScene = talkScene;
            _pendingEventIndex = index;
            var type = talkScene.GetType();
            var btnEventField = type.GetField("buttonEvent",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var btnEvent = btnEventField.GetValue(talkScene) as UnityEngine.UI.Button;
            if (btnEvent != null)
                btnEvent.onClick.Invoke();

            // 延迟3秒让AI回复显示完，再点击按钮
            Invoke("ClickPendingEventButton", 3f);
        }
        private void ClickPendingEventButton()
        {
            if (_pendingTalkScene == null) return;
            var type = _pendingTalkScene.GetType();
            var field = type.GetField("buttonEventContents",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var buttons = field.GetValue(_pendingTalkScene) as UnityEngine.UI.Button[];
            if (buttons == null || _pendingEventIndex >= buttons.Length) return;

            if (buttons[_pendingEventIndex] != null && buttons[_pendingEventIndex].gameObject.activeInHierarchy)
            {
                buttons[_pendingEventIndex].onClick.Invoke();
                AITalkPlugin.Log.LogInfo($"事件触发成功: index={_pendingEventIndex}");
                // 开始轮询点击Skip
                InvokeRepeating("TryClickSkip", 0.3f, 0.2f);
            }
            else
                AITalkPlugin.Log.LogWarning($"按钮{_pendingEventIndex}未激活");

            _pendingTalkScene = null;
        }

        private void TryClickSkip()
        {
            var msgWindow = FindMsgWindowCanvas();
            if (msgWindow == null || !msgWindow.activeInHierarchy)
            {
                CancelInvoke("TryClickSkip");
                AITalkPlugin.Log.LogInfo("MsgWindow已关闭，停止Skip轮询");
                return;
            }

            var skip = msgWindow.transform.Find("MsgWindow02/Buttons/Under_BG/Under_Right/Skip");
            if (skip != null && skip.gameObject.activeInHierarchy)
            {
                var pointer = new UnityEngine.EventSystems.PointerEventData(
                    UnityEngine.EventSystems.EventSystem.current);
                UnityEngine.EventSystems.ExecuteEvents.Execute(
                    skip.gameObject, pointer,
                    UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                AITalkPlugin.Log.LogInfo("TryClickSkip: 模拟点击Skip");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AITalkPlugin.Log.LogInfo($"场景加载: {scene.name}, mode={mode}");

            if (scene.name == "Talk")
            {
                _talkSceneWasLoaded = true;
                _sceneBeforeTalk = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                Invoke("OnTalkSceneReady", 0.1f);
            }

            if (scene.name == "DiningRoom")
            {
                AITalkPlugin.Log.LogInfo($"DiningRoom加载, _talkSceneWasLoaded={_talkSceneWasLoaded}, _eventTriggered={_eventTriggered}, _sceneBeforeTalk={_sceneBeforeTalk}");
                if (_eventTriggered && _sceneBeforeTalk != "DiningRoom")
                {
                    _talkSceneWasLoaded = false;
                    _eventTriggered = false;
                    _pendingEventScene = "DiningRoom";
                    Invoke("OnEventSceneReady", 1f);
                }
            }
        }
        private GameObject FindMsgWindowCanvas()
        {
            var allCanvas = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (var c in allCanvas)
                if (c.name == "MsgWindowCanvas")
                    return c.gameObject;
            return null;
        }
        private void OnEventSceneReady()
        {
            AITalkPlugin.Log.LogInfo($"OnEventSceneReady执行, CurrentHeroine={CurrentHeroine?.Name ?? "null"}");
            // 尝试隐藏原版对话框
            var msgWindow = FindMsgWindowCanvas();
            if (msgWindow != null)
            {
                msgWindow.SetActive(false);
                AITalkPlugin.Log.LogInfo("隐藏原版对话框");
            }
            else
                AITalkPlugin.Log.LogWarning("MsgWindowCanvas未找到");

            if (CurrentHeroine == null)
            {
                AITalkPlugin.Log.LogWarning("OnEventSceneReady: CurrentHeroine为null");
                return;
            }

            string charaName = CurrentHeroine.Name ?? "";

            AIDialogueUI.Instance?.ShowInputMode(charaName);
            AITalkPlugin.Log.LogInfo("事件场景AI模式开启");

            var heroine = CurrentHeroine;
            var controller = FindObjectOfType<AITalkGameController>();
            if (controller != null)
            {
                AIDialogueUI.Instance.OnUserInput = (input) =>
                {
                    if (_isFirstEventInput)
                    {
                        _isFirstEventInput = false;
                        string sysInput = input + GetFirstInputSuffix(_pendingEventScene);
                        AITalkPlugin.Log.LogInfo($"第一次发言附加指令: {sysInput}");

                        AITalkPlugin.OnReplyReceived = () =>
                        {
                            AITalkPlugin.Log.LogInfo("OnReplyReceived触发，准备调用FinishDiningRoom");
                            if (_pendingEventScene == "DiningRoom")
                                AITalkPlugin.Instance.Invoke("FinishDiningRoom", 0.5f);
                        };
                        AITalkPlugin.Log.LogInfo("OnReplyReceived已赋值");

                        controller.OnTalkStart(heroine, sysInput);
                    }
                    else
                    {
                        controller.OnTalkStart(heroine, input);
                    }
                };
                AITalkPlugin.Log.LogInfo("事件场景输入事件订阅完成");

                string autoInput = GetAutoInput(_pendingEventScene);
                if (!string.IsNullOrEmpty(autoInput))
                {
                    _isFirstEventInput = true;
                    AITalkPlugin.Log.LogInfo($"AI主动发言触发: {autoInput}");
                    controller.OnTalkStart(heroine, autoInput);
                }
            }


        }
        private void FinishDiningRoom()
        {
            AITalkPlugin.Log.LogInfo("FinishDiningRoom执行");
            var msgWindow = FindMsgWindowCanvas();
            AITalkPlugin.Log.LogInfo($"FindMsgWindowCanvas结果: {msgWindow?.name ?? "null"}");
            if (msgWindow != null)
            {
                msgWindow.SetActive(true);
                AITalkPlugin.Log.LogInfo($"SetActive后active: {msgWindow.activeInHierarchy}");
                InvokeRepeating("TryClickSkip", 0f, 0.3f);
            }
            Invoke("FinishDiningRoomFinal", 3f);
        }

        private void FinishDiningRoomFinal()
        {
            AITalkPlugin.Log.LogInfo("FinishDiningRoomFinal执行");
            CancelInvoke("TryClickSkip");
            var msgWindow = FindMsgWindowCanvas();
            AITalkPlugin.Log.LogInfo($"FinishDiningRoomFinal FindMsgWindowCanvas: {msgWindow?.name ?? "null"}, active={msgWindow?.activeInHierarchy}");
            if (msgWindow != null)
            {
                msgWindow.SetActive(true);
                InvokeRepeating("TryClickSkip", 0f, 0.3f);
                AITalkPlugin.Log.LogInfo("开始快进脱离场景");
            }
            AIDialogueUI.Instance?.Hide();
        }
        private string GetAutoInput(string sceneName)
        {
            switch (sceneName)
            {
                case "DiningRoom": return "[system]:[你们正在一起吃午饭，请用一句话描述此刻的心情或说一句开场白。]";
                default: return "";
            }
        }

        private string GetFirstInputSuffix(string sceneName)
        {
            switch (sceneName)
            {
                case "DiningRoom": return " [system]:[这是午饭的最后一句对话，请用一句话结束这顿饭，表达吃完了或者该离开了。]";
                default: return "";
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "Talk")
            {
                AIDialogueUI.Instance?.Hide();

                if (_sceneBeforeTalk == "DiningRoom" && _eventTriggered)
                {
                    _eventTriggered = false;
                    AITalkPlugin.Log.LogInfo("Talk结束，直接回到DiningRoom");
                    _pendingEventScene = "DiningRoom";
                    Invoke("OnEventSceneReady", 1f);
                }
                else
                {
                    _talkSceneWasLoaded = false;
                    // 不重置_eventTriggered，让OnSceneLoaded里的DiningRoom判断能用到它
                }
            }
        }

        private void OnTalkSceneReady()
        {
            _elapsed = 0f;
            InvokeRepeating("WaitForMsgWindow", 0f, 0.1f);
        }

        private float _elapsed = 0f;
        private void WaitForMsgWindow()
        {
            _elapsed += 0.1f;
            if (_elapsed > 5f)
            {
                CancelInvoke("WaitForMsgWindow");
                _elapsed = 0f;
                return;
            }

            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in all)
            {
                if (obj.name == "MsgWindowCanvas" && obj.activeInHierarchy)
                {
                    CancelInvoke("WaitForMsgWindow");
                    _elapsed = 0f;
                    OnMsgWindowReady();
                    return;
                }
            }
        }
        private void OnMsgWindowReady()
        {
            var talkScene = FindObjectOfType<TalkScene>();
            if (talkScene == null) return;

            // 延迟后开始轮询点击Skip
            Invoke("StartSkipping", 1f);

            string charaName = talkScene.targetHeroine?.Name ?? "";
            AIDialogueUI.Instance?.ShowInputMode(charaName);
            AITalkPlugin.Log.LogInfo("AI模式自动开启");

            var controller = FindObjectOfType<AITalkGameController>();
            AITalkPlugin.Log.LogInfo($"controller是否为null: {controller == null}");

            if (controller != null && talkScene.targetHeroine != null)
            {
                var heroine = talkScene.targetHeroine;
                CurrentHeroine = heroine;
                AIDialogueUI.Instance.OnUserInput = (input) =>
                {
                    controller.OnTalkStart(heroine, input);
                };
                AITalkPlugin.Log.LogInfo("输入事件订阅完成，等待玩家输入");
            }
            else
                AITalkPlugin.Log.LogWarning($"controller={controller == null}, heroine={talkScene.targetHeroine == null}");
        }

        private void StartSkipping()
        {
            InvokeRepeating("TryClickSkip", 0f, 0.3f);
        }

        
        private void PrintChildren(Transform t, int depth)
        {
            string indent = new string('-', depth * 2);
            AITalkPlugin.Log.LogInfo(indent + t.name + " | active: " + t.gameObject.activeInHierarchy);
            foreach (Transform child in t)
                PrintChildren(child, depth + 1);
        }
        private string GetFullPath(GameObject obj)
        {
            string path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private void SimulateClick()
        {
            // Unity 5.x 没有Input.SimulateTouch，用Input模拟不了
            // 需要找到按钮的Button组件直接调用onClick
        }
    }
}