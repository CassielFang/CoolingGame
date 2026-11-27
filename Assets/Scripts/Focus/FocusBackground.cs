using UnityEngine;
using UnityEngine.EventSystems;

namespace FocusTest
{
    /// <summary>
    /// 挂在背景图上：点空白处取消所有选中（立绘与确认键一起收）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusBackground : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            FocusLabController controller = FindAnyObjectByType<FocusLabController>();
            if (controller != null)
            {
                controller.ClearSelection();
            }
        }
    }
}
