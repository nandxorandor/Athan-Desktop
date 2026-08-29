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
    "after_athan_dua", "after_athan_dua_explainer",
    "reminder_title_short", "reminder_summary_off", "reminder_explainer",
    "time_adjustment_hint", "madhab_shafi_long", "for_fajr", "for_other_prayers",
    "about_version", "about_developed_by", "about_github", "about_privacy",
    "about_audio_credit", "about_audio_credit_detail", "credits_software_detail",
    "athan_audio_options", "dont_show_again", "open_settings", "search", "use_gps",
    "city_hint", "city_not_found", "delete", "not_now", "view_downloaded_short",
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
    # "Resources & credits" does not fit a third of the row; the window it
    # opens carries the full title. The athkar keep their full names \u2014 the
    # window was widened to fit them rather than the words cut.
    "credits_short": (u"Credits", u"\u0627\u0644\u0645\u0635\u0627\u062f\u0631"),
    "at_time": (u"at {0}", u"الساعة {0}"),
    "qibla_line": (u"Qibla {0}° from true north  ·  {1} km to Mecca",
                   u"القبلة {0}° من الشمال الجغرافي  ·  {1} كم إلى مكة"),
    "no_location_yet": (u"No location yet", u"\u0644\u0645 \u064a\u064f\u062d\u062f\u0651\u062f \u0627\u0644\u0645\u0648\u0642\u0639 \u0628\u0639\u062f"),

    # Settings section headings.
    "sec_athan_sound": (u"ATHAN SOUND", u"\u0635\u0648\u062a \u0627\u0644\u0623\u0630\u0627\u0646"),
    "sec_before_prayer": (u"BEFORE THE PRAYER", u"\u0642\u0628\u0644 \u0627\u0644\u0635\u0644\u0627\u0629"),
    "sec_calculation": (u"CALCULATION", u"\u0627\u0644\u062d\u0633\u0627\u0628"),
    "sec_windows": (u"WINDOWS", u"\u0648\u064a\u0646\u062f\u0648\u0632"),
    "sec_temperature": (u"TEMPERATURE", u"\u062f\u0631\u062c\u0629 \u0627\u0644\u062d\u0631\u0627\u0631\u0629"),
    "sec_ramadan": (u"RAMADAN", u"\u0631\u0645\u0636\u0627\u0646"),
    "sec_about": (u"ABOUT", u"\u0639\u0646 \u0627\u0644\u062a\u0637\u0628\u064a\u0642"),
    "sec_software": (u"SOFTWARE", u"\u0627\u0644\u0628\u0631\u0645\u062c\u064a\u0627\u062a"),
    "sec_weather": (u"WEATHER", u"\u0627\u0644\u0637\u0642\u0633"),

    # Settings rows that have no Android counterpart, because they describe
    # behaviour only Windows has.
    "other_prayers": (u"Other prayers", u"\u0628\u0627\u0642\u064a \u0627\u0644\u0635\u0644\u0648\u0627\u062a"),
    "reminder_enable_win": (u"Show a heads-up before each prayer",
                            u"\u0625\u0638\u0647\u0627\u0631 \u062a\u0646\u0628\u064a\u0647 \u0642\u0628\u0644 \u0643\u0644 \u0635\u0644\u0627\u0629"),
    "reminder_preview": (u"Show me what it looks like", u"\u0623\u0631\u0650\u0646\u064a \u0643\u064a\u0641 \u064a\u0628\u062f\u0648"),
    "reminder_explainer_win": (u"A popup so you can finish what you are doing. It closes itself after ten seconds, and is silent unless you turn on the sound below.",
                               u"\u0646\u0627\u0641\u0630\u0629 \u062a\u062a\u064a\u062d \u0644\u0643 \u0625\u0646\u0647\u0627\u0621 \u0645\u0627 \u0628\u064a\u062f\u0643\u060c \u0648\u062a\u063a\u0644\u0642 \u0646\u0641\u0633\u0647\u0627 \u0628\u0639\u062f \u0639\u0634\u0631 \u062b\u0648\u0627\u0646\u064d. \u0648\u0647\u064a \u0635\u0627\u0645\u062a\u0629 \u0645\u0627 \u0644\u0645 \u062a\u064f\u0641\u0639\u0651\u0644 \u0627\u0644\u0635\u0648\u062a \u0623\u062f\u0646\u0627\u0647."),
    "startup_explainer": (u"Athan sits in the notification area, so prayers are called without you opening it. It keeps running there when you close the main window.",
                          u"\u064a\u0628\u0642\u0649 \u0627\u0644\u062a\u0637\u0628\u064a\u0642 \u0641\u064a \u0634\u0631\u064a\u0637 \u0627\u0644\u0625\u0634\u0639\u0627\u0631\u0627\u062a\u060c \u0641\u062a\u064f\u0631\u0641\u0639 \u0627\u0644\u0635\u0644\u0648\u0627\u062a \u062f\u0648\u0646 \u0623\u0646 \u062a\u0641\u062a\u062d\u0647. \u0648\u064a\u0638\u0644 \u064a\u0639\u0645\u0644 \u0647\u0646\u0627\u0643 \u0628\u0639\u062f \u0625\u063a\u0644\u0627\u0642 \u0627\u0644\u0646\u0627\u0641\u0630\u0629 \u0627\u0644\u0631\u0626\u064a\u0633\u064a\u0629."),
    "show_window_explainer": (u"Untick to have Athan start quietly in the notification area, with no window on your desktop.",
                              u"\u0623\u0632\u0644 \u0627\u0644\u062a\u062d\u062f\u064a\u062f \u0644\u064a\u0628\u062f\u0623 \u0627\u0644\u062a\u0637\u0628\u064a\u0642 \u0628\u0647\u062f\u0648\u0621 \u0641\u064a \u0634\u0631\u064a\u0637 \u0627\u0644\u0625\u0634\u0639\u0627\u0631\u0627\u062a \u062f\u0648\u0646 \u0646\u0627\u0641\u0630\u0629 \u0639\u0644\u0649 \u0633\u0637\u062d \u0627\u0644\u0645\u0643\u062a\u0628."),
    "ramadan_open": (u"Ramadan calendar\u2026", u"\u062a\u0642\u0648\u064a\u0645 \u0631\u0645\u0636\u0627\u0646\u2026"),
    "ramadan_auto_explainer": (u"Asks once, in the fortnight before the first fast, whether you want the month's timetable. Dismissing it only silences that year.",
                               u"\u064a\u0633\u0623\u0644 \u0645\u0631\u0629 \u0648\u0627\u062d\u062f\u0629 \u0641\u064a \u0627\u0644\u0623\u0633\u0628\u0648\u0639\u064a\u0646 \u0627\u0644\u0633\u0627\u0628\u0642\u064a\u0646 \u0644\u0623\u0648\u0644 \u0635\u0648\u0645 \u0625\u0646 \u0643\u0646\u062a \u062a\u0631\u064a\u062f \u062c\u062f\u0648\u0644 \u0627\u0644\u0634\u0647\u0631. \u0648\u062a\u062c\u0627\u0647\u0644\u0647 \u064a\u064f\u0633\u0643\u062a\u0647 \u0644\u0630\u0644\u0643 \u0627\u0644\u0639\u0627\u0645 \u0641\u0642\u0637."),
    "about_athan": (u"About Athan", u"\u0639\u0646 \u0627\u0644\u062a\u0637\u0628\u064a\u0642"),
    "method_label": (u"Method", u"\u0627\u0644\u0637\u0631\u064a\u0642\u0629"),
    "reminder_sound_enable": (u"Play a sound with the heads-up",
                              u"\u062a\u0634\u063a\u064a\u0644 \u0635\u0648\u062a \u0645\u0639 \u0627\u0644\u062a\u0646\u0628\u064a\u0647"),
    "reminder_sound_explainer": (u"Off by default. Choose any audio file \u2014 some people set a hadith urging them to come early to the row, so everyone in the room gets ready.",
                                 u"\u0645\u062a\u0648\u0642\u0651\u0641 \u0627\u0641\u062a\u0631\u0627\u0636\u064a\u064b\u0651\u0627. \u0627\u062e\u062a\u0631 \u0623\u064a \u0645\u0644\u0641 \u0635\u0648\u062a\u064a \u2014 \u0648\u0628\u0639\u0636 \u0627\u0644\u0646\u0627\u0633 \u064a\u0636\u0639 \u062d\u062f\u064a\u062b\u064b\u0627 \u0641\u064a \u0641\u0636\u0644 \u0627\u0644\u062a\u0628\u0643\u064a\u0631 \u0648\u0627\u0644\u0635\u0641\u0651 \u0627\u0644\u0623\u0648\u0644\u060c \u0641\u064a\u0633\u062a\u0639\u062f\u0651 \u0643\u0644\u0651 \u0645\u0646 \u0641\u064a \u0627\u0644\u0645\u062c\u0644\u0633."),
    "reminder_sound_choose": (u"Choose a sound file\u2026", u"\u0627\u062e\u062a\u064a\u0627\u0631 \u0645\u0644\u0641 \u0635\u0648\u062a\u064a\u2026"),
    "reminder_sound_default": (u"System notification sound", u"\u0635\u0648\u062a \u0627\u0644\u0625\u0634\u0639\u0627\u0631 \u0627\u0644\u0627\u0641\u062a\u0631\u0627\u0636\u064a"),
    "reminder_sound_system": (u"Use the system sound", u"\u0627\u0633\u062a\u0639\u0645\u0627\u0644 \u0635\u0648\u062a \u0627\u0644\u0646\u0638\u0627\u0645"),
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
