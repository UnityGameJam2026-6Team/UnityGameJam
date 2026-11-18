using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityGameJam.View;

namespace UnityGameJam.Editor.Art
{
    internal static class EmbeddedCharacterAnimationBinder
    {
        private const string MopAction = "_01a1ed94_4b92_72e8_bf78_42f661410eaf";
        private const string MopWalk = "_01a1ed95_d307_7701_a839_04051b7ec198";
        private const string SkeletonMopWalk = "_01a1ed96_6620_7713_9bb7_f6a44c873f65";
        private const string Carry = "Spear_Walk";
        private const string CarryHeavy = "Carry_Heavy_Object_Walk_inplace";

        private sealed class ClipNames
        {
            public string Idle;
            public string Walk;
            public string Run;
            public string PickUp;
            public string PickUpHeavy;
            public string Carry;
            public string CarryHeavy;
            public string MopWalk;
            public string Mop;
            public string Eat;
            public string Dispatch;
            public bool LeftHandCarry;
        }

        private static readonly Dictionary<string, ClipNames> _bySource = new()
        {
            ["CH_RamHornMaidPrincess"] = new ClipNames
            {
                Idle = "Idle_12", Walk = "Walking", Run = "Running",
                PickUp = "Female_Bend_Over_Pick_Up_Inspect", PickUpHeavy = "Female_Crouch_Pick_Throw_Forward",
                Carry = Carry, CarryHeavy = CarryHeavy, LeftHandCarry = true, MopWalk = MopWalk, Mop = MopAction,
            },
            ["CH_GoblinSparkplug"] = new ClipNames
            {
                Idle = "Idle_11", Walk = "Walking", Run = "Running", PickUp = "Female_Crouch_Pick_Up_Place_Side",
                Carry = Carry, MopWalk = MopWalk, Mop = MopAction,
            },
            ["CH_GoblinTrailblazer"] = new ClipNames
            {
                Idle = "Idle_3", Walk = "Walking", Run = "Running", PickUp = "Collect_Object", PickUpHeavy = "Collect_Object",
                Carry = Carry, CarryHeavy = CarryHeavy, LeftHandCarry = true, MopWalk = MopWalk, Mop = MopAction,
            },
            ["CH_Skeleton"] = new ClipNames
            {
                Idle = "Idle_6", Walk = "Walking", Run = "Running", PickUp = "Idle_6", MopWalk = SkeletonMopWalk, Mop = MopAction,
            },
            ["CH_Slime"] = new ClipNames { Idle = "Idle", Walk = "Idle", Eat = "EAT" },
            ["CH_Hellhound"] = new ClipNames { Idle = "dog_idle", Walk = "dog_run", Run = "dog_run", Dispatch = "dog_run" },
            ["CH_CrimsonHoodedMage"] = new ClipNames { Idle = "Idle", Walk = "Walking" },
        };

        public static bool Supports(ArtBindingItem item)
        {
            return _bySource.ContainsKey(item.ArtKey) && string.Equals(Path.GetExtension(item.SourcePath), ".fbx", StringComparison.OrdinalIgnoreCase);
        }

        public static void EnsureImport(ArtBindingItem item)
        {
            if (!Supports(item)) { return; }
            var importer = (ModelImporter)AssetImporter.GetAtPath(item.SourcePath);
            if (importer.importAnimation && importer.animationType == ModelImporterAnimationType.Generic
                && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel) { return; }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.SaveAndReimport();
        }

