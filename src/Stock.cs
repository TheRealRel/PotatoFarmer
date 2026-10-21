// What a trough holds: item prefab names and counts, kept in the trough's network data as "Raspberry:12;Carrot:5".
using System;
using System.Collections.Generic;
using System.Linq;

namespace PotatoFarmer
{
    internal sealed class Stock
    {
        private readonly List<KeyValuePair<string, int>> entries = new List<KeyValuePair<string, int>>();

        public IEnumerable<KeyValuePair<string, int>> Entries => entries;
        public int Total => entries.Sum(entry => entry.Value);

        public static Stock Parse(string text)
        {
            var stock = new Stock();
            if (string.IsNullOrEmpty(text)) return stock;
            foreach (var part in text.Split(';'))
            {
                int split = part.LastIndexOf(':');
                if (split <= 0) continue;
                if (int.TryParse(part.Substring(split + 1), out int count)) stock.Add(part.Substring(0, split), count);
            }
            return stock;
        }

        public string Format() => string.Join(";", entries.Select(entry => entry.Key + ":" + entry.Value).ToArray());

        public int Count(string item)
        {
            int index = entries.FindIndex(entry => entry.Key == item);
            return index < 0 ? 0 : entries[index].Value;
        }

        public void Add(string item, int count)
        {
            if (string.IsNullOrEmpty(item) || count <= 0 || item.IndexOf(';') >= 0 || item.IndexOf(':') >= 0) return;
            int index = entries.FindIndex(entry => entry.Key == item);
            if (index < 0) entries.Add(new KeyValuePair<string, int>(item, count));
            else entries[index] = new KeyValuePair<string, int>(item, (int)Math.Min((long)entries[index].Value + count, int.MaxValue));
        }

        public bool Remove(string item, int count)
        {
            int index = entries.FindIndex(entry => entry.Key == item);
            if (index < 0 || count <= 0 || entries[index].Value < count) return false;
            int left = entries[index].Value - count;
            if (left == 0) entries.RemoveAt(index);
            else entries[index] = new KeyValuePair<string, int>(item, left);
            return true;
        }
    }
}
