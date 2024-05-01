﻿using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace FamilyMoney.DataAccess
{
    internal class DesignerRepository : IRepository
    {
        public void DeleteAccount(Guid id)
        {
            throw new NotImplementedException();
        }

        public Account GetAccount(Guid id)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Account> GetAccounts()
        {
            return new [] { new Account { Name = "Тест", Id = Guid.NewGuid() } };
        }

        public IEnumerable<Category> GetCategories()
        {
            throw new NotImplementedException();
        }

        public Category GetCategory(Guid id)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<SubCategory> GetSubCategories()
        {
            throw new NotImplementedException();
        }

        public SubCategory GetSubCategory(Guid id)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Transaction> GetTransactions(DateTime from, DateTime to)
        {
            throw new NotImplementedException();
        }

        public void InsertTransactions(IEnumerable<Transaction> transactions)
        {
            throw new NotImplementedException();
        }

        public Stream? TryGetImage(Guid id)
        {
            return null;
        }

        public void UpdateAccount(Account account)
        {            
        }

        public void UpdateCategroty(Category category)
        {
            throw new NotImplementedException();
        }

        public void UpdateImage(Guid id, string fileName, Stream stream)
        {
        }

        public void UpdateSubCategroty(SubCategory subCategory)
        {
            throw new NotImplementedException();
        }

        public void UpdateTransaction(Transaction transaction)
        {
            throw new NotImplementedException();
        }
    }
}
