using BepInEx;
using BepInEx.Logging;
using KKAITalk.Context;
using KKAITalk.LLM;
using KKAITalk.UI;
using KKAPI.MainGame;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace KKAITalk
{
    [BepInPlugin("com.yourname.kkaitalik", "KKAITalk", "1.0.0")]
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    public class AITalkPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static LlamaClient Client;

        private void Awake()
        {
            var uiObj = new GameObject("AIDialogueUI");
            DontDestroyOnLoad(uiObj);
            uiObj.AddComponent<AIDialogueUI>();

            Log = Logger;
            Log.LogInfo("KKAITalk 插件已加载！");
            Client = gameObject.AddComponent<LlamaClient>();

            GameAPI.RegisterExtraBehaviour<AITalkGameController>(KKAPI.KoikatuAPI.GameProcessName);

            // 订阅场景加载事件
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            Log.LogInfo("GameController 注册完成");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Talk")
            {
                StartCoroutine(OnTalkSceneReady());
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "Talk")
            {
                AIDialogueUI.Instance?.Hide();
            }
        }

        private System.Collections.IEnumerator OnTalkSceneReady()
        {
            yield return null;
            var talkScene = FindObjectOfType<TalkScene>();
            if (talkScene == null) yield break;


            var saveData = Manager.Game.Instance?.saveData;
            string accName = saveData?.accademyName ?? "default";
            string savePath = SaveData.Path ?? "";
            string saveFileName = System.IO.Path.GetFileNameWithoutExtension(savePath);

            // 最终存档文件夹名：学校名_存档文件名
            string saveId = string.IsNullOrEmpty(saveFileName)
                ? accName
                : accName + "_" + saveFileName;

            AITalkPlugin.Log.LogInfo("存档ID: " + saveId);

            // 轮询等待MsgWindowCanvas激活
            GameObject msgWindowCanvas = null;
            float elapsed = 0f;
            while (elapsed < 5f)
            {
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in all)
                {
                    if (obj.name == "MsgWindowCanvas" && obj.activeInHierarchy)
                    {
                        msgWindowCanvas = obj;
                        break;
                    }
                }
                if (msgWindowCanvas != null) break;
                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (msgWindowCanvas != null)
            {
                var skip = msgWindowCanvas.transform.Find(
                    "MsgWindow02/Buttons/Under_BG/Under_Right/Skip");

                if (skip != null && skip.gameObject.activeInHierarchy)
                {
                    var pointer = new UnityEngine.EventSystems.PointerEventData(
                        UnityEngine.EventSystems.EventSystem.current);
                    UnityEngine.EventSystems.ExecuteEvents.Execute(
                        skip.gameObject, pointer,
                        UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                    AITalkPlugin.Log.LogInfo("点击Skip，等待对话自然结束");

                    // 等对话自然结束
                    yield return new WaitForSeconds(1f);
                }
                else
                    AITalkPlugin.Log.LogWarning("Skip未激活，跳过");

                // 不再主动SetActive(false)
            }

            // 开启AI模式
            string charaName = talkScene.targetHeroine?.Name ?? "";
            AIDialogueUI.Instance?.ShowInputMode(charaName);
            AITalkPlugin.Log.LogInfo("AI模式自动开启");

            var controller = FindObjectOfType<AITalkGameController>();
            if (controller != null && talkScene.targetHeroine != null)
            {
                var heroine = talkScene.targetHeroine;
                AIDialogueUI.Instance.OnUserInput = (input) =>
                {
                    controller.OnTalkStart(heroine, input);
                };
                AITalkPlugin.Log.LogInfo("输入事件订阅完成，等待玩家输入");
            }
        }

        private IEnumerator HoldCtrlToSkip()
        {
            yield return new WaitForSeconds(0.5f);

            // 打印ADVScene下所有激活对象
            var advScene = GameObject.Find("ADVScene");
            if (advScene != null)
            {
                AITalkPlugin.Log.LogInfo("ADVScene found, active: " + advScene.activeInHierarchy);
                PrintChildren(advScene.transform, 0);
            }
            else
                AITalkPlugin.Log.LogWarning("ADVScene未找到");
        }

        private IEnumerator SkipGreeting()
        {
            yield return new WaitForSeconds(1f);

            var talkCanvas = GameObject.Find("TalkScene/Canvas");
            var msgWindow = talkCanvas?.transform.Find("MsgWindow0");

            AITalkPlugin.Log.LogInfo("MsgWindow0 active: " +
                (msgWindow?.gameObject.activeInHierarchy.ToString() ?? "null"));
            AITalkPlugin.Log.LogInfo("Button Talk active: " +
                (talkCanvas?.transform.Find("Button Talk")?.gameObject.activeInHierarchy.ToString() ?? "null"));
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