using UnityEngine;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 把「一组参数」和「保存 / 读取 / 丢弃」两态操作绑在一起。
    /// 以后每加一个调试对象，只要在 <see cref="ArtTuningStore"/> 的列表里加一行。
    /// </summary>
    public sealed class VnStandingBlock : IArtTuningBlock
    {
        private readonly VnStandingSettings _saved;
        private readonly VnStandingSettings _working;

        public VnStandingBlock(VnStandingSettings saved, VnStandingSettings working)
        {
            _saved = saved;
            _working = working;
        }

        public void CaptureSavedToWorking()
        {
            _working.CopyFrom(_saved);
        }

        public void ApplySavedToWorking()
        {
            _working.CopyFrom(_saved);
        }

        public void CommitWorkingToSaved()
        {
            _saved.CopyFrom(_working);
        }
    }

    /// <summary>
    /// 美术调试参数的「两态」中枢：
    ///
    /// - <see cref="Saved"/>：Resources/ArtTuningSettings.asset 里的那份（文件）；
    /// - <see cref="Working"/>：内存里的工作副本，游戏每帧读的就是它。
    ///
    /// 打开窗口 / 进 Play 时用 Saved 初始化 Working；窗口里所有编辑只改 Working；
    /// 点「保存」才写回 Saved（编辑器里会存盘），点「读取」把 Saved 抄回 Working，点「丢弃」回到 Saved。
    /// 这样运行中随便试、退出 Play 也不会污染参数，和代码里写死的数值一样稳定。
    /// </summary>
    public static class ArtTuningStore
    {
        private static ArtTuningSettings _working;

        /// <summary>保存的那份（只读用途：显示、读取、保存时写的目标）。</summary>
        public static ArtTuningSettings Saved
        {
            get { return ArtTuningSettings.Current; }
        }

        /// <summary>工作副本（游戏读它，窗口改它）。</summary>
        public static ArtTuningSettings Working
        {
            get
            {
                if (_working == null)
                {
                    var instance = ScriptableObject.CreateInstance<ArtTuningSettings>();
                    instance.name = "美术调试（正在调）";
                    instance.hideFlags = HideFlags.DontSave;
                    instance.EnsureDefaults();
                    instance.VnStanding.CopyFrom(Saved.VnStanding);
                    _working = instance;
                }

                return _working;
            }
        }

        public static VnStandingSettings WorkingVnStanding
        {
            get
            {
                ArtTuningSettings working = Working;
                working.EnsureDefaults();
                return working.VnStanding;
            }
        }

        /// <summary>读取：Saved → Working。</summary>
        public static void LoadSaved()
        {
            Working.EnsureDefaults();
            Working.VnStanding.CopyFrom(Saved.VnStanding);
            Saved.EnsureDefaults();
            Working.EnsureDefaults();
        }

        /// <summary>保存：Working → Saved。</summary>
        public static void SaveWorking()
        {
            Saved.VnStanding.CopyFrom(WorkingVnStanding);
            Saved.EnsureDefaults();
        }

        /// <summary>丢弃：回到 Saved（= 读取）。</summary>
        public static void DiscardChanges()
        {
            LoadSaved();
        }

        /// <summary>Working 是否和 Saved 不一样（窗口用来提示「有改动没保存」）。</summary>
        public static bool IsWorkingDirty()
        {
            VnStandingSettings working = WorkingVnStanding;
            VnStandingSettings saved = Saved.VnStanding;
            if (saved == null)
            {
                return true;
            }

            return working.apply != saved.apply
                   || working.applyDim != saved.applyDim
                   || working.applyArt != saved.applyArt
                   || !Mathf.Approximately(working.unifiedHeight, saved.unifiedHeight)
                   || !Mathf.Approximately(working.unifiedAlignY, saved.unifiedAlignY)
                   || !SameSide(working.left, saved.left)
                   || !SameSide(working.right, saved.right)
                   || working.applyBackground != saved.applyBackground
                   || working.backgroundTop != saved.backgroundTop
                   || working.backgroundBottom != saved.backgroundBottom;
        }

        /// <summary>加载 / 存档后丢掉缓存（资产被替换、域重载前后用）。</summary>
        public static void Forget()
        {
            if (_working != null)
            {
                Object.DestroyImmediate(_working);
            }

            _working = null;
            ArtTuningSettings.Forget();
        }

        private static bool SameSide(VnStandingSide a, VnStandingSide b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return a.apply == b.apply
                   && a.anchor == b.anchor
                   && Mathf.Approximately(a.gapLeft, b.gapLeft)
                   && Mathf.Approximately(a.gapRight, b.gapRight)
                   && Mathf.Approximately(a.gapBottom, b.gapBottom)
                   && Mathf.Approximately(a.scale, b.scale)
                   && a.baseSize == b.baseSize
                   && Mathf.Approximately(a.dim, b.dim)
                   && a.rawMode == b.rawMode
                   && Mathf.Approximately(a.artScale, b.artScale)
                   && a.artOffset == b.artOffset
                   && a.flipHorizontal == b.flipHorizontal;
        }
    }
}
