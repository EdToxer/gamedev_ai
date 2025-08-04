using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

namespace QuestGenerator
{
    public class QuestGeneratorToolWindow : EditorWindow
    {
        // === Пулька для вкладок ===
        private int _selectedTab = 0;
        private static readonly string[] _tabs = { "Генерация", "Текст", "Импорт" };

        // === Данные квеста ===
        [SerializeField] private QuestData data = new QuestData();
        private SerializedObject so;

        // === Настройки AI-API ===
        [SerializeField] private string aiApiUrl = "http://127.0.0.1:1234/v1/completions";
        [SerializeField] private string aiApiKey = "Local host";
        private bool isGenerating = false;
        private string aiError = "";

        // === Пути для .txt и .json ===
        [SerializeField] private string txtSavePath = "Assets/Quests/quest.txt";
        [SerializeField] private string jsonLoadPath = "Assets/Quests/quest.json";

        // === Навигаторы для каждой вкладки ===
        private ControlNavigator _genNav, _txtNav, _impNav;

        private string lastExportedText = "";

        private string generatedJson = "";
        private Vector2 generatedJsonScroll;
        private bool generationDone = false;

        [MenuItem("Tools/Quest Tool v0.2")]
        public static void ShowWindow()
        {
            var w = GetWindow<QuestGeneratorToolWindow>("Quest Tool");
            w.minSize = new Vector2(450, 550);
        }

        private void OnEnable()
        {
            so = new SerializedObject(this);

            // имена контролов для навигации в каждом табе
            _genNav = new ControlNavigator(new[]
            {
                "Gen_UseStruct",
                "Gen_RawDesc",
                "Gen_Genre", 
                "Gen_Hero", 
                "Gen_Goal", 
                "Gen_Depth",
                "Gen_MinBranches", 
                "Gen_MaxBranches",
                "Gen_QuestType", 
                "Gen_Level", 
                "Gen_Reward", 
                "Gen_EnemyCount",
                "Gen_ApiUrl", 
                "Gen_ApiKey", 
                "Gen_BtnGenerate"
            });

            _txtNav = new ControlNavigator(new[]
            {
                "Txt_RawDesc", 
                "Txt_Path", 
                "Txt_BtnExport"
            });

            _impNav = new ControlNavigator(new[]
            {
                "Imp_Path", 
                "Imp_BtnImport"
            });
        }

        private void OnGUI()
        {
            so.Update();

            // 1) Сам выбор вкладки
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);

            GUILayout.Space(10);

            // 2) В зависимости от вкладки — рисуем разный UI
            switch (_selectedTab)
            {
                case 0:
                    DrawGenerationTab();
                    break;
                case 1:
                    DrawTextTab();
                    break;
                case 2:
                    DrawImportTab();
                    break;
            }

            so.ApplyModifiedProperties();
        }

