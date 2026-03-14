using ActionGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace KKAITalk.Context
{
    public class CharacterContext
    {
        public string Name;
        public string Personality;
        public string CharaId;
        public Cycle.Type CurrentPeriod;
        public string ProfileText;

        // 关系状态
        public int Favor;
        public bool IsGirlfriend;
        public bool IsStaff;
        public bool IsDate;
        public bool IsAnger;
        public int Anger;
        public bool Confessed;
        public bool IsFirstGirlfriend;

        // 亲密度
        public int Intimacy;
        public bool IsLunch;
        public int MyRoomCount;

        // 经历
        public int Lewdness;
        public bool IsVirgin;
        public bool IsAnalVirgin;
        public bool IsKiss;

    }
}