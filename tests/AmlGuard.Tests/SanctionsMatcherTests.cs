using System;
using System.Collections.Generic;
using AmlGuard.Plugins.Domain;
using Xunit;

namespace AmlGuard.Tests
{
    public class SanctionsMatcherTests
    {
        private static SanctionsMatcher MatcherFor(params string[] sanctionedNames)
        {
            return new SanctionsMatcher(sanctionedNames);
        }

        [Fact]
        public void ExactNameMatches()
        {
            var matcher = MatcherFor("Ivan Petrov");

            Assert.True(matcher.IsMatch("Ivan", "Petrov"));
        }

        [Fact]
        public void MatchIgnoresCase()
        {
            var matcher = MatcherFor("Ivan Petrov");

            Assert.True(matcher.IsMatch("IVAN", "petrov"));
        }

        [Fact]
        public void MatchIgnoresExtraSpaces()
        {
            var matcher = MatcherFor("Ivan Petrov");

            Assert.True(matcher.IsMatch("  Ivan ", "   Petrov  "));
        }

        [Fact]
        public void MatchIgnoresSpacingInTheStoredListToo()
        {
            var matcher = MatcherFor("  Ivan    Petrov ");

            Assert.True(matcher.IsMatch("Ivan", "Petrov"));
        }

        [Fact]
        public void MatchIgnoresDiacriticsOnEitherSide()
        {
            Assert.True(MatcherFor("Jos\u00e9 Garc\u00eda").IsMatch("Jose", "Garcia"));
            Assert.True(MatcherFor("Jose Garcia").IsMatch("Jos\u00e9", "Garc\u00eda"));
        }

        [Fact]
        public void DifferentNameDoesNotMatch()
        {
            var matcher = MatcherFor("Ivan Petrov");

            Assert.False(matcher.IsMatch("Anna", "Petrova"));
        }

        [Fact]
        public void PartialNameDoesNotMatch_NoFuzzyMatching()
        {
            var matcher = MatcherFor("Ivan Petrov");

            Assert.False(matcher.IsMatch("Ivan", "Petrovich"));
            Assert.False(matcher.IsMatch("Ivan", ""));
        }

        [Fact]
        public void ReversedWordOrderDoesNotMatch_KnownLimitation()
        {
            var matcher = MatcherFor("Ivan Petrov");

            Assert.False(matcher.IsMatch("Petrov", "Ivan"));
        }

        [Fact]
        public void EmptyContactNameNeverMatches()
        {
            var matcher = MatcherFor("Ivan Petrov", "   ", "");

            Assert.False(matcher.IsMatch(null, null));
            Assert.False(matcher.IsMatch("", ""));
            Assert.False(matcher.IsMatch("   ", "   "));
        }

        [Fact]
        public void FindMatchReturnsTheNameAsStoredInTheList()
        {
            var matcher = MatcherFor("  Jos\u00e9   Garc\u00eda ");

            Assert.Equal("Jos\u00e9   Garc\u00eda", matcher.FindMatch("JOSE", "GARCIA"));
        }

        [Fact]
        public void FindMatchReturnsNullWhenNothingMatches()
        {
            var matcher = MatcherFor("Ivan Petrov");

            Assert.Null(matcher.FindMatch("Anna", "Smith"));
        }

        [Fact]
        public void EmptyOrMissingListNeverMatches()
        {
            Assert.False(MatcherFor().IsMatch("Ivan", "Petrov"));
            Assert.False(new SanctionsMatcher(null).IsMatch("Ivan", "Petrov"));
        }

        [Fact]
        public void DuplicateEntriesAreHarmless()
        {
            var matcher = new SanctionsMatcher(new List<string> { "Ivan Petrov", "ivan  PETROV", null });

            Assert.True(matcher.IsMatch("Ivan", "Petrov"));
        }
    }
}
