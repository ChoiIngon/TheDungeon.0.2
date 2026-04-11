# MetaData 클래스 명세

## 1. 개요

`MetaData`는 CSV 파일을 읽어 C# 프로퍼티/필드에 자동으로 바인딩하는 추상 베이스 클래스입니다.  
파생 클래스는 생성자에서 `Bind()` 호출만으로 컬럼-멤버 매핑을 선언하며, 실제 파일 읽기는 `Reader<TMeta>`가 담당합니다.

---

## 2. CSV 파일 포맷

```
Row 0 : 컬럼 헤더 (필드명)
Row 1 : 설명 행   (읽기 시 건너뜀)
Row 2+: 실제 데이터
```

### 헤더 표기 규칙

| 종류 | 헤더 형식 | 예시 |
|------|-----------|------|
| 일반 필드 | `FieldName` | `DungeonId` |
| 배열(List) 필드 | `FieldName[n]` | `RewardIds[0]`, `RewardIds[1]` |
| 중첩 MetaData | `FieldName.ChildField` | `SpawnInfo.X` |
| MetaData 배열 | `FieldName[n].ChildField` | `RewardDatas[0].ItemId` |

> **컬럼명은 프로퍼티명과 대소문자까지 동일하게 맞춰야 합니다.** 자동 변환(camelCase 등) 없음.

---

## 3. 지원 타입

### 스칼라

| C# 타입 | CSV 값 예시 |
|---------|------------|
| `bool` | `TRUE` / `FALSE` / `1` / `0` |
| `short`, `ushort` | `255` |
| `int`, `uint` | `1001` |
| `long`, `ulong` | `9999999999` |
| `float` | `3.14` |
| `double` | `3.14159265` |
| `string` | `어둠의 동굴` |
| `enum` | `Easy` (이름) 또는 `1` (정수) 모두 허용, 대소문자 무시 |

### 컬렉션

| C# 타입 | 헤더 형식 | 비고 |
|---------|----------|------|
| `List<스칼라>` | `Field[0]`, `Field[1]` | 기본 파서 자동 선택 |
| `List<MetaData 파생>` | `Field[0].Child`, `Field[1].Child` | 요소 인스턴스 자동 생성 |

> `Dictionary`, `HashSet` 등 다른 컬렉션은 지원하지 않습니다.  
> CSV의 행/열 구조와 궁합이 맞지 않아 의도적으로 제외하였습니다.

### 중첩 MetaData (단일)

단일 서브 객체는 점 표기로 접근합니다.

```
SpawnInfo.X,  SpawnInfo.Y
```

---

## 4. API

### `MetaData.Reader<TMeta>`

```csharp
var reader = new MetaData.Reader<DungeonLevelMetaData>();
bool ok = reader.Read(filePath);           // CSV 파일 읽기
IReadOnlyList<DungeonLevelMetaData> all = reader.All;
```

| 멤버 | 설명 |
|------|------|
| `bool Read(string filePath)` | 파일이 없으면 `false` 반환, 파싱 후 `All` 채움 |
| `IReadOnlyList<TMeta> All` | 파싱된 인스턴스 전체 목록 |

---

### `Bind()` — 자동 바인딩 (권장)

```csharp
Bind(PropertyName);
```

- `CallerArgumentExpression`으로 인수 표현식 텍스트를 컬럼명으로 자동 추출
- 프로퍼티/필드 타입을 리플렉션으로 읽어 파서 자동 선택
- `List<T>` / `List<MetaData 파생>` / `enum` 모두 자동 처리

---

### `Bind()` — 명시적 바인딩 (고급)

```csharp
// 스칼라
Bind("ColumnName", (int v)    => Field = v);
Bind("ColumnName", (string v) => Field = v);
// ... bool, short, ushort, int, uint, long, ulong, float, double, string

// List<스칼라>
Bind("ColumnName", listField, int.Parse);

// List<MetaData 파생>
Bind("ColumnName", listField);           // 파서 인수 없음

// 단일 MetaData 서브 객체
Bind("ColumnName", subMetaField);

// 커스텀 파서
BindFunc("ColumnName", raw => Field = MyParse(raw));
```

---

## 5. 파생 클래스 작성 예시

```csharp
public class DungeonLevelMetaData : MetaData
{
    public enum DifficultyLevel { Easy = 1, Normal, Hard, VeryHard, Nightmare }

    public int             DungeonId   { get; private set; }
    public string          Name        { get; private set; }
    public DifficultyLevel Difficulty  { get; private set; }
    public List<RewardMetaData> RewardDatas { get; private set; } = new List<RewardMetaData>();

    public class RewardMetaData : MetaData
    {
        public int ItemId { get; private set; }
        public int Count  { get; private set; }

        public RewardMetaData()
        {
            Bind(ItemId);
            Bind(Count);
        }
    }

    public DungeonLevelMetaData()
    {
        Bind(DungeonId);
        Bind(Name);
        Bind(Difficulty);    // enum 자동 처리
        Bind(RewardDatas);   // List<MetaData 파생> 자동 처리
    }
}
```

대응 CSV:

```
DungeonId,Name,Difficulty,RewardDatas[0].ItemId,RewardDatas[0].Count
ID,이름,난이도,보상아이템ID,보상수량
1001,어둠의 동굴,Easy,2001,1
1002,잊혀진 유적,Normal,2003,1
```

---

## 6. 제약 사항

- 파생 클래스는 `new()` 제약을 충족해야 합니다 (기본 생성자 필수).
- `List<MetaData 파생>` 요소 타입도 `new()` 제약 필수.
- 컬럼명 대소문자는 프로퍼티명과 **정확히** 일치해야 합니다.
- `bool` 파싱: `"false"`, `"0"` 이외의 값은 모두 `true`로 처리됩니다.
- CSV 셀 값이 비어 있으면 해당 필드는 바인딩을 건너뛰고 기본값을 유지합니다.
- BOM(`\uFEFF`) 자동 제거 (UTF-8 with BOM 파일 지원).

---

## 7. 플랫폼별 경로 처리

```csharp
#if UNITY_ANDROID || UNITY_WEBGL
    string csvPath = Application.streamingAssetsPath + "/MetaData/DungeonLevel.csv";
#else
    string csvPath = Path.Combine(Application.streamingAssetsPath, "MetaData", "DungeonLevel.csv");
#endif
var reader = new MetaData.Reader<DungeonLevelMetaData>();
reader.Read(csvPath);
```
