using System;
using Microsoft.Xrm.Sdk;

namespace AmlGuard.Plugins.Data
{
    // Reads attribute values without throwing on a missing or differently typed column.
    // A plug-in that throws on odd data would roll back the user's save, so reading stays forgiving.
    internal static class AttributeReader
    {
        // Accepts Currency (Money) as well as Decimal columns, so a portal choice cannot break the rule.
        public static decimal? GetDecimal(Entity entity, string attributeName)
        {
            object value;
            if (!entity.Attributes.TryGetValue(attributeName, out value) || value == null)
            {
                return null;
            }

            var money = value as Money;
            if (money != null)
            {
                return money.Value;
            }

            if (value is decimal)
            {
                return (decimal)value;
            }

            if (value is int)
            {
                return (int)value;
            }

            if (value is double)
            {
                return Convert.ToDecimal((double)value);
            }

            return null;
        }

        public static int? GetInt(Entity entity, string attributeName)
        {
            object value;
            if (!entity.Attributes.TryGetValue(attributeName, out value) || value == null)
            {
                return null;
            }

            var optionSet = value as OptionSetValue;
            if (optionSet != null)
            {
                return optionSet.Value;
            }

            if (value is int)
            {
                return (int)value;
            }

            if (value is decimal)
            {
                return decimal.ToInt32((decimal)value);
            }

            return null;
        }

        public static DateTime? GetUtcDate(Entity entity, string attributeName)
        {
            object value;
            if (!entity.Attributes.TryGetValue(attributeName, out value) || !(value is DateTime))
            {
                return null;
            }

            return ToUtc((DateTime)value);
        }

        // Dataverse stores UTC. An unspecified Kind is therefore read as UTC, not as local time.
        public static DateTime ToUtc(DateTime value)
        {
            switch (value.Kind)
            {
                case DateTimeKind.Utc:
                    return value;
                case DateTimeKind.Local:
                    return value.ToUniversalTime();
                default:
                    return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }
        }
    }
}