        #region Generation Tab
        private void DrawGenerationTab()
        {
            _genNav.HandleArrowKeys();
            _genNav.ResetAssignment();

            DrawStructuredMode(_genNav);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. AI-Генерация", EditorStyles.boldLabel);
            _genNav.SetNextControlName();
            aiApiUrl = EditorGUILayout.TextField("API URL", aiApiUrl);
            _genNav.SetNextControlName();
            aiApiKey = EditorGUILayout.PasswordField("API Key", aiApiKey);

            EditorGUILayout.Space(5);
            _genNav.SetNextControlName();
            GUI.enabled = !isGenerating;
            if (GUILayout.Button("Сгенерировать"))
            {
                aiError = "";
                generatedJson = "";      // сброс предыдущего результата
                generationDone = false;
                if (string.IsNullOrEmpty(aiApiUrl))
                    aiError = "Укажите API URL.";
                else
                    StartAIGeneration();
            }
            GUI.enabled = true;

            if (isGenerating)
                EditorGUILayout.LabelField("Генерация…", EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(aiError))
                EditorGUILayout.HelpBox(aiError, MessageType.Error);

            // Если генерация завершена и у нас есть JSON — показываем его
            if (generationDone && !string.IsNullOrEmpty(generatedJson))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Результат AI-Генерации (JSON)", EditorStyles.boldLabel);

                generatedJsonScroll = EditorGUILayout.BeginScrollView(
                    generatedJsonScroll,
                    GUILayout.Height(200),
                    GUILayout.ExpandWidth(true)
                );
                EditorGUILayout.TextArea(generatedJson, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Сохранить в .json"))
                {
                    SaveGeneratedJson();
                }
                if (GUILayout.Button("Очистить"))
                {
                    generatedJson = "";
                    generationDone = false;
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
        }
        #endregion

        #region Text Tab
        private void DrawTextTab()
        {
            _txtNav.HandleArrowKeys();
            _txtNav.ResetAssignment();

            DrawStructuredMode(_txtNav);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("2. Сохранить в файл .txt", EditorStyles.boldLabel);

            _txtNav.SetNextControlName();
            txtSavePath = EditorGUILayout.TextField("Path", txtSavePath);
            if (GUILayout.Button("Выбрать…", GUILayout.MaxWidth(100)))
            {
                string folder = Path.GetDirectoryName(txtSavePath);
                string file = Path.GetFileName(txtSavePath);
                string picked = EditorUtility.SaveFilePanel(
                    "Сохранить .txt", folder, file, "txt");
                if (!string.IsNullOrEmpty(picked))
                    txtSavePath = FileUtil.GetProjectRelativePath(picked);
            }

            EditorGUILayout.Space(5);
            _txtNav.SetNextControlName();
            if (GUILayout.Button("Экспортировать .txt"))
            {
                ExportTxt();
            }

            GUILayout.FlexibleSpace();
        }
        #endregion

        #region Import Tab
        private void DrawImportTab()
        {
            _impNav.HandleArrowKeys();
            _impNav.ResetAssignment();

            EditorGUILayout.LabelField("Импорт из .json", EditorStyles.boldLabel);

            _impNav.SetNextControlName();
            jsonLoadPath = EditorGUILayout.TextField("Path", jsonLoadPath);
            if (GUILayout.Button("Выбрать…", GUILayout.MaxWidth(100)))
            {
                string picked = EditorUtility.OpenFilePanel(
                    "Открыть .json", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(picked))
                    jsonLoadPath = FileUtil.GetProjectRelativePath(picked);
            }

            EditorGUILayout.Space(5);
            _impNav.SetNextControlName();
            if (GUILayout.Button("Импортировать .json"))
            {
                ImportJson();
            }

            GUILayout.FlexibleSpace();
        }
        #endregion

        private void DrawStructuredMode(ControlNavigator nav)
        {
            EditorGUILayout.LabelField("1. Описание квеста", EditorStyles.boldLabel);
            nav.SetNextControlName();
            EditorGUILayout.PropertyField(
                so.FindProperty("data.useStructuredMode"),
                new GUIContent("Структурированная форма"));
            EditorGUILayout.Space(5);

            if (!data.useStructuredMode)
            {
                nav.SetNextControlName();
                EditorGUILayout.PropertyField(
                    so.FindProperty("data.rawDescription"),
                    new GUIContent("Описание (текст)"),
                    GUILayout.Height(120));
            }
            else
            {
                // структура
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.genre"), new GUIContent("Жанр"));
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.hero"), new GUIContent("Герой"));
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.goal"), new GUIContent("Цель"));
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.depth"), new GUIContent("Глубина"));
                EditorGUILayout.BeginHorizontal();
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.minBranches"), new GUIContent("Мин. ветв."));
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.maxBranches"), new GUIContent("Макс. ветв."));
                EditorGUILayout.EndHorizontal();
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.questType"), new GUIContent("Тип"));
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.level"), new GUIContent("Уровень"));
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.reward"), new GUIContent("Награда"));
                nav.SetNextControlName(); EditorGUILayout.PropertyField(
                    so.FindProperty("data.enemyCount"), new GUIContent("Врагов"));
            }
        }

        #region AI Generation Coroutine
        private void StartAIGeneration()
        {
            isGenerating = true;
            EditorCoroutineRunner.Start(GenerateCoroutine());
        }

        private IEnumerator GenerateCoroutine()
        {
            Debug.Log("[Gen] START GenerateCoroutine");

            // ——— 0. Прекращаем, если окно уже уничтожено
            if (this == null)
            {
                Debug.LogWarning("[Gen] Окно разрушено, прерываем корутину");
                yield break;
            }

            // ——— 1. Проверяем data
            if (data == null)
            {
                Debug.LogError("[Gen] data == null! Создаю новый QuestData.");
                data = new QuestData();
            }
            Debug.Log("[Gen] data не null");

            isGenerating = true;
            aiError = "";

            // ——— 2. Формируем prompt
            string prompt = data.useStructuredMode
                ? BuildStructuredPrompt()
                : data.rawDescription;
            Debug.Log($"[Gen] Prompt сформирован: \"{prompt.Substring(0, Mathf.Min(30, prompt.Length))}...\"");

            // ——— 3. Строим запрос
            var payload = new LocalRequest
            {
                model = "DeepSeek-R1-Distill-Qwen-7B-Q4_K_M.gguf",
                prompt = prompt,
                max_tokens = 512,
                temperature = 0.4f,
                n = 1,
                stream = false
            };
            string jsonBody = JsonUtility.ToJson(payload);
            Debug.Log($"[Gen] JSON тела: {jsonBody}");

            string url = aiApiUrl;
            using var uwr = new UnityWebRequest(url, "POST");
            uwr.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.timeout = 120;

            // ——— 4. Отправляем
            Debug.Log("[Gen] Отправляю запрос...");
            var t0 = Time.realtimeSinceStartup;
            var op = uwr.SendWebRequest();

            // ждём, пока UnityWebRequest не завершит работу
            while (!op.isDone)
                yield return null;

            var dt = Time.realtimeSinceStartup - t0;
            Debug.Log($"Запрос длился {dt:F2} сек, responseCode={uwr.responseCode}, error={uwr.error}");

            // ——— 5. Проверяем сам запрос
            Debug.Log($"[Gen] Ответ получен: code={uwr.responseCode}, error={uwr.error}");
            if (uwr.downloadHandler == null)
            {
                aiError = "downloadHandler == null";
                Debug.LogError("[Gen] downloadHandler == null");
                isGenerating = false;
                yield break;
            }

            string raw = uwr.downloadHandler.text;
            Debug.Log($"[Gen] raw.text = {raw}");

#if UNITY_2020_2_OR_NEWER
            if (uwr.result == UnityWebRequest.Result.ConnectionError ||
                uwr.result == UnityWebRequest.Result.ProtocolError)
#else
    if (uwr.isNetworkError || uwr.isHttpError)
#endif
            {
                aiError = $"Network/HTTP error {uwr.responseCode}: {uwr.error}";
                Debug.LogError("[Gen] " + aiError);
                isGenerating = false;
                Repaint();
                yield break;
            }

            // ——— 6. Парсим JSON
            LocalResponse resp = null;
            try
            {
                resp = JsonUtility.FromJson<LocalResponse>(raw);
            }
            catch (System.Exception ex)
            {
                aiError = "JSON parse error: " + ex.Message;
                Debug.LogError("[Gen] " + aiError + "\n" + ex);
                isGenerating = false;
                Repaint();
                yield break;
            }

            // ——— 7. Проверяем resp
            if (resp == null)
            {
                aiError = "CompletionsResponse == null";
                Debug.LogError("[Gen] " + aiError);
                Debug.Log("[Gen] Raw JSON:\n" + raw);
                isGenerating = false;
                Repaint();
                yield break;
            }
            if (resp.choices == null || resp.choices.Length == 0)
            {
                aiError = "resp.choices пуст или null";
                Debug.LogError("[Gen] " + aiError);
                isGenerating = false;
                Repaint();
                yield break;
            }

            // ——— 8. Применяем результат
            string resultText = resp.choices[0].text;
            if (resultText == null)
            {
                aiError = "resp.choices[0].text == null";
                Debug.LogError("[Gen] " + aiError);
            }
            else
            {
                data.rawDescription = resultText.Trim();
                Debug.Log("[Gen] Успешно записали data.rawDescription");
            }

            generatedJson = raw;
            generationDone = true;
            isGenerating = false;
            Repaint();
            Debug.Log("[Gen] END GenerateCoroutine");
        }

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

        private void ProcessAIResult(string json)
        {
            try
            {
                var fromAi = JsonUtility.FromJson<QuestData>(json);
                if (fromAi != null)
                    data = fromAi;
                else
                    data.rawDescription = json;
            }
            catch
            {
                data.rawDescription = json;
            }
        }
        #endregion

        private void SaveGeneratedJson()
        {
            // Стандартное окно выбора пути внутри проекта
            string path = EditorUtility.SaveFilePanelInProject(
                "Сохранить AI-JSON",
                "GeneratedQuest",
                "json",
                "Выберите куда сохранить сгенерированный JSON"
            );

            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                File.WriteAllText(path, generatedJson);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Успех", $"JSON сохранён по пути:\n{path}", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Не удалось записать файл JSON:\n{e}");
                EditorUtility.DisplayDialog("Ошибка", "Не удалось сохранить JSON. Смотрите консоль.", "OK");
            }
        }

