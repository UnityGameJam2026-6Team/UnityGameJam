using UnityEngine;

namespace UnityGameJam.View
{
    public enum EArtFitMode
    {
        None,                   // 모델 크기 그대로
        MatchPlaceholderHeight, // Placeholder 높이에 맞춤
        MatchPlaceholderBounds, // Placeholder 상자 안에 들어가게 맞춤
    }

    /// <summary>
    /// 아트를 받는 프리팹의 Visual 오브젝트에 붙인다(docs/rules/prefab.md 3절).
    /// 모델 교체는 에디터 메뉴(ArtBinder)가 하고, 이 컴포넌트는 보정 값과 참조만 들고 있다.
    /// 보정 값이 프리팹에 저장되므로 모델 파일을 바꿔도 유지된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArtSlot : MonoBehaviour
    {
        public const string ModelObjectName = "Model";
        public const string PlaceholderObjectName = "Placeholder";

        [Header("ArtKey")]
        [Tooltip("비워 두면 프리팹 파일 이름이 ArtKey다. 다른 프리팹의 모델을 함께 쓸 때만 적는다.")]
        [SerializeField] private string _artKeyOverride;
        [Tooltip("모델 없이 텍스처(<ArtKey>_BaseMap)만 Placeholder에 입힌다. 바닥 얼룩 등")]
        [SerializeField] private bool _isTextureOnly;

        [Header("참조 (ArtBinder가 채움)")]
        [SerializeField] private GameObject _placeholder;
        [SerializeField] private GameObject _model;

        [Header("보정")]
        [SerializeField] private EArtFitMode _fitMode = EArtFitMode.None;
        [SerializeField] private Vector3 _modelOffset = Vector3.zero;
        [SerializeField] private Vector3 _modelEuler = Vector3.zero;
        [Tooltip("Fit Mode로 맞춘 뒤 추가로 곱하는 배율")]
        [SerializeField] private float _modelScale = 1f;

        private Animator _animator;

        public string ArtKeyOverride => _artKeyOverride;
        public bool IsTextureOnly => _isTextureOnly;
        public GameObject Placeholder => _placeholder;
        public GameObject Model => _model;
        public EArtFitMode FitMode => _fitMode;
        public Vector3 ModelOffset => _modelOffset;
        public Vector3 ModelEuler => _modelEuler;
        public float ModelScale => _modelScale;
        public CharacterAnimationSet AnimationSet => GetComponent<CharacterAnimationSet>();

        /// <summary>게임 스크립트는 Visual 아래를 경로로 찾지 말고 이 접근자로 애니메이터를 얻는다.</summary>
        public Animator Animator
        {
            get
            {
                if (_animator == null && _model != null)
                {
                    _animator = _model.GetComponentInChildren<Animator>(true);
                }
                return _animator;
            }
        }
    }
}
