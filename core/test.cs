using ExtensibleSaveFormat;
using KKAPI.Maker;
using System;
using System.Linq;
using UnityEngine;
using static SaveData;
using static Studio.Info.LightLoadInfo;
using System.Collections;
using System.Reflection;
using KK_Pregnancy;

namespace KKAITalk
{
    public class TestRunner : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                RunTest();

            if (Input.GetKeyDown(KeyCode.F9))
                DumpVoiceFlagFields();

            if (Input.GetKeyDown(KeyCode.F10))
                DumpCurrentHeroineFields();

            if (Input.GetKeyDown(KeyCode.F11))
                DumpPregnancyStatus();
        }

        private void RunTest()
        {
            var hProc = FindObjectOfType<HSceneProc>();
            if (hProc == null)
            {
                AITalkPlugin.Log.LogWarning("不在H场景");
                return;
            }

            AITalkPlugin.Log.LogInfo("=== H场景字段列表 ===");

            // 找HFlag - 使用安全反射方法
            var flags = SafeGetFieldValue(hProc, "flags");
            if (flags == null)
            {
                AITalkPlugin.Log.LogWarning("flags字段未找到");
                return;
            }

            AITalkPlugin.Log.LogInfo($"flags类型: {flags.GetType().FullName}");
            AITalkPlugin.Log.LogInfo("=== flags 的所有字段和值 ===");

            // 列出flags的所有字段和值
            var flagFields = SafeGetAllFields(flags.GetType());
            foreach (var f in flagFields)
            {
                try
                {
                    var value = f.GetValue(flags);
                    string valueStr = value != null ? value.ToString() : "null";
                    AITalkPlugin.Log.LogInfo($"  {f.Name} ({f.FieldType.Name}) = {valueStr}");
                }
                catch (Exception ex)
                {
                    AITalkPlugin.Log.LogWarning($"  {f.Name}: {ex.Message}");
                }
            }

            // 也列出HSceneProc的其他可能相关字段
            AITalkPlugin.Log.LogInfo("=== HSceneProc 的其他字段 ===");
            var procFields = SafeGetAllFields(hProc.GetType());
            foreach (var f in procFields)
            {
                if (f.Name == "flags") continue; // 跳过flags，已经列过了
                try
                {
                    var value = f.GetValue(hProc);
                    string valueStr = value != null ? value.ToString() : "null";
                    AITalkPlugin.Log.LogInfo($"  {f.Name} ({f.FieldType.Name}) = {valueStr}");
                }
                catch (Exception ex)
                {
                    AITalkPlugin.Log.LogWarning($"  {f.Name}: {ex.Message}");
                }
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

        private void DumpVoiceFlagFields()
        {
            var hProc = FindObjectOfType<HSceneProc>();
            if (hProc == null)
            {
                AITalkPlugin.Log.LogWarning("不在H场景");
                return;
            }

            // 获取flags
            var flags = SafeGetFieldValue(hProc, "flags");
            if (flags == null)
            {
                AITalkPlugin.Log.LogWarning("flags未找到");
                return;
            }

            // 获取voice对象
            var voice = SafeGetFieldValue(flags, "voice");
            if (voice == null)
            {
                AITalkPlugin.Log.LogWarning("voice字段未找到");
                return;
            }

            AITalkPlugin.Log.LogInfo("=== VoiceFlag 详细字段列表 ===");

            // 列出voice的所有字段和值
            var voiceFields = SafeGetAllFields(voice.GetType());
            foreach (var f in voiceFields)
            {
                try
                {
                    var value = f.GetValue(voice);
                    string valueStr = value != null ? value.ToString() : "null";
                    AITalkPlugin.Log.LogInfo($"  {f.Name} ({f.FieldType.Name}) = {valueStr}");
                }
                catch (Exception ex)
                {
                    AITalkPlugin.Log.LogWarning($"  {f.Name}: {ex.Message}");
                }
            }
        }

        private void DumpCurrentHeroineFields()
        {
            var heroine = AITalkPlugin.CurrentHeroine;
            if (heroine == null)
            {
                AITalkPlugin.Log.LogWarning("CurrentHeroine未找到");
                return;
            }

            AITalkPlugin.Log.LogInfo("=== 女主角详细字段列表 ===");
            AITalkPlugin.Log.LogInfo($"女主角: {heroine.Name}");

            // 列出女主角的所有字段
            var heroineFields = SafeGetAllFields(heroine.GetType());
            foreach (var f in heroineFields)
            {
                try
                {
                    var value = f.GetValue(heroine);
                    string valueStr = value != null ? value.ToString() : "null";
                    AITalkPlugin.Log.LogInfo($"  {f.Name} ({f.FieldType.Name}) = {valueStr}");
                }
                catch (Exception ex)
                {
                    AITalkPlugin.Log.LogWarning($"  {f.Name}: {ex.Message}");
                }
            }
        }

        private void DumpPregnancyStatus()
        {
            var heroine = AITalkPlugin.CurrentHeroine;
            if (heroine == null)
            {
                AITalkPlugin.Log.LogWarning("CurrentHeroine未找到");
                return;
            }

            AITalkPlugin.Log.LogInfo("=== 怀孕状态（KK_Pregnancy） ===");
            AITalkPlugin.Log.LogInfo($"女主角: {heroine.Name}");

            try
            {
                // 使用 PregnancyDataUtils 获取状态
                var status = PregnancyDataUtils.GetCharaStatus(heroine);
                AITalkPlugin.Log.LogInfo($"  HeroineStatus = {status}");

                // 获取详细数据
                var pregData = PregnancyDataUtils.GetPregnancyData(heroine);
                if (pregData != null)
                {
                    AITalkPlugin.Log.LogInfo($"  怀孕数据存在，Week = {pregData.Week}");
                }
                else
                {
                    AITalkPlugin.Log.LogInfo("  怀孕数据为null（未怀孕）");
                }
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogWarning($"读取怀孕状态失败: {ex.Message}");
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
