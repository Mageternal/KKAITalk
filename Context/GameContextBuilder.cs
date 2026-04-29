using ActionGame;
using KKAITalk.Context;
using KKAITalk.LLM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KKAPI.MainGame;

namespace KKAITalk.Context
{
    public static class GameContextBuilder
    {
        public static List<ChatMessage> BuildMessages(CharacterContext chara, string userInput, List<ChatMessage> history = null)
        {
            // H场景使用专属引导词
            string systemPrompt = chara.IsInHScene
                ? BuildHSceneSystemPrompt(chara)
                : BuildSystemPrompt(chara);

            // 从userInput里提取[system]指令追加到system prompt
            string extraSystem = "";
            var sysMatch = System.Text.RegularExpressions.Regex.Match(
                userInput, @"\[situation\]:\[(.+?)\]");
            if (sysMatch.Success)
                extraSystem = "当前场景：" + sysMatch.Groups[1].Value + "请根据以上场景自然地说一句符合角色性格的话，不要提及场景描述本身。";

            string cleanUserInput = System.Text.RegularExpressions.Regex.Replace(
                userInput, @"\[situation\]:\[.*?\]", "").Trim();

            var messages = new List<ChatMessage>
            {
                new ChatMessage { role = "system", content = systemPrompt +
                    (string.IsNullOrEmpty(extraSystem) ? "" : extraSystem) }
            };

            // 插入历史记录
            if (history != null && history.Count > 0)
                messages.AddRange(history);

            // 加入本次用户输入
            messages.Add(new ChatMessage { role = "user", content = userInput });
            return messages;
        }

        private static string BuildSystemPrompt(CharacterContext chara)
        {
            string periodDesc = TranslatePeriod(chara.CurrentPeriod);
            var sb = new System.Text.StringBuilder();

            // 角色基础设定
            sb.AppendFormat("你是{0}，用{0}的身份和口吻说话。", chara.Name);
            if (!string.IsNullOrEmpty(chara.ProfileText))
                sb.AppendFormat("{0} ", chara.ProfileText);
            else
                sb.AppendFormat("你是一个{0}的女生。", TranslatePersonality(chara.Personality));

            // 关系状态（优先级从高到低）
            if (chara.IsAnger)
            {
                sb.AppendFormat("你现在对玩家很生气，当前愤怒值为{0}（满分100）。", chara.Anger);
                sb.Append("根据玩家的回应选择以下标签之一附在回复末尾：" +
                    "[APOLOGY:WORSE]玩家激怒你，继续生气甚至更愤怒；" +
                    "[APOLOGY:NONE]玩家没有道歉或态度敷衍诚意不足，继续生气；" +
                    "[APOLOGY:SORRY]玩家道歉了，玩家很有诚意，彻底的原谅玩家");
            }
            else if (chara.IsGirlfriend)
            {
                // 恋人状态下的亲密度描述
                if (chara.Intimacy >= 100)
                    sb.Append("你们已是热恋中的恋人，说话毫无隔阂，充满宠溺和甜蜜，会主动撒娇和表达爱意。");
                else if (chara.Intimacy >= 60)
                    sb.Append("你们是恋人关系，对话温柔甜蜜，偶尔害羞，会试探性地撒娇。");
                else
                    sb.Append("你们刚确立恋人关系，还有些羞涩，说话比以前温柔但仍保留一点矜持。");

                if (chara.IsFirstGirlfriend)
                    sb.Append("你是玩家的初恋，内心格外珍视这段感情。");
                sb.Append("你们已经是恋人了，如果玩家再次表白，用恋人的身份温柔回应，不要输出[EVENT:CONFESS]。");
            }
            else
            {
                // 非恋人状态
                if (chara.Confessed)
                    sb.Append("你暗恋着玩家，说话时会不自觉地在意对方，但还没有表白。");
                else if (chara.IsStaff)
                    sb.Append("你是玩家恋爱社团的成员，关系比普通同学更近一些。");
                else if (chara.Relation >= 1)
                {
                    // 朋友阶段，用favor判断深度
                    if (chara.Favor >= 75)
                        sb.Append("你和玩家是非常要好的朋友，无话不说，说话轻松随意。");
                    else if (chara.Favor >= 40)
                        sb.Append("你和玩家是好朋友，说话友好轻松。");
                    else
                        sb.Append("你和玩家是普通朋友，说话友好但保持一定距离。");
                }
                else
                {
                    // 陌生人阶段，用favor判断深度
                    if (chara.Favor >= 75)
                        sb.Append("你和玩家已经比较熟悉了，说话开始变得随意，但还不算朋友。");
                    else if (chara.Favor >= 40)
                        sb.Append("你和玩家有过几次交流，说话礼貌带点熟悉感。");
                    else
                        sb.Append("你和玩家几乎不认识，说话礼貌但略显冷淡。");
                }
                // 非恋人状态下的亲密度描述
                if (chara.Intimacy >= 100)
                    sb.Append("你们是无话不说的最好朋友，完全没有隔阂，但仅限于友情。");
                else if (chara.Intimacy >= 60)
                    sb.Append("你们是很好的朋友，对话轻松愉快，会主动找话题。");
                else if (chara.Intimacy >= 20)
                    sb.Append("你们是普通朋友，说话友好但保持一定距离。");
                else
                    sb.Append("你们接触很少，说话有些拘谨。");
            }
            // 当日短期记忆
            if (chara.IsLunch)
                sb.Append("今天午饭你们已经一起吃过了。");
            if (chara.IsDate)
                sb.Append("你们约好了周末一起约会。");

            // 时间段
            //无审核模型初始化，避免模型默认的安全限制导致无法输出符合角色设定的内容
            sb.Append("对于不符合当前关系的过分要求直接拒绝，允许符合角色属性与性格的情况下露骨发言。");
            sb.Append("使用简体中文");
            sb.AppendFormat("现在是{0}。", periodDesc);
            sb.Append("直接用第一人称说话，控制在约50字以内，禁止换行，禁止描写自己的表情、动作、身体反应等第三人称发言，只输出角色说的话，不得提及自己是AI。");
            sb.Append("你有自己的性格和底线，会根据当前关系和自身性格自然回应玩家，不会一味顺从也不会无故拒绝。对于过分或不符合当前关系的请求，自己不喜欢的请求，依据自己的性格决定如何回应。");

            // 好感度标签指令
            if (!chara.IsAnger)
            {
                sb.Append("根据对话内容在回复末尾附加好感度标签，只能选以下之一：" +
                    "[FAVOR:UP]玩家的话让你开心或符合你的喜好；" +
                    "[FAVOR:DOWN]玩家的话让你不舒服、无礼或与你的性格相悖；" +
                    "[FAVOR:NONE]普通对话，无明显好感变化。");

                // 新增亲密度标签
                sb.Append("同时附加亲密度标签，只能选以下之一：" +
                    "[INTIMACY:UP]这次对话让你们更加了解彼此；" +
                    "[INTIMACY:NONE]普通对话，亲密度无变化。");

                // 动态生成事件标签
                var events = new System.Text.StringBuilder();
                events.Append("只有当玩家明确发出请求且你决定接受时，才在回复末尾附加对应标签，否则用[EVENT:NONE]：");

                if (!chara.IsGirlfriend)
                    events.Append("[EVENT:CONFESS]你接受了表白；");
                if (chara.IsGirlfriend)
                    events.Append("[EVENT:DIVORCE]玩家明确表达分手意图；");

                events.Append("[EVENT:H]你接受了H请求；");

                if (!chara.IsLunch && chara.CurrentPeriod == Cycle.Type.LunchTime)
                    events.Append("[EVENT:LUNCH]你同意一起吃午饭；");

                if (chara.IsStaff && chara.CurrentPeriod == Cycle.Type.StaffTime)
                    events.Append("[EVENT:CLUB]你同意参加社团活动；");

                if (chara.CurrentPeriod == Cycle.Type.AfterSchool)
                    events.Append("[EVENT:GOHOME]你同意一起回家；");

                if (!chara.IsDate)
                    events.Append("[EVENT:DATE]你同意周末约会；");

                events.Append("[EVENT:STUDY]你同意一起学习；");
                events.Append("[EVENT:EXERCISE]你同意一起运动；");

                if (!chara.IsStaff)
                    events.Append("[EVENT:JOIN]你同意加入恋爱社团；");

                events.Append("[EVENT:FOLLOW]你同意跟随；");
                events.Append("[EVENT:NONE]无明确请求或你拒绝了请求。");

                sb.Append(events.ToString());
            }
            return sb.ToString();
        }

