using System;
using AmlGuard.Plugins.Domain;
using AmlGuard.Plugins.Schema;
using Xunit;

namespace AmlGuard.Tests
{
    // Default rules in these tests: threshold 10,000, at least 3 deposits, 24 hour window.
    public class SplitDepositEvaluatorTests
    {
        private static readonly DateTime Now = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        private static DepositRecord NewDeposit(decimal amount)
        {
            return new DepositRecord(Guid.NewGuid(), amount, Now);
        }

        private static DepositRecord Earlier(decimal amount, double hoursAgo)
        {
            return new DepositRecord(Guid.NewGuid(), amount, Now.AddHours(-hoursAgo));
        }

        private static SplitDepositResult Evaluate(
            DepositRecord newDeposit, DateTime? latestAlertUtc, params DepositRecord[] others)
        {
            var evaluator = new SplitDepositEvaluator(new DepositRules(10000m, 3, 24));
            return evaluator.Evaluate(newDeposit, others, latestAlertUtc);
        }

        // ---- Threshold boundaries for the new deposit ----

        [Fact]
        public void NewDepositExactlyAtThreshold_IsNotEligible()
        {
            var result = Evaluate(NewDeposit(10000m), null, Earlier(4000m, 1), Earlier(4000m, 2));

            Assert.Equal(SplitDepositOutcome.NewDepositNotEligible, result.Outcome);
            Assert.False(result.ShouldCreateAlert);
        }

        [Fact]
        public void NewDepositAboveThreshold_IsNotEligible()
        {
            var result = Evaluate(NewDeposit(12000m), null);

            Assert.Equal(SplitDepositOutcome.NewDepositNotEligible, result.Outcome);
        }

        [Fact]
        public void NewDepositOfZeroOrLess_IsNotEligible()
        {
            Assert.Equal(SplitDepositOutcome.NewDepositNotEligible, Evaluate(NewDeposit(0m), null).Outcome);
            Assert.Equal(SplitDepositOutcome.NewDepositNotEligible, Evaluate(NewDeposit(-50m), null).Outcome);
        }

        [Fact]
        public void NewDepositJustBelowThreshold_IsEvaluated()
        {
            var result = Evaluate(NewDeposit(9999.99m), null, Earlier(100m, 1), Earlier(100m, 2));

            Assert.Equal(SplitDepositOutcome.AlertRequired, result.Outcome);
        }

        // ---- Count and sum boundaries ----

        [Fact]
        public void ThreeDepositsBelowThresholdWithTotalOverThreshold_RaisesAlert()
        {
            var result = Evaluate(NewDeposit(4000m), null, Earlier(4000m, 1), Earlier(3000m, 2));

            Assert.True(result.ShouldCreateAlert);
            Assert.Equal(3, result.QualifyingDepositCount);
            Assert.Equal(11000m, result.QualifyingTotal);
        }

        [Fact]
        public void TotalExactlyEqualToThreshold_RaisesAlert()
        {
            var result = Evaluate(NewDeposit(5000m), null, Earlier(3000m, 1), Earlier(2000m, 2));

            Assert.Equal(10000m, result.QualifyingTotal);
            Assert.Equal(SplitDepositOutcome.AlertRequired, result.Outcome);
        }

        [Fact]
        public void TotalJustBelowThreshold_DoesNotRaiseAlert()
        {
            var result = Evaluate(NewDeposit(4000m), null, Earlier(3000m, 1), Earlier(2999.99m, 2));

            Assert.Equal(SplitDepositOutcome.TotalBelowThreshold, result.Outcome);
        }

        [Fact]
        public void OneDepositFewerThanTheMinimum_DoesNotRaiseAlertEvenWhenTotalIsHigh()
        {
            var result = Evaluate(NewDeposit(9000m), null, Earlier(9000m, 1));

            Assert.Equal(2, result.QualifyingDepositCount);
            Assert.Equal(SplitDepositOutcome.NotEnoughDeposits, result.Outcome);
        }

        [Fact]
        public void DepositsAboveThresholdInTheWindow_AreIgnored()
        {
            // With the 15,000 deposit the total would pass, but it is not a "below threshold" deposit.
            var result = Evaluate(NewDeposit(4000m), null, Earlier(15000m, 1), Earlier(4000m, 2));

            Assert.Equal(2, result.QualifyingDepositCount);
            Assert.Equal(SplitDepositOutcome.NotEnoughDeposits, result.Outcome);
        }

        [Fact]
        public void NoOtherDeposits_DoesNotRaiseAlert()
        {
            var result = Evaluate(NewDeposit(4000m), null);

            Assert.Equal(SplitDepositOutcome.NotEnoughDeposits, result.Outcome);
        }

        // ---- Window edges ----

        [Fact]
        public void DepositExactlyAtWindowStart_IsIncluded()
        {
            var result = Evaluate(NewDeposit(4000m), null, Earlier(4000m, 24), Earlier(2000m, 1));

            Assert.Equal(3, result.QualifyingDepositCount);
            Assert.Equal(SplitDepositOutcome.AlertRequired, result.Outcome);
        }

        [Fact]
        public void DepositOneSecondBeforeWindowStart_IsExcluded()
        {
            var tooOld = new DepositRecord(Guid.NewGuid(), 4000m, Now.AddHours(-24).AddSeconds(-1));

            var result = Evaluate(NewDeposit(4000m), null, tooOld, Earlier(2000m, 1));

            Assert.Equal(2, result.QualifyingDepositCount);
            Assert.Equal(SplitDepositOutcome.NotEnoughDeposits, result.Outcome);
        }

        [Fact]
        public void DepositDatedAfterTheNewOne_IsExcluded()
        {
            var result = Evaluate(NewDeposit(4000m), null, Earlier(4000m, -1), Earlier(2000m, 1));

            Assert.Equal(2, result.QualifyingDepositCount);
        }

        [Fact]
        public void WindowIsReportedInTheResult()
        {
            var result = Evaluate(NewDeposit(4000m), null);

            Assert.Equal(Now, result.WindowEndUtc);
            Assert.Equal(Now.AddHours(-24), result.WindowStartUtc);
        }

        // ---- Counting the new deposit once ----

        [Fact]
        public void NewDepositAlsoPresentInTheList_IsCountedOnce()
        {
            var newDeposit = NewDeposit(4000m);

            var result = Evaluate(newDeposit, null, newDeposit, Earlier(4000m, 1));

            Assert.Equal(2, result.QualifyingDepositCount);
            Assert.Equal(SplitDepositOutcome.NotEnoughDeposits, result.Outcome);
        }

        [Fact]
        public void SameDepositListedTwice_IsCountedOnce()
        {
            var repeated = Earlier(4000m, 1);

            var result = Evaluate(NewDeposit(4000m), null, repeated, repeated);

            Assert.Equal(2, result.QualifyingDepositCount);
        }

        // ---- Duplicate alert prevention ----

        [Fact]
        public void AlertCreatedInsideTheWindow_PreventsADuplicate()
        {
            var result = Evaluate(NewDeposit(4000m), Now.AddHours(-5), Earlier(4000m, 1), Earlier(3000m, 2));

            Assert.Equal(SplitDepositOutcome.AlertAlreadyRaised, result.Outcome);
            Assert.False(result.ShouldCreateAlert);
        }

        [Fact]
        public void AlertCreatedExactlyAtWindowStart_StillCountsAsDuplicate()
        {
            var result = Evaluate(
                NewDeposit(4000m), Now.AddHours(-24), Earlier(4000m, 1), Earlier(3000m, 2));

            Assert.Equal(SplitDepositOutcome.AlertAlreadyRaised, result.Outcome);
        }

        [Fact]
        public void AlertOlderThanTheWindow_AllowsANewAlert()
        {
            var result = Evaluate(
                NewDeposit(4000m), Now.AddHours(-24).AddSeconds(-1), Earlier(4000m, 1), Earlier(3000m, 2));

            Assert.Equal(SplitDepositOutcome.AlertRequired, result.Outcome);
        }

        [Fact]
        public void ExistingAlertDoesNotMatterWhenThereIsNoPattern()
        {
            var result = Evaluate(NewDeposit(4000m), Now.AddHours(-1), Earlier(100m, 1));

            Assert.Equal(SplitDepositOutcome.NotEnoughDeposits, result.Outcome);
        }

        // ---- Configuration is respected ----

        [Fact]
        public void CustomRulesAreRespected()
        {
            var evaluator = new SplitDepositEvaluator(new DepositRules(5000m, 2, 1));

            var withinWindow = evaluator.Evaluate(
                NewDeposit(2500m), new[] { Earlier(2500m, 0.5) }, null);
            var outsideWindow = evaluator.Evaluate(
                NewDeposit(2500m), new[] { Earlier(2500m, 1.5) }, null);

            Assert.Equal(SplitDepositOutcome.AlertRequired, withinWindow.Outcome);
            Assert.Equal(SplitDepositOutcome.NotEnoughDeposits, outsideWindow.Outcome);
        }

        [Fact]
        public void MissingDepositList_IsTreatedAsNoOtherDeposits()
        {
            var evaluator = new SplitDepositEvaluator(DepositRules.Defaults());

            var result = evaluator.Evaluate(NewDeposit(4000m), null, null);

            Assert.Equal(SplitDepositOutcome.NotEnoughDeposits, result.Outcome);
        }

        [Fact]
        public void NullArguments_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new SplitDepositEvaluator(null));

            var evaluator = new SplitDepositEvaluator(DepositRules.Defaults());
            Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null, null, null));
        }

        // ---- Alert text ----

        [Fact]
        public void Description_StatesCountTotalThresholdAndWindow()
        {
            var result = Evaluate(NewDeposit(4000m), null, Earlier(4000m, 1), Earlier(3000m, 2));

            var description = result.BuildDescription();

            Assert.Contains("3 deposits", description);
            Assert.Contains("11,000.00", description);
            Assert.Contains("10,000.00", description);
            Assert.Contains("24 hours", description);
        }
    }

    public class DepositRulesTests
    {
        [Fact]
        public void Defaults_MatchTheDocumentedSettings()
        {
            var rules = DepositRules.Defaults();

            Assert.Equal(SettingsDefaults.ReportingThreshold, rules.ReportingThreshold);
            Assert.Equal(SettingsDefaults.MinDepositCount, rules.MinDepositCount);
            Assert.Equal(SettingsDefaults.WindowHours, rules.WindowHours);
        }

        [Fact]
        public void ThresholdMustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DepositRules(0m, 3, 24));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DepositRules(-1m, 3, 24));
        }

        [Fact]
        public void MinimumDepositCountMustBeAtLeastOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DepositRules(10000m, 0, 24));
            Assert.Equal(1, new DepositRules(10000m, 1, 24).MinDepositCount);
        }

        [Fact]
        public void WindowMustBeBetweenOneHourAndOneYear()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DepositRules(10000m, 3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DepositRules(10000m, 3, DepositRules.MaxWindowHours + 1));
            Assert.Equal(1, new DepositRules(10000m, 3, 1).WindowHours);
            Assert.Equal(DepositRules.MaxWindowHours, new DepositRules(10000m, 3, DepositRules.MaxWindowHours).WindowHours);
        }
    }
}
