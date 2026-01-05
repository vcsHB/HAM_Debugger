using UnityEngine;
using UnityEngine.EventSystems;
namespace HAM_DeBugger.UISystem
{
    public abstract class UILayer : UIBehaviour
    {
        protected CanvasGroup _canvasGroup;

        protected override void Awake()
        {
            base.Awake();
            _canvasGroup = GetComponent<CanvasGroup>();

        }

        protected void SetLayerAlpha(float alpha)
        {
            _canvasGroup.alpha = alpha;
        }
    }
}