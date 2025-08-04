using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QuestGenerator
{
    /// <summary>
    /// Помогает назначать контролам имена и переключать фокус стрелками Up/Down.
    /// </summary>
    public class ControlNavigator
    {
        private readonly List<string> _controlNames;
        private int _lastAssignedIndex = -1;

        /// <summary>
        /// Создаёт навигатор с заранее известным упорядоченным списком имён контролов.
        /// </summary>
        public ControlNavigator(IEnumerable<string> controlNames)
        {
            _controlNames = new List<string>(controlNames);
        }

        /// <summary>
        /// Вызывать в начале OnGUI() — отловит Up/Down и переместит фокус.
        /// </summary>
        public void HandleArrowKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            int dir = 0;
            if (e.keyCode == KeyCode.DownArrow) dir = +1;
            else if (e.keyCode == KeyCode.UpArrow) dir = -1;

            if (dir == 0)
                return;

            string curr = GUI.GetNameOfFocusedControl();
            int idx = _controlNames.IndexOf(curr);
            if (idx < 0) idx = 0;
            else
            {
                idx = (idx + dir) % _controlNames.Count;
                if (idx < 0) idx += _controlNames.Count;
            }

            GUI.FocusControl(_controlNames[idx]);
            EditorGUI.FocusTextInControl(_controlNames[idx]);
            e.Use();
        }

        /// <summary>
        /// Сбрасывает внутренний счётчик, чтобы начать заново.
        /// </summary>
        public void ResetAssignment()
        {
            _lastAssignedIndex = -1;
        }

        /// <summary>
        /// Назначает следующее имя из списка _controlNames[_lastAssignedIndex+1] и возвращает его.
        /// Вызывать сразу перед каждым полем, которое должно участвовать в навигации.
        /// </summary>
        public string SetNextControlName()
        {
            _lastAssignedIndex++;
            if (_lastAssignedIndex >= _controlNames.Count)
                _lastAssignedIndex = 0;
            var name = _controlNames[_lastAssignedIndex];
            GUI.SetNextControlName(name);
            return name;
        }
    }
}
