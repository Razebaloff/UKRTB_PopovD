using System;

string s = Console.ReadLine()!;
Console.WriteLine(Palindrome(s, 0, s.Length - 1) ? "YES" : "NO");

static bool Palindrome(string s, int a, int b) =>
    a >= b || s[a] == s[b] && Palindrome(s, a + 1, b - 1);