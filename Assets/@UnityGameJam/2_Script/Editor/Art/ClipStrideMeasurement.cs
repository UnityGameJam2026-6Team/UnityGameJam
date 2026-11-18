using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityGameJam.Editor.Art
{
    /// <summary>게임 프리팹의 실제 크기로 FBX를 샘플링해 제자리 보행 속도와 집는 순간을 구한다.</summary>
    internal static class ClipStrideMeasurement
    {
        private const float SamplesPerSecond = 30f;

        public static float Measure(GameObject model, AnimationClip clip)
        {
            if (model == null || clip == null || clip.length <= 0f) { return 0f; }
            using var sample = new ClipSample(model);
            Transform hips = sample.FindBone("Hips");
            if (hips == null) { return 0f; }
            var feet = new List<Transform>();
            foreach (string name in new[] { "LeftFoot", "RightFoot", "rearFoot.L", "rearFoot.R", "frontFoot.L", "frontFoot.R" })
            {
                Transform foot = sample.FindBone(name);
                if (foot != null) { feet.Add(foot); }
            }
            if (feet.Count < 2) { return 0f; }

            int steps = Mathf.Max(2, Mathf.CeilToInt(clip.length * SamplesPerSecond));
            float dt = clip.length / steps;
            var previous = new Vector3[feet.Count];
            var speeds = new List<float>(steps);
            for (int frame = 0; frame <= steps; frame++)
            {
                clip.SampleAnimation(sample.Model, frame * dt);
                float lowest = float.PositiveInfinity;
                float contactSpeed = 0f;
                for (int i = 0; i < feet.Count; i++)
                {
                    Vector3 relative = feet[i].position - hips.position;
                    float height = (relative.y + previous[i].y) * 0.5f;
                    if (height < lowest)
                    {
                        lowest = height;
                        Vector3 delta = relative - previous[i];
                        contactSpeed = new Vector2(delta.x, delta.z).magnitude / dt;
                    }
                    previous[i] = relative;
                }
                if (frame > 0) { speeds.Add(contactSpeed); }
            }
            // 발을 바꿔 디디는 찰나와 루프 경계의 미끄러짐에 영향을 덜 받는 중앙값.
            speeds.Sort();
            float speed = speeds[speeds.Count / 2];
            return speed > 0.01f ? speed : 0f;
        }

        /// <summary>x = 손이 물건에 닿는 시간, y = 들어 올리기를 마친 시간. 뒤따르는 검사·던지기·옆에 놓기는 제외한다.</summary>
        public static Vector2 MeasureHandling(GameObject model, AnimationClip clip, bool leftHand)
        {
            if (model == null || clip == null || clip.length <= 0f) { return Vector2.zero; }
            if (clip.name.StartsWith("Idle", StringComparison.OrdinalIgnoreCase))
            {
                float end = Mathf.Min(0.5f, clip.length);
                return new Vector2(end * 0.5f, end);
            }
            using var sample = new ClipSample(model);
            Transform hand = sample.FindBone(leftHand ? "LeftHand" : "RightHand");
            if (hand == null) { return Vector2.zero; }
            int steps = Mathf.Max(4, Mathf.CeilToInt(clip.length * SamplesPerSecond));
            float dt = clip.length / steps;
            var heights = new float[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                clip.SampleAnimation(sample.Model, i * dt);
                heights[i] = hand.position.y;
            }
            int contact = 1;
            for (int i = 2; i <= steps * 0.65f; i++)
            {
                if (heights[i] < heights[contact]) { contact = i; }
            }
            float liftHeight = Mathf.Lerp(heights[contact], heights[0], 0.85f);
            int finish = contact + 1;
            while (finish < steps && heights[finish] < liftHeight) { finish++; }
            return new Vector2(contact * dt, Mathf.Min(clip.length, finish * dt + 0.1f));
        }

        private sealed class ClipSample : IDisposable
        {
            private readonly Scene _preview;
            private readonly Transform[] _bones;
            public GameObject Model { get; }

            public ClipSample(GameObject model)
            {
                _preview = EditorSceneManager.NewPreviewScene();
                var anchor = new GameObject("ClipSample");
                SceneManager.MoveGameObjectToScene(anchor, _preview);
                Model = Object.Instantiate(model, anchor.transform, false);
                Model.transform.SetPositionAndRotation(Vector3.zero, model.transform.rotation);
                Model.transform.localScale = model.transform.lossyScale;
                _bones = Model.GetComponentsInChildren<Transform>(true);
            }

            public Transform FindBone(string suffix)
            {
                foreach (Transform bone in _bones)
                {
                    if (bone.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { return bone; }
                }
                return null;
            }

            public void Dispose()
            {
                EditorSceneManager.ClosePreviewScene(_preview);
            }
        }
    }
}
