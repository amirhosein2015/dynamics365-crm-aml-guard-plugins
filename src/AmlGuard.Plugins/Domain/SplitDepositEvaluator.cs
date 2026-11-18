using System;
using System.Collections.Generic;
using System.Linq;

namespace AmlGuard.Plugins.Domain
{
    // Decides whether a new deposit completes a "split deposit" (structuring) pattern.
    //
    // Window: from (new deposit time - WindowHours) up to the new deposit time, both ends included.
    // Qualifying deposit: positive amount strictly below the threshold. Larger deposits are ignored.
    // Alert when: new deposit qualifies, qualifying count >= MinDepositCount and qualifying total >= threshold.
    // Duplicates: no alert if the customer's latest split-deposit alert was created inside the window.
    public sealed class SplitDepositEvaluator
    {
        private readonly DepositRules _rules;

        public SplitDepositEvaluator(DepositRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rules = rules;
        }

        // otherDeposits: the same customer's other deposits (the new one may be included; it is counted once).
        // latestSplitAlertUtc: creation time of the customer's most recent split-deposit alert, if any.
        public SplitDepositResult Evaluate(
            DepositRecord newDeposit,
            IEnumerable<DepositRecord> otherDeposits,
            DateTime? latestSplitAlertUtc)
        {
            if (newDeposit == null)
            {
                throw new ArgumentNullException(nameof(newDeposit));
            }

            var windowEnd = newDeposit.DateUtc;
            var windowStart = windowEnd - TimeSpan.FromHours(_rules.WindowHours);

            if (!Qualifies(newDeposit))
            {
                return Result(SplitDepositOutcome.NewDepositNotEligible, windowStart, windowEnd, 0, 0m);
            }

            var qualifying = new List<DepositRecord> { newDeposit };
            if (otherDeposits != null)
            {
                qualifying.AddRange(
                    otherDeposits
                        .Where(deposit => deposit != null && deposit.Id != newDeposit.Id)
                        .GroupBy(deposit => deposit.Id)
                        .Select(group => group.First())
                        .Where(deposit => deposit.DateUtc >= windowStart && deposit.DateUtc <= windowEnd)
                        .Where(Qualifies));
            }

            var count = qualifying.Count;
            var total = qualifying.Sum(deposit => deposit.Amount);

            if (count < _rules.MinDepositCount)
            {
                return Result(SplitDepositOutcome.NotEnoughDeposits, windowStart, windowEnd, count, total);
            }

            if (total < _rules.ReportingThreshold)
            {
                return Result(SplitDepositOutcome.TotalBelowThreshold, windowStart, windowEnd, count, total);
            }

            if (latestSplitAlertUtc.HasValue && latestSplitAlertUtc.Value >= windowStart)
            {
                return Result(SplitDepositOutcome.AlertAlreadyRaised, windowStart, windowEnd, count, total);
            }

            return Result(SplitDepositOutcome.AlertRequired, windowStart, windowEnd, count, total);
        }

        private bool Qualifies(DepositRecord deposit)
        {
            return deposit.Amount > 0m && deposit.Amount < _rules.ReportingThreshold;
        }

        private SplitDepositResult Result(
            SplitDepositOutcome outcome,
            DateTime windowStart,
            DateTime windowEnd,
            int count,
            decimal total)
        {
            return SplitDepositResult.Create(outcome, _rules, windowStart, windowEnd, count, total);
        }
    }
}
