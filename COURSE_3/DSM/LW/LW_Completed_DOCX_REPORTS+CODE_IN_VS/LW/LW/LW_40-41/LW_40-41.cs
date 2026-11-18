using System;
using System.Collections.Generic;
using System.IO;

internal static class Lab40_41
{
    static void Main()
    {
        string folder = AppContext.BaseDirectory;
        string input = Path.Combine(folder, "books.txt");
        if (!File.Exists(input))
        {
            Console.WriteLine("Файл не найден: " + input);
            return;
        }

        var books = new List<Book>();
        var years = new Dictionary<int, int>();
        int max = 0;

        foreach (string line in File.ReadLines(input))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] p = line.Split(';');
            if (p.Length != 4 || !int.TryParse(p[3], out int year))
            {
                Console.WriteLine("Ошибка формата: " + line);
                return;
            }

            books.Add(new Book
            {
                Author = p[0].Trim(),
                Title = p[1].Trim(),
                Publisher = p[2].Trim(),
                Year = year
            });

            years.TryGetValue(year, out int count);
            years[year] = count + 1;
            max = Math.Max(max, years[year]);
        }

        var authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var result = new StreamWriter(Path.Combine(folder, "result.txt"));

        foreach (Book book in books)
        {
            if (years[book.Year] == max && authors.Add(book.Author))
            {
                Console.WriteLine(book.Author);
                result.WriteLine(book.Author);
            }
        }
    }
}

class Book
{
    public string Author = "";
    public string Title = "";
    public string Publisher = "";
    public int Year;
}
