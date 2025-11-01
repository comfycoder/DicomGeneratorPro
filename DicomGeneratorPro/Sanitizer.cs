using System.Text.RegularExpressions;

namespace DicomGeneratorPro;

public static class Sanitizer
{
    private static readonly char[] Invalid = Path.GetInvalidFileNameChars();
    public static string ForPath(string value, int max=120)
    {
        var s = value.Trim();
        foreach (var ch in Invalid) s = s.Replace(ch, '_');
        s = Regex.Replace(s, @"\s+", " ").Replace(' ', '_');
        if (s.Length > max) s = s[..max];
        return s;
    }
}