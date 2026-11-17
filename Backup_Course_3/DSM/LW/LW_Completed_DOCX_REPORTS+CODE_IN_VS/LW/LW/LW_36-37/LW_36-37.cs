using System;

string text = "В понедельник утром Nikolay прибыл в офис раньше всех. У него была важная встреча с клиентом, и ему нужно было подготовить презентацию. Nikolay открыл ноутбук и начал работу. Он знал, что успех этого проекта во многом зависит от его подготовки.\n\n" +
              "Коллеги начали подтягиваться к девяти часам. Первым зашел Nikolay из отдела логистики, чтобы согласовать сроки поставок. \"Доброе утро, Nikolay\", — поздоровался он. \"Доброе утро, Nikolay\", — ответил наш Nikolay, не отрываясь от экрана. \"Ты же помнишь, что в среду у нас совещание с инвесторами? Твоя часть критически важна\", — напомнил коллега. \"Конечно, помню, Nikolay\", — кивнул он.";
Console.WriteLine("1) " + text.Replace("Nikolay", "Oleg"));

string str = "В ногах нет правды. На вкус и цвет товарища нет. На нет и суда нет." +
             "Нет дыму без огня. Нет худа без добра. Там хорошо, где нас нет.";
char[] chars = str.ToCharArray();
for (int i = 0; i < chars.Length; i++)
    if (!char.IsLetter(chars[i]))
        chars[i] = ' ';

string[] words = new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries);
Console.Write("\n2)");
foreach (string word in words)
    if (word[word.Length - 1] == 'а')
        Console.Write(" " + word);
Console.WriteLine();