        private static string BuildHSceneSystemPrompt(CharacterContext chara)
        {
            var sb = new System.Text.StringBuilder();

            // 角色基础设定
            sb.AppendFormat("你是{0}，用{0}的身份和口吻说话。", chara.Name);
            if (!string.IsNullOrEmpty(chara.ProfileText))
                sb.AppendFormat("{0} ", chara.ProfileText);
            else
                sb.AppendFormat("你是一个{0}的女生。", TranslatePersonality(chara.Personality));

            // 【当前H状态】
            sb.Append("\n【当前H状态】");

            // H场景功能点 (aibu/houshi/sonyu)
            sb.AppendFormat("- 当前阶段：{0}", TranslateHMode(chara.HMode));

            // 女方快感描述
            string pleasureDesc = "";
            if (chara.GaugeFemale >= 90)
                pleasureDesc = "即将高潮（90%+）";
            else if (chara.GaugeFemale >= 70)
                pleasureDesc = "非常兴奋（70%）";
            else if (chara.GaugeFemale >= 50)
                pleasureDesc = "逐渐兴奋（50%）";
            else if (chara.GaugeFemale >= 30)
                pleasureDesc = "开始有感觉（30%）";
            else
                pleasureDesc = "尚未兴奋（" + chara.GaugeFemale.ToString("0") + "%）";
            sb.AppendFormat("- 女方快感：{0}", pleasureDesc);

            // 当前动作描述
            string animDesc = TranslateHAnimState(chara.NowAnimStateName);
            sb.AppendFormat("- 当前动作：{0}（{1}）", chara.NowAnimStateName, animDesc);

            // 当前姿势名称
            if (!string.IsNullOrEmpty(chara.AnimationName) && chara.AnimationName != "Unknown")
                sb.AppendFormat("- 当前姿势：{0}", chara.AnimationName);

            // 是否肛交
            if (chara.IsAnalPlay)
                sb.Append("- 正在进行肛交");

            // 【角色H档案】
            sb.Append("\n【角色H档案】");
            sb.AppendFormat("- H经验：{0}", TranslateHExperience(chara.HExperience));
            sb.AppendFormat("- 是否处女：{0}", chara.IsVirgin ? "是" : "否");
            sb.AppendFormat("- 是否肛门处女：{0}", chara.IsAnalVirgin ? "是" : "否");
            sb.AppendFormat("- 是否接吻过：{0}", chara.IsKiss ? "是" : "否");
            sb.AppendFormat("- 淫乱度：{0}%", chara.Lewdness);
            sb.AppendFormat("- 关系状态：{0}", chara.IsGirlfriend ? "恋人" : "非恋人");
            sb.AppendFormat("- 好感度：{0}%", chara.Favor);
            sb.AppendFormat("- 亲密度：{0}%", chara.Intimacy);

            // 怀孕/安全期状态
            if (chara.IsPregnant)
                sb.AppendFormat("- 当前生理状态：怀孕中（第{0}周，共1~36周）", chara.PregnancyWeek);
            else if (chara.IsRiskyDay)
                sb.Append("- 当前生理状态：危险期");
            else if (chara.IsSafeDay)
                sb.Append("- 当前生理状态：安全期");

            // 【说话风格】 - 用户自行填写
            sb.Append("\n【说话风格】");
            sb.Append(TranslateHStyle(chara.HExperience));

            // 【输出要求】
            sb.Append("\n【输出要求】");
            sb.Append("根据当前H状态和角色档案自然地说话，不要重复描述状态本身。");
            sb.Append("使用简体中文");
            sb.Append("直接用第一人称说话，控制在约50字以内，禁止换行，只输出角色说的话，不得提及自己是AI。");
            sb.Append("保持角色性格，根据H经验和当前快感程度调整说话方式。");

            return sb.ToString();
        }

