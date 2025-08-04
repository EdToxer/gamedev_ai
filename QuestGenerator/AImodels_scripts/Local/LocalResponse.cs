using System;

namespace QuestGenerator
{
    [Serializable]
    public class LocalResponse
    {
        public LocalChoice[] choices;
        public LocalUsage usage;
        // public Dictionary<string,object> stats; // если нужно
    }
}
