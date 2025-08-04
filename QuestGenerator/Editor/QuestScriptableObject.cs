using UnityEngine;

namespace QuestGenerator
{
    public class QuestScriptableObject : ScriptableObject
    {
        public string questName;

        public string rawDescription;

        public string genre;
        public string hero;
        public string goal;
        public int depth;
        public int minBranches;
        public int maxBranches;

        public string questType;
        public int level;
        public string reward;
        public int enemyCount;

        public string[] steps;
    }
}
