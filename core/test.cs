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
            var talkScene = FindObjectOfType<TalkScene>();
            if (talkScene == null) return;

            // 探state枚举值
            var necessaryInfo = talkScene.necessaryInfo;
            var stateVal = necessaryInfo.state;
            AITalkPlugin.Log.LogInfo($"state当前值: {stateVal}");

            var enumType = stateVal.GetType();
            AITalkPlugin.Log.LogInfo($"state类型: {enumType.FullName}");
            foreach (var val in System.Enum.GetValues(enumType))
                AITalkPlugin.Log.LogInfo($"  {val} = {(int)val}");
        }
        private void DumpHeroineStatus()
        {
            AITalkPlugin.Log.LogInfo("=== Heroine Status ===");
            

            var saveData = Manager.Game.Instance?.saveData;
            if (saveData == null)
            {
                AITalkPlugin.Log.LogWarning("saveData为null");
                return;
            }

            var heroines = saveData.heroineList;
            if (heroines == null || heroines.Count == 0)
            {
                AITalkPlugin.Log.LogWarning("没有找到heroine");
                return;
            }

            // HExperience测试
            var testHeroine = saveData.heroineList[0];
            AITalkPlugin.Log.LogInfo($"HExperience类型: {testHeroine.HExperience.GetType().FullName}");
            AITalkPlugin.Log.LogInfo($"HExperience当前值: {testHeroine.HExperience}");

            // 反射列出HExperienceKind的所有可能值
            var enumType = testHeroine.HExperience.GetType();
            AITalkPlugin.Log.LogInfo("HExperienceKind所有枚举值:");
            foreach (var val in Enum.GetValues(enumType))
                AITalkPlugin.Log.LogInfo($"  {val} = {(int)val}");


            AITalkPlugin.Log.LogInfo("=== End ===");
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