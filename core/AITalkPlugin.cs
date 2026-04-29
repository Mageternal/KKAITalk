using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAITalk;
using KKAITalk.Context;
using KKAITalk.LLM;
using KKAITalk.Memory;
using KKAITalk.UI;
using KKAPI.MainGame;
using KK_Pregnancy;
using System;
using System.Reflection;
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
        internal string _pendingEventScene = "";
        private TalkScene _pendingTalkScene;
        private int _pendingEventIndex;
        private bool _isFirstEventInput = false;
        private string _sceneBeforeTalk = "";
        public delegate void ReplyReceivedDelegate();
        private bool _talkSceneWasLoaded = false;
        private bool _eventTriggered = false;
        private HSceneProc _hSceneProc;
        private object _hFlags;
        private string _lastAnimState = "";
        private float _phase3StartTime = 0f; // 阶段3（IN_Loop/OUT_Loop）开始时间
        private string _prevAnimState = ""; // 上一帧的动画状态（边沿检测）
        private string _lastLoopType = ""; // 记录进入的是哪种Loop（K/M/A/S）




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
            _pendingTalkScene = talkScene;
            _pendingEventIndex = index;
            var type = talkScene.GetType();
            var btnEventField = type.GetField("buttonEvent",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var btnEvent = btnEventField.GetValue(talkScene) as UnityEngine.UI.Button;
            if (btnEvent != null)
                btnEvent.onClick.Invoke();

            // 先展开列表检查按钮是否激活
            Invoke("CheckAndClickEventButton", 0.3f);
        }

        private void CheckAndClickEventButton()
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
                _eventTriggered = true; // 确认按钮可用才设置
                Invoke("ClickPendingEventButton", 3f);
                AITalkPlugin.Log.LogInfo($"按钮{_pendingEventIndex}可用，延迟3秒点击");
            }
            else
            {
                AITalkPlugin.Log.LogWarning($"按钮{_pendingEventIndex}未激活，事件取消");
                _pendingTalkScene = null;
            }
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
                InvokeRepeating("TryClickSkip", 0.3f, 0.2f);
            }
            else
                AITalkPlugin.Log.LogWarning($"按钮{_pendingEventIndex}点击时已失效");

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
            //吃饭场景
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
            if (scene.name == "Rooftop" && _eventTriggered && _sceneBeforeTalk != "Rooftop")
            {
                _eventTriggered = false;
                _pendingEventScene = "Rooftop";
                Invoke("OnEventSceneReady", 1f);
            }
            // 学习场景
            if ((scene.name == "LibraryRoom" || scene.name == "1-1" ||
                 scene.name == "2-1" || scene.name == "2-2" || scene.name == "3-1")
                && _eventTriggered && _sceneBeforeTalk != scene.name)
            {
                _eventTriggered = false;
                _pendingEventScene = "Study";
                Invoke("OnEventSceneReady", 1f);
            }
            // 运动场景
            if (scene.name == "Ground" && _eventTriggered && _sceneBeforeTalk != "Ground")
            {
                _eventTriggered = false;
                _pendingEventScene = "Exercise";
                Invoke("OnEventSceneReady", 1f);
            }
            //社团活动室
            if (scene.name == "StaffRoom")
            {
                AITalkPlugin.Log.LogInfo($"StaffRoom加载, _eventTriggered={_eventTriggered}, _sceneBeforeTalk={_sceneBeforeTalk}");
                if (_eventTriggered && _sceneBeforeTalk != "StaffRoom")
                {
                    _eventTriggered = false;
                    _pendingEventScene = "Club";
                    Invoke("OnEventSceneReady", 1f);
                }
            }
            // 回家场景（MyRoom是中间过渡场景，忽略；Courtyard才是实际回家场景）
            if (scene.name == "Courtyard")
            {
                AITalkPlugin.Log.LogInfo($"Courtyard加载, _eventTriggered={_eventTriggered}, _sceneBeforeTalk={_sceneBeforeTalk}");
                if (_eventTriggered && _sceneBeforeTalk != "Courtyard")
                {
                    _eventTriggered = false;
                    _pendingEventScene = "GoHome";
                    Invoke("OnEventSceneReady", 1f);
                }
            }
            if (scene.name == "H")
            {
                // 延迟一下等场景加载完再关闭
                Invoke("CloseSubtitles", 1f);
            }
        }
        private void CloseSubtitles()
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "KK_Subtitles_Caption")
                {
                    obj.SetActive(false);
                    AITalkPlugin.Log.LogInfo("关闭字幕");
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
            AIDialogueUI.Instance?.SetPanelHeight(200f); // 恢复默认对话框高度
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
                            AITalkPlugin.Log.LogInfo("OnReplyReceived触发，准备调用FinishEventScene");
                            Invoke("FinishEventScene", 3f);//事件最后阶段等待时间。
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

                _isFirstEventInput = false; // 先重置，防止上一个场景的残留状态
                string autoInput = GetAutoInput(_pendingEventScene);
                if (!string.IsNullOrEmpty(autoInput))
                {
                    _isFirstEventInput = true;
                    AITalkPlugin.Log.LogInfo($"AI主动发言触发: {autoInput}");
                    controller.OnTalkStart(heroine, autoInput);
                }
            }


        }
        private void TryClickSkipSoft()
        {
            var msgWindow = FindMsgWindowCanvas();
            if (msgWindow == null) return;

            if (!msgWindow.activeInHierarchy)
                msgWindow.SetActive(true);

            var skip = msgWindow.transform.Find("MsgWindow02/Buttons/Under_BG/Under_Right/Skip");
            if (skip != null && skip.gameObject.activeInHierarchy)
            {
                var pointer = new UnityEngine.EventSystems.PointerEventData(
                    UnityEngine.EventSystems.EventSystem.current);
                UnityEngine.EventSystems.ExecuteEvents.Execute(
                    skip.gameObject, pointer,
                    UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                AITalkPlugin.Log.LogInfo("TryClickSkipSoft: 模拟点击Skip");
            }
        }
        private void FinishEventScene()
        {
            AITalkPlugin.Log.LogInfo("FinishEventScene执行");
            var msgWindow = FindMsgWindowCanvas();
            if (msgWindow != null)
            {
                msgWindow.SetActive(true);
                AITalkPlugin.Log.LogInfo("显示原对话框，开始快进");
                InvokeRepeating("TryClickSkipSoft", 0f, 0.3f);
            }
            Invoke("FinishEventSceneFinal", 3f);
        }

        private void FinishEventSceneFinal()
        {
            AITalkPlugin.Log.LogInfo("FinishEventSceneFinal执行");
            CancelInvoke("TryClickSkipSoft");
            AIDialogueUI.Instance?.Hide();
        }
        private string GetAutoInput(string sceneName)
        {
            switch (sceneName)
            {
                case "DiningRoom": return "[situation]:[你们正在一起吃午饭，请用一句话说一句符合当前场景的开场白。]";
                case "Study": return "[situation]:[你们正在一起学习，话题围绕学习内容展开，请说一句符合当前场景的开场白。]";
                case "Exercise": return "[situation]:[你们正在一起运动，话题围绕运动感受展开，请说一句符合当前场景的开场白。]";
                case "Club": return "[situation]:[你们正在进行恋爱练习，请说一句符合当前场景的开场白。]";
                case "GoHome": return "[situation]:[放学后你们正一起走在回家的路上，请用一句话说一句符合当前场景的开场白。]";
                default: return "";
            }
        }

        private string GetFirstInputSuffix(string sceneName)
        {
            switch (sceneName)
            {
                case "DiningRoom": return " [situation]:[午饭快结束了，请用一句话自然地结束这次用餐。]";
                case "Study": return " [situation]:[学习时间快结束了，请用一句话自然地结束这次学习。]";
                case "Exercise": return " [situation]:[运动快结束了，请用一句话自然地结束这次运动。]";
                case "Club": return " [situation]:[社团活动快结束了，请用一句话自然地结束这次恋爱练习。]";
                case "GoHome": return " [situation]:[快要走到家了，请用一句话自然地道别结束这次回家路上的对话。]";
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
                }

                if (_sceneBeforeTalk == "LibraryRoom" || _sceneBeforeTalk == "1-1" ||
                    _sceneBeforeTalk == "2-1" || _sceneBeforeTalk == "2-2" ||
                    _sceneBeforeTalk == "3-1")
                {
                    if (_eventTriggered)
                    {
                        _eventTriggered = false;
                        _pendingEventScene = "Study";
                        Invoke("OnEventSceneReady", 1f);
                    }
                }
                else if (_sceneBeforeTalk == "Ground" && _eventTriggered && _pendingEventScene != "GoHome")
                {
                    _eventTriggered = false;
                    _pendingEventScene = "Exercise";
                    Invoke("OnEventSceneReady", 1f);
                }
                else if (_sceneBeforeTalk == "Rooftop" && _eventTriggered)
                {
                    _eventTriggered = false;
                    _pendingEventScene = "Rooftop";
                    Invoke("OnEventSceneReady", 1f);
                }
                else if (_sceneBeforeTalk == "StaffRoom" && _eventTriggered)
                {
                    _eventTriggered = false;
                    _pendingEventScene = "Club";
                    Invoke("OnEventSceneReady", 1f);
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
            // 重置事件场景状态
            _isFirstEventInput = false;
            _pendingEventScene = "";

            // 关闭字幕
            CloseSubtitles();

            var talkScene = FindObjectOfType<TalkScene>();
            if (talkScene == null) return;

            // 延迟后开始轮询点击Skip
            Invoke("StartSkipping", 1f);
            
            string charaName = talkScene.targetHeroine?.Name ?? "";
            AIDialogueUI.Instance?.ShowInputMode(charaName);
            AIDialogueUI.Instance?.SetPanelHeight(200f); // 恢复默认对话框高度
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
        private static object SafeGetFieldValue(object obj, string fieldName)
        {
            if (obj == null) return null;

            try
            {
                var type = obj.GetType();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var f in fields)
                {
                    if (f.Name == fieldName)
                        return f.GetValue(obj);
                }
                return null;
            }
            catch (MissingMethodException ex)
            {
                AITalkPlugin.Log.LogWarning($"SafeGetFieldValue: {ex.Message}");
                return null;
            }
        }
        private static object SafeGetFieldOrPropertyValue(object obj, string name)
        {
            if (obj == null) return null;

            try
            {
                var type = obj.GetType();

                // 先尝试属性
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var p in properties)
                {
                    if (p.Name == name)
                        return p.GetValue(obj, null);
                }

                // 再尝试字段
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (f.Name == name)
                        return f.GetValue(obj);
                }

                return null;
            }
            catch (MissingMethodException ex)
            {
                AITalkPlugin.Log.LogWarning($"SafeGetFieldOrPropertyValue({name}): {ex.Message}");
                return null;
            }
        }
        public void OnHSceneStart(HSceneProc hSceneProc)
        {
            _hSceneProc = hSceneProc;
            _hFlags = SafeGetFieldValue(hSceneProc, "flags");
            _lastAnimState = "";
            _lastEMode = ""; // 重置EMode记录

            CloseSubtitles();

            // 显示AI对话框
            if (CurrentHeroine != null)
            {
                AIDialogueUI.Instance?.ShowInputMode(CurrentHeroine.Name ?? "");
                AIDialogueUI.Instance?.SetPanelHeight(120f); // H场景使用较小的对话框

                // 直接读取EMode并触发开场白
                var emode = SafeGetFieldOrPropertyValue(_hFlags, "mode");
                string currentMode = emode?.ToString() ?? "";
                _isFirstHLoad = true;

                if (!string.IsNullOrEmpty(currentMode))
                {
                    AITalkPlugin.Log.LogInfo($"H场景开始，EMode={currentMode}");
                    TriggerHSceneIntro(currentMode);
                    _lastEMode = currentMode;
                    _isFirstHLoad = false;
                }
            }

            // 订阅玩家输入事件
            if (CurrentHeroine != null)
            {
                AIDialogueUI.Instance.OnUserInput = (input) =>
                {
                    TriggerHSceneTalk(CurrentHeroine, input);
                };
                AITalkPlugin.Log.LogInfo("H场景输入事件订阅完成");
            }

            // 启动状态监听
            InvokeRepeating("MonitorHScene", 1f, 0.5f);
            AITalkPlugin.Log.LogInfo("H场景AI模式开启");
        }

        public void OnHSceneEnd()
        {
            CancelInvoke("MonitorHScene");
            AIDialogueUI.Instance?.Hide();
            _hSceneProc = null;
            _hFlags = null;
            _lastEMode = ""; // 重置EMode记录
            AITalkPlugin.Log.LogInfo("H场景AI模式关闭");
        }

        private string _pendingHAction = ""; // 记录待触发AI的动作类型
        private bool _isASeries = false; // 是否是A系列（A_Idle后还需要Idle再触发一次）
        private bool _isSSeries = false; // 是否是S系列（S_Idle后还需要Idle再触发一次）
        private string _lastEMode = ""; // 记录上一次的EMode，用于检测阶段切换
        private bool _isFirstHLoad = true; // H场景首次加载标志

        private void MonitorHScene()
        {
            if (_hFlags == null) return;

            string animState = SafeGetFieldOrPropertyValue(_hFlags, "nowAnimStateName")?.ToString() ?? "";

            // 边沿检测：记录上一帧状态
            string cur = animState;
            string prev = _prevAnimState;
            _prevAnimState = cur;

            if (cur == _lastAnimState) return;

            AITalkPlugin.Log.LogInfo($"动作状态变化: {_lastAnimState} -> {cur}");

            // 读取当前EMode
            var emode = SafeGetFieldOrPropertyValue(_hFlags, "mode");
            string currentMode = emode?.ToString() ?? "";

            // H场景首次加载或EMode变化时，触发开场白（优先于动作状态检查）
            if (_isFirstHLoad || (!string.IsNullOrEmpty(currentMode) && currentMode != _lastEMode))
            {
                AITalkPlugin.Log.LogInfo($"触发开场白: {_lastEMode} -> {currentMode}（首次加载:{_isFirstHLoad}）");
                TriggerHSceneIntro(currentMode);
                _lastEMode = currentMode;
                _isFirstHLoad = false;
            }

            // houshi 模式单独处理
            if (currentMode == "houshi")
            {
                HandleHoushiMode(cur, prev);
            }
            // aibu/sonyu 模式
            else
            {
                HandleNormalMode(cur, prev, currentMode);
            }

            _lastAnimState = cur;
        }

        private void TriggerHSceneIntro(string mode)
        {
            if (CurrentHeroine == null) return;

            string prompt = "";
            switch (mode)
            {
                case "aibu": prompt = "[system]:[H场景开始爱抚阶段，用符合角色性格与情形的话说一句话。]"; // TODO: aibu阶段开始
                    break;
                case "houshi": prompt = "[system]:[H场景开始侍奉阶段，用符合角色性格与情形的话说一句话。]"; // TODO: houshi阶段开始
                    break;
                case "sonyu": prompt = "[system]:[H场景开始正式H阶段，用符合角色性格与情形的话说一句话。]"; // TODO: sonyu阶段开始
                    break;
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                TriggerHSceneTalk(CurrentHeroine, prompt);
            }
        }

        private void HandleHoushiMode(string cur, string prev)
        {
            AITalkPlugin.Log.LogInfo($"houshi模式: {cur}");

            // WLoop、SLoop：平缓发言（立即触发）
            if (cur == "WLoop" || cur == "SLoop")
            {
                if (CurrentHeroine != null)
                {
                    TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("houshi_calm", "houshi"));
                }
            }
            // OLoop：激烈发言（立即触发）
            else if (cur == "OLoop")
            {
                if (CurrentHeroine != null)
                {
                    TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("houshi_intense", "houshi"));
                }
            }
            // Vomit_A：呕吐结束（立即触发）
            else if (cur == "Vomit_A")
            {
                if (CurrentHeroine != null)
                {
                    TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("Vomit", "houshi"));
                }
            }
            // Drink_A：吞精结束（立即触发）
            else if (cur == "Drink_A")
            {
                if (CurrentHeroine != null)
                {
                    TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("Drink", "houshi"));
                }
            }
            // OUT_A：射精结束（立即触发）
            else if (cur == "OUT_A")
            {
                if (CurrentHeroine != null)
                {
                    TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("OUT", "houshi"));
                }
            }
        }

        private void HandleNormalMode(string cur, string prev, string currentMode)
        {
            // aibu 模式：检测进入 K_Loop
            if (cur.Contains("K_Loop") && !prev.Contains("K_Loop"))
            {
                _lastLoopType = "K";
                AITalkPlugin.Log.LogInfo("进入K_Loop，记录但不触发");
                return;
            }

            // aibu 模式：检测脱离 K_Loop，触发 K 对话
            if (!cur.Contains("K_Loop") && prev.Contains("K_Loop"))
            {
                AITalkPlugin.Log.LogInfo("脱离K_Loop，触发K");
                if (CurrentHeroine != null)
                {
                    TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("K", currentMode));
                }
                // 不要立即清空 _lastLoopType，让 M/A_Idle 能检查到
                return;
            }

            // M_Idle 触发，但要排除从 K_Loop 过来的情况
            if (cur == "M_Idle" && prev != "M_Idle")
            {
                if (_lastLoopType != "K") // K_Loop 打断的 M 不触发
                {
                    AITalkPlugin.Log.LogInfo("进入M_Idle，触发M");
                    if (CurrentHeroine != null)
                    {
                        TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("M", currentMode));
                    }
                }
                else
                {
                    AITalkPlugin.Log.LogInfo("进入M_Idle，但被K_Loop打断，跳过");
                }
                return;
            }

            // A_Idle 触发，但要排除从 K_Loop 过来的情况
            if (cur == "A_Idle" && prev != "A_Idle")
            {
                if (_lastLoopType != "K")
                {
                    AITalkPlugin.Log.LogInfo("进入A_Idle，触发A_start");
                    if (CurrentHeroine != null)
                    {
                        TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("A_start", currentMode));
                    }
                    _pendingHAction = "A";
                }
                else
                {
                    AITalkPlugin.Log.LogInfo("进入A_Idle，但被K_Loop打断，跳过");
                }
                return;
            }

            // S_Idle 触发
            if (cur == "S_Idle" && prev != "S_Idle")
            {
                AITalkPlugin.Log.LogInfo("进入S_Idle，触发S_start");
                if (CurrentHeroine != null)
                {
                    TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("S_start", currentMode));
                }
                _pendingHAction = "S";
                return;
            }

            // 回到 Idle 时清空 lastLoopType
            if (cur == "Idle" && prev != "Idle")
            {
                AITalkPlugin.Log.LogInfo("回到Idle，清空lastLoopType");
                _lastLoopType = "";
            }

            // ========== sonyu 模式：阶段2-5 的 Loop 处理 ==========
            if (currentMode == "sonyu" && cur.Contains("_Loop"))
            {
                int currentPhase = Context.GameContextBuilder.GetHAnimPhase(cur);

                if (currentPhase == 3)
                {
                    // 阶段3：记录开始时间，延迟10秒
                    if (_phase3StartTime == 0f)
                    {
                        _phase3StartTime = Time.time;
                        AITalkPlugin.Log.LogInfo($"进入阶段3: {cur}，开始10秒延迟");
                    }

                    // 在10秒延迟期间，跳过触发
                    if (Time.time - _phase3StartTime < 10f)
                    {
                        AITalkPlugin.Log.LogInfo($"阶段3延迟中，剩余{10f - (Time.time - _phase3StartTime):F1}秒");
                        return;
                    }
                    else
                    {
                        // 10秒延迟结束，继续检测阶段4
                        AITalkPlugin.Log.LogInfo($"阶段3延迟结束，开始检测阶段4");
                    }
                }
                else if (currentPhase == 2)
                {
                    // 阶段2：sonyu 模式的 W/S/O Loop
                    string prefix = cur.Split('_')[0];
                    AITalkPlugin.Log.LogInfo($"sonyu阶段2: {prefix}，等待Idle触发");
                    _pendingHAction = prefix;
                    _phase3StartTime = 0f;
                }
            }

            // ========== sonyu 模式：阶段4-5 的 IN_A/OUT_A/Idle 处理 ==========
            if (currentMode == "sonyu")
            {
                // 阶段4：IN_A/OUT_A（高潮结束）
                if (cur.EndsWith("_IN_A") || cur.EndsWith("_OUT_A") ||
                    cur == "IN_A" || cur == "OUT_A")
                {
                    AITalkPlugin.Log.LogInfo($"sonyu阶段4触发: {cur}");
                    if (CurrentHeroine != null)
                    {
                        TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput(cur, currentMode));
                    }
                    _phase3StartTime = 0f;
                    return;
                }

                // Idle状态时触发（sonyu 模式）
                if (cur == "Idle")
                {
                    if (!string.IsNullOrEmpty(_pendingHAction))
                    {
                        string action = _pendingHAction;
                        AITalkPlugin.Log.LogInfo($"sonyu Idle触发: {action}");
                        if (CurrentHeroine != null)
                        {
                            TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput(action, currentMode));
                        }
                    }

                    // A系列在最后Idle再触发一次
                    if (_isASeries && CurrentHeroine != null)
                    {
                        TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("A_stop", currentMode));
                    }
                    // S系列在最后Idle再触发一次
                    if (_isSSeries && CurrentHeroine != null)
                    {
                        TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("S_stop", currentMode));
                    }

                    // 重置状态
                    _pendingHAction = "";
                    _isASeries = false;
                    _isSSeries = false;
                    _phase3StartTime = 0f;
                }

                // Orgasm高潮：Orgasm_A -> 立即触发
                if (cur == "Orgasm_A")
                {
                    AITalkPlugin.Log.LogInfo("Orgasm高潮触发");
                    if (CurrentHeroine != null)
                    {
                        TriggerHSceneTalk(CurrentHeroine, GetHLoopCompletedInput("Orgasm", currentMode));
                    }
                    _pendingHAction = "";
                    _isASeries = false;
                    _isSSeries = false;
                    _phase3StartTime = 0f;
                }
            }
        }

        private string GetHLoopCompletedInput(string actionType, string currentMode = "")
        {
            // houshi 模式（固定）
            if (currentMode == "houshi")
            {
                switch (actionType)
                {
                    case "houshi_calm": return "[system]:[你在温柔按摩男主，用符合角色性格与情形的话说一句话。]"; // TODO: WLoop/SLoop
                    case "houshi_intense": return "[system]:[你在激烈按摩男主，用符合角色性格与情形的话说一句话。]"; // TODO: OLoop
                    case "Vomit": return "[system]:[你把牛奶吐出了，用符合角色性格与情形的话说一句话。]"; // TODO: Vomit_A
                    case "Drink": return "[system]:[你把牛奶喝了，用符合角色性格与情形的话说一句话。]"; // TODO: Drink_A
                    case "OUT": return "[system]:[男主在你面前撒水，用符合角色性格与情形的话说一句话。]"; // TODO: OUT_A
                    default: return "";
                }
            }

            // ========== aibu 模式：K/M/A/S 系列 ==========
            if (currentMode == "aibu")
            {
                switch (actionType)
                {
                    case "K": return "[system]:[刚才完成了接吻，用符合角色性格与情形的话说一句话。]"; // TODO: K_Loop 后的 Idle
                    case "M": return "[system]:[开始被上身按摩，用符合角色性格与情形的话说一句话。]"; // TODO: M_Idle
                    case "A_start": return "[system]:[开始被下身按摩，用符合角色性格与情形的话说一句话。]"; // TODO: A_Idle
                    case "A_stop": return "[system]:[刚才完成了下身按摩，用符合角色性格与情形的话说一句话。]"; // TODO: A_Idle 后的 Idle
                    case "S_start": return "[system]:[开始被打屁股，用符合角色性格与情形的话说一句话。]"; // TODO: S_Idle
                    case "S_stop": return "[system]:[刚才完成了打屁股，用符合角色性格与情形的话说一句话。]"; // TODO: S_Idle 后的 Idle
                    case "Orgasm": return "[system]:[你刚刚高潮完了，用符合角色性格与情形的话说一句话。]"; // TODO: Orgasm_A
                    default: return "";
                }
            }

            // ========== sonyu 模式：5 阶段流程 ==========
            if (currentMode == "sonyu")
            {
                switch (actionType)
                {
                    // 阶段1: InsertIdle
                    case "InsertIdle": return "[system]:[男主与你融合了，用符合角色性格与情形的话说一句话。]"; // InsertIdle值
                    case "A_InsertIdle": return "[system]:[男主与你屁股融合了，用符合角色性格与情形的话说一句话。]"; // A_InsertIdle值

                    // 阶段2: WLoop/SLoop/OLoop
                    case "WLoop": return "[system]:[你在缓慢温柔地感受男主，用符合角色性格与情形的话说一句话。]"; // WLoop值
                    case "SLoop": return "[system]:[你在快速感受男主，用符合角色性格与情形的话说一句话。]"; // SLoop值
                    case "OLoop": return "[system]:[你在疯狂极速感受男主，用符合角色性格与情形的话说一句话。] "; // OLoop值
                    case "A_WLoop": return "[system]:[你在用屁股缓慢温柔地感受男主，用符合角色性格与情形的话说一句话。]"; // A_WLoop值
                    case "A_SLoop": return "[system]:[你在用屁股快速感受男主，用符合角色性格与情形的话说一句话。]"; // A_SLoop值
                    case "A_OLoop": return "[system]:[你在用屁股疯狂极速感受男主，用符合角色性格与情形的话说一句话。]"; // A_OLoop值

                    // 阶段3: IN_Loop/OUT_Loop（带S型前缀）
                    case "WS_IN_Loop": return "[system]:[你们在缓慢温柔地交合下一起高潮了，用符合角色性格与情形的话说一句话。]"; // WS_IN_Loop值
                    case "SF_IN_Loop": return "[system]:[你在交合时独自高潮了，用符合角色性格与情形的话说一句话。]"; // SF_IN_Loop值
                    case "SS_IN_Loop": return "[system]:[你们在快速交合下高潮一起高潮了，用符合角色性格与情形的话说一句话。]"; // SS_IN_Loop值
                    case "M_IN_Loop": return "[system]:[你在交合时男主独自高潮了，用符合角色性格与情形的话说一句话。]"; // M_IN_Loop值
                    case "M_OUT_Loop": return "[system]:[你在交合时男主高潮了，还撒了你一身水，用符合角色性格与情形的话说一句话。]"; // M_OUT_Loop值
                    case "A_WS_IN_Loop": return "[system]:[你在用自己的屁股缓慢温柔地交合下一起高潮了，用符合角色性格与情形的话说一句话。]"; // A_WS_IN_Loop值
                    case "A_SF_IN_Loop": return "[system]:[你在用自己的屁股交合时独自高潮了，用符合角色性格与情形的话说一句话。]"; // A_SF_IN_Loop值
                    case "A_SS_IN_Loop": return "[system]:[你在用自己的屁股快速交合下高潮一起高潮了，用符合角色性格与情形的话说一句话。]"; // A_SS_IN_Loop值
                    case "A_M_IN_Loop": return "[system]:[你在用自己的屁股交合时男主独自高潮了，用符合角色性格与情形的话说一句话。]"; // A_M_IN_Loop值
                    case "A_M_OUT_Loop": return "[system]:[你在用自己的屁股交合时男主高潮了，还撒了你一身水，用符合角色性格与情形的话说一句话。]"; // A_WS_OUT_Loop值

                    // 阶段4: IN_A/OUT_A
                    case "IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // IN_A值
                    case "OUT_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // OUT_A值
                    case "WS_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // WS_IN_A值
                    case "SF_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // SF_IN_A值
                    case "SS_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // SS_IN_A值
                    case "M_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // M_IN_A值
                    case "WS_OUT_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // WS_OUT_A值
                    case "SF_OUT_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // SF_OUT_A值
                    case "SS_OUT_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // SS_OUT_A值
                    case "A_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_IN_A值
                    case "A_OUT_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_OUT_A值
                    case "A_WS_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_WS_IN_A值
                    case "A_SF_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_SF_IN_A值
                    case "A_SS_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_SS_IN_A值
                    case "A_M_IN_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_M_IN_A值
                    case "A_WS_OUT_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_WS_OUT_A值
                    case "A_SF_OUT_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_SF_OUT_A值
                    case "A_SS_OUT_A": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_SS_OUT_A值

                    // 阶段5: Drop/Idle
                    case "Drop": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // Drop值
                    case "Idle": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // Idle值
                    case "A_Drop": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_Drop值
                    case "A_Idle": return "[system]:[，用符合角色性格与情形的话说一句话。]"; // A_Idle值

                    default: return "";
                }
            }

            return "";
        }

        private void TriggerHSceneTalk(SaveData.Heroine heroine, string playerInput)
        {
            if (heroine == null) return;

            // 构建H场景的CharacterContext
            var chara = CharacterDataReader.ReadFromHeroine(heroine);
            if (chara == null) return;

            // 读取H场景动态数据
            chara.IsInHScene = true;
            chara.NowAnimStateName = _lastAnimState;
            chara.IsAnalPlay = (bool?)SafeGetFieldOrPropertyValue(_hFlags, "isAnalPlay") ?? false;

            var gaugeFemale = SafeGetFieldOrPropertyValue(_hFlags, "gaugeFemale");
            if (gaugeFemale != null && gaugeFemale is float)
                chara.GaugeFemale = (float)gaugeFemale;
            else
                chara.GaugeFemale = 0f;

            // 读取H场景功能点状态 (aibu/houshi/sonyu)
            var emode = SafeGetFieldOrPropertyValue(_hFlags, "mode");
            if (emode != null)
                chara.HMode = emode.ToString();
            else
                chara.HMode = "Unknown";

            // 读取当前姿势名称 (nowAnimationInfo.nameAnimation)
            var nowAnimInfo = SafeGetFieldOrPropertyValue(_hFlags, "nowAnimationInfo");
            if (nowAnimInfo != null)
            {
                var animName = SafeGetFieldOrPropertyValue(nowAnimInfo, "nameAnimation");
                if (animName != null)
                    chara.AnimationName = animName.ToString();
                else
                    chara.AnimationName = "Unknown";
            }
            else
                chara.AnimationName = "Unknown";

            // 读取怀孕/安全期状态（KK_Pregnancy）
            try
            {
                var status = KK_Pregnancy.PregnancyDataUtils.GetCharaStatus(heroine);
                chara.IsSafeDay = (status == KK_Pregnancy.HeroineStatus.Safe);
                chara.IsRiskyDay = (status == KK_Pregnancy.HeroineStatus.Risky);
                chara.IsPregnant = (status == KK_Pregnancy.HeroineStatus.Pregnant);

                // 读取怀孕周期
                var pregData = KK_Pregnancy.PregnancyDataUtils.GetPregnancyData(heroine);
                chara.PregnancyWeek = (pregData != null) ? pregData.Week : 0;

                AITalkPlugin.Log.LogInfo($"怀孕状态: Safe={chara.IsSafeDay}, Risky={chara.IsRiskyDay}, Pregnant={chara.IsPregnant}, Week={chara.PregnancyWeek}");
            }
            catch
            {
                chara.IsSafeDay = false;
                chara.IsRiskyDay = false;
                chara.IsPregnant = false;
                chara.PregnancyWeek = 0;
            }

            AITalkPlugin.Log.LogInfo($"H场景数据: GaugeFemale={chara.GaugeFemale}%, AnimState={chara.NowAnimStateName}, Animation={chara.AnimationName}, IsAnalPlay={chara.IsAnalPlay}, HMode={chara.HMode}");

            AIDialogueUI.Instance?.ShowWaiting(chara.Name);

            // 读取记忆
            string saveId = MemoryManager.GetSaveId();
            var history = MemoryManager.LoadHistory(saveId, chara.CharaId);

            // 使用H场景引导词构建消息
            var messages = GameContextBuilder.BuildMessages(chara, playerInput, history);

            AITalkPlugin.Client.SendMessage(
                messages,
                reply =>
                {
                    AITalkPlugin.Log.LogInfo($"[H场景回复] {reply}");

                    // 清理标签（H场景不需要标签，但以防万一）
                    string cleanReply = System.Text.RegularExpressions.Regex.Replace(
                        reply, @"\[APOLOGY:[^\]]+\]|\[FAVOR:[^\]]+\]|\[INTIMACY:[^\]]+\]|\[LEWD:[^\]]+\]|\[EVENT:[^\]]+\]", "").Trim();
                    cleanReply = cleanReply.Replace("\n", "").Replace("\r", "");

                    AIDialogueUI.Instance?.ShowReply(cleanReply);

                    // 保存历史记录
                    string cleanInput = System.Text.RegularExpressions.Regex.Replace(
                        playerInput, @"\[situation\]:\[.*?\]", "").Trim();
                    if (!string.IsNullOrEmpty(cleanInput))
                    {
                        history.Add(new ChatMessage { role = "user", content = cleanInput });
                        history.Add(new ChatMessage { role = "assistant", content = cleanReply });
                    }
                    MemoryManager.SaveHistory(saveId, chara.CharaId, history);
                },
                err => AITalkPlugin.Log.LogError("H场景请求失败: " + err)
            );
        }

        private string GetHAutoInput(string animState)
        {
            if (animState.Contains("Idle") && !animState.Contains("Insert"))
                return "[system]:[H场景，待插入状态，用符合角色性格与情形的话说一句话。]";
            if (animState == "Insert")
                return "[system]:[H场景，刚插入的瞬间，用符合角色性格与情形的话说一句话。]";
            if (animState.Contains("IN_A"))
                return "[system]:[H场景，高潮结束后，用符合角色性格与情形的话说一句话。]";
            return "";
        }
    }
}