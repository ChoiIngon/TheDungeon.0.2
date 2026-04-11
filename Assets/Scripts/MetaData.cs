using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text;

/// <summary>
/// CSV 기반 메타데이터 시스템의 베이스 클래스.
///
/// [CSV 포맷 규칙]
///   Row 0 : 컬럼 헤더 (필드명)
///   Row 1 : 설명 행  (읽기 시 건너뜀)
///   Row 2+: 실제 데이터
///
/// [헤더 표기 규칙]
///   일반 필드  : fieldName
///   배열 필드  : fieldName[0], fieldName[1], fieldName[2], ...
///   계층 필드  : parentField.childField
///
/// [파생 클래스 사용 예시]
/// <code>
/// public class DungeonLevelData : MetaData
/// {
///     public int    DungeonId   { get; private set; }
///     public string Name        { get; private set; }
///     public float  Difficulty  { get; private set; }
///     public List<int> RewardIds = new List<int>();
///
///     public DungeonLevelData()
///     {
///         // CallerArgumentExpression 기반 자동 바인딩 (권장)
///         Bind(DungeonId);
///         Bind(Name);
///         Bind(Difficulty);
///         Bind(RewardIds);
///
///         // 명시적 바인딩이 필요한 경우 (TSelf 를 명시)
///         Bind<DungeonLevelData>("dungeonId",  (self, v) => self.DungeonId  = v);
///         Bind<DungeonLevelData, int>("rewardId", self => self.RewardIds, int.Parse);
///     }
/// }
///
/// // 읽기
/// var reader = new MetaData.Reader<DungeonLevelData>();
/// reader.Read("DungeonLevel.csv");
/// foreach (var data in reader.All) { ... }
/// </code>
/// </summary>
public abstract class MetaData
{
    // =========================================================
    //  내부 타입
    // =========================================================

    private class Header
    {
        public int    Index { get; set; } = -1;
        public string Name  { get; set; } = string.Empty;
        public Header Child { get; set; } = null;
    }

    private class Cell
    {
        public Header Header { get; set; }
        public string Value  { get; set; } = string.Empty;
    }

    // =========================================================
    //  Reader<TMeta>
    // =========================================================

    public class Reader<TMeta> where TMeta : MetaData, new()
    {
        private readonly List<TMeta> _metaDatas = new List<TMeta>();

        /// <summary>파싱된 메타데이터 전체 목록</summary>
        public IReadOnlyList<TMeta> All => _metaDatas;

        /// <summary>CSV 파일을 읽어 TMeta 목록을 구성합니다.</summary>
        public bool Read(string filePath)
        {
            if (false == File.Exists(filePath))
            {
                return false;
            }

            var rows = ParseCsv(filePath);
            if (rows.Count == 0)
            {
                return false;
            }

            // Row 0: 헤더
            var headerRow = rows[0];
            var headers = new List<Header>(headerRow.Count);
            foreach (var h in headerRow)
            {
                headers.Add(ReadHeader(h));
            }

            // Row 1: 건너뜀 (설명 행)
            // Row 2+: 데이터
            for (int rowNum = 2; rowNum < rows.Count; rowNum++)
            {
                var row = rows[rowNum];

                // 빈 줄 건너뜀
                if (row.Count == 0 || (row.Count == 1 && true == string.IsNullOrWhiteSpace(row[0])))
                {
                    continue;
                }

                var cells = new List<Cell>(headers.Count);
                for (int colNum = 0; colNum < headers.Count; colNum++)
                {
                    cells.Add(new Cell
                    {
                        Header = headers[colNum],
                        Value  = colNum < row.Count ? row[colNum] : string.Empty
                    });
                }

                var meta = new TMeta();
                meta.Init(cells);
                _metaDatas.Add(meta);
            }

            return true;
        }

        // ---------------------------------------------------------
        //  CSV 파싱
        // ---------------------------------------------------------

