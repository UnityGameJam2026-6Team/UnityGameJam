using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace UnityGameJam.Editor.Art
{
    public enum EArtCategory
    {
        Character,
        Item,
        Prop,
        Environment,
    }

    public enum ETextureMap
    {
        BaseMap,
        Normal,
        Mask,
        Emission,
    }

    /// <summary>
    /// 아트 파일 이름 규칙(docs/rules/art-asset.md 1절). 파일 이름만으로 프리팹과 짝을 짓기 때문에
    /// 규칙 판정은 이 클래스 한 곳에서만 한다.
    /// </summary>
    public static class ArtKeyRule
    {
        public const char AnimationSeparator = '@';

        private static readonly Regex _artKeyPattern = new Regex(@"^(CH|IT|PR|ENV)_[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)*$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, EArtCategory> _prefixToCategory = new Dictionary<string, EArtCategory>
        {
            { "CH", EArtCategory.Character },
            { "IT", EArtCategory.Item },
            { "PR", EArtCategory.Prop },
            { "ENV", EArtCategory.Environment },
        };

        private static readonly string[] _allowedClipNames = { "Idle", "Walk", "Carry", "CarryHeavy", "Mop", "Eat", "Talk", "Dispatch" };

        public static IReadOnlyList<string> AllowedClipNames => _allowedClipNames;

        public static bool IsValidArtKey(string key)
        {
            return !string.IsNullOrEmpty(key) && _artKeyPattern.IsMatch(key);
        }

        public static bool TryGetCategory(string key, out EArtCategory category)
        {
            category = default;
            if (!IsValidArtKey(key)) { return false; }

            string prefix = key.Substring(0, key.IndexOf('_'));
            return _prefixToCategory.TryGetValue(prefix, out category);
        }

        /// <summary>베이스 프리팹(Variant의 부모)은 직접 배치하지 않으므로 아트를 적용하지 않는다.</summary>
        public static bool IsBaseKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.EndsWith("Base", StringComparison.Ordinal);
        }

        /// <summary>ArtSlot의 오버라이드 키가 비어 있으면 프리팹 파일 이름이 곧 ArtKey다.</summary>
        public static string ResolvePrefabKey(string overrideKey, string prefabPath)
        {
            if (!string.IsNullOrWhiteSpace(overrideKey)) { return overrideKey.Trim(); }
            return Path.GetFileNameWithoutExtension(prefabPath);
        }

        public static bool TrySplitAnimation(string fileName, out string artKey, out string clipName)
        {
            artKey = null;
            clipName = null;
            if (string.IsNullOrEmpty(fileName)) { return false; }

            int index = fileName.IndexOf(AnimationSeparator);
            if (index <= 0 || index >= fileName.Length - 1) { return false; }

            artKey = fileName.Substring(0, index);
            clipName = fileName.Substring(index + 1);
            return true;
        }

        public static bool IsAnimationFileName(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) && fileName.IndexOf(AnimationSeparator) >= 0;
        }

        public static bool IsAllowedClipName(string clipName)
        {
            return Array.IndexOf(_allowedClipNames, clipName) >= 0;
        }

        /// <summary>`<ArtKey>[_재질]_<맵>`을 기준 이름과 맵으로 나눈다.</summary>
        public static bool TrySplitTexture(string fileName, out string baseName, out ETextureMap map)
        {
            baseName = null;
            map = default;
            if (string.IsNullOrEmpty(fileName)) { return false; }

            int index = fileName.LastIndexOf('_');
            if (index <= 0) { return false; }

            string suffix = fileName.Substring(index + 1);
            if (!Enum.TryParse(suffix, false, out map) || !Enum.IsDefined(typeof(ETextureMap), map)) { return false; }

            baseName = fileName.Substring(0, index);
            return true;
        }
    }
}
