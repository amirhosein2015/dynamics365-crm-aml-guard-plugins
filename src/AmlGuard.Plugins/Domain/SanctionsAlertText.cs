using System.Linq;

namespace AmlGuard.Plugins.Domain
{
    // Text of the sanctions alert. Names are written as stored; the alert title is cut to fit a
    // typical primary-name column.
    public static class SanctionsAlertText
    {
        public const int MaxTitleLength = 100;

        public static string Title(string matchedListName)
        {
            var title = "Sanctions match: " + (matchedListName ?? string.Empty).Trim();
            return title.Length <= MaxTitleLength ? title : title.Substring(0, MaxTitleLength);
        }

        public static string Description(string firstName, string lastName, string matchedListName)
        {
            var contactName = string.Join(
                " ",
                new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));

            return "The contact name \"" + contactName + "\" matches the sanctioned name \""
                + (matchedListName ?? string.Empty).Trim()
                + "\" (exact match after trimming, case-insensitive, diacritics removed). "
                + "Manual review is required; the contact is set to Under Review.";
        }
    }
}
