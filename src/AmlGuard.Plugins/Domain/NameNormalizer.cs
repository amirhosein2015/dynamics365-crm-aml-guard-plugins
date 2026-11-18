using System;
using System.Globalization;
using System.Text;

namespace AmlGuard.Plugins.Domain
{
    // Turns a name into a canonical form so two spellings of the same name compare equal:
    // trimmed, single spaces, lower case, no diacritics. No fuzzy matching on purpose.
    public static class NameNormalizer
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // FormD splits "e-acute" into "e" plus a combining mark, so the mark can be dropped.
            var decomposed = SafeNormalize(value, NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            var pendingSpace = false;

            foreach (var character in decomposed)
            {
                if (char.IsWhiteSpace(character))
                {
                    // Leading whitespace is skipped; trailing whitespace is never written.
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            // Recompose scripts (e.g. Hangul) that FormD split into letters, not marks.
            return SafeNormalize(builder.ToString(), NormalizationForm.FormC);
        }

        public static string NormalizeFullName(string firstName, string lastName)
        {
            return Normalize((firstName ?? string.Empty) + " " + (lastName ?? string.Empty));
        }

        // string.Normalize throws on malformed Unicode (e.g. a lone surrogate). A bad character in a
        // contact name must never make the plug-in throw and block the save, so fall back to the input.
        private static string SafeNormalize(string value, NormalizationForm form)
        {
            try
            {
                return value.Normalize(form);
            }
            catch (ArgumentException)
            {
                return value;
            }
        }
    }
}