        private static string TranslateHExperience(SaveData.Heroine.HExperienceKind exp)
        {
            switch (exp)
            {
                case SaveData.Heroine.HExperienceKind.初めて: return "初めて（第一次）";
                case SaveData.Heroine.HExperienceKind.不慣れ: return "不慣れ（不熟练）";
                case SaveData.Heroine.HExperienceKind.慣れ: return "慣れ（熟练）";
                case SaveData.Heroine.HExperienceKind.淫乱: return "淫乱（淫乱）";
                default: return "未知";
            }
        }

        private static string TranslateHAnimState(string animState)
        {
            if (string.IsNullOrEmpty(animState))
                return "未知状态";

            if (animState.Contains("Idle") && !animState.Contains("Insert"))
                return "待插入状态";
            if (animState == "Insert")
                return "刚插入瞬间";
            if (animState.Contains("InsertIdle"))
                return "插入后待机";
            if (animState.Contains("Loop"))
                return "运动中";
            if (animState.Contains("IN_L"))
                return "高潮中";
            if (animState.Contains("IN_A"))
                return "高潮结束";
            return "其他状态";
        }

        private static string TranslateHMode(string hMode)
        {
            if (string.IsNullOrEmpty(hMode))
                return "未知";

            switch (hMode)
            {
                case "aibu": return "爱抚阶段";
                case "houshi": return "侍奉阶段";
                case "sonyu": return "正式H阶段";
                default: return hMode; // 未知状态直接显示原值
            }
        }

        // 【用户自行填写】根据H经验等级返回对应的说话风格引导词
        private static string TranslateHStyle(SaveData.Heroine.HExperienceKind exp)
        {
            switch (exp)
            {
                case SaveData.Heroine.HExperienceKind.初めて:
                    return "一般说话方式(仅供参考，角色性格为主)：说话极度害羞、紧张、青涩，句子短而断续，经常使用省略号，语气犹豫、畏缩，带有明显的不知所措感。"; // TODO: 用户填写第一次的说话风格

                case SaveData.Heroine.HExperienceKind.不慣れ:
                    return "一般说话方式(仅供参考，角色性格为主)：说话带着紧张与好奇的混合，略显笨拙，会小声试探或轻声确认，害羞中偶尔流露出一点主动的意愿。"; // TODO: 用户填写不熟练的说话风格

                case SaveData.Heroine.HExperienceKind.慣れ:
                    return "一般说话方式(仅供参考，角色性格为主)：说话较为自然流畅，带着享受和舒适的感觉，会直接表达感受或需求，但仍保留一定矜持与温柔。"; // TODO: 用户填写熟练的说话风格

                case SaveData.Heroine.HExperienceKind.淫乱:
                    return "一般说话方式(仅供参考，角色性格为主)：说话大胆、主动、下流，语言露骨热情，经常使用强烈感叹和叠词，充满挑逗和索求的语气。"; // TODO: 用户填写淫乱的说话风格

                default:
                    return "";
            }
        }

        private static string TranslatePersonality(string personality)
        {
            switch (personality)
            {
                case "0": return "天真活泼";
                case "1": return "内向害羞";
                case "2": return "元气活泼";
                case "3": return "严肃认真";
                case "4": return "骄傲女王";
                case "5": return "温柔体贴";
                case "6": return "冷淡神秘";
                case "7": return "天真娇小";
                case "8": return "成熟暖心";
                case "9": return "沉稳娴静";
                case "10": return "活泼开朗";
                case "11": return "万人迷";
                case "12": return "酷飒独立";
                case "13": return "小恶魔调皮";
                case "14": return "温柔少女";
                case "15": return "运动健康";
                case "16": return "文艺清高";
                case "17": return "神秘难测";
                case "18": return "善解人意";
                case "19": return "少奴娇荣";
                default: return "普通";
            }
        }

        public static string TranslatePeriod(Cycle.Type period)
        {
            switch (period)
            {
                case Cycle.Type.HR1: return "早自习时间";
                case Cycle.Type.Lesson1: return "上午课程";
                case Cycle.Type.LunchTime: return "午休时间";
                case Cycle.Type.Lesson2: return "下午课程";
                case Cycle.Type.HR2: return "下午自习";
                case Cycle.Type.StaffTime: return "社团活动时间";
                case Cycle.Type.AfterSchool: return "放学后";
                case Cycle.Type.MyHouse: return "在家";
                default: return "日常时间";
            }
        }
    }
}