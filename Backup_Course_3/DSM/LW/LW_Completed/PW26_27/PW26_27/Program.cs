using System;
using System.Numerics;

int m = 2, n = 5;
BigInteger s = 0;

for (int k = m; k <= n; k++)
{
    BigInteger c = 1;
    for (int j = 1; j <= m; j++)
        c = c * (k - j + 1) / j;
    s += c;
}
Console.WriteLine("а) S = " + s);

double[] a = { 4, -2, 0, -5, 3 };
double[] b = new double[a.Length];
int p = 0;

for (int i = 0; i < a.Length; i++)
    if (a[i] < 0) b[p++] = a[i];
for (int i = 0; i < a.Length; i++)
    if (a[i] == 0) b[p++] = a[i];
for (int i = 0; i < a.Length; i++)
    if (a[i] > 0) b[p++] = a[i];

Console.Write("б) ");
for (int i = 0; i < a.Length; i++)
{
    a[i] = b[i];
    Console.Write(a[i] + " ");
}