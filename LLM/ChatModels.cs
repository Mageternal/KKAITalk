using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KKAITalk.LLM
{
    [Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class ChatRequest
    {
        public string model;
        public List<ChatMessage> messages;
        public bool thinking = false;
        public int max_tokens = 300;
    }

    [Serializable]
    public class ChatChoice
    {
        public ChatMessage message;
        public string finish_reason;
    }

    [Serializable]
    public class ChatResponse
    {
        public List<ChatChoice> choices;
    }
}