using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.View;

namespace UnityGameJam.Test.View
{
    public sealed class UnitMotionTuningTest
    {
        [Test]
        public void 누락된_설정은_원래_이동_속도를_사용한다()
        {
            Assert.That(default(UnitMotionTuning).ResolveMoveSpeed(1.2f, 0.4f, 4f, 2f, false,
                EContaminationType.HeavyDebris, false), Is.EqualTo(0.6f).Within(0.001f));
        }

        [Test]
        public void 두_손_운반_배율은_조정된_보행과_규칙의_감속에_적용된다()
        {
            var asset = ScriptableObject.CreateInstance<ViewSettingsSO>();
            try
            {
                var settings = new SerializedObject(asset);
                var player = settings.FindProperty("_playerMotion");
                player.FindPropertyRelative("_walkRate").floatValue = 1.5f;
                player.FindPropertyRelative("_heavyCarryRate").floatValue = 2f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var tuning = asset.GetMotionTuning(EWorkerType.Player);
                Assert.That(tuning.ResolveMoveSpeed(1.2f, 0.4f, 4f, 2f, false, EContaminationType.HeavyDebris, false),
                    Is.EqualTo(1.8f).Within(0.001f));
                Assert.That(asset.GetMotionTuning(EWorkerType.Goblin).ResolveMoveSpeed(1.2f, 1.2f, 4f, 4f, false, null, false),
                    Is.EqualTo(1.2f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void 정방향과_역방향의_접촉과_잠금_종료는_각자_배속을_따른다()
        {
            var pickup = new UnitHandlingTiming(5f, 2f, 2f, true);
            var drop = new UnitHandlingTiming(5f, 2f, 0.5f, false);
            Assert.That(pickup.Duration, Is.EqualTo(2.5f));
            Assert.That(drop.Duration, Is.EqualTo(10f));
            Assert.That(pickup.ContactSeconds, Is.EqualTo(1f));
            Assert.That(drop.ContactSeconds, Is.EqualTo(6f));
            Assert.That(pickup.GripHasItem(0.9f), Is.False);
            Assert.That(pickup.GripHasItem(1.1f), Is.True);
            Assert.That(drop.GripHasItem(5.9f), Is.True);
            Assert.That(drop.GripHasItem(6.1f), Is.False);
            Assert.That(drop.ClipTime(10f), Is.Zero);
        }

        [Test]
        public void 접촉_시점은_선택한_클립_구간을_넘지_않는다()
        {
            var timing = new UnitHandlingTiming(3f, 8f, 1f, true);
            Assert.That(timing.ContactSeconds, Is.EqualTo(3f));
            Assert.That(timing.ClipTime(8f), Is.EqualTo(3f));
        }
    }
}
