using CsvHelper.Configuration;
using CsvHelper;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Import;

internal class CsvImporter: IImporter
{
    private readonly ILogger _logger;
    private readonly IRepository _repository;

    public CsvImporter(ILogger<CsvImporter> logger, IRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public void DoImport(Stream stream)
    {
        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = false,
            };

            var categories = _repository.GetCategories().ToList();
            var subCategories = _repository.GetSubCategories().ToList();
            var accounts = _repository.GetAccounts().Where(a => !a.IsGroup).ToList();

            var transactions = new List<Transaction>();

            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, config);

            var records = csv.GetRecords<CsvImportRow>().ToList();
            foreach (var record in records)
            {
                var isTransfer = record.Accounts?.Contains(':') == true;
                var isCredit = record.Sum < 0 && string.IsNullOrEmpty(record.DebetAccount);

                Category? category = TryGetCategory(_repository, categories, record, isTransfer, isCredit);
                SubCategory? subCategory = TryGetSubCategory(_repository, category, subCategories, record, isTransfer, isCredit);

                var creditAccount = TryGetAccount(_repository, accounts, record.CreditAccount);
                var debetAccount = TryGetAccount(_repository, accounts, record.DebetAccount);

                Transaction? transaction = null;
                if (isTransfer)
                {
                    transaction = new TransferTransaction
                    {
                        Id = Guid.NewGuid(),
                        Date = DateTime.ParseExact(record.Date!, "dd.MM.yyyy", CultureInfo.InvariantCulture),
                        AccountId = creditAccount?.Id,
                        ToAccountId = debetAccount?.Id,
                        CategoryId = category?.Id,
                        SubCategoryId = subCategory?.Id,
                        Sum = Math.Abs(record.CreditSum!.Value),
                        ToSum = Math.Abs(record.DebetSum!.Value),
                        Comment = record.Comment,
                        LastChange = DateTime.Now,
                    };
                }
                else if (isCredit)
                {
                    transaction = new CreditTransaction
                    {
                        Id = Guid.NewGuid(),
                        Date = DateTime.ParseExact(record.Date!, "dd.MM.yyyy", CultureInfo.InvariantCulture),
                        AccountId = creditAccount?.Id,
                        CategoryId = category?.Id,
                        SubCategoryId = subCategory?.Id,
                        Sum = Math.Abs(record.Sum),
                        Comment = record.Comment,
                        LastChange = DateTime.Now,
                    };
                }
                else
                {
                    transaction = new DebetTransaction
                    {
                        Id = Guid.NewGuid(),
                        Date = DateTime.ParseExact(record.Date!, "dd.MM.yyyy", CultureInfo.InvariantCulture),
                        AccountId = debetAccount?.Id,
                        CategoryId = category?.Id,
                        SubCategoryId = subCategory?.Id,
                        Sum = Math.Abs(record.Sum),
                        Comment = record.Comment,
                        LastChange = DateTime.Now,
                    };
                }

                transactions.Add(transaction);
            }


            _repository.InsertTransactions(transactions);

            var dbTransactions = _repository.GetTransactions(new TransactionsFilter { PeriodFrom = DateTime.MinValue, PeriodTo = DateTime.MaxValue });

            foreach (var account in accounts)
            {
                var debetSum = dbTransactions.OfType<DebetTransaction>().Where(t => t.AccountId == account.Id).Sum(a => a.Sum);
                var debetTransferSum = dbTransactions.OfType<TransferTransaction>().Where(t => t.ToAccountId == account.Id).Sum(a => a.ToSum);
                var creditSum = dbTransactions.OfType<CreditTransaction>().Where(t => t.AccountId == account.Id).Sum(a => a.Sum);
                var creditTransferSum = dbTransactions.OfType<TransferTransaction>().Where(t => t.AccountId == account.Id).Sum(a => a.Sum);
                account.Sum = debetSum + debetTransferSum - creditSum - creditTransferSum;

                _repository.UpdateAccount(account);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
    }

    private static Account? TryGetAccount(IRepository repository, List<Account> accounts, string? accountName)
    {
        Account? account = null;
        if (!string.IsNullOrEmpty(accountName))
        {
            account = accounts.FirstOrDefault(c => c.Name == accountName);
            if (account == null)
            {
                account = new Account
                {
                    Id = Guid.NewGuid(),
                    Name = accountName,
                };

                repository.UpdateAccount(account);
                accounts.Add(account);
            }
        }

        return account;
    }

    private Category? TryGetCategory(IRepository repository, List<Category> categories, CsvImportRow record, bool isTransfer, bool isCredit)
    {
        Category? category = null;
        if (!string.IsNullOrEmpty(record.Category))
        {
            if (isTransfer)
            {
                category = new TransferCategory
                {
                    Id = Guid.NewGuid(),
                    Name = record.Category,
                };
            }
            else if (isCredit)
            {
                category = new CreditCategory
                {
                    Id = Guid.NewGuid(),
                    Name = record.Category,
                };
            }
            else
            {
                category = new DebetCategory
                {
                    Id = Guid.NewGuid(),
                    Name = record.Category,
                };
            }

            var foundCategory = categories.FirstOrDefault(c => c.Name == record.Category && c.GetType() == category.GetType());
            if (foundCategory == null)
            {
                _repository.UpdateCategroty(category);
                categories.Add(category);
            }
            else
            {
                category = foundCategory;
            }
        }

        return category;
    }

    private SubCategory? TryGetSubCategory(IRepository repository, Category? category, List<SubCategory> subCategories, CsvImportRow record, bool isTransfer, bool isCredit)
    {
        SubCategory? subCategory = null;
        if (!string.IsNullOrEmpty(record.SubCategory))
        {
            if (isTransfer)
            {
                subCategory = new TransferSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = record.SubCategory,
                    CategoryId = category?.Id,
                };
            }
            else if (isCredit)
            {
                subCategory = new CreditSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = record.SubCategory,
                    CategoryId = category?.Id,
                };
            }
            else
            {
                subCategory = new DebetSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = record.SubCategory,
                    CategoryId = category?.Id,
                };
            }

            var foundSubCategory = subCategories.FirstOrDefault(c => c.Name == record.SubCategory 
                                                                && c.CategoryId == category?.Id 
                                                                && c.GetType() == subCategory.GetType());
            if (foundSubCategory == null)
            {
                _repository.UpdateSubCategory(subCategory);
                subCategories.Add(subCategory);
            }
            else
            {
                subCategory = foundSubCategory;
            }
        }

        return subCategory;
    }
}
