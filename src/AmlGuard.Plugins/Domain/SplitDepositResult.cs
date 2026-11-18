using System;
using System.Globalization;

namespace AmlGuard.Plugins.Domain
{
    public enum SplitDepositOutcome
    {
        // Create an alert.
        AlertRequired,

        // The new deposit is not a positive amount below the threshold, so this rule does not apply.
        NewDepositNotEligible,

        // Fewer qualifying deposits in the window than the configured minimum.
        NotEnoughDeposits,

        // Enough deposits, but together they stay below the threshold.
        TotalBelowThreshold,

        // The pattern is there, but an alert for this customer already covers the window.
        AlertAlreadyRaised
    }

    public sealed class SplitDepositResult
    {
        private readonly DepositRules _rules;

        private SplitDepositResult(
            SplitDepositOutcome outcome,
            DepositRules rules,
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            int qualifyingDepositCount,
            decimal qualifyingTotal)
        {
            Outcome = outcome;
            _rules = rules;
            WindowStartUtc = windowStartUtc;
            WindowEndUtc = windowEndUtc;
            QualifyingDepositCount = qualifyingDepositCount;
            QualifyingTotal = qualifyingTotal;
        }

        public SplitDepositOutcome Outcome { get; }

        public bool ShouldCreateAlert
        {
            get { return Outcome == SplitDepositOutcome.AlertRequired; }
        }

        public DateTime WindowStartUtc { get; }

        public DateTime WindowEndUtc { get; }

        // Deposits in the window that are below the threshold, including the new one.
        public int QualifyingDepositCount { get; }

        public decimal QualifyingTotal { get; }

        internal static SplitDepositResult Create(
            SplitDepositOutcome outcome,
            DepositRules rules,
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            int qualifyingDepositCount,
            decimal qualifyingTotal)
        {
            return new SplitDepositResult(
                outcome, rules, windowStartUtc, windowEndUtc, qualifyingDepositCount, qualifyingTotal);
        }

        public string BuildDescription()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Possible structuring: {0} deposits, each below the reporting threshold of {1:N2}, " +
                "total {2:N2} within {3} hours (window {4:yyyy-MM-dd HH:mm} to {5:yyyy-MM-dd HH:mm} UTC).",
                QualifyingDepositCount,
                _rules.ReportingThreshold,
                QualifyingTotal,
                _rules.WindowHours,
                WindowStartUtc,
                WindowEndUtc);
        }
    }
}
