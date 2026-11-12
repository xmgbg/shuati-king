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
    public const int GenerationBatchCharacters = 3000;
    private static readonly Regex Heading = new(@"^\s*(第[一二三四五六七八九十百零〇0-9]+章|Chapter\s+\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex QuestionStart = new(@"^\s*(?:\d+(?:[.．](?!\d)|[、)）])|[（(]\d+[)）])\s*");

    private static readonly Regex ContentsEntry = new(@"(?:(?:[.．·]\s*){3,}|(?:…\s*){2,})\d+\s*$");
    private static readonly Regex ExerciseHeading = new(@"^\s*(课后习题|课后练习|本章习题|本章练习|习题|练习题)\s*[:：]?\s*$");

    public static MaterialPlan Create(SourceDocument source, bool existing, IEnumerable<QuestionType> selectedTypes, AiConfiguration? configuration = null)
    {
        if (!source.HasText) throw new ArgumentException("资料内容不能为空。");
        var types = existing ? [] : selectedTypes.Distinct().Order().ToImmutableArray();
        if (!existing && (types.IsEmpty || types.Any(type => !Enum.IsDefined(type))))
            throw new ArgumentException("请选择有效的题型。");
        var units = new List<MaterialUnit>();
        var pieces = new List<SourceExcerpt>();
        var scope = "全文";
        var batchCharacters = existing ? BatchCharacters : GenerationBatchCharacters;
        var inContents = false;
        var inExercises = false;
        void Flush()
        {
            if (pieces.Count == 0) return;
            units.Add(new(units.Count + 1, scope, pieces.ToImmutableArray(), false, inExercises)); pieces.Clear();
        }
        foreach (var section in source.Sections)
        {
            // 位置来自已确认预览；知识资料中的分页标记不会切断正文。
            var pageNumber = existing ? "" : Regex.Match(section.Location, @"^第 (\d+) 页$").Groups[1].Value;
            var firstLine = true;
            foreach (Match match in Regex.Matches(section.Text + "\n", @"[^\n]*\n"))
            {
                var line = match.Value;
                if (!existing)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (!inExercises) Flush();
                        continue;
                    }
                    if (firstLine)
                    {
                        firstLine = false;
                        if (line.Trim() == pageNumber) continue;
                    }
                    if (Regex.IsMatch(line, @"^\s*目\s*录\s*$"))
                    {
                        Flush();
                        inContents = true;
                        continue;
                    }
                    if (inContents && ContentsEntry.IsMatch(line)) continue;
                    inContents = false;
                    if (ExerciseHeading.IsMatch(line))
                    {
                        Flush();
                        inExercises = true;
                        continue;
                    }
                }
                var heading = Heading.Match(line);
                if (heading.Success)
                {
                    Flush();
                    inExercises = false;
                    scope = Regex.Replace(heading.Groups[1].Value, @"\s+", "").ToLowerInvariant();
                }
                else if ((existing || inExercises) && QuestionStart.IsMatch(line)) Flush();
                if (pieces.Count > 0 && pieces[^1].Location == section.Location)
                    pieces[^1] = pieces[^1] with { Text = pieces[^1].Text + line };
                else pieces.Add(new(section.Location, line));
            }
        }
        Flush();
        var bounded = new List<MaterialUnit>();
        foreach (var unit in units)
        {
            var split = unit.Length > batchCharacters;
            var current = new List<SourceExcerpt>(); var size = 0;
            void AddUnit()
            {
                if (size == 0) return;
                bounded.Add(new(bounded.Count + 1, unit.Scope, current.ToImmutableArray(), split, unit.Exercise)); current.Clear(); size = 0;
            }
            foreach (var piece in unit.Sources)
            {
                var offset = 0;
                while (offset < piece.Text.Length)
                {
                    var take = Math.Min(batchCharacters - size, piece.Text.Length - offset);
                    // 超长段落优先在句末或换行处分开，保持字符完整覆盖。
                    if (take < piece.Text.Length - offset && take > 100)
                    {
                        var end = piece.Text.LastIndexOfAny(['。', '！', '？', '\n'], offset + take - 1, take);
                        if (end >= offset + take / 2) take = end - offset + 1;
                    }
                    current.Add(new(piece.Location, piece.Text.Substring(offset, take))); size += take; offset += take;
                    if (size == batchCharacters || offset < piece.Text.Length) AddUnit();
                }
            }
            AddUnit();
        }
        var groups = new List<List<MaterialUnit>>();
        foreach (var unit in bounded)
        {
            if (groups.Count == 0 || groups[^1].Sum(item => item.Length) + unit.Length > batchCharacters || groups[^1][0].Scope != unit.Scope)
                groups.Add([]);
            groups[^1].Add(unit);
        }
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Text)));
        return new(source.Name, version, existing, types, groups.Select((group, index) =>
            new MaterialBatch(index + 1, group.ToImmutableArray())).ToImmutableArray(),
            configuration?.Id ?? Guid.Empty, configuration?.Model.Trim() ?? "");
    }
}
