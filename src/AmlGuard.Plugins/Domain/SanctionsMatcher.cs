using System;
using System.Collections.Generic;

namespace AmlGuard.Plugins.Domain
{
    // Exact match of a person's full name against a sanctions list, after normalization.
    // Word order matters ("Smith John" does not match "John Smith"); there is no fuzzy matching.
    public sealed class SanctionsMatcher
    {
        private readonly Dictionary<string, string> _originalByNormalizedName =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public SanctionsMatcher(IEnumerable<string> sanctionedNames)
        {
            if (sanctionedNames == null)
            {
                return;
            }

            foreach (var original in sanctionedNames)
            {
                var normalized = NameNormalizer.Normalize(original);
                if (normalized.Length == 0 || _originalByNormalizedName.ContainsKey(normalized))
                {
                    continue;
                }

                _originalByNormalizedName.Add(normalized, original.Trim());
            }
        }

        public bool IsMatch(string firstName, string lastName)
        {
            return FindMatch(firstName, lastName) != null;
        }

        // Returns the sanctioned name as stored in the list (for the alert text), or null if no match.
        public string FindMatch(string firstName, string lastName)
        {
            var fullName = NameNormalizer.NormalizeFullName(firstName, lastName);
            if (fullName.Length == 0)
            {
                return null;
            }

            string original;
            return _originalByNormalizedName.TryGetValue(fullName, out original) ? original : null;
        }
    }
}