        #region Txt Export & Json Import
        //private void ExportTxt()
        //{
        //    if (string.IsNullOrEmpty(data.rawDescription))
        //    {
        //        EditorUtility.DisplayDialog("Ошибка", "Описание пустое!", "OK");
        //        return;
        //    }

        //    string full = Path.Combine(
        //        Application.dataPath, txtSavePath.Substring("Assets/".Length));
        //    Directory.CreateDirectory(Path.GetDirectoryName(full));
        //    File.WriteAllText(full, data.rawDescription);
        //    AssetDatabase.Refresh();
        //    EditorUtility.DisplayDialog("Успех", $".txt сохранён:\n{txtSavePath}", "OK");
        //}

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
            string full = Path.Combine(
                Application.dataPath, jsonLoadPath.Substring("Assets/".Length));
            if (!File.Exists(full))
            {
                EditorUtility.DisplayDialog("Ошибка", $"Не найден:\n{jsonLoadPath}", "OK");
                return;
            }

            string json = File.ReadAllText(full);
            QuestData d = JsonUtility.FromJson<QuestData>(json);
            if (d == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Не распарсился JSON", "OK");
                return;
            }

            // создаём ScriptableObject
            var asset = ScriptableObject.CreateInstance<QuestScriptableObject>();
            asset.questName = d.questName;
            asset.steps = d.steps;
            string assetPath = $"Assets/Quests/{d.questName}.asset";
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Успех", $"Создано:\n{assetPath}", "OK");
        }
        #endregion
    }
}