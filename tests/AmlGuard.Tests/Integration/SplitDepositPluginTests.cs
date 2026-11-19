using System;
using System.Collections.Generic;
using System.Linq;
using AmlGuard.Plugins;
using AmlGuard.Plugins.Schema;
using DataverseFakes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Xunit;

namespace AmlGuard.Tests.Integration
{
    // Runs the real plug-in class against an in-memory Dataverse. The new deposit is seeded first and then
    // passed as the Target, which mirrors PostOperation: the record already exists when the plug-in runs.
    public class SplitDepositPluginTests
    {
        private const int PostOperationStage = 40;

        private static readonly DateTime Now = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        private readonly XrmFakedContext _context = new XrmFakedContext();
        private readonly Guid _customerId = Guid.NewGuid();

        private IOrganizationService Service
        {
            get { return _context.GetOrganizationService(); }
        }

        // ---- Helpers ----

        private Entity Transaction(
            decimal amount, double hoursAgo, TransactionType type = TransactionType.Deposit, Guid? customerId = null)
        {
            var transaction = new Entity(TransactionFields.LogicalName, Guid.NewGuid());
            transaction[TransactionFields.Customer] =
                new EntityReference(ContactFields.LogicalName, customerId ?? _customerId);
            transaction[TransactionFields.Amount] = new Money(amount);
            transaction[TransactionFields.TransactionDate] = Now.AddHours(-hoursAgo);
            transaction[TransactionFields.Type] = new OptionSetValue((int)type);
            return transaction;
        }

        private static Entity ExistingAlert(Guid customerId, double createdHoursAgo)
        {
            var alert = new Entity(AlertFields.LogicalName, Guid.NewGuid());
            alert[AlertFields.Type] = new OptionSetValue((int)AlertType.SplitDeposit);
            alert[AlertFields.Severity] = new OptionSetValue((int)AlertSeverity.High);
            alert[AlertFields.Customer] = new EntityReference(ContactFields.LogicalName, customerId);
            alert[AlertFields.Status] = new OptionSetValue((int)AlertStatus.Open);
            alert[SystemFields.CreatedOn] = Now.AddHours(-createdHoursAgo);
            return alert;
        }

        private static Entity Settings(object threshold, int minCount, int windowHours)
        {
            var settings = new Entity(SettingsFields.LogicalName, Guid.NewGuid());
            settings[SettingsFields.ReportingThreshold] = threshold;
            settings[SettingsFields.MinDepositCount] = minCount;
            settings[SettingsFields.WindowHours] = windowHours;
            return settings;
        }

        // Seeds the data, then runs the plug-in for the last entity of the "transactions" list.
        private void SeedAndRun(IEnumerable<Entity> seed, Entity newTransaction)
        {
            var all = seed.ToList();
            if (!all.Contains(newTransaction))
            {
                all.Add(newTransaction);
            }

            _context.Initialize(all);
            Run(newTransaction);
        }

        private void Run(Entity newTransaction)
        {
            _context.ExecutePluginWithTarget<SplitDepositPlugin>(
                newTransaction, messageName: "Create", stage: PostOperationStage);
        }

        private IList<Entity> Alerts()
        {
            var query = new QueryExpression(AlertFields.LogicalName) { ColumnSet = new ColumnSet(true) };
            return Service.RetrieveMultiple(query).Entities.ToList();
        }

        // ---- Alert is raised ----

        [Fact]
        public void ThirdSmallDepositInTheWindow_CreatesAHighSeveritySplitDepositAlert()
        {
            var newDeposit = Transaction(3000m, 0);

            SeedAndRun(new[] { Transaction(4000m, 2), Transaction(4000m, 1) }, newDeposit);

            var alert = Assert.Single(Alerts());
            Assert.Equal((int)AlertType.SplitDeposit, alert.GetAttributeValue<OptionSetValue>(AlertFields.Type).Value);
            Assert.Equal((int)AlertSeverity.High, alert.GetAttributeValue<OptionSetValue>(AlertFields.Severity).Value);
            Assert.Equal((int)AlertStatus.Open, alert.GetAttributeValue<OptionSetValue>(AlertFields.Status).Value);
            Assert.Equal(_customerId, alert.GetAttributeValue<EntityReference>(AlertFields.Customer).Id);
        }

        [Fact]
        public void AlertDescription_ExplainsWhyItWasRaised()
        {
            var newDeposit = Transaction(3000m, 0);

            SeedAndRun(new[] { Transaction(4000m, 2), Transaction(4000m, 1) }, newDeposit);

            var description = Assert.Single(Alerts()).GetAttributeValue<string>(AlertFields.Description);
            Assert.Contains("3 deposits", description);
            Assert.Contains("11,000.00", description);
            Assert.Contains("10,000.00", description);
        }

        [Fact]
        public void DepositExactlyAtWindowStart_CountsTowardsTheAlert()
        {
            var newDeposit = Transaction(4000m, 0);

            SeedAndRun(new[] { Transaction(4000m, 24), Transaction(2000m, 1) }, newDeposit);

            Assert.Single(Alerts());
        }

        // ---- No alert ----

        [Fact]
        public void TwoDeposits_DoNotCreateAnAlertEvenWhenTheTotalIsHigh()
        {
            var newDeposit = Transaction(9000m, 0);

            SeedAndRun(new[] { Transaction(9000m, 1) }, newDeposit);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void ThreeDepositsWithATotalBelowTheThreshold_DoNotCreateAnAlert()
        {
            var newDeposit = Transaction(1000m, 0);

            SeedAndRun(new[] { Transaction(1000m, 1), Transaction(1000m, 2) }, newDeposit);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void NewDepositAtTheThreshold_DoesNotCreateAnAlert()
        {
            var newDeposit = Transaction(10000m, 0);

            SeedAndRun(new[] { Transaction(4000m, 1), Transaction(4000m, 2) }, newDeposit);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void DepositOlderThanTheWindow_IsNotCounted()
        {
            var newDeposit = Transaction(4000m, 0);

            SeedAndRun(new[] { Transaction(4000m, 25), Transaction(3000m, 1) }, newDeposit);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void WithdrawalTarget_IsIgnored()
        {
            var withdrawal = Transaction(4000m, 0, TransactionType.Withdrawal);

            SeedAndRun(new[] { Transaction(4000m, 1), Transaction(4000m, 2) }, withdrawal);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void EarlierWithdrawals_AreNotCountedAsDeposits()
        {
            var newDeposit = Transaction(4000m, 0);

            SeedAndRun(
                new[]
                {
                    Transaction(4000m, 1, TransactionType.Withdrawal),
                    Transaction(4000m, 2, TransactionType.Withdrawal)
                },
                newDeposit);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void DepositsOfAnotherCustomer_AreNotCounted()
        {
            var otherCustomer = Guid.NewGuid();
            var newDeposit = Transaction(4000m, 0);

            SeedAndRun(
                new[]
                {
                    Transaction(4000m, 1, customerId: otherCustomer),
                    Transaction(4000m, 2, customerId: otherCustomer)
                },
                newDeposit);

            Assert.Empty(Alerts());
        }

        // ---- Duplicate prevention ----

        [Fact]
        public void ExistingAlertInsideTheWindow_PreventsADuplicate()
        {
            var newDeposit = Transaction(3000m, 0);

            SeedAndRun(
                new[] { Transaction(4000m, 2), Transaction(4000m, 1), ExistingAlert(_customerId, 3) },
                newDeposit);

            Assert.Single(Alerts());
        }

        [Fact]
        public void ClosedAlertInsideTheWindow_StillPreventsADuplicate()
        {
            var closed = ExistingAlert(_customerId, 3);
            closed[AlertFields.Status] = new OptionSetValue((int)AlertStatus.Closed);
            var newDeposit = Transaction(3000m, 0);

            SeedAndRun(new[] { Transaction(4000m, 2), Transaction(4000m, 1), closed }, newDeposit);

            Assert.Single(Alerts());
        }

        [Fact]
        public void AlertOlderThanTheWindow_DoesNotPreventANewAlert()
        {
            var newDeposit = Transaction(3000m, 0);

            SeedAndRun(
                new[] { Transaction(4000m, 2), Transaction(4000m, 1), ExistingAlert(_customerId, 30) },
                newDeposit);

            Assert.Equal(2, Alerts().Count);
        }

        [Fact]
        public void AlertOfAnotherCustomer_DoesNotPreventAnAlert()
        {
            var newDeposit = Transaction(3000m, 0);

            SeedAndRun(
                new[] { Transaction(4000m, 2), Transaction(4000m, 1), ExistingAlert(Guid.NewGuid(), 3) },
                newDeposit);

            Assert.Equal(2, Alerts().Count);
        }

        [Fact]
        public void RunningTheSameCreateTwice_CreatesOnlyOneAlert()
        {
            var newDeposit = Transaction(3000m, 0);
            SeedAndRun(new[] { Transaction(4000m, 2), Transaction(4000m, 1) }, newDeposit);

            Run(newDeposit);

            Assert.Single(Alerts());
        }

        // ---- Settings table ----

        [Fact]
        public void ValuesFromTheSettingsTable_AreUsedInsteadOfTheDefaults()
        {
            // Threshold 5,000 (stored as Currency), 2 deposits, 24 hours: two deposits of 2,500 are enough.
            var newDeposit = Transaction(2500m, 0);

            SeedAndRun(
                new[] { Transaction(2500m, 1), Settings(new Money(5000m), 2, 24) },
                newDeposit);

            Assert.Single(Alerts());
        }

        [Fact]
        public void ThresholdStoredAsDecimal_IsAlsoUnderstood()
        {
            var newDeposit = Transaction(2500m, 0);

            SeedAndRun(new[] { Transaction(2500m, 1), Settings(5000m, 2, 24) }, newDeposit);

            Assert.Single(Alerts());
        }

        [Fact]
        public void ShorterWindowFromSettings_ExcludesOlderDeposits()
        {
            var newDeposit = Transaction(4000m, 0);

            SeedAndRun(
                new[] { Transaction(4000m, 5), Transaction(3000m, 1), Settings(10000m, 3, 2) },
                newDeposit);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void InvalidSettingsValue_FallsBackToTheDefaultsAndDoesNotBlockTheDeposit()
        {
            var newDeposit = Transaction(3000m, 0);

            SeedAndRun(
                new[] { Transaction(4000m, 2), Transaction(4000m, 1), Settings(0m, 3, 24) },
                newDeposit);

            Assert.Single(Alerts());
        }

        // ---- Context guards ----

        [Fact]
        public void CallForAnotherMessage_IsIgnored()
        {
            var newDeposit = Transaction(3000m, 0);
            _context.Initialize(new[] { Transaction(4000m, 2), Transaction(4000m, 1), newDeposit });

            _context.ExecutePluginWithTarget<SplitDepositPlugin>(
                newDeposit, messageName: "Update", stage: PostOperationStage);

            Assert.Empty(Alerts());
        }
    }
}