        public static bool Bind(ArtSlot slot, GameObject model, ArtBindingItem item)
        {
            if (!Supports(item)) { return Remove(slot); }
            ClipNames names = _bySource[item.ArtKey];
            AnimatorController controller = GetOrCreateController(item, names);

            CharacterAnimationSet set = slot.AnimationSet;
            bool changed = set == null;
            if (set == null) { set = slot.gameObject.AddComponent<CharacterAnimationSet>(); }
            var settings = new SerializedObject(set);
            var speeds = new Dictionary<AnimationClip, float>();
            void SetSpeed(string property, ECharacterAnimation state)
            {
                AnimationClip clip = GetMotion(controller, state);
                float speed = 0f;
                if (clip != null && clip != GetMotion(controller, ECharacterAnimation.Idle) && !speeds.TryGetValue(clip, out speed))
                {
                    speed = ClipStrideMeasurement.Measure(model, clip);
                    speeds.Add(clip, speed);
                }
                SetFloat(property, speed);
            }
            void SetFloat(string property, float value)
            {
                SerializedProperty field = settings.FindProperty(property);
                if (!Mathf.Approximately(field.floatValue, value)) { field.floatValue = value; }
            }

            SetSpeed("_walkSpeed", ECharacterAnimation.Walk);
            SetSpeed("_runSpeed", ECharacterAnimation.Run);
            AnimationClip pickup = GetMotion(controller, ECharacterAnimation.PickUp);
            AnimationClip pickupHeavy = GetMotion(controller, ECharacterAnimation.PickUpHeavy);
            Vector2 lightTimes = pickup != null ? ClipStrideMeasurement.MeasureHandling(model, pickup, names.LeftHandCarry) : Vector2.zero;
            Vector2 heavyTimes = lightTimes;
            if (pickupHeavy != pickup)
            {
                heavyTimes = pickupHeavy != null ? ClipStrideMeasurement.MeasureHandling(model, pickupHeavy, false) : Vector2.zero;
            }
            SetFloat("_pickUpContact", lightTimes.x);
            SetFloat("_pickUpEnd", lightTimes.y);
            SetFloat("_pickUpHeavyContact", heavyTimes.x);
            SetFloat("_pickUpHeavyEnd", heavyTimes.y);
            SetSpeed("_carrySpeed", ECharacterAnimation.Carry);
            SetSpeed("_carryHeavySpeed", ECharacterAnimation.CarryHeavy);
            SetSpeed("_mopWalkSpeed", ECharacterAnimation.MopWalk);
            SerializedProperty hand = settings.FindProperty("_leftHandCarry");
            if (hand.boolValue != names.LeftHandCarry) { hand.boolValue = names.LeftHandCarry; }
            changed |= settings.ApplyModifiedPropertiesWithoutUndo();

            var root = slot.transform.parent.gameObject;
            var driver = root.GetComponent<CharacterAnimatorDriver>();
            if (driver == null) { driver = root.AddComponent<CharacterAnimatorDriver>(); changed = true; }
            var driverSettings = new SerializedObject(driver);
            SerializedProperty driverSlot = driverSettings.FindProperty("_artSlot");
            if (driverSlot.objectReferenceValue != slot)
            {
                driverSlot.objectReferenceValue = slot;
                changed |= driverSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            var animator = model.GetComponent<Animator>();
            if (animator == null) { animator = model.AddComponent<Animator>(); changed = true; }
            if (animator.runtimeAnimatorController != controller || animator.applyRootMotion)
            {
                using var animatorData = new SerializedObject(animator);
                animatorData.FindProperty("m_Controller").objectReferenceValue = controller;
                animatorData.FindProperty("m_ApplyRootMotion").boolValue = false;
                animatorData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                changed = true;
            }
            return changed;
        }

        internal static AnimationClip GetMotion(RuntimeAnimatorController controller, ECharacterAnimation state)
        {
            if (controller is not AnimatorController asset || asset.layers.Length == 0) { return null; }
            return FindState(asset.layers[0].stateMachine, state)?.motion as AnimationClip;
        }

        private static AnimatorController GetOrCreateController(ArtBindingItem item, ClipNames names)
        {
            string folder = Path.GetDirectoryName(item.SourcePath).Replace('\\', '/');
            string role = Path.GetFileNameWithoutExtension(item.PrefabPath);
            string path = $"{folder}/AC_{role}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) { return controller; }

            var clips = AssetDatabase.LoadAllAssetsAtPath(item.SourcePath).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToDictionary(clip => clip.name, StringComparer.Ordinal);
            var roles = new (ECharacterAnimation State, string Name)[]
            {
                (ECharacterAnimation.Idle, names.Idle), (ECharacterAnimation.Walk, names.Walk),
                (ECharacterAnimation.Run, names.Run), (ECharacterAnimation.PickUp, names.PickUp),
                (ECharacterAnimation.PickUpHeavy, names.PickUpHeavy), (ECharacterAnimation.Carry, names.Carry),
                (ECharacterAnimation.CarryHeavy, names.CarryHeavy), (ECharacterAnimation.MopWalk, names.MopWalk),
                (ECharacterAnimation.Mop, names.Mop), (ECharacterAnimation.Eat, names.Eat),
                (ECharacterAnimation.Dispatch, names.Dispatch)
            };
            foreach (var entry in roles)
            {
                if (entry.Name != null && !clips.ContainsKey(entry.Name))
                {
                    throw new InvalidOperationException($"[Art] {item.SourcePath}에서 행동 클립 {entry.Name}을 찾을 수 없습니다.");
                }
            }
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            int index = 0;
            foreach (var entry in roles)
            {
                if (entry.Name == null) { continue; }
                AnimatorState state = machine.AddState(entry.State.ToString(),
                    new Vector3(270f + 210f * (index % 3), 70f + 65f * (index / 3)));
                state.motion = clips[entry.Name];
                if (entry.State == ECharacterAnimation.Idle) { machine.defaultState = state; }
                EditorUtility.SetDirty(state);
                index++;
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, ECharacterAnimation state)
        {
            string name = state.ToString();
            return machine.states.FirstOrDefault(entry => entry.state.name == name).state;
        }

        public static bool Remove(ArtSlot slot)
        {
            CharacterAnimationSet set = slot.AnimationSet;
            if (set == null) { return false; }
            UnityEngine.Object.DestroyImmediate(set);
            return true;
        }
    }
}
