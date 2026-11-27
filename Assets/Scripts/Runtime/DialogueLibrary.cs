using System;
using System.Collections.Generic;
using CoolingGame.Core;

namespace CoolingGame.Runtime
{
    /// <summary>
    /// 剧本库的 JSON 载体。JsonUtility 只认字段，所以这里保持最朴素的结构。
    /// 运行时优先读这个 JSON，读不到才回落到代码里生成的 data。
    /// </summary>
    [Serializable]
    public sealed class DialogueLibrary
    {
        public List<DialogueScript> scripts = new List<DialogueScript>();
    }
}
