using System;
using AmlGuard.Plugins.Schema;

namespace AmlGuard.Plugins.Domain
{
    // The three configurable values of the split-deposit rule, validated once on creation.
    public sealed class DepositRules
    {
        // A window longer than a year is almost certainly a configuration mistake.
        public const int MaxWindowHours = 24 * 365;

        public DepositRules(decimal reportingThreshold, int minDepositCount, int windowHours)
        {
            if (reportingThreshold <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(reportingThreshold), "Must be greater than zero.");
            }

            if (minDepositCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minDepositCount), "Must be at least 1.");
            }

            if (windowHours < 1 || windowHours > MaxWindowHours)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(windowHours), "Must be between 1 and " + MaxWindowHours + ".");
            }

            ReportingThreshold = reportingThreshold;
            MinDepositCount = minDepositCount;
            WindowHours = windowHours;
        }

        public decimal ReportingThreshold { get; }

        public int MinDepositCount { get; }

        public int WindowHours { get; }

        public static DepositRules Defaults()
        {
            return new DepositRules(
                SettingsDefaults.ReportingThreshold,
                SettingsDefaults.MinDepositCount,
                SettingsDefaults.WindowHours);
        }

        // Builds the rules from stored values; a value that is not set falls back to its default.
        // A value that is set but invalid (for example 0) throws, so the caller decides what to do.
        public static DepositRules FromSettings(decimal? reportingThreshold, int? minDepositCount, int? windowHours)
        {
            return new DepositRules(
                reportingThreshold ?? SettingsDefaults.ReportingThreshold,
                minDepositCount ?? SettingsDefaults.MinDepositCount,
                windowHours ?? SettingsDefaults.WindowHours);
        }

        // The single place that defines the window, so queries and the rule always agree on it.
        public DateTime WindowStartFor(DateTime newDepositUtc)
        {
            return newDepositUtc - TimeSpan.FromHours(WindowHours);
        }
    }
}
