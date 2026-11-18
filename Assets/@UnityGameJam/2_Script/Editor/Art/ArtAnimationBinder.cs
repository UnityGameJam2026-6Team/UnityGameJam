using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityGameJam.Editor.Art
{
    /// <summary>
    /// `<ArtKey>@<Clip>.fbx` 클립을 캐릭터 공용 베이스(AC_Character)의 같은 이름 상태에 덮어쓰는
    /// `AOC_<ArtKey>` 오버라이드 컨트롤러를 만든다. 상태 사이의 전이와 파라미터는 B3에서 정한다.
    /// TODO: 보류 (미정 U201. 캐릭터 애니메이터 상태)
    /// </summary>
    public static class ArtAnimationBinder
    {
        public const string BaseControllerPath = ArtBinder.DefaultModelRoot + "/Character/AC_Character.controller";

        public static AnimatorController EnsureBaseController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(BaseControllerPath);
            }

            // 클립 자리(빈 클립)를 컨트롤러 안에 서브 에셋으로 둔다. 오버라이드는 이 클립 이름으로 짝을 짓는다.
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            List<AnimationClip> placeholders = AssetDatabase.LoadAllAssetsAtPath(BaseControllerPath).OfType<AnimationClip>().ToList();
            bool isChanged = false;
            foreach (string clipName in ArtKeyRule.AllowedClipNames)
            {
                AnimationClip clip = placeholders.FirstOrDefault(c => c.name == clipName);
                if (clip == null)
                {
                    clip = new AnimationClip { name = clipName };
                    AssetDatabase.AddObjectToAsset(clip, controller);
                    isChanged = true;
                }
                if (stateMachine.states.All(s => s.state.name != clipName))
                {
                    AnimatorState state = stateMachine.AddState(clipName);
                    state.motion = clip;
                    isChanged = true;
                }
            }

            AnimatorState idle = stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle");
            if (idle != null && stateMachine.defaultState != idle)
            {
                stateMachine.defaultState = idle;
                isChanged = true;
            }

            isChanged |= EnsurePoseTransitions(controller, stateMachine);

            if (isChanged)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }
            return controller;
        }

        public const string PoseParameter = "Pose";

        /// <summary>
        /// int 파라미터 Pose와, Any State에서 각 상태로 가는 전이(Pose == 클립 순서)를 보장한다(U201, Spec MinionAgent 규칙 6).
        /// 이미 있으면 바꾸지 않는다.
        /// </summary>
        private static bool EnsurePoseTransitions(AnimatorController controller, AnimatorStateMachine stateMachine)
        {
            bool isChanged = false;
            if (controller.parameters.All(p => p.name != PoseParameter))
            {
                controller.AddParameter(PoseParameter, AnimatorControllerParameterType.Int);
                isChanged = true;
            }

            for (int index = 0; index < ArtKeyRule.AllowedClipNames.Count; index++)
            {
                string clipName = ArtKeyRule.AllowedClipNames[index];
                AnimatorState state = stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == clipName);
                if (state == null) { continue; }
                bool hasTransition = stateMachine.anyStateTransitions.Any(t => t.destinationState == state);
                if (hasTransition) { continue; }

                AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(state);
                transition.AddCondition(AnimatorConditionMode.Equals, index, PoseParameter);
                transition.hasExitTime = false;
                transition.duration = 0.1f;
                transition.canTransitionToSelf = false;
                isChanged = true;
            }
            return isChanged;
        }

        /// <summary>모델 폴더의 애니메이션 fbx로 AOC를 만들거나 갱신한다. 애니메이션이 없으면 null.</summary>
        public static AnimatorOverrideController BuildOverride(string artKey, string modelFolder)
        {
            Dictionary<string, AnimationClip> clips = FindClips(artKey, modelFolder);
            if (clips.Count == 0) { return null; }

            AnimatorController baseController = EnsureBaseController();
            string path = modelFolder + "/AOC_" + artKey + ".overrideController";
            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            bool isChanged = false;
            if (overrideController == null)
            {
                overrideController = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(overrideController, path);
                isChanged = true;
            }
            else if (overrideController.runtimeAnimatorController != baseController)
            {
                overrideController.runtimeAnimatorController = baseController;
                isChanged = true;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                clips.TryGetValue(overrides[i].Key.name, out AnimationClip clip);
                if (overrides[i].Value == clip) { continue; }
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
                isChanged = true;
            }

            if (isChanged)
            {
                overrideController.ApplyOverrides(overrides);
                EditorUtility.SetDirty(overrideController);
                AssetDatabase.SaveAssets();
            }
            return overrideController;
        }

        /// <summary>모델 루트의 Animator에 컨트롤러를 연결한다. 바뀌었으면 true.</summary>
        public static bool AssignToModel(GameObject model, RuntimeAnimatorController controller)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) { animator = model.AddComponent<Animator>(); }
            if (animator.runtimeAnimatorController == controller) { return false; }
            animator.runtimeAnimatorController = controller;
            return true;
        }

        private static Dictionary<string, AnimationClip> FindClips(string artKey, string modelFolder)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string guid in AssetDatabase.FindAssets(artKey + "@", new[] { modelFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!ArtKeyRule.TrySplitAnimation(fileName, out string key, out string clipName) || key != artKey) { continue; }
                if (!ArtKeyRule.IsAllowedClipName(clipName))
                {
                    Debug.LogWarning($"[Art] 허용되지 않은 클립 이름이라 연결하지 않습니다: {path} (허용: {string.Join(", ", ArtKeyRule.AllowedClipNames)})");
                    continue;
                }

                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null) { clips[clipName] = clip; }
            }
            return clips;
        }
    }
}
