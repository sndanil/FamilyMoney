using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.Utils
{
    internal static class KeyboardHelper
    {
        private static readonly Dictionary<char, char> _keyboard = new()
        {
            { 'q', 'й' },
            { 'w', 'ц' },
            { 'e', 'у' },
            { 'r', 'к' },
            { 't', 'е' },
            { 'y', 'н' },
            { 'u', 'г' },
            { 'i', 'ш' },
            { 'o', 'щ' },
            { 'p', 'з' },
            { '[', 'х' },
            { ']', 'ъ' },
            { 'a', 'ф' },
            { 's', 'ы' },
            { 'd', 'в' },
            { 'f', 'а' },
            { 'g', 'п' },
            { 'h', 'р' },
            { 'j', 'о' },
            { 'k', 'л' },
            { 'l', 'д' },
            { ';', 'ж' },
            { ':', 'ж' },
            { '\'', 'э' },
            { '"', 'э' },
            { 'z', 'я' },
            { 'x', 'ч' },
            { 'c', 'с' },
            { 'v', 'м' },
            { 'b', 'и' },
            { 'n', 'т' },
            { 'm', 'ь' },
            { ',', 'б' },
            { '.', 'ю' },
            { '/', '.' },
            { '<', 'б' },
            { '>', 'ю' },
            { '?', '.' },
        };

        public static string Translate(string str)
        {
            return new string(str.ToLower().Select(c =>
            {
                char result;
                return _keyboard.TryGetValue(c, out result) ? result : c;
            }).ToArray());
        }
    }
}
