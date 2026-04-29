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
        public int Relation;

        // 亲密度
        public int Intimacy;
        public bool IsLunch;
        public int MyRoomCount;

        // 经历
        public int Lewdness;
        public bool IsVirgin;
        public bool IsAnalVirgin;
        public bool IsKiss;
        public SaveData.Heroine.HExperienceKind HExperience;

        // H场景动态状态
        public float GaugeFemale;
        public string NowAnimStateName;
        public string AnimationName; // 当前姿势名称
        public bool IsAnalPlay;
        public bool IsInHScene;
        public string HMode; // aibu/houshi/sonyu

        // 怀孕/安全期状态（KK_Pregnancy）
        public bool IsSafeDay; // 安全期
        public bool IsRiskyDay; // 危险期
        public bool IsPregnant; // 怀孕中
        public int PregnancyWeek; // 怀孕周期（1~36周）

    }
}