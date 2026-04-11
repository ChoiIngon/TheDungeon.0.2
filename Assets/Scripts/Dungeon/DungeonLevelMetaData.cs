using System.Collections.Generic;

/// <summary>
/// 던전 레벨 디자인 메타데이터.
/// 대응 CSV: StreamingAssets/MetaData/DungeonLevel.csv
/// </summary>
public class DungeonLevelMetaData : MetaData
{
    public enum DifficultyLevel
    {
        Easy = 1,
        Normal = 2,
        Hard = 3,
        VeryHard = 4,
        Nightmare = 5
    }
    /// <summary>던전 고유 ID</summary>
    public int DungeonId { get; private set; }

    /// <summary>던전 이름</summary>
    public string Name { get; private set; }

    /// <summary>던전 설명</summary>
    public string Description { get; private set; }

    /// <summary>입장 가능 레벨</summary>
    public int RequiredLevel { get; private set; }

    /// <summary>권장 전투력</summary>
    public int RecommendPower { get; private set; }

    /// <summary>난이도 (1.0 ~ 5.0)</summary>
    public DifficultyLevel Difficulty { get; private set; }

    /// <summary>보스 스테이지 여부</summary>
    public bool IsBossStage { get; private set; }

    /// <summary>제한 시간 (초, 0 = 무제한)</summary>
    public int TimeLimit { get; private set; }

    /// <summary>최대 입장 인원</summary>
    public int MaxPlayers { get; private set; }

    /// <summary>맵 리소스 ID</summary>
    public int MapId { get; private set; }

    /// <summary>BGM ID</summary>
    public int BgmId { get; private set; }

    /// <summary>몬스터 그룹 ID</summary>
    public int MonsterGroupId { get; private set; }

    public class RewardMetaData : MetaData
    {
        public int ItemId { get; private set; }
        public int Count { get; private set; }

        public RewardMetaData()
        {
            Bind(ItemId);
            Bind(Count);
        }
    }
    /// <summary>클리어 보상 아이템 ID 목록 (rewardId[0], rewardId[1], ...)</summary>
    public List<RewardMetaData> RewardDatas { get; private set; } = new List<RewardMetaData>();

    
    /// <summary>클리어 시 획득 경험치</summary>
    public int ClearExp { get; private set; }

    /// <summary>클리어 시 획득 골드</summary>
    public int ClearGold { get; private set; }

    public DungeonLevelMetaData()
    {
        Bind(DungeonId);
        Bind(Name);
        Bind(Description);
        Bind(RequiredLevel);
        Bind(RecommendPower);
        Bind(Difficulty);
        Bind(IsBossStage);
        Bind(TimeLimit);
        Bind(MaxPlayers);
        Bind(MapId);
        Bind(BgmId);
        Bind(MonsterGroupId);
        Bind(RewardDatas);
        Bind(ClearExp);
        Bind(ClearGold);
    }
}