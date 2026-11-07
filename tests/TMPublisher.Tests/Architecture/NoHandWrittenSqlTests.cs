using System.Text.RegularExpressions;
using Shouldly;

namespace TMPublisher.Tests.Architecture;

/// <summary>
/// No SQL written by hand anywhere under <c>src/</c> (rule SQL1): the database is reached only through EF Core's model and LINQ (queries,
/// SaveChanges, ExecuteUpdate / ExecuteDelete, EnsureCreated). No command text, no raw or interpolated SQL methods, no statements in strings.
/// </summary>
public sealed partial class NoHandWrittenSqlTests
{
    [Fact]
    public void SourcesContainNoHandWrittenSql()
    {
        var violations = RepositoryFiles.Sources("*.cs")
            .SelectMany(p => File.ReadAllLines(Path.Combine(RepositoryFiles.Root, p))
                .Select((line, i) => (Path: p, Number: i + 1, Line: line)))
            .Where(l => !l.Line.TrimStart().StartsWith("//", StringComparison.Ordinal) && Sql().IsMatch(l.Line))
            .Select(l => $"SQL1 {l.Path}:{l.Number}: {l.Line.Trim()}")
            .ToList();
        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData("command.CommandText = x;")]
    [InlineData("await context.Database.ExecuteSqlRawAsync(sql);")]
    [InlineData("context.Database.ExecuteSql($\"x\");")]
    [InlineData("context.Posts.FromSqlRaw(sql)")]
    [InlineData("context.Database.SqlQuery<int>($\"x\")")]
    [InlineData("var c = new SqliteCommand(sql, connection);")]
    [InlineData("var s = \"SELECT \\\"Id\\\" FROM \\\"posts\\\"\";")]
    [InlineData("var s = \"PRAGMA journal_mode = 'wal'\";")]
    public void HandWrittenSqlIsFound(string line) => Sql().IsMatch(line).ShouldBeTrue();

    [GeneratedRegex(
        @"CommandText|SqliteCommand|CreateCommand\(|ExecuteSql|FromSql|SqlQuery|ExecuteScalar|ExecuteReader|ExecuteNonQuery|""\s*(SELECT|INSERT|UPDATE|DELETE|CREATE|DROP|ALTER|PRAGMA|BEGIN)\b")]
    private static partial Regex Sql();
}
