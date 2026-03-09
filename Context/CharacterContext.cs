using ActionGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace KKAITalk.Context
{
    public class CharacterContext
    {
        public string Name;           // 角色名字
        public string Personality;    // 性格类型
        public string CharaId;        // 唯一ID，用于区分不同角色的记忆存档
        public Cycle.Type CurrentPeriod;  // 新增时间段
        public string ProfileText;

        public override string ToString()
        {
            return $"Name={Name}, Personality={Personality}, ID={CharaId}";
        }
    }
}