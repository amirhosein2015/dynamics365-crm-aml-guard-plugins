using System;
using AmlGuard.Plugins.Domain;
using Xunit;

namespace AmlGuard.Tests
{
    // Non-ASCII characters are written as \u escapes so the tests do not depend on file encoding.
    public class NameNormalizerTests
    {
        [Theory]
        [InlineData("John Smith", "john smith")]
        [InlineData("  John Smith  ", "john smith")]
        [InlineData("John    Smith", "john smith")]
        [InlineData("John\t\u00a0Smith", "john smith")]
        [InlineData("JOHN SMITH", "john smith")]
        [InlineData("jOhN sMiTh", "john smith")]
        public void Normalize_TrimsCollapsesSpacesAndIgnoresCase(string input, string expected)
        {
            Assert.Equal(expected, NameNormalizer.Normalize(input));
        }

        [Theory]
        [InlineData("Jos\u00e9", "jose")]
        [InlineData("JOS\u00c9", "jose")]
        [InlineData("M\u00fcller", "muller")]
        [InlineData("Dvo\u0159\u00e1k", "dvorak")]
        [InlineData("Zo\u00eb", "zoe")]
        [InlineData("\u00c9mile Zola", "emile zola")]
        public void Normalize_RemovesDiacritics(string input, string expected)
        {
            Assert.Equal(expected, NameNormalizer.Normalize(input));
        }

        [Fact]
        public void Normalize_TreatsPrecomposedAndDecomposedAccentsAsEqual()
        {
            // "e-acute" as one character versus "e" followed by a combining acute accent.
            Assert.Equal(NameNormalizer.Normalize("Jos\u00e9"), NameNormalizer.Normalize("Jose\u0301"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void Normalize_ReturnsEmptyStringForBlankValues(string input)
        {
            Assert.Equal(string.Empty, NameNormalizer.Normalize(input));
        }

        [Fact]
        public void Normalize_ReturnsEmptyStringForNull()
        {
            Assert.Equal(string.Empty, NameNormalizer.Normalize(null));
        }

        [Theory]
        [InlineData("  \u00c9MILE   Zola ")]
        [InlineData("John Smith")]
        [InlineData("Dvo\u0159\u00e1k")]
        public void Normalize_IsIdempotent(string input)
        {
            var once = NameNormalizer.Normalize(input);

            Assert.Equal(once, NameNormalizer.Normalize(once));
        }

        [Fact]
        public void Normalize_KeepsLettersThatHaveNoDecomposition_KnownLimitation()
        {
            // Letters such as the Polish L-with-stroke are not "base letter + accent", so they stay.
            Assert.Equal("\u0142ukasz", NameNormalizer.Normalize("\u0141ukasz"));
        }

        [Fact]
        public void Normalize_DoesNotThrowOnMalformedUnicode()
        {
            var exception = Record.Exception(() => NameNormalizer.Normalize("a\ud800b"));

            Assert.Null(exception);
        }

        [Fact]
        public void NormalizeFullName_JoinsFirstAndLastName()
        {
            Assert.Equal("jose garcia", NameNormalizer.NormalizeFullName("  Jos\u00e9 ", "GARC\u00cdA"));
        }

        [Fact]
        public void NormalizeFullName_HandlesAMissingPart()
        {
            Assert.Equal("smith", NameNormalizer.NormalizeFullName(null, "Smith"));
            Assert.Equal("john", NameNormalizer.NormalizeFullName("John", ""));
        }

        [Fact]
        public void NormalizeFullName_ReturnsEmptyWhenBothPartsAreMissing()
        {
            Assert.Equal(string.Empty, NameNormalizer.NormalizeFullName(null, null));
        }
    }
}
