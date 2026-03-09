using ExtensibleSaveFormat;
using KKAPI.Maker;
using System;
using System.Linq;
using UnityEngine;

namespace KKAITalk
{
    public class TestRunner : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                string cardPath = "F:\\Temp\\game\\PC game\\Koikatsu BetterRepack RX21\\UserData\\chara\\female\\Koikatu_F_20260314190351791_高垣 枫_Mageternal.png";

                if (!System.IO.File.Exists(cardPath))
                {
                    AITalkPlugin.Log.LogWarning("文件不存在: " + cardPath);
                    return;
                }

                var chaFileCtrl = new ChaFileControl();
                chaFileCtrl.LoadCharaFile(cardPath, 1, false, true);

                AITalkPlugin.Log.LogInfo($"从文件加载: {chaFileCtrl.parameter.fullname}");

                var allData = ExtendedSave.GetAllExtendedData(chaFileCtrl);
                if (allData == null || allData.Count == 0)
                {
                    AITalkPlugin.Log.LogWarning("文件里也没有扩展数据");
                    return;
                }

                foreach (var kv in allData)
                {
                    AITalkPlugin.Log.LogInfo($"  GUID: {kv.Key}");
                    if (kv.Value?.data == null) continue;
                    foreach (var field in kv.Value.data)
                        AITalkPlugin.Log.LogInfo($"    key={field.Key}, value={field.Value}");
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
        private void RunTest()
        {
            AITalkPlugin.Log.LogInfo("=== TestRunner 开始 ===");

            // 方案1：在CharaMaker里按F8，直接拿Maker角色
            if (MakerAPI.InsideMaker)
            {
                var chaCtrl = MakerAPI.GetCharacterControl();
                if (chaCtrl != null)
                {
                    AITalkPlugin.Log.LogInfo($"Maker角色: {chaCtrl.fileParam.fullname}");
                    DumpExtData(chaCtrl.chaFile);
                }
                return;
            }

            // 方案2：在游戏里按F8，遍历场景里所有ChaControl
            var allCharas = FindObjectsOfType<ChaControl>();
            if (allCharas == null || allCharas.Length == 0)
            {
                AITalkPlugin.Log.LogWarning("场景里没有找到ChaControl");
                return;
            }

            foreach (var chara in allCharas)
            {
                AITalkPlugin.Log.LogInfo($"--- 角色: {chara.fileParam.fullname} ---");
                DumpExtData(chara.chaFile);
            }

            AITalkPlugin.Log.LogInfo("=== TestRunner 结束 ===");
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