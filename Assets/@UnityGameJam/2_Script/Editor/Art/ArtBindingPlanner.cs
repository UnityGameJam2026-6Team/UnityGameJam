using System;
using System.Collections.Generic;
using System.IO;

namespace UnityGameJam.Editor.Art
{
    public enum EArtBindingStatus
    {
        Applied,      // 적용됨
        Ready,        // 적용 가능
        MissingModel, // 모델 없음
        Orphan,       // 주인 없음 (프리팹이 없는 모델)
        InvalidName,  // 이름 오류
        Duplicate,    // 같은 키의 원본이 둘 이상
        Base,         // 베이스 프리팹 (적용하지 않음)
    }

    /// <summary>에디터에서 모은 프리팹 정보. 계획 로직이 AssetDatabase 없이 돌도록 값만 담는다.</summary>
    public readonly struct ArtPrefabInfo
    {
        public string PrefabPath { get; }
        public string ArtKey { get; }
        public string AppliedSourcePath { get; } // 지금 들어 있는 모델(또는 텍스처 전용이면 BaseMap) 경로. 없으면 null
        public bool IsTextureOnly { get; }

        public ArtPrefabInfo(string prefabPath, string artKey, string appliedSourcePath, bool isTextureOnly)
        {
            PrefabPath = prefabPath;
            ArtKey = artKey;
            AppliedSourcePath = appliedSourcePath;
            IsTextureOnly = isTextureOnly;
        }
    }

    public readonly struct ArtBindingItem
    {
        public EArtBindingStatus Status { get; }
        public string ArtKey { get; }
        public string PrefabPath { get; } // 주인 없음, 모델 이름 오류일 때는 null
        public string SourcePath { get; } // 모델 없음일 때는 null
        public string Message { get; }

        public ArtBindingItem(EArtBindingStatus status, string artKey, string prefabPath, string sourcePath, string message)
        {
            Status = status;
            ArtKey = artKey;
            PrefabPath = prefabPath;
            SourcePath = sourcePath;
            Message = message;
        }
    }

    /// <summary>
    /// 프리팹 키와 아트 파일 이름을 짝지어 항목마다 상태를 정한다(Spec ArtPipeline 규칙 5~11).
    /// 결과는 프리팹 항목 → 주인 없는 모델 → 이름이 틀린 모델 순서로 돌려준다.
    /// </summary>
    public static class ArtBindingPlanner
    {
        public static List<ArtBindingItem> Plan(IReadOnlyList<ArtPrefabInfo> prefabs, IReadOnlyList<string> modelPaths, IReadOnlyList<string> texturePaths)
        {
            var result = new List<ArtBindingItem>();
            var modelsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var invalidModels = new List<string>();
            CollectModels(modelPaths, modelsByKey, invalidModels);
            Dictionary<string, List<string>> baseMapsByKey = CollectBaseMaps(texturePaths);

            var claimedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ArtPrefabInfo prefab in prefabs)
            {
                claimedKeys.Add(prefab.ArtKey ?? string.Empty);
                result.Add(PlanPrefab(prefab, modelsByKey, baseMapsByKey));
            }

            foreach (KeyValuePair<string, List<string>> pair in modelsByKey)
            {
                if (claimedKeys.Contains(pair.Key)) { continue; }
                foreach (string path in pair.Value)
                {
                    result.Add(new ArtBindingItem(EArtBindingStatus.Orphan, pair.Key, null, path, "같은 이름의 프리팹이 없습니다. 프리팹을 만들거나 파일 이름을 고치세요."));
                }
            }

            foreach (string path in invalidModels)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                result.Add(new ArtBindingItem(EArtBindingStatus.InvalidName, name, null, path, "이름 규칙 위반: <CH|IT|PR|ENV>_<이름>[_<변형>]"));
            }

            return result;
        }

        private static ArtBindingItem PlanPrefab(ArtPrefabInfo prefab, Dictionary<string, List<string>> modelsByKey, Dictionary<string, List<string>> baseMapsByKey)
        {
            string key = prefab.ArtKey;
            if (ArtKeyRule.IsBaseKey(key))
            {
                return new ArtBindingItem(EArtBindingStatus.Base, key, prefab.PrefabPath, null, "베이스 프리팹은 적용하지 않습니다.");
            }
            if (!ArtKeyRule.IsValidArtKey(key))
            {
                return new ArtBindingItem(EArtBindingStatus.InvalidName, key, prefab.PrefabPath, null, "프리팹 이름(또는 오버라이드 키)이 ArtKey 규칙에 맞지 않습니다.");
            }

            Dictionary<string, List<string>> sources = prefab.IsTextureOnly ? baseMapsByKey : modelsByKey;
            if (!sources.TryGetValue(key, out List<string> candidates) || candidates.Count == 0)
            {
                string missing = prefab.IsTextureOnly ? key + "_BaseMap 텍스처가 없습니다." : "모델이 없습니다. 화이트박스를 유지합니다.";
                return new ArtBindingItem(EArtBindingStatus.MissingModel, key, prefab.PrefabPath, null, missing);
            }
            if (candidates.Count > 1)
            {
                return new ArtBindingItem(EArtBindingStatus.Duplicate, key, prefab.PrefabPath, string.Join(", ", candidates), "같은 키의 원본이 둘 이상입니다. 하나만 남기세요.");
            }

            string source = candidates[0];
            bool isApplied = string.Equals(prefab.AppliedSourcePath, source, StringComparison.Ordinal);
            return isApplied
                ? new ArtBindingItem(EArtBindingStatus.Applied, key, prefab.PrefabPath, source, "적용됨")
                : new ArtBindingItem(EArtBindingStatus.Ready, key, prefab.PrefabPath, source, "적용 가능");
        }

        private static void CollectModels(IReadOnlyList<string> modelPaths, Dictionary<string, List<string>> modelsByKey, List<string> invalidModels)
        {
            foreach (string path in modelPaths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (ArtKeyRule.IsAnimationFileName(name)) { continue; } // 애니메이션은 ArtAnimationBinder가 따로 다룬다
                if (!ArtKeyRule.IsValidArtKey(name))
                {
                    invalidModels.Add(path);
                    continue;
                }
                Add(modelsByKey, name, path);
            }
        }

        private static Dictionary<string, List<string>> CollectBaseMaps(IReadOnlyList<string> texturePaths)
        {
            var baseMaps = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string path in texturePaths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (ArtKeyRule.TrySplitTexture(name, out string baseName, out ETextureMap map) && map == ETextureMap.BaseMap)
                {
                    Add(baseMaps, baseName, path);
                }
            }
            return baseMaps;
        }

        private static void Add(Dictionary<string, List<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out List<string> list))
            {
                list = new List<string>();
                map[key] = list;
            }
            list.Add(value);
        }
    }
}
