using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor.Networking;

namespace QuestGenerator
{
    public class QuestToolWindow : EditorWindow
    {
        // Вся «модель» окна в одном объекте
        [SerializeField] private QuestData data = new QuestData();
        // SerializedObject для Undo/Redo
        private SerializedObject so;

        // Путь для экспорта .txt и импорта .json
        [SerializeField] private string txtSavePath = "Assets/Quests/quest.txt";
        [SerializeField] private string jsonLoadPath = "Assets/Quests/quest.json";

        // Храним последний экспортированный текст, чтобы при закрытии
        // понимать — есть ли несохранённые изменения
        private string lastExportedText = "";

        // ≈===== новые поля для AI API =====
        [SerializeField] private string aiApiUrl = "https://your.api.endpoint/generate";
        [SerializeField] private string aiApiKey = "";
        private bool isGenerating = false;
        private string aiError = "";

        // Имена контролов в том порядке, в каком хотим «шагать» стрелками
        private readonly List<string> _names = new List<string>
        {
            "RawDesc",
            "Genre",
            "Hero",
            "Goal",
            "Depth",
            "MinBranches",
            "MaxBranches",
            "QuestType",
            "Level",
            "Reward",
            "EnemyCount", // после основных полей — добавим имена ещё и для AI-блока:
            "ApiUrl", 
            "ApiKey", 
            "BtnGenerate"
        };

        // Наш навигатор
        private ControlNavigator _navigator;

        [MenuItem("Tools/Quest Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuestToolWindow>("Quest Tool");
            window.minSize = new Vector2(400, 400);
        }

        private void OnEnable()
        {
            // Если data ещё не создан — создаём
            if (data == null)
                data = new QuestData();

            // Создаём SerializedObject на основе data и этого окна
            so = new SerializedObject(this);
            so.Update();

            _navigator = new ControlNavigator(_names);
        }

        private void OnGUI()
        {
            // Сначала обработка стрелок
            _navigator.HandleArrowKeys();

            // Обновляем сериализацию для Undo/Redo
            so.Update();

            // Сбрасываем счётчик назначения имён перед началом рисования полей
            _navigator.ResetAssignment();

            // Рисуем все поля через PropertyField
            EditorGUILayout.LabelField("Настройки экспорта квеста", EditorStyles.boldLabel);
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(
                so.FindProperty("data.useStructuredMode"),
                new GUIContent("Форма", "Off = свободный текст, On = набор полей")
            );
            GUILayout.Space(8);

            if (!data.useStructuredMode)
                DrawRawMode();
            else
                DrawStructuredMode();

            GUILayout.Space(12);
            DrawExportSection();
            GUILayout.Space(12);
            DrawImportSection();

            // Блок API Generator
            GUILayout.Space(18);
            DrawAPISection();

            // Применяем изменения к SerializedObject
            so.ApplyModifiedProperties();
        }

        private void DrawRawMode()
        {
            _navigator.SetNextControlName();
            EditorGUILayout.LabelField("Свободное описание квеста", EditorStyles.miniBoldLabel);
            var prop = so.FindProperty("data.rawDescription");
            EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Height(150));
        }

        private void DrawStructuredMode()
        {
            EditorGUILayout.LabelField("Структурированные поля квеста", EditorStyles.miniBoldLabel);

            EditorGUILayout.LabelField("Обязательные", EditorStyles.label);
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.genre"), new GUIContent("Жанр"));
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.hero"), new GUIContent("Главный герой"));
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.goal"), new GUIContent("Цель"));
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.depth"), new GUIContent("Глубина квеста"));

            EditorGUILayout.BeginHorizontal();
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.minBranches"), new GUIContent("Мин. развилок"));
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.maxBranches"), new GUIContent("Макс. развилок"));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            EditorGUILayout.LabelField("Опциональные", EditorStyles.label);
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.questType"), new GUIContent("Тип квеста"));
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.level"), new GUIContent("Уровень"));
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.reward"), new GUIContent("Награда"));
            _navigator.SetNextControlName();
            EditorGUILayout.PropertyField(so.FindProperty("data.enemyCount"), new GUIContent("Кол-во противников"));
        }

