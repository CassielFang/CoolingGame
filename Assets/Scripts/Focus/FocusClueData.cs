using UnityEngine;

namespace FocusTest
{
    /// <summary>
    /// 一条线索的定义。实例放在 Assets/Data/Clues。
    /// 线索页按「来源卡牌」分组：同一条来源下的线索条目按获得的先后（旧→新）排开。
    /// </summary>
    [CreateAssetMenu(fileName = "FocusClue", menuName = "Focus Lab/Notebook/Clue")]
    public sealed class FocusClueData : ScriptableObject
    {
        [Header("身份")]
        [SerializeField] private string _clueId = "clue_01";
        [Tooltip("来源卡牌：线索页会把这组线索的标题设成这张卡的卡名。")]
        [SerializeField] private FocusCardData _sourceCard;

        [Header("内容")]
        [Tooltip("线索条目上显示的短语或短句。")]
        [SerializeField, TextArea(1, 3)] private string _text = string.Empty;
        [Tooltip("展开线索页时看到的补充说明。")]
        [SerializeField, TextArea(2, 6)] private string _detail = string.Empty;
        [SerializeField] private FocusClueKind _kind = FocusClueKind.Thing;

        public string ClueId => _clueId;
        public FocusCardData SourceCard => _sourceCard;
        public string Text => _text;
        public string Detail => _detail;
        public FocusClueKind Kind => _kind;

        /// <summary>来源卡的卡名；没配来源就退化成「来源未记」。</summary>
        public string SourceName => _sourceCard != null ? _sourceCard.DisplayName : "来源未记";

        public void Configure(string clueId, FocusCardData sourceCard, string text, string detail, FocusClueKind kind)
        {
            _clueId = clueId;
            _sourceCard = sourceCard;
            _text = text;
            _detail = detail;
            _kind = kind;
        }
    }
}