        private static List<List<string>> ParseCsv(string filePath)
        {
            var result = new List<List<string>>();

            // detectEncodingFromByteOrderMarks: true → BOM 자동 감지 및 스킵
            using var sr = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            bool isFirstLine = true;
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                // StreamReader가 BOM을 처리하지 못한 경우를 대비한 이중 방어
                if (true == isFirstLine)
                {
                    if (line.Length > 0 && line[0] == '\uFEFF')
                    {
                        line = line.Substring(1);
                    }
                    isFirstLine = false;
                }
                result.Add(ParseCsvLine(line));
            }
            return result;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var cells   = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            int i = 0;

            while (i < line.Length)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (false == inQuotes && current.Length == 0)
                    {
                        // 필드 맨 앞 따옴표 → quoted field 시작
                        inQuotes = true;
                    }
                    else if (true == inQuotes)
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            // "" → 이스케이프된 따옴표 한 개
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            // 닫는 따옴표 → quoted field 종료
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        // unquoted field 중간에 등장한 따옴표 → 문자 그대로 추가
                        current.Append(c);
                    }
                }
                else if (c == ',' && false == inQuotes)
                {
                    cells.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }

                i++;
            }

            cells.Add(current.ToString());
            return cells;
        }

        // ---------------------------------------------------------
        //  헤더 파싱  (재귀)
        // ---------------------------------------------------------

        private static Header ReadHeader(string cellValue)
        {
            var root = new Header();

            // 계층 구분자 '.' 처리
            int dotPos = cellValue.IndexOf('.');
            string column = dotPos >= 0 ? cellValue.Substring(0, dotPos) : cellValue;

            // 배열 인덱스 '[n]' 처리
            int braceStart = column.IndexOf('[');
            int braceEnd   = column.IndexOf(']');

            bool hasBraceStart = braceStart >= 0;
            bool hasBraceEnd   = braceEnd   >= 0;

            if (hasBraceStart != hasBraceEnd)
            {
                throw new FormatException($"컬럼명 오류: '{cellValue}', 대괄호 짝이 맞지 않습니다.");
            }

            if (true == hasBraceStart)
            {
                root.Index = int.Parse(column.Substring(braceStart + 1, braceEnd - braceStart - 1));
            }

            // 순수 이름 부분만 추출
            int nameEnd = int.MaxValue;
            if (dotPos     >= 0) { nameEnd = Math.Min(nameEnd, dotPos); }
            if (braceStart >= 0) { nameEnd = Math.Min(nameEnd, braceStart); }
            root.Name = nameEnd == int.MaxValue ? column : column.Substring(0, nameEnd);

            // 자식 헤더 재귀 처리
            if (dotPos >= 0)
            {
                root.Child = ReadHeader(cellValue.Substring(dotPos + 1));
            }

            return root;
        }
    }

    // =========================================================
    //  Init
    // =========================================================

    /// <summary>파싱된 Cell 목록을 바탕으로 멤버를 초기화합니다.</summary>
    private void Init(IList<Cell> row)
    {
        if (false == _typeBindings.TryGetValue(GetType(), out var bindings))
        {
            return;
        }
        foreach (var cell in row)
        {
            string key = cell.Header?.Name ?? string.Empty;
            if (true == string.IsNullOrEmpty(key))                 { continue; }
            if (true == string.IsNullOrEmpty(cell.Value))          { continue; }
            if (false == bindings.TryGetValue(key, out var func))  { continue; }
            func(this, cell);
        }
    }

    // =========================================================
    //  Bind 함수 등록부
    // =========================================================

    /// <summary>
    /// 타입별 바인딩 딕셔너리.
    /// 같은 타입의 모든 인스턴스가 공유하며, 최초 인스턴스 생성 시 1회만 등록됩니다.
    /// </summary>
    private static readonly Dictionary<Type, Dictionary<string, Action<MetaData, Cell>>> _typeBindings = new();

    private static Dictionary<string, Action<MetaData, Cell>> GetOrCreateBindings(Type type)
    {
        if (false == _typeBindings.TryGetValue(type, out var dict))
        {
            _typeBindings[type] = dict = new();
        }
        return dict;
    }

    // ---------------------------------------------------------
    //  스칼라 타입
    //  사용 예: Bind<MyData>("fieldName", (self, v) => self.Field = v);
    // ---------------------------------------------------------

    protected static void Bind<TSelf>(string name, Action<TSelf, bool> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name))
        {
            return;
        }
        b[name] = (inst, cell) => { string l = cell.Value.ToLowerInvariant(); setter((TSelf)inst, l != "false" && l != "0"); };
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, short> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, short.Parse(cell.Value));
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, ushort> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, ushort.Parse(cell.Value));
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, int> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, int.Parse(cell.Value));
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, uint> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, uint.Parse(cell.Value));
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, long> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, long.Parse(cell.Value));
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, ulong> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, ulong.Parse(cell.Value));
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, float> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, float.Parse(cell.Value));
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, double> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, double.Parse(cell.Value));
    }

    protected static void Bind<TSelf>(string name, Action<TSelf, string> setter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name)) { return; }
        b[name] = (inst, cell) => setter((TSelf)inst, cell.Value);
    }

    // ---------------------------------------------------------
    //  배열 타입  (헤더에 [n] 인덱스가 있어야 함)
    //  사용 예: Bind<MyData, int>("rewardId", self => self.RewardIds, int.Parse);
    // ---------------------------------------------------------

    protected static void Bind<TSelf, T>(string name, Func<TSelf, List<T>> listGetter, Func<string, T> parser)
        where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name))
        {
            return;
        }
        b[name] = (inst, cell) =>
        {
            int idx = cell.Header.Index;
            if (idx < 0)
            {
                throw new InvalidOperationException(
                    $"컬럼 '{name}' 은 배열 컬럼이 아닙니다. 헤더에 [n] 인덱스가 필요합니다.");
            }
            var list = listGetter((TSelf)inst);
            while (list.Count <= idx)
            {
                list.Add(default);
            }
            list[idx] = parser(cell.Value);
        };
    }

    // ---------------------------------------------------------
    //  MetaData 서브클래스 배열 타입  (헤더에 [n].field 형식)
    //  사용 예: Bind<MyData, RewardData>("RewardDatas", self => self.RewardDatas);
    //  CSV 헤더 형식: RewardDatas[0].ItemId, RewardDatas[0].Count, ...
    // ---------------------------------------------------------

    protected static void Bind<TSelf, T>(string name, Func<TSelf, List<T>> listGetter)
        where TSelf : MetaData where T : MetaData, new()
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name))
        {
            return;
        }
        b[name] = (inst, cell) =>
        {
            int idx = cell.Header.Index;
            if (idx < 0)
            {
                throw new InvalidOperationException($"컬럼 '{name}' 은 배열 컬럼이 아닙니다. 헤더에 [n] 인덱스가 필요합니다.");
            }
            var list = listGetter((TSelf)inst);
            while (list.Count <= idx)
            {
                list.Add(new T());
            }
            if (cell.Header.Child != null)
            {
                list[idx].Init(new List<Cell> { new Cell { Header = cell.Header.Child, Value = cell.Value } });
            }
        };
    }

    // ---------------------------------------------------------
    //  하위 MetaData (점 표기 계층 구조)
    //  사용 예: Bind<MyData>("spawn", self => self.SpawnInfo);
    // ---------------------------------------------------------

    protected static void Bind<TSelf>(string name, Func<TSelf, MetaData> subMetaGetter) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name))
        {
            return;
        }
        b[name] = (inst, cell) =>
        {
            if (cell.Header?.Child == null)
            {
                return;
            }
            var childCell = new Cell { Header = cell.Header.Child, Value = cell.Value };
            subMetaGetter((TSelf)inst).Init(new List<Cell> { childCell });
        };
    }

    // ---------------------------------------------------------
    //  커스텀 함수 바인딩
    //  사용 예: BindFunc<MyData>("flags", (self, raw) => self.ParseFlags(raw));
    // ---------------------------------------------------------

    protected static void BindFunc<TSelf>(string name, Action<TSelf, string> customFunction) where TSelf : MetaData
    {
        var b = GetOrCreateBindings(typeof(TSelf));
        if (true == b.ContainsKey(name))
        {
            return;
        }
        b[name] = (inst, cell) => customFunction((TSelf)inst, cell.Value);
    }

    // ---------------------------------------------------------
    //  CallerArgumentExpression 기반 자동 바인딩  (C# 10+)
    //  사용 예: Bind(DungeonId);
    //  · 인수 표현식 텍스트("DungeonId")를 컬럼명으로 자동 사용
    //  · 프로퍼티 타입에 맞는 파서 자동 선택
    //  · List<T> 프로퍼티는 요소 타입의 기본 파서를 자동 적용
    //  · 같은 타입의 두 번째 이후 인스턴스는 등록을 건너뜁니다.
    // ---------------------------------------------------------

    protected void Bind<T>(T value, [CallerArgumentExpression("value")] string memberName = null)
    {
        _ = value; // T 타입 추론에만 사용. CallerArgumentExpression 에 의해 memberName 으로 변환됨.
        if (true == string.IsNullOrEmpty(memberName))
        {
            return;
        }

        Type selfType = GetType();
        var bindings = GetOrCreateBindings(selfType);
        if (true == bindings.ContainsKey(memberName))
        {
            return; // 이미 등록됨 → skip
        }

        Type type = typeof(T);

        // ── List<TElem> ──────────────────────────────────────────
        if (true == type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            Type elemType = type.GetGenericArguments()[0];

            FieldInfo    listField = selfType.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo listProp  = listField == null ? selfType.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) : null;

            if (true == typeof(MetaData).IsAssignableFrom(elemType))
            {
                typeof(MetaData)
                    .GetMethod(nameof(RegisterMetaDataListBinding), BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(elemType)
                    .Invoke(null, new object[] { bindings, memberName, listField, listProp });
            }
            else
            {
                typeof(MetaData)
                    .GetMethod(nameof(RegisterPrimitiveListBinding), BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(elemType)
                    .Invoke(null, new object[] { bindings, memberName, listField, listProp, GetDefaultParser(elemType) });
            }
            return;
        }

        // ── Scalar ───────────────────────────────────────────────
        PropertyInfo prop = selfType.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo field = prop == null ? selfType.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) : null;

        // prop/field 만 capture. this 는 capture 하지 않음.
        void SetOnInstance(MetaData inst, object v)
        {
            prop?.GetSetMethod(nonPublic: true)?.Invoke(inst, new object[] { v });
            field?.SetValue(inst, v);
        }

        if      (type == typeof(bool))   { bindings[memberName] = (inst, cell) => { string l = cell.Value.ToLowerInvariant(); SetOnInstance(inst, l != "false" && l != "0"); }; }
        else if (type == typeof(short))  { bindings[memberName] = (inst, cell) => SetOnInstance(inst, short.Parse(cell.Value)); }
        else if (type == typeof(ushort)) { bindings[memberName] = (inst, cell) => SetOnInstance(inst, ushort.Parse(cell.Value)); }
        else if (type == typeof(int))    { bindings[memberName] = (inst, cell) => SetOnInstance(inst, int.Parse(cell.Value)); }
        else if (type == typeof(uint))   { bindings[memberName] = (inst, cell) => SetOnInstance(inst, uint.Parse(cell.Value)); }
        else if (type == typeof(long))   { bindings[memberName] = (inst, cell) => SetOnInstance(inst, long.Parse(cell.Value)); }
        else if (type == typeof(ulong))  { bindings[memberName] = (inst, cell) => SetOnInstance(inst, ulong.Parse(cell.Value)); }
        else if (type == typeof(float))  { bindings[memberName] = (inst, cell) => SetOnInstance(inst, float.Parse(cell.Value)); }
        else if (type == typeof(double)) { bindings[memberName] = (inst, cell) => SetOnInstance(inst, double.Parse(cell.Value)); }
        else if (type == typeof(string)) { bindings[memberName] = (inst, cell) => SetOnInstance(inst, cell.Value); }
        else if (true == type.IsEnum)    { bindings[memberName] = (inst, cell) => SetOnInstance(inst, ParseEnum(type, cell.Value)); }
        else                             { throw new NotSupportedException($"지원하지 않는 타입: {type.Name}"); }
    }

    // ---------------------------------------------------------
    //  List 바인딩 등록 헬퍼 (reflection 으로 제네릭 타입 확정 후 1회 호출)
    // ---------------------------------------------------------

    private static void RegisterPrimitiveListBinding<TElem>(
        Dictionary<string, Action<MetaData, Cell>> bindings,
        string memberName,
        FieldInfo    listField,
        PropertyInfo listProp,
        Func<string, TElem> parser)
    {
        bindings[memberName] = (instance, cell) =>
        {
            int idx = cell.Header.Index;
            if (idx < 0)
            {
                throw new InvalidOperationException(
                    $"컬럼 '{memberName}' 은 배열 컬럼이 아닙니다. 헤더에 [n] 인덱스가 필요합니다.");
            }
            var list = (List<TElem>)(listField?.GetValue(instance) ?? listProp.GetValue(instance));
            while (list.Count <= idx)
            {
                list.Add(default);
            }
            list[idx] = parser(cell.Value);
        };
    }

    private static void RegisterMetaDataListBinding<TElem>(
        Dictionary<string, Action<MetaData, Cell>> bindings,
        string memberName,
        FieldInfo    listField,
        PropertyInfo listProp) where TElem : MetaData, new()
    {
        bindings[memberName] = (instance, cell) =>
        {
            int idx = cell.Header.Index;
            if (idx < 0)
            {
                throw new InvalidOperationException(
                    $"컬럼 '{memberName}' 은 배열 컬럼이 아닙니다. 헤더에 [n] 인덱스가 필요합니다.");
            }
            var list = (List<TElem>)(listField?.GetValue(instance) ?? listProp.GetValue(instance));
            while (list.Count <= idx)
            {
                list.Add(new TElem());
            }
            if (cell.Header.Child != null)
            {
                list[idx].Init(new List<Cell> { new Cell { Header = cell.Header.Child, Value = cell.Value } });
            }
        };
    }

    // ---------------------------------------------------------

    /// <summary>
    /// 문자열을 enum 값으로 변환합니다.
    /// · "Easy", "Normal" 등 이름 형식과 "1", "2" 등 정수 형식을 모두 허용합니다.
    /// </summary>
    private static object ParseEnum(Type enumType, string value)
    {
        // 이름 형식 (대소문자 무시)
        try { return Enum.Parse(enumType, value, ignoreCase: true); }
        catch { /* ignored */ }

        // 정수 형식
        if (true == int.TryParse(value, out int intVal))
        {
            return Enum.ToObject(enumType, intVal);
        }

        throw new FormatException($"'{value}'을 {enumType.Name} 열거형으로 변환할 수 없습니다.");
    }

    private static object GetDefaultParser(Type elemType)
    {
        if (elemType == typeof(bool))
        {
            return (Func<string, bool>)(s => { var l = s.ToLowerInvariant(); return l != "false" && l != "0"; });
        }
        if (elemType == typeof(short))  { return (Func<string, short>) short.Parse; }
        if (elemType == typeof(ushort)) { return (Func<string, ushort>)ushort.Parse; }
        if (elemType == typeof(int))    { return (Func<string, int>)   int.Parse; }
        if (elemType == typeof(uint))   { return (Func<string, uint>)  uint.Parse; }
        if (elemType == typeof(long))   { return (Func<string, long>)  long.Parse; }
        if (elemType == typeof(ulong))  { return (Func<string, ulong>) ulong.Parse; }
        if (elemType == typeof(float))  { return (Func<string, float>) float.Parse; }
        if (elemType == typeof(double)) { return (Func<string, double>)double.Parse; }
        if (elemType == typeof(string)) { return (Func<string, string>)(s => s); }
        throw new NotSupportedException($"List<{elemType.Name}>의 기본 파서가 없습니다.");
    }
}

// CallerArgumentExpression 폴리필 — Unity가 포함하지 않는 버전을 위한 정의.
// Roslyn 컴파일러가 이름으로 특성을 찾으므로 같은 네임스페이스에 internal 로 선언하면 동작합니다.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
            => ParameterName = parameterName;
        public string ParameterName { get; }
    }
}