        private void DrawExportSection()
        {
            EditorGUILayout.LabelField("Экспорт в .txt", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            txtSavePath = EditorGUILayout.TextField("Путь к .txt", txtSavePath);
            if (GUILayout.Button("…", GUILayout.MaxWidth(30)))
            {
                string folder = Path.GetDirectoryName(txtSavePath);
                string file = Path.GetFileName(txtSavePath);
                string picked = EditorUtility.SaveFilePanel("Сохранить .txt", folder, file, "txt");
                if (!string.IsNullOrEmpty(picked))
                    txtSavePath = FileUtil.GetProjectRelativePath(picked);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Экспортировать .txt"))
                ExportTxt();
        }

        private void DrawImportSection()
        {
            EditorGUILayout.LabelField("Импорт JSON от нейросети", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            jsonLoadPath = EditorGUILayout.TextField("Путь к .json", jsonLoadPath);
            if (GUILayout.Button("…", GUILayout.MaxWidth(30)))
            {
                string picked = EditorUtility.OpenFilePanel("Выбрать .json", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(picked))
                    jsonLoadPath = FileUtil.GetProjectRelativePath(picked);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Импортировать .json в Unity"))
                ImportJson();
        }

        private void DrawAPISection()
        {
            // === БЛОК API GENERATION ===
            EditorGUILayout.LabelField("2. Генерация через AI-API", EditorStyles.boldLabel);

            _navigator.SetNextControlName(); // "ApiUrl"
            aiApiUrl = EditorGUILayout.TextField("API URL", aiApiUrl);

            _navigator.SetNextControlName(); // "ApiKey"
            aiApiKey = EditorGUILayout.PasswordField("API Key (Bearer)", aiApiKey);

            EditorGUILayout.Space(5);

            _navigator.SetNextControlName(); // "BtnGenerate"
            GUI.enabled = !isGenerating;
            if (GUILayout.Button("Сгенерировать квест через AI"))
            {
                aiError = "";
                if (string.IsNullOrEmpty(aiApiUrl))
                    aiError = "Не задан URL API.";
                else
                    StartAIGeneration();
            }
            GUI.enabled = true;

            if (isGenerating)
                EditorGUILayout.LabelField("Генерация… подождите", EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(aiError))
                EditorGUILayout.HelpBox(aiError, MessageType.Error);

            EditorGUILayout.Space(15);
        }

        private void ExportTxt()
        {
            // Собираем итоговый текст в зависимости от режима
            string output = BuildOutputText();

            if (string.IsNullOrEmpty(output))
            {
                EditorUtility.DisplayDialog("Ошибка", "Нет данных для экспорта!", "OK");
                return;
            }

            // Запись файла
            string fullPath = Path.Combine(Application.dataPath, txtSavePath.Substring("Assets/".Length));
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, output);
            AssetDatabase.Refresh();

            lastExportedText = output;  // Сохраняем «чистое» состояние
            EditorUtility.DisplayDialog("Успех", $".txt сохранён по пути:\n{txtSavePath}", "OK");
        }

        private string BuildOutputText()
        {
            if (!data.useStructuredMode)
            {
                return data.rawDescription?.Trim();
            }
            // Структурированный режим
            if (string.IsNullOrEmpty(data.genre) ||
                string.IsNullOrEmpty(data.hero) ||
                string.IsNullOrEmpty(data.goal) ||
                data.depth <= 0 ||
                data.minBranches < 0 ||
                data.maxBranches < data.minBranches)
            {
                EditorUtility.DisplayDialog("Ошибка", "Заполните корректно все обязательные поля.", "OK");
                return null;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Жанр: {data.genre}");
            sb.AppendLine($"Главный герой: {data.hero}");
            sb.AppendLine($"Цель: {data.goal}");
            sb.AppendLine($"Глубина квеста: {data.depth}");
            sb.AppendLine($"Диапазон развилок: {data.minBranches}–{data.maxBranches}");

            if (!string.IsNullOrEmpty(data.questType)) sb.AppendLine($"Тип квеста: {data.questType}");
            if (data.level > 0) sb.AppendLine($"Уровень: {data.level}");
            if (!string.IsNullOrEmpty(data.reward)) sb.AppendLine($"Награда: {data.reward}");
            if (data.enemyCount > 0) sb.AppendLine($"Кол-во противников: {data.enemyCount}");

            return sb.ToString().TrimEnd();
        }

        private void ImportJson()
        {
            string fullPath = Path.Combine(Application.dataPath, jsonLoadPath.Substring("Assets/".Length));
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Ошибка", $"JSON не найден:\n{jsonLoadPath}", "OK");
                return;
            }

            string json = File.ReadAllText(fullPath);
            var imported = JsonUtility.FromJson<QuestData>(json);
            if (imported == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Не удалось распарсить JSON.", "OK");
                return;
            }

            // Копируем данные
            data = imported;
            lastExportedText = BuildOutputText(); // считаем, что импорт — «сохранённое» состояние
            OnEnable(); // пересоздаём SerializedObject
            Repaint();

            // Создаем ScriptableObject
            var asset = ScriptableObject.CreateInstance<QuestScriptableObject>();
            // Заполняем
            asset.rawDescription = data.rawDescription;
            asset.genre = data.genre;
            asset.hero = data.hero;
            asset.goal = data.goal;
            asset.depth = data.depth;
            asset.minBranches = data.minBranches;
            asset.maxBranches = data.maxBranches;
            asset.questType = data.questType;
            asset.level = data.level;
            asset.reward = data.reward;
            asset.enemyCount = data.enemyCount;
            asset.steps = data.steps;

            string assetName = data.useStructuredMode
                ? $"{data.genre}_{data.hero}.asset"
                : "QuestFromRaw.asset";
            string assetPath = $"Assets/Quests/{assetName}";

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Успех", $"ScriptableObject создан:\n{assetPath}", "OK");
        }

        private void StartAIGeneration()
        {
            isGenerating = true;
#if UNITY_EDITOR && UNITY_2019_1_OR_NEWER
            EditorCoroutineRunner.Start(GenerateCoroutine());
#endif
        }

#if UNITY_EDITOR && UNITY_2019_1_OR_NEWER
        private IEnumerator GenerateCoroutine()
        {
            // 1) Формируем текст запроса
            string prompt = data.useStructuredMode
                ? BuildStructuredPrompt()
                : data.rawDescription;

            // 2) Делаем POST
            UnityWebRequest req = new UnityWebRequest(aiApiUrl, "POST");
            byte[] body = System.Text.Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(new { prompt = prompt })
            );
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(aiApiKey))
                req.SetRequestHeader("Authorization", "Bearer " + aiApiKey);

            yield return req.SendWebRequest();

            isGenerating = false;
            if (req.result == UnityWebRequest.Result.Success)
            {
                ProcessAIResult(req.downloadHandler.text);
            }
            else
            {
                aiError = "Ошибка при запросе AI: " + req.error;
            }

            Repaint();
        }
#endif

        // Собираем «структурированный» промпт
        private string BuildStructuredPrompt()
        {
            return $"Create quest:\n" +
                   $"- Genre: {data.genre}\n" +
                   $"- Hero: {data.hero}\n" +
                   $"- Goal: {data.goal}\n" +
                   $"- Depth: {data.depth}\n" +
                   $"- Branches: {data.minBranches}..{data.maxBranches}\n" +
                   $"- Type: {data.questType}\n" +
                   $"- Level: {data.level}\n" +
                   $"- Reward: {data.reward}\n" +
                   $"- Enemies: {data.enemyCount}";
        }

        // Разбираем ответ AI и записываем в data
        private void ProcessAIResult(string json)
        {
            try
            {
                // Предполагаем, что AI вернул JSON близкий к QuestData
                var fromAi = JsonUtility.FromJson<QuestData>(json);
                if (fromAi != null)
                {
                    data = fromAi;
                }
                else
                {
                    // Или просто вставляем сырой текст в rawDescription
                    data.rawDescription = json;
                }
            }
            catch
            {
                data.rawDescription = json;
            }
        }

        private void OnDestroy()
        {
            // При закрытии окна, если есть несохранённый текст и это не смена плеймода/компиляция
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            string current = BuildOutputText();
            if (string.IsNullOrEmpty(current) || current == lastExportedText)
                return;

            int opt = EditorUtility.DisplayDialogComplex(
                "Несохранённые данные",
                "У вас есть несохранённые данные. Сохранить перед закрытием?",
                "Сохранить",
                "Не сохранять",
                "Отмена"
            );

            if (opt == 0)
            {
                // Сохраняем и даём окну закрыться
                ExportTxt();
            }
            else if (opt == 2)
            {
                // Отмена — открываем окно заново
                EditorApplication.delayCall += ShowWindow;
            }
            // opt == 1 — «Не сохранять» — закрываем без вопросов дальше
        }
    }
}
