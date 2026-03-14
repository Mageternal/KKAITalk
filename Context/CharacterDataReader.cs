using ActionGame;
using ExtensibleSaveFormat;
using KKAPI.MainGame;
using KKAPI.Utilities;
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

            return new CharacterContext
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
            };
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