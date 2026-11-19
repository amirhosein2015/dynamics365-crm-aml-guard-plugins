using AmlGuard.Plugins.Domain;
using Xunit;

namespace AmlGuard.Tests
{
    public class ExecutionDepthGuardTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void UserActionAndDirectAutomation_AreAllowed(int depth)
        {
            Assert.True(ExecutionDepthGuard.IsAllowed(depth));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(8)]
        public void DeeperChains_AreStopped(int depth)
        {
            Assert.False(ExecutionDepthGuard.IsAllowed(depth));
        }

        [Fact]
        public void TheLimitIsBelowTheDataverseMaximumOfEight()
        {
            Assert.True(ExecutionDepthGuard.MaxDepth < 8);
        }
    }

    public class SanctionsAlertTextTests
    {
        [Fact]
        public void Description_NamesTheContactAndTheListEntry()
        {
            var description = SanctionsAlertText.Description("  Ivan ", "Petrov", "IVAN  PETROV");

            Assert.Contains("\"Ivan Petrov\"", description);
            Assert.Contains("\"IVAN  PETROV\"", description);
            Assert.Contains("Under Review", description);
        }

        [Fact]
        public void Description_HandlesAMissingNamePart()
        {
            Assert.Contains("\"Petrov\"", SanctionsAlertText.Description(null, "Petrov", "Petrov"));
        }

        [Fact]
        public void Title_StartsWithTheMatchedName()
        {
            Assert.Equal("Sanctions match: Ivan Petrov", SanctionsAlertText.Title(" Ivan Petrov "));
        }

        [Fact]
        public void Title_IsCutToTheMaximumLength()
        {
            var title = SanctionsAlertText.Title(new string('a', 500));

            Assert.Equal(SanctionsAlertText.MaxTitleLength, title.Length);
        }
    }
}
