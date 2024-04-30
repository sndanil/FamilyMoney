using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.Csv
{
    public sealed class ImportRow
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
    }
}
