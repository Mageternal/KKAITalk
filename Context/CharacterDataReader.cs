using ActionGame;
using ExtensibleSaveFormat;
using KKAPI.MainGame;
using KKAPI.Utilities;
using KK_Pregnancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KKAITalk.Context
{
    public static class CharacterDataReader
    {
        // 从KKAPI的ActionControllerCharaTarget读取角色数据
        public static CharacterContext ReadFromChara(SaveData.CharaData charaData)
        {
            if (charaData == null) return null;

            return new CharacterContext
            {
                Name = charaData.charFile.parameter.fullname,
                Personality = charaData.charFile.parameter.personality.ToString(),
                CharaId = charaData.charFile.parameter.fullname + "_" +
                              charaData.charFile.parameter.birthDay.ToString()
            };
        }
        public static CharacterContext ReadFromHeroine(SaveData.Heroine heroine)
        {
            if (heroine == null) return null;

            int relation = 0;
            var relationProp = heroine.GetType().GetProperty("relation",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            try
            {
                relation = (int)relationProp.GetValue(heroine, null);
            }

            catch { }

            var chara = new CharacterContext
            {
                Name = heroine.Name,
                Personality = heroine.personality.ToString(),
                CharaId = heroine.Name + "_" + heroine.chaCtrl?.fileParam?.birthDay,
                ProfileText = ReadProfileText(heroine.chaCtrl?.chaFile),

                Favor = heroine.favor,
                IsGirlfriend = heroine.isGirlfriend,
                IsStaff = heroine.isStaff,
                IsDate = heroine.isDate,
                IsAnger = heroine.isAnger,
                Anger = heroine.anger,
                Confessed = heroine.confessed,
                IsFirstGirlfriend = heroine.isFirstGirlfriend,

                Intimacy = heroine.intimacy,
                IsLunch = heroine.isLunch,
                MyRoomCount = heroine.myRoomCount,

                Lewdness = heroine.lewdness,
                IsVirgin = heroine.isVirgin,
                IsAnalVirgin = heroine.isAnalVirgin,
                IsKiss = heroine.isKiss,
                Relation = relation,

                HExperience = heroine.HExperience,

                // 怀孕/安全期状态（KK_Pregnancy）
                IsSafeDay = false,
                IsRiskyDay = false,
                IsPregnant = false,
                PregnancyWeek = 0,
            };

            // 读取怀孕/安全期状态
            try
            {
                var status = PregnancyDataUtils.GetCharaStatus(heroine);
                chara.IsSafeDay = (status == HeroineStatus.Safe);
                chara.IsRiskyDay = (status == HeroineStatus.Risky);
                chara.IsPregnant = (status == HeroineStatus.Pregnant);

                var pregData = PregnancyDataUtils.GetPregnancyData(heroine);
                chara.PregnancyWeek = (pregData != null) ? pregData.Week : 0;
            }
            catch { }

            return chara;
        }
        public static string ReadProfileText(ChaFile chaFile)
        {
            if (chaFile == null) return null;
            var data = ExtendedSave.GetExtendedDataById(chaFile, "KK_Profile");
            if (data == null || !data.data.ContainsKey("ProfileText"))
                return null;
            return data.data["ProfileText"] as string;
        }
    }
}