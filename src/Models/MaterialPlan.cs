using System.Collections.Immutable;

namespace ShuatiKing.Models;

public sealed record SourceExcerpt(string Location, string Text);
public sealed record MaterialUnit(int Id, string Scope, ImmutableArray<SourceExcerpt> Sources, bool Split)
{
    public int Length => Sources.Sum(source => source.Text.Length);
    public string Text => string.Join("", Sources.Select(source => source.Text));
}
public sealed record MaterialBatch(int Number, ImmutableArray<MaterialUnit> Units, ImmutableArray<int> Targets);
public sealed record MaterialPlan(string Name, string SourceVersion, bool ExistingQuestions,
    ImmutableArray<int> Targets, ImmutableArray<MaterialBatch> Batches, Guid ConfigurationId, string Model);
