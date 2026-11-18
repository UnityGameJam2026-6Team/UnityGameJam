namespace UnityGameJam.Contract
{
    /// <summary>졸개 간 대사의 현재 화자와 문장을 제공하는 선택 계약.</summary>
    public interface IMinionSpeechSource
    {
        (EWorkerType Speaker, string Text)? CurrentSpeech { get; }
    }
}
