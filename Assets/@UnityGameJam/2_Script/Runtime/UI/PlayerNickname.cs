using UnityEngine;

namespace UnityGameJam.UI
{
    public static class PlayerNickname
    {
        private const string PreferenceKey = "PlayerNickname";

        public static string Current => PlayerPrefs.GetString(PreferenceKey, string.Empty);

        internal static void Register(string nickname)
        {
            PlayerPrefs.SetString(PreferenceKey, nickname);
            PlayerPrefs.Save();
        }
    }
}
