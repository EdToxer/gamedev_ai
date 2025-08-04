using System;

namespace QuestGenerator
{
    [Serializable]
    public class LocalChoice
    {
        public int index;
        public string text;
        public string finish_reason;
        // public object logprobs;    // при желании можно его тоже описать
    }
}
