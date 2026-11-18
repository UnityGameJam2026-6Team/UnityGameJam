using UnityEngine;

namespace UnityGameJam.View
{
    /// <summary>
    /// BoxCollider의 월드 범위를 Transform만으로 계산한다.
    /// collider.bounds는 물리 씬이 동기화되기 전(에디터, 프리팹 미리보기)에는 비어 있을 수 있어서 쓰지 않는다.
    /// </summary>
    public static class AuthoringBounds
    {
        public static Bounds FromBox(BoxCollider box)
        {
            Transform transform = box.transform;
            Vector3 half = box.size * 0.5f;
            var bounds = new Bounds(transform.TransformPoint(box.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
                bounds.Encapsulate(transform.TransformPoint(box.center + corner));
            }
            return bounds;
        }

        public static void DrawBox(BoxCollider box, Color color)
        {
            if (box == null) { return; }
            Bounds bounds = FromBox(box);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.15f);
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
