using System;

namespace QuestGenerator
{
    [Serializable]
    public class LocalRequest
    {
        public string model;
        public string prompt;
        public float temperature = 0.4f;
        public int max_tokens = 512;
        public int n = 1;
        public bool stream = false;  // <— очень важно!
    }
}
