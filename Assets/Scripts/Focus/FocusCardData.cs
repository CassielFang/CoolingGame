using UnityEngine;

namespace FocusTest
{
    /// <summary>卡牌分类：角色 / 地点 / 物品。</summary>
    public enum FocusCardCategory
    {
        Character = 0,
        Location = 1,
        Item = 2,
    }

    /// <summary>分类的中文名与配色，场景构建和字幕都从这里取，避免到处写字符串。</summary>
    public static class FocusCardCategories
    {
        public static string ToChinese(FocusCardCategory category)
        {
            switch (category)
            {
                case FocusCardCategory.Character:
                    return "角色";
                case FocusCardCategory.Location:
                    return "地点";
                case FocusCardCategory.Item:
                    return "物品";
                default:
                    return "未知";
            }
        }

        public static Color Accent(FocusCardCategory category)
        {
            switch (category)
            {
                case FocusCardCategory.Character:
                    return new Color(0.184f, 0.714f, 0.659f, 1f); // 青
                case FocusCardCategory.Location:
                    return new Color(0.847f, 0.631f, 0.227f, 1f); // 赤金
                case FocusCardCategory.Item:
                    return new Color(0.545f, 0.435f, 0.839f, 1f); // 紫
                default:
                    return Color.white;
            }
        }

        /// <summary>是不是人物（只有人物卡会弹立绘、才当行动的发起方）。</summary>
        public static bool IsCharacter(FocusCardCategory category)
        {
            return category == FocusCardCategory.Character;
        }
    }

    /// <summary>
    /// 一张测试卡牌的静态数据。实例放在 Assets/Data，场景里的卡牌引用它来取图、名字与分类。
    /// </summary>
    [CreateAssetMenu(fileName = "FocusCard", menuName = "Focus Lab/Card Data")]
    public sealed class FocusCardData : ScriptableObject
    {
        [SerializeField] private string _cardId = "focus_01";
        [SerializeField] private string _displayName = "测试卡";
        [SerializeField, TextArea(2, 5)] private string _description = string.Empty;
        [SerializeField] private FocusCardCategory _category = FocusCardCategory.Item;
        [SerializeField] private Sprite _frontSprite;
        [SerializeField] private Sprite _backSprite;

        [Header("人物立绘（只有角色卡用得上）")]
        [SerializeField] private Sprite _portrait;
        [Tooltip("立绘的区分色：占位美术只有一张图，不同人物靠这个颜色区分。")]
        [SerializeField] private Color _portraitTint = Color.white;

        public string CardId => _cardId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public FocusCardCategory Category => _category;
        public string CategoryName => FocusCardCategories.ToChinese(_category);
        public Sprite FrontSprite => _frontSprite;
        public Sprite BackSprite => _backSprite;
        public Sprite Portrait => _portrait;
        public Color PortraitTint => _portraitTint;

        /// <summary>有立绘图的人物卡才会在左侧弹立绘。</summary>
        public bool HasPortrait => _portrait != null;

        /// <summary>由编辑器构建脚本调用，把美术与文字灌进来。</summary>
        public void Configure(
            string cardId,
            string displayName,
            string description,
            FocusCardCategory category,
            Sprite front,
            Sprite back)
        {
            Configure(cardId, displayName, description, category, front, back, null, Color.white);
        }

        /// <summary>带人物立绘的版本。</summary>
        public void Configure(
            string cardId,
            string displayName,
            string description,
            FocusCardCategory category,
            Sprite front,
            Sprite back,
            Sprite portrait,
            Color portraitTint)
        {
            _cardId = cardId;
            _displayName = displayName;
            _description = description;
            _category = category;
            _frontSprite = front;
            _backSprite = back;
            _portrait = portrait;
            _portraitTint = portraitTint;
        }
    }
}
