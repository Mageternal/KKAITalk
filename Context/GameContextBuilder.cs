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
        public static List<ChatMessage> BuildMessages(CharacterContext chara, string userInput)
        {
            string systemPrompt = BuildSystemPrompt(chara);
            return new List<ChatMessage>
            {
                new ChatMessage { role = "system", content = systemPrompt },
                new ChatMessage { role = "user",   content = userInput }
            };
        }

        private static string BuildSystemPrompt(CharacterContext chara)
        {
            string personalityDesc = TranslatePersonality(chara.Personality);
            string periodDesc = TranslatePeriod(chara.CurrentPeriod);

            return chara.Name + "是一个" + personalityDesc + "的女生。" +
                   "现在是" + periodDesc + "。" +
                   "你就是" + chara.Name + "，直接用第一人称回答，" +
                   "不超过80字，不得提及自己是AI。";
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