# -*- coding: utf-8 -*-
"""
Generate the desktop Strings.cs from the Android string resources.

The Android app is the source of truth for wording in both languages; copying
by hand would guarantee the two drift. Only the keys the desktop actually uses
are emitted, plus a handful that exist only on Windows.
"""
import io
import re

ANDROID = u"c:/Users/msicu/android_apps/Athan/app/src/main/res/"
OUT = u"c:/Users/msicu/android_apps/Athan-Desktop/AthanDesktop/Strings.cs"

# Keys the desktop UI needs, taken from the Android resources.
SHARED = [
    "fajr", "sunrise", "dhuhr", "asr", "maghrib", "isha",
    "set_location", "location_set", "settings", "qibla", "close", "stop",
    "next_in", "countdown_hm", "countdown_ms",
    "mode_sound", "mode_silent",
    "calculation_method", "juristic_method", "time_adjustment",
    "madhab_shafi", "madhab_hanafi", "adjust_none",
    "fajr_athan_sound", "athan_sound_other", "app_volume",
    "credits_row", "credits_title", "credits_intro", "credits_islamweb_permission",
    "credit_source", "credit_source_unknown", "credit_own_recording",
    "credits_software", "credits_weather", "credits_weather_detail",
    "about", "about_contact", "about_privacy",
    "adhkar_morning", "adhkar_evening", "adhkar_morning_when", "adhkar_evening_when",
    "adhkar_unavailable",
    "temperature", "temperature_enable", "temperature_explainer", "temperature_units",
    "unit_celsius", "unit_fahrenheit", "temperature_summary_on", "temperature_summary_off",
    "temperature_notice_title", "temperature_notice_body",
    "temperature_notice_yes", "temperature_notice_no",
    "ramadan", "ramadan_short", "ramadan_title", "ramadan_col_day", "ramadan_col_date",
    "ramadan_col_suhoor", "ramadan_col_iftar", "ramadan_footnote", "ramadan_no_location",
    "ramadan_prompt_title", "ramadan_prompt_body", "ramadan_prompt_yes", "ramadan_prompt_no",
    "ramadan_auto_offer",
    "after_athan_dua", "reminder_title_short", "reminder_summary_off",
    "lang_english", "lang_arabic", "language",
    "method_north_america", "method_mwl", "method_egyptian", "method_karachi",
    "method_umm_al_qura", "method_dubai", "method_moonsighting", "method_kuwait",
    "method_qatar", "method_singapore", "method_turkey", "method_tehran", "method_other",
    "category_developer", "category_mecca", "category_madina", "category_emarat",
    "category_various", "category_egyptian", "category_turkish", "category_kuwait",
    "category_georgia", "category_fajr",
]

# Windows-only wording, with no Android counterpart.
DESKTOP_ONLY = {
    "mode_popup": (u"Popup", u"\u0646\u0627\u0641\u0630\u0629"),
    "test_athan": (u"Test athan", u"\u062a\u062c\u0631\u0628\u0629 \u0627\u0644\u0623\u0630\u0627\u0646"),
    "next_prayer": (u"NEXT PRAYER", u"\u0627\u0644\u0635\u0644\u0627\u0629 \u0627\u0644\u0642\u0627\u062f\u0645\u0629"),
    "start_with_windows": (u"Start Athan when I sign in",
                           u"\u062a\u0634\u063a\u064a\u0644 \u0627\u0644\u062a\u0637\u0628\u064a\u0642 \u0639\u0646\u062f \u0628\u062f\u0621 \u0627\u0644\u0648\u064a\u0646\u062f\u0648\u0632"),
    "show_window_on_startup": (u"Show the Athan window when it starts",
                               u"\u0625\u0638\u0647\u0627\u0631 \u0627\u0644\u0646\u0627\u0641\u0630\u0629 \u0639\u0646\u062f \u0627\u0644\u0628\u062f\u0621"),
    "athkar_button": (u"Athkar", u"\u0627\u0644\u0623\u0630\u0643\u0627\u0631"),
    # Short forms for the main window's quarter-width buttons. The full
    # headings are on the windows themselves, where there is room for them.
    "adhkar_morning_short": (u"Morning", u"\u0623\u0630\u0643\u0627\u0631 \u0627\u0644\u0635\u0628\u0627\u062d"),
    "adhkar_evening_short": (u"Evening", u"\u0623\u0630\u0643\u0627\u0631 \u0627\u0644\u0645\u0633\u0627\u0621"),
    "credits_short": (u"Credits", u"\u0627\u0644\u0645\u0635\u0627\u062f\u0631"),
    "at_time": (u"at {0}", u"الساعة {0}"),
    "qibla_line": (u"Qibla {0}° from true north  ·  {1} km to Mecca",
                   u"القبلة {0}° من الشمال الجغرافي  ·  {1} كم إلى مكة"),
    "no_location_yet": (u"No location yet", u"\u0644\u0645 \u064a\u064f\u062d\u062f\u0651\u062f \u0627\u0644\u0645\u0648\u0642\u0639 \u0628\u0639\u062f"),
}


def read(path):
    s = io.open(path, encoding="utf-8").read()
    out = {}
    for m in re.finditer(r'<string name="([^"]+)">(.*?)</string>', s, re.S):
        key, val = m.group(1), m.group(2)
        val = (val.replace(u"\\'", u"'")
                  .replace(u"&amp;", u"&").replace(u"&lt;", u"<").replace(u"&gt;", u">"))
        out[key] = val
    return out


en = read(ANDROID + u"values/strings.xml")
ar = read(ANDROID + u"values-ar/strings.xml")


def placeholders(val):
    """
    Android's %1$s / %2$02d become .NET's {0} / {1:00}.

    The zero-padded form matters: "%2$02d" is what keeps a countdown reading
    "3h 05m" rather than "3h 5m", and an earlier version of this regex matched
    only the unpadded "%2$d" — which left the literal "02d" in the string and
    put "3 س 02d$2% د" on the window.
    """
    def one(m):
        index = int(m.group(1)) - 1
        width = m.group(2)
        return "{%d:%s}" % (index, "0" * int(width)) if width else "{%d}" % index

    return re.sub(r"%(\d)\$0?(\d*)[sd]", one, val)


def cs(val):
    """C# string literal, with Android's placeholders converted."""
    val = placeholders(val)
    return u'"' + val.replace(u'\\', u'\\\\').replace(u'"', u'\\"').replace(u"\n", u"\\n") + u'"'


rows = []
missing = []
for key in SHARED:
    if key not in en:
        missing.append(key)
        continue
    rows.append((key, en[key], ar.get(key, en[key])))
for key, (e, a) in DESKTOP_ONLY.items():
    rows.append((key, e, a))

body = []
for key, e, a in rows:
    body.append(u'        ["%s"] = (%s, %s),' % (key, cs(e), cs(a)))

src = u'''using System.Globalization;

namespace AthanDesktop;

/// <summary>
/// The interface in English and Arabic.
///
/// WPF has no equivalent of Android's values-ar, so the two languages live in
/// one table here. It is GENERATED from the Android app's string resources by
/// tools/gen-strings.py - the phone is the source of truth for wording, and
/// hand-copying 90 strings between two repositories would guarantee they drift.
/// Edit the Android resources and re-run the script; do not edit this file.
///
/// Like the phone, Arabic uses Western digits: the forms in everyday use, and
/// on every other clock the reader looks at, are the Western ones.
/// </summary>
public static class Strings
{
    private static readonly Dictionary<string, (string En, string Ar)> Table = new()
    {
%s
    };

    /// <summary>The chosen language, following Windows until the user picks one.</summary>
    public static bool IsArabic =>
        App.Settings.Language.Length > 0
            ? App.Settings.Language == "ar"
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    public static string Get(string key) =>
        Table.TryGetValue(key, out var pair) ? (IsArabic ? pair.Ar : pair.En) : key;

    public static string Get(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), args);

    /// <summary>Arabic mirrors the whole window.</summary>
    public static System.Windows.FlowDirection Flow =>
        IsArabic ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
}
''' % u"\n".join(body)

io.open(OUT, "w", encoding="utf-8", newline="\n").write(src)
print("wrote %d strings to Strings.cs" % len(rows))
if missing:
    print("MISSING from Android resources:", missing)
