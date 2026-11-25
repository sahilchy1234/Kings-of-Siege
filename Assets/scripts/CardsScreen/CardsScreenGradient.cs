using UnityEngine;
using UnityEngine.UI;

namespace Siege.CardsScreen
{
    /// <summary>
    /// Two-colour gradient for any uGUI Graphic (works with sliced Images).
    /// Vertical: <see cref="from"/> is the top colour. Horizontal: <see cref="from"/> is the left colour.
    /// The gradient multiplies the Graphic's own colour, so fades and tints still work.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class CardsScreenGradient : BaseMeshEffect
    {
        public Color from = Color.white;
        public Color to = Color.white;
        public bool horizontal;

        public void Set(Color fromColor, Color toColor, bool isHorizontal = false)
        {
            from = fromColor;
            to = toColor;
            horizontal = isHorizontal;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            var v = new UIVertex();
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float p = horizontal ? v.position.x : v.position.y;
                if (p < min) min = p;
                if (p > max) max = p;
            }
            float range = Mathf.Max(0.0001f, max - min);

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = ((horizontal ? v.position.x : v.position.y) - min) / range;
                // vertical: t = 1 at the top, so lerp to 'from'; horizontal: t = 0 at the left ('from')
                Color g = horizontal ? Color.Lerp(from, to, t) : Color.Lerp(to, from, t);
                v.color = (Color32)((Color)v.color * g);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
