using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KKAPI.MainGame;
using KKAPI.Utilities;
using ActionGame;
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
                CharaId = heroine.Name + "_" + heroine.chaCtrl?.fileParam?.birthDay
            };
        }
    }
}