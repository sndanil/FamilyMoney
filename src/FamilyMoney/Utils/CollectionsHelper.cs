using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FamilyMoney.Utils;

public static class CollectionsHelper
{
    public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            collection.Add(item); 
        }
    }
}
