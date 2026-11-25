using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Siege.Shop
{
    /// <summary>
    /// Juicy press feedback for any UI element: squashes on press, springs back with an
    /// overshoot on release, and can play a punch on demand (e.g. after a purchase).
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.9f;
        [SerializeField] private float pressDuration = 0.08f;
        [SerializeField] private float releaseDuration = 0.32f;

        private Vector3 baseScale = Vector3.one;
        private bool isPressed;
        private bool hasBaseScale;

        private void Awake()
        {
            CacheBaseScale();
        }

        private void CacheBaseScale()
        {
            if (hasBaseScale) return;
            baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            hasBaseScale = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            CacheBaseScale();
            isPressed = true;
            transform.DOKill();
            transform.localRotation = Quaternion.identity;
            transform.DOScale(baseScale * pressedScale, pressDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isPressed) return;
            isPressed = false;
            transform.DOKill();
            transform.DOScale(baseScale, releaseDuration).SetEase(Ease.OutBack, 3f).SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isPressed) return;
            isPressed = false;
            transform.DOKill();
            transform.DOScale(baseScale, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        /// <summary>Big celebratory punch (purchase success).</summary>
        public void Punch(float strength = 0.22f)
        {
            CacheBaseScale();
            transform.DOKill();
            transform.localScale = baseScale;
            transform.DOPunchScale(Vector3.one * strength, 0.45f, 7, 0.6f).SetUpdate(true);
        }

        /// <summary>Side-to-side "no" shake (can't afford / sold out).</summary>
        public void Deny()
        {
            CacheBaseScale();
            transform.DOKill();
            transform.localScale = baseScale;
            transform.localRotation = Quaternion.identity;
            transform.DOPunchRotation(new Vector3(0f, 0f, 9f), 0.4f, 12, 0.5f).SetUpdate(true);
        }

        private void OnDisable()
        {
            transform.DOKill();
            if (hasBaseScale) transform.localScale = baseScale;
            transform.localRotation = Quaternion.identity;
            isPressed = false;
        }
    }
}
