using System;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace UnityGameJam.UI
{
    public sealed class ComicPanelImage : Image
    {
        [SerializeField] private Vector2[] _contour = Array.Empty<Vector2>();
        [SerializeField, Min(0f)] private float _sourcePadding;

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            if (_contour.Length < 3)
            {
                base.OnPopulateMesh(vertices);
                return;
            }

            Sprite art = overrideSprite != null ? overrideSprite : sprite;
            Vector4 uv = art != null ? DataUtility.GetOuterUV(art) : new Vector4(0f, 0f, 1f, 1f);
            Vector2 padding = art != null
                ? new Vector2(_sourcePadding / art.rect.width, _sourcePadding / art.rect.height)
                : Vector2.zero;
            Rect rect = GetPixelAdjustedRect();
            vertices.Clear();
            foreach (Vector2 point in _contour)
            {
                var position = new Vector3(
                    Mathf.Lerp(rect.xMin, rect.xMax, point.x),
                    Mathf.Lerp(rect.yMin, rect.yMax, point.y));
                var texture = new Vector2(
                    Mathf.Lerp(uv.x, uv.z, Mathf.Lerp(padding.x, 1f - padding.x, point.x)),
                    Mathf.Lerp(uv.y, uv.w, Mathf.Lerp(padding.y, 1f - padding.y, point.y)));
                vertices.AddVert(position, color, texture);
            }

            for (int index = 1; index < _contour.Length - 1; index++)
            {
                vertices.AddTriangle(0, index + 1, index);
            }
        }
    }
}
