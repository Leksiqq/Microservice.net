using System.Text.RegularExpressions;

namespace Net.Leksi.MicroService.Common;

public static class Util
{
    private const string s_slash = "/";
#pragma warning disable SYSLIB1045 // Преобразовать в "GeneratedRegexAttribute".
    private static readonly Regex manySlashes = new("/{2,}");
#pragma warning restore SYSLIB1045 // Преобразовать в "GeneratedRegexAttribute".
    public static string CollapseSlashes(string source)
    {
        return manySlashes.Replace(source, s_slash);
    }
}
