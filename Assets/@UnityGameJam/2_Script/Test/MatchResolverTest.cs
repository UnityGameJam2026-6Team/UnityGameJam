using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Facility;

namespace UnityGameJam.Test
{
    public class MatchResolverTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void 시설이_기준에_도달해도_모든_구역이_기준을_충족해야_성공한다(bool allZonesMeetThreshold)
        {
            var items = new List<ContaminationItem>();
            var toClean = new List<int>();
            foreach (EZoneId zone in Enum.GetValues(typeof(EZoneId)))
            {
                int left = (int)zone switch
                {
                    0 => allZonesMeetThreshold ? 8 : 9,
                    1 => allZonesMeetThreshold ? 3 : 2,
                    2 or 3 => 3,
                    _ => 2
                };
                for (int index = 0; index < 20; index++)
                {
                    int id = items.Count + 1;
                    items.Add(new ContaminationItem(id, EContaminationType.FloorStain, 1, zone, Vector3.zero));
                    if (index >= left) { toClean.Add(id); }
                }
            }
            var registry = new ContaminationRegistry(new ZoneMap(new FacilityLayout()), items);
            var calculator = new CleanlinessCalculator(registry);
            foreach (int id in toClean)
            {
                Assert.That(registry.TryClaim(id, EWorkerType.Player), Is.True);
                Assert.That(registry.TryCompleteMop(id, EWorkerType.Player), Is.True);
            }

            MatchResult result = MatchResolver.Resolve(registry, calculator,
                new Dictionary<EWorkerType, IWorkerBody>(), 60f, 85f);

            Assert.That(result.FacilityCleanliness, Is.EqualTo(85f));
            Assert.That(result.ZoneCleanliness[EZoneId.Floor1Lobby], Is.EqualTo(allZonesMeetThreshold ? 60f : 55f));
            Assert.That(result.IsSuccess, Is.EqualTo(allZonesMeetThreshold));
        }
    }
}
