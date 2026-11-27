using System.Collections.Generic;
using UnityEngine;

namespace FocusTest
{
    /// <summary>案件板上的问题空槽：案件 / 凶手 / 凶器 / 物证 / 人证。</summary>
    [CreateAssetMenu(fileName = "FocusQuestion", menuName = "Focus Lab/Notebook/Question")]
    public sealed class FocusQuestionData : ScriptableObject
    {
        [SerializeField] private string _questionId = "case";
        [SerializeField] private string _title = "案件";
        [SerializeField, TextArea(1, 3)] private string _hint = string.Empty;
        [Tooltip("空槽里显示的例子，只做提示，不参与判定。")]
        [SerializeField] private string _example = string.Empty;

        [Header("判定（本期 UI 不做判定，先留字段）")]
        [SerializeField] private List<FocusClueData> _acceptedClues = new List<FocusClueData>();

        public string QuestionId => _questionId;
        public string Title => _title;
        public string Hint => _hint;
        public string Example => _example;
        public IReadOnlyList<FocusClueData> AcceptedClues => _acceptedClues;

        public bool Accepts(FocusClueData clue)
        {
            if (clue == null || _acceptedClues == null)
            {
                return false;
            }

            for (int i = 0; i < _acceptedClues.Count; i++)
            {
                if (_acceptedClues[i] == clue)
                {
                    return true;
                }
            }

            return false;
        }

        public void Configure(string questionId, string title, string hint, string example, IEnumerable<FocusClueData> accepted)
        {
            _questionId = questionId;
            _title = title;
            _hint = hint;
            _example = example;
            _acceptedClues = accepted != null ? new List<FocusClueData>(accepted) : new List<FocusClueData>();
        }
    }
}
