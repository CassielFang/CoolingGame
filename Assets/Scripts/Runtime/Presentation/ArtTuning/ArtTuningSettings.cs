using UnityEngine;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 美术调试参数的落地点（ScriptableObject）。原本写死在代码里的视觉数值都搬到这里，
    /// 代码只读它，Tools/美术调试 窗口只改它。
    ///
    /// 约定（以后加新的调试对象照这里来）：
    /// - 这个类只当**目录**，每个调试对象一组自己的设置，各有出厂默认值。
    /// - 每组都有独立的「启用」开关：没启用的组，运行时完全不接管，回落到代码里的原值。
    /// - 资产放在 Resources/ArtTuningSettings.asset；找不到就整份用出厂默认值（并给一次警告）。
    /// - 运行时要每帧读（窗口在编辑器里直接改资产），用 <see cref="Current"/>。
    /// </summary>
    [CreateAssetMenu(menuName = "CoolingGame/美术调试参数", fileName = "ArtTuningSettings")]
    public sealed class ArtTuningSettings : ScriptableObject
    {
        /// <summary>Resources 下的路径（不带扩展名）。</summary>
        public const string ResourcePath = "ArtTuningSettings";

        [Tooltip("视觉小说：左右立绘")]
        public VnStandingSettings VnStanding = new VnStandingSettings();

        private static ArtTuningSettings _current;

        /// <summary>当前生效的参数：Resources 里的资产；没有就是带出厂默认值的临时实例。</summary>
        public static ArtTuningSettings Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<ArtTuningSettings>(ResourcePath);
                    if (_current == null)
                    {
#if UNITY_EDITOR
                        Debug.LogWarning("[美术调试] 找不到 Resources/" + ResourcePath +
                                         ".asset，正在使用出厂默认值。打开 Tools/美术调试 点「创建参数资产」即可生成。");
#endif
                        _current = CreateInstance<ArtTuningSettings>();
                        _current.name = "(运行时默认) ArtTuningSettings";
                        _current.hideFlags = HideFlags.DontSave;
                    }
                }

                return _current;
            }
        }

        /// <summary>丢掉缓存，下次读取重新从 Resources 取（编辑器导入资产后调）。</summary>
        public static void Forget()
        {
            _current = null;
        }

        private void OnEnable()
        {
            EnsureDefaults();
        }

        private void OnValidate()
        {
            EnsureDefaults();
        }

        /// <summary>补空组 + 收范围。资产被手改、旧版本升级时兜底。</summary>
        public void EnsureDefaults()
        {
            VnStanding = VnStanding ?? new VnStandingSettings();
            VnStanding.Sanitize();
        }
    }
}
