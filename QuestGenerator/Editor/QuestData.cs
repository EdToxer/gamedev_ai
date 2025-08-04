using UnityEngine;

namespace QuestGenerator
{
    /// <summary>
    /// Простой класс для десериализации JSON
    /// </summary>
    [System.Serializable]
    public class QuestData
    {
        [SerializeField] public string questName;

        [SerializeField] public bool useStructuredMode;

        // Для свободного текста
        [SerializeField] public string rawDescription;

        // Для структурированой формы
        [SerializeField] public string genre;               // Жанр
        [SerializeField] public string hero;                // Главный герой
        [SerializeField] public string goal;                // Цель
        [SerializeField] public int depth;                  // Глубина квеста
        [SerializeField] public int minBranches;            // Мин. развилки
        [SerializeField] public int maxBranches;            // Макс. развилки

        // Опциональные
        [SerializeField] public string questType;           // Тип квеста
        [SerializeField] public int level;                  // Уровень
        [SerializeField] public string reward;              // Награда
        [SerializeField] public int enemyCount;             // Кол-во противников

        [SerializeField] public string[] steps;             // Для импорта JSON
    }
}
