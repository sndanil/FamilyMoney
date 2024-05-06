using CsvHelper.Configuration.Attributes;
using System;

namespace FamilyMoney.Import;

public sealed class CsvImportRow
{
    [Index(0)]
    public string? Date { get; set; }
    [Index(1)]
    public decimal Sum { get; set; }
    [Index(2)]
    public string? CreditAccount { get; set; }
    [Index(3)]
    public string? DebetAccount { get; set; }
    [Index(4)]
    public string? Category { get; set; }
    [Index(5)]
    public string? SubCategory { get; set; }
    [Index(6)]
    public string? Comment { get; set; }
    [Index(7)]
    public decimal? CreditSum { get; set; }
    [Index(8)]
    public decimal? DebetSum { get; set; }
    [Index(9)]
    public string? Accounts { get; set; }
    [Index(10)]
    public string? Id { get; set; }
}
