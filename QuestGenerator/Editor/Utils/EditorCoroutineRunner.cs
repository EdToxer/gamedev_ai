using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace QuestGenerator
{
    /// <summary>
    /// Простой Editor-раннер для IEnumerator-корутин.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorCoroutineRunner
    {
        // все запущенные корутины
        private static readonly List<IEnumerator> s_Coroutines = new List<IEnumerator>();

        static EditorCoroutineRunner()
        {
            // вызывается каждый кадр редактора
            EditorApplication.update += Update;
        }

        public static void Start(IEnumerator coroutine)
        {
            if (coroutine == null) return;
            s_Coroutines.Add(coroutine);
        }

        private static void Update()
        {
            for (int i = s_Coroutines.Count - 1; i >= 0; i--)
            {
                IEnumerator c = s_Coroutines[i];
                try
                {
                    if (!c.MoveNext())
                        s_Coroutines.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"EditorCoroutine exception: {ex}");
                    s_Coroutines.RemoveAt(i);
                }
            }
        }
    }
}
