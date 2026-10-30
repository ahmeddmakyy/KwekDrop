using System;
using System.Collections.Generic;
using System.Globalization;

namespace KwekDrop;

// Interface language: Arabic ("ar") and English ("en").
internal static partial class L
{
    private static string? language;

    public static event Action? Changed;

    public static string Language
    {
        get => language ??= Override() ?? Load() ?? Default();
        set
        {
            string next = value is "ar" ? "ar" : "en";
            if (next == Language)
                return;
            language = next;
            Settings.Set("language", next);
            Changed?.Invoke();
        }
    }

    private static string Default()
    {
        string ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return ui is "ar" ? "ar" : "en";
    }

    public static bool English => Language == "en";
    public static bool Arabic => Language == "ar";

    public static string T(string key)
    {
        if (Arabic && Ar.TryGetValue(key, out var arabic))
            return arabic;
        if (English && En.TryGetValue(key, out var english))
            return english;
        if (En.TryGetValue(key, out var fallback))
            return fallback;
        if (Ar.TryGetValue(key, out var fallbackAr))
            return fallbackAr;
        return key;
    }

    public static string F(string key, params object?[] values) =>
        string.Format(CultureInfo.InvariantCulture, T(key), values);

    public static void UseForSession(string value) => language = value is "ar" ? "ar" : "en";

    private static string? Override() => Environment.GetEnvironmentVariable("KWEKDROP_LANG") is "ar" or "en" ? Environment.GetEnvironmentVariable("KWEKDROP_LANG") : null;

    private static string? Load()
    {
        string? saved = Settings.GetString("language");
        return saved is "ar" or "en" ? saved : null;
    }

    public static IReadOnlyDictionary<string, string> Translations => Arabic ? Ar : En;
}
