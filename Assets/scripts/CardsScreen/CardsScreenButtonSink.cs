using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Siege.CardsScreen
{
    /// <summary>
    /// Pushes a 3D button's face down onto its lip while pressed, then springs it back up.
    /// Works alongside ShopPressFeedback (which squashes the whole button).
    /// </summary>
    [DisallowMultipleComponent]
    public class CardsScreenButtonSink : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform face;
        public float depth = 8f;

        private Vector2 restOffsetMin;
        private Vector2 restOffsetMax;
        private bool cached;
        private bool pressed;

        private void Cache()
        {
            if (cached || face == null) return;
            restOffsetMin = face.offsetMin;
            restOffsetMax = face.offsetMax;
            cached = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (face == null) return;
            Cache();
            pressed = true;
            Move(-depth, 0.06f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed) return;
            pressed = false;
            Move(0f, 0.25f, Ease.OutBack);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!pressed) return;
            pressed = false;
            Move(0f, 0.15f, Ease.OutQuad);
        }

        private void Move(float dy, float duration, Ease ease)
        {
            DOTween.Kill(this);
            float start = face.offsetMin.y - restOffsetMin.y;
            DOTween.To(() => start, y =>
                {
                    start = y;
                    if (face == null) return;
                    face.offsetMin = new Vector2(restOffsetMin.x, restOffsetMin.y + y);
                    face.offsetMax = new Vector2(restOffsetMax.x, restOffsetMax.y + y);
                }, dy, duration)
                .SetEase(ease).SetUpdate(true).SetTarget(this);
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            if (cached && face != null)
            {
                face.offsetMin = restOffsetMin;
                face.offsetMax = restOffsetMax;
            }
            pressed = false;
        }
    }
}
