using ExtensibleSaveFormat;
using KKAPI.Maker;
using System;
using System.Linq;
using UnityEngine;
using static SaveData;
using static Studio.Info.LightLoadInfo;
using System.Collections;
using System.Reflection;

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
            var hProc = FindObjectOfType<HSceneProc>();
            if (hProc == null)
            {
                AITalkPlugin.Log.LogWarning("不在H场景");
                return;
            }

            AITalkPlugin.Log.LogInfo("找到HSceneProc");

            // 找HFlag - 使用安全反射方法
            var flags = SafeGetFieldValue(hProc, "flags");
            if (flags == null)
            {
                AITalkPlugin.Log.LogWarning("flags字段未找到，尝试搜索所有字段");
                var allFields = SafeGetAllFields(hProc.GetType());
                foreach (var f in allFields)
                {
                    AITalkPlugin.Log.LogInfo($"  字段: {f.Name} = {f.FieldType}");
                }
                return;
            }

            AITalkPlugin.Log.LogInfo($"flags类型: {flags.GetType().FullName}");

            // 读取关键字段
            string[] targets = { "gaugeMale", "gaugeFemale", "speed", "nowAnimStateName", "isAnalPlay" };
            foreach (var name in targets)
            {
                // 使用安全的反射查找
                var value = SafeGetFieldOrPropertyValue(flags, name);
                if (value != null)
                    AITalkPlugin.Log.LogInfo($"  {name} = {value}");
                else
                    AITalkPlugin.Log.LogWarning($"  {name} 未找到");
            }
        }

        /// <summary>
        /// 安全获取字段值 - 避免 op_Equality 问题
        /// </summary>
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

        /// <summary>
        /// 安全获取所有字段 - 避免 op_Equality 问题
        /// </summary>
        private static FieldInfo[] SafeGetAllFields(Type type)
        {
            try
            {
                return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (MissingMethodException ex)
            {
                AITalkPlugin.Log.LogWarning($"SafeGetAllFields: {ex.Message}");
                return new FieldInfo[0];
            }
        }

        /// <summary>
        /// 安全获取字段或属性值 - 避免 op_Equality 问题
        /// </summary>
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

        private void DumpHeroineStatus()
        {
            var saveData = Manager.Game.Instance?.saveData;
            if (saveData == null) return;

            var testHeroine = saveData.heroineList[0];

            var talkScene = FindObjectOfType<TalkScene>();
            if (talkScene != null && talkScene.targetHeroine != null)
            {
                var heroine = talkScene.targetHeroine;
                var relationValue = SafeGetFieldOrPropertyValue(heroine, "relation");
                AITalkPlugin.Log.LogInfo($"TalkScene heroine relation = {relationValue}");
            }
        }

        private void Start()
        {
            MakerAPI.MakerFinishedLoading += (s, e) => DumpCurrentMakerChara();

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

            var param = chaCtrl.chaFile.parameter;
            AITalkPlugin.Log.LogInfo($"  nickname: {param.nickname}");
            AITalkPlugin.Log.LogInfo($"  sex: {param.sex}");

            var fields = SafeGetAllFields(param.GetType());
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

            var btnEventField = SafeGetFieldValue(talkScene, "buttonEvent") as UnityEngine.UI.Button;
            if (btnEventField != null)
            {
                btnEventField.onClick.Invoke();
                AITalkPlugin.Log.LogInfo("展开Event列表");
            }

            yield return new WaitForSeconds(0.3f);

            var buttons = SafeGetFieldValue(talkScene, "buttonEventContents") as UnityEngine.UI.Button[];
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
            try
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
            catch (MissingMethodException ex)
            {
                AITalkPlugin.Log.LogWarning($"DumpExtData: {ex.Message}");
            }
        }
    }
}
