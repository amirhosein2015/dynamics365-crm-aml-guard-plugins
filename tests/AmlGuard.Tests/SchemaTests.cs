using System;
using System.Linq;
using AmlGuard.Plugins.Schema;
using Xunit;

namespace AmlGuard.Tests
{
    public class SchemaTests
    {
        [Theory]
        [InlineData(TransactionFields.LogicalName)]
        [InlineData(SanctionedNameFields.LogicalName)]
        [InlineData(AlertFields.LogicalName)]
        [InlineData(SettingsFields.LogicalName)]
        public void CustomTableNames_UseAmlPrefix_AndAreLowercase(string logicalName)
        {
            Assert.StartsWith("aml_", logicalName);
            Assert.Equal(logicalName.ToLowerInvariant(), logicalName);
        }

        [Theory]
        [InlineData(typeof(TransactionType))]
        [InlineData(typeof(AlertType))]
        [InlineData(typeof(AlertSeverity))]
        [InlineData(typeof(AlertStatus))]
        [InlineData(typeof(ReviewStatus))]
        public void ChoiceValues_AreUnique_AndInTheCustomRange(Type choiceEnum)
        {
            var values = Enum.GetValues(choiceEnum).Cast<int>().ToList();

            Assert.Equal(values.Count, values.Distinct().Count());
            Assert.All(values, value => Assert.True(value >= 100000000));
        }

        [Fact]
        public void SettingsDefaults_MatchTheDocumentedBusinessRules()
        {
            Assert.Equal(10000m, SettingsDefaults.ReportingThreshold);
            Assert.Equal(3, SettingsDefaults.MinDepositCount);
            Assert.Equal(24, SettingsDefaults.WindowHours);
        }
    }
}
