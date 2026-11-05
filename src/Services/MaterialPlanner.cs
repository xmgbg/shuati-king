using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShuatiKing.Models;

namespace ShuatiKing.Services;

public static class MaterialPlanner
{
    // 字符数是请求体大小的边界，不冒充精确 Token 数；不删除页眉等无法可靠判断的内容。
    public const int BatchCharacters = 6000;
    private static readonly Regex Heading = new(@"^\s*(第[一二三四五六七八九十百零〇0-9]+章|Chapter\s+\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex QuestionStart = new(@"^\s*(?:\d+[.．、)）]|[（(]\d+[)）])\s*");

    public static MaterialPlan Create(SourceDocument source, bool existing, int[] targets, AiConfiguration? configuration = null)
    {
        if (!source.HasText) throw new ArgumentException("资料内容不能为空。");
        if (targets.Length != 4 || targets.Any(number => number < 0 || number > 100)) throw new ArgumentException("题量必须为四种题型各 0–100 道。");
        var units = new List<MaterialUnit>();
        var pieces = new List<SourceExcerpt>();
        var scope = "全文";
        void Flush()
        {
            if (pieces.Count == 0) return;
            units.Add(new(units.Count + 1, scope, pieces.ToImmutableArray(), false)); pieces.Clear();
        }
        foreach (var section in source.Sections)
        {
            // 保留换行和空段，位置来自已确认预览，后续编辑不会改写该快照。
            foreach (Match match in Regex.Matches(section.Text + "\n", @"[^\n]*\n"))
            {
                var line = match.Value;
                var heading = Heading.Match(line);
                if (heading.Success) { Flush(); scope = Regex.Replace(heading.Groups[1].Value, @"\s+", "").ToLowerInvariant(); }
                else if (existing && QuestionStart.IsMatch(line) || !existing && string.IsNullOrWhiteSpace(line)) Flush();
                if (pieces.Count > 0 && pieces[^1].Location == section.Location)
                    pieces[^1] = pieces[^1] with { Text = pieces[^1].Text + line };
                else pieces.Add(new(section.Location, line));
                if (!existing && !string.IsNullOrWhiteSpace(line)) Flush();
            }
        }
        Flush();
        var bounded = new List<MaterialUnit>();
        foreach (var unit in units)
        {
            var split = unit.Length > BatchCharacters;
            var current = new List<SourceExcerpt>(); var size = 0;
            void AddUnit()
            {
                if (size == 0) return;
                bounded.Add(new(bounded.Count + 1, unit.Scope, current.ToImmutableArray(), split)); current.Clear(); size = 0;
            }
            foreach (var piece in unit.Sources)
            {
                var offset = 0;
                while (offset < piece.Text.Length)
                {
                    var take = Math.Min(BatchCharacters - size, piece.Text.Length - offset);
                    // 超长段落优先在句末或换行处分开，保持字符完整覆盖。
                    if (take < piece.Text.Length - offset && take > 100)
                    {
                        var end = piece.Text.LastIndexOfAny(['。', '！', '？', '\n'], offset + take - 1, take);
                        if (end >= offset + take / 2) take = end - offset + 1;
                    }
                    current.Add(new(piece.Location, piece.Text.Substring(offset, take))); size += take; offset += take;
                    if (size == BatchCharacters || offset < piece.Text.Length) AddUnit();
                }
            }
            AddUnit();
        }
        var groups = new List<List<MaterialUnit>>();
        foreach (var unit in bounded)
        {
            if (groups.Count == 0 || groups[^1].Sum(item => item.Length) + unit.Length > BatchCharacters || groups[^1][0].Scope != unit.Scope)
                groups.Add([]);
            groups[^1].Add(unit);
        }
        var allocations = groups.Select(_ => new int[4]).ToArray();
        if (!existing)
        {
            var weights = groups.Select(group => group.Sum(unit => unit.Text.Count(character => !char.IsWhiteSpace(character)))).ToArray();
            var total = weights.Sum();
            for (var type = 0; type < 4; type++)
            {
                var quotas = weights.Select(weight => (double)weight / total * targets[type]).ToArray();
                for (var i = 0; i < groups.Count; i++) allocations[i][type] = (int)Math.Floor(quotas[i]);
                foreach (var index in Enumerable.Range(0, groups.Count).OrderByDescending(i => quotas[i] - allocations[i][type]).ThenBy(i => i)
                    .Take(targets[type] - allocations.Sum(values => values[type]))) allocations[index][type]++;
            }
        }
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Text)));
        return new(source.Name, version, existing, targets.ToImmutableArray(), groups.Select((group, index) =>
            new MaterialBatch(index + 1, group.ToImmutableArray(), allocations[index].ToImmutableArray())).ToImmutableArray(),
            configuration?.Id ?? Guid.Empty, configuration?.Model.Trim() ?? "");
    }
}
