using ExtensibleSaveFormat;
using KKAPI.Maker;
using System;
using System.Linq;
using UnityEngine;
using static SaveData;
using static Studio.Info.LightLoadInfo;
using System.Collections;

namespace KKAITalk
{
    public class TestRunner : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                RunTest();

            if (Input.GetKeyDown(KeyCode.F9))
                DumpHeroineStatus();
        }
        private void RunTest()
        {
            // 尝试关闭字幕
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "KK_Subtitles_Caption")
                {
                    obj.SetActive(false);
                    AITalkPlugin.Log.LogInfo($"关闭字幕: {obj.name}");
                }
            }
        }
        private void DumpHeroineStatus()
        {
            var saveData = Manager.Game.Instance?.saveData;
            if (saveData == null) return;

            var testHeroine = saveData.heroineList[0];

            // 加在这里
            var talkScene = FindObjectOfType<TalkScene>();
            if (talkScene != null && talkScene.targetHeroine != null)
            {
                var heroine = talkScene.targetHeroine;
                var relationProp = heroine.GetType().GetProperty("relation",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                try
                {
                    AITalkPlugin.Log.LogInfo($"TalkScene heroine relation = {relationProp.GetValue(heroine, null)}");
                }
                catch
                {
                    AITalkPlugin.Log.LogWarning("relation读取失败");
                }
            }
        }
        private void Start()
        {
            // 监听Maker里角色被替换/重载的事件
            MakerAPI.MakerFinishedLoading += (s, e) => DumpCurrentMakerChara();

            // 这个事件在Maker里加载新角色卡时触发
            KKAPI.Chara.CharacterApi.CharacterReloaded += (s, e) =>
            {
                if (!MakerAPI.InsideMaker) return;
                AITalkPlugin.Log.LogInfo("=== 角色重载事件触发 ===");
                DumpCurrentMakerChara();
            };
        }

        private void DumpCurrentMakerChara()
        {
            if (!MakerAPI.InsideMaker) return;
            var chaCtrl = MakerAPI.GetCharacterControl();
            if (chaCtrl == null) return;

            AITalkPlugin.Log.LogInfo($"当前角色: {chaCtrl.fileParam.fullname}");

            // 直接读ChaFile原生字段
            var param = chaCtrl.chaFile.parameter;
            AITalkPlugin.Log.LogInfo($"  nickname: {param.nickname}");
            AITalkPlugin.Log.LogInfo($"  sex: {param.sex}");

            // 用反射列出parameter所有字段
            var fields = param.GetType().GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                try { AITalkPlugin.Log.LogInfo($"  field: {f.Name} = {f.GetValue(param)}"); }
                catch { }
            }
        }
        private void DumpEventButtons()
        {
            StartCoroutine(AutoClickEvent(1));
        }

        private IEnumerator AutoClickEvent(int index)
        {
            var talkScene = FindObjectOfType<TalkScene>();
            if (talkScene == null) yield break;

            var type = talkScene.GetType();

            // 先点击buttonEvent展开列表
            var btnEventField = type.GetField("buttonEvent",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var btnEvent = btnEventField.GetValue(talkScene) as UnityEngine.UI.Button;
            if (btnEvent != null)
            {
                btnEvent.onClick.Invoke();
                AITalkPlugin.Log.LogInfo("展开Event列表");
            }

            yield return new WaitForSeconds(0.3f);

            // 点击指定索引的按钮
            var field = type.GetField("buttonEventContents",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var buttons = field.GetValue(talkScene) as UnityEngine.UI.Button[];
            if (buttons == null || index >= buttons.Length) yield break;

            if (buttons[index] != null && buttons[index].gameObject.activeInHierarchy)
            {
                AITalkPlugin.Log.LogInfo($"点击按钮索引: {index}");
                buttons[index].onClick.Invoke();
            }
            else
                AITalkPlugin.Log.LogWarning($"按钮{index}未激活");
        }


        private void DumpExtData(ChaFile chaFile)
        {
            var allData = ExtendedSave.GetAllExtendedData(chaFile);
            if (allData == null || allData.Count == 0)
            {
                AITalkPlugin.Log.LogWarning("  无扩展数据");
                return;
            }

            foreach (var guid in allData.Keys)
            {
                AITalkPlugin.Log.LogInfo($"  GUID: {guid}");
                foreach (var kv in allData[guid].data)
                    AITalkPlugin.Log.LogInfo($"    key={kv.Key}, value={kv.Value}");
            }
        }
    }
}