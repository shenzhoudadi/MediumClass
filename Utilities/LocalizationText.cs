using Kingmaker.Localization;
using Kingmaker.Localization.Shared;

namespace MediumClass.Utils
{
    // These UI messages may be displayed before embedded localization packs are loaded.
    internal static class LocalizationText
    {
        internal static string Get(string english, string chinese)
        {
            return LocalizationManager.CurrentLocale == Locale.zhCN ? chinese : english;
        }
    }
}
