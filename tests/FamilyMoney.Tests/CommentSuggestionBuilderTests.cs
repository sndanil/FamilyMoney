using FamilyMoney.DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FamilyMoney.Tests;

public class CommentSuggestionBuilderTests
{
    private static readonly DateTime BaseDate = new(2025, 1, 31);

    [Fact]
    public void Build_ReturnsAtMostTotalLimit_RecentFirst()
    {
        var candidates = Enumerable.Range(0, 30)
            .Select(i => Candidate(i, $"Комментарий {i}"))
            .ToList();

        var result = CommentSuggestionBuilder.Build(candidates);

        Assert.Equal(20, result.Count);
        Assert.Equal("Комментарий 0", result[0]);
        Assert.Equal("Комментарий 9", result[9]);
    }

    [Fact]
    public void Build_FillsRemainingSlotsWithPopular()
    {
        var candidates = new List<CommentCandidate>
        {
            Candidate(1, "Свежий 1"),
            Candidate(2, "Свежий 2"),
            Candidate(5, "Популярный 1"),
            Candidate(6, "Популярный 1"),
            Candidate(7, "Популярный 1"),
            Candidate(8, "Популярный 2"),
            Candidate(9, "Популярный 2"),
            Candidate(10, "Популярный 3"),
        };

        var result = CommentSuggestionBuilder.Build(candidates, recentLimit: 2, totalLimit: 5);

        Assert.Equal(
            new[] { "Свежий 1", "Свежий 2", "Популярный 1", "Популярный 2", "Популярный 3" },
            result);
    }

    [Fact]
    public void Build_DeduplicatesCaseInsensitively_AndKeepsNewestSpelling()
    {
        var candidates = new List<CommentCandidate>
        {
            Candidate(5, "обед"),
            Candidate(1, "Обед"),
        };

        var result = CommentSuggestionBuilder.Build(candidates);

        Assert.Equal(new[] { "Обед" }, result);
    }

    [Fact]
    public void Build_IgnoresEmptyAndWhitespaceComments()
    {
        var candidates = new List<CommentCandidate>
        {
            Candidate(3, "  "),
            Candidate(2, ""),
            Candidate(1, " Нормальный "),
        };

        var result = CommentSuggestionBuilder.Build(candidates);

        Assert.Equal(new[] { "Нормальный" }, result);
    }

    [Fact]
    public void Build_SortsEqualPopularityByLastDate()
    {
        var candidates = new List<CommentCandidate>
        {
            Candidate(5, "Старый"),
            Candidate(1, "Новый"),
        };

        var result = CommentSuggestionBuilder.Build(candidates, recentLimit: 0, totalLimit: 2);

        Assert.Equal(new[] { "Новый", "Старый" }, result);
    }

    private static CommentCandidate Candidate(int daysAgo, string comment)
    {
        var date = BaseDate.AddDays(-daysAgo);
        return new CommentCandidate(date, date, comment);
    }
}
