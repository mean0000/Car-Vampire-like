namespace Meta
{
    /// <summary>
    /// 메타 진행(지갑·업그레이드) 직렬화 백엔드. 스켈레톤 구현 = persistentDataPath JSON 파일.
    /// 인터페이스로 분리해 테스트/플랫폼 교체 시 소비처(Wallet·Upgrades)를 안 건드린다.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>저장된 JSON 문자열. 없으면 null 또는 빈 문자열.</summary>
        string Load();

        /// <summary>JSON 문자열을 저장(덮어쓰기).</summary>
        void Save(string json);
    }
}
