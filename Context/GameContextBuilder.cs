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
            string systemPrompt = BuildSystemPrompt(chara);
            var messages = new List<ChatMessage>
            {
                new ChatMessage { role = "system", content = systemPrompt }
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
            else if (chara.Confessed)
                sb.Append("你暗恋着玩家，说话时会不自觉地在意对方，但还没有表白。");
            else if (chara.IsStaff)
                sb.Append("你是玩家恋爱社团的成员，关系比普通同学更近一些。");
            else if (chara.Favor >= 80)
                sb.Append("你和玩家是非常要好的朋友，说话轻松随意。");
            else if (chara.Favor >= 40)
                sb.Append("你和玩家是普通朋友，说话友好但保持距离。");
            else
                sb.Append("你和玩家几乎不认识，说话礼貌但略显冷淡。");

            // 亲密度影响对话风格
            if (!chara.IsAnger)
            {
                if (chara.IsGirlfriend)
                {
                    // 恋人状态下的亲密度描述
                    if (chara.Intimacy >= 100)
                        sb.Append("你们已是热恋中的恋人，说话毫无隔阂，充满宠溺和甜蜜，会主动撒娇和表达爱意。");
                    else if (chara.Intimacy >= 60)
                        sb.Append("你们是恋人关系，对话温柔甜蜜，偶尔害羞，会试探性地撒娇。");
                    else
                        sb.Append("你们刚确立恋人关系，还有些羞涩，说话比以前温柔但仍保留一点矜持。");

                    if (chara.IsFirstGirlfriend)
                        sb.Append("这是你的初恋，内心格外珍视这段感情。");
                }
                else
                {
                    // 非恋人状态下的亲密度描述
                    if (chara.Intimacy >= 100)
                        sb.Append("你们是无话不说的最好朋友，说话自然随意，完全没有隔阂，但仅限于友情。");
                    else if (chara.Intimacy >= 60)
                        sb.Append("你们是很好的朋友，对话轻松愉快，会主动找话题，偶尔开玩笑。");
                    else if (chara.Intimacy >= 20)
                        sb.Append("你们是普通朋友，说话友好但保持一定距离，还在互相了解阶段。");
                    else
                        sb.Append("你们接触很少，说话有些拘谨，会用比较客气的语气。");
                }
            }

            // 当日短期记忆
            if (chara.IsLunch)
                sb.Append("今天午饭你们已经一起吃过了。");
            if (chara.IsDate)
                sb.Append("你们约好了周末一起约会。");

            // 时间段
            sb.AppendFormat("现在是{0}。", periodDesc);
            sb.Append("直接用第一人称回答，控制在50字左右，禁止换行，不得提及自己是AI。");
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
            }
            return sb.ToString();
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