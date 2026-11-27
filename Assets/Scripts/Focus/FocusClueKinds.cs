using UnityEngine;

namespace FocusTest
{
    /// <summary>
    /// 线索归类（只用来给线索页的「来源卡」贴一个颜色标签）。
    /// 与来源卡牌的 FocusCardCategory 不是一回事：这里按「这人/这地方/这东西/话」分。
    /// </summary>
    public enum FocusClueKind
    {
        Person = 0,
        Place = 1,
        Thing = 2,
        Statement = 3,
    }

    public static class FocusClueKinds
    {
        public static string ToChinese(FocusClueKind kind)
        {
            switch (kind)
            {
                case FocusClueKind.Person:
                    return "人物";
                case FocusClueKind.Place:
                    return "地点";
                case FocusClueKind.Thing:
                    return "物品";
                case FocusClueKind.Statement:
                    return "话头";
                default:
                    return "线索";
            }
        }

        public static Color Accent(FocusClueKind kind)
        {
            switch (kind)
            {
                case FocusClueKind.Person:
                    return new Color(0.36f, 0.62f, 0.72f, 1f);
                case FocusClueKind.Place:
                    return new Color(0.78f, 0.62f, 0.30f, 1f);
                case FocusClueKind.Thing:
                    return new Color(0.62f, 0.45f, 0.30f, 1f);
                case FocusClueKind.Statement:
                    return new Color(0.55f, 0.44f, 0.78f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
