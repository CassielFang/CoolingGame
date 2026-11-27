using System;
using System.Collections.Generic;
using CoolingGame.Core;

namespace CoolingGame.Runtime
{
    /// <summary>报纸的 JSON 载体（和 DialogueLibrary 一样，供运行时与导入工具共用）。</summary>
    [Serializable]
    public sealed class NewspaperLibrary
    {
        public List<NewspaperDefinition> newspapers = new List<NewspaperDefinition>();
    }
}
