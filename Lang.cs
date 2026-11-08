using System.Globalization;
using Microsoft.Win32;

namespace FileLockFinder;

/// <summary>Turkish / English UI text. Defaults to the Windows UI language; the choice is saved per user.</summary>
internal static class Lang
{
    private const string KeyPath = @"Software\FileLockFinder";
    private const string ValueName = "Language";

    public static bool English { get; private set; }

    static Lang()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        English = (key?.GetValue(ValueName) as string) switch
        {
            "en" => true,
            "tr" => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "tr",
        };
    }

    public static void Set(bool english)
    {
        English = english;
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(ValueName, english ? "en" : "tr");
    }

    public static string T(string tr, string en) => English ? en : tr;
}
