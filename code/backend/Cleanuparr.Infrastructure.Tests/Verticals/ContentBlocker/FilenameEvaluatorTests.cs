﻿using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Verticals.ContentBlocker;

public class FilenameEvaluatorTests : IClassFixture<FilenameEvaluatorFixture>
{
    private readonly FilenameEvaluatorFixture _fixture;

    public FilenameEvaluatorTests(FilenameEvaluatorFixture fixture)
    {
        _fixture = fixture;
    }

    public class PatternTests : FilenameEvaluatorTests
    {
        public PatternTests(FilenameEvaluatorFixture fixture) : base(fixture) { }

        [Fact]
        public void WhenNoPatterns_ShouldReturnTrue()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string>();
            var regexes = new ConcurrentBag<Regex>();

            // Act
            var result = sut.IsValid("test.txt", BlocklistType.Blacklist, patterns, regexes);

            // Assert
            result.ShouldBeTrue();
        }

        [Theory]
        [InlineData("test.txt", "test.txt", true)]  // Exact match
        [InlineData("test.txt", "*.txt", true)]     // End wildcard
        [InlineData("test.txt", "test.*", true)]    // Start wildcard
        [InlineData("test.txt", "*test*", true)]    // Both wildcards
        [InlineData("test.txt", "other.txt", false)] // No match
        public void Blacklist_ShouldMatchPatterns(string filename, string pattern, bool shouldBeBlocked)
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string> { pattern };
            var regexes = new ConcurrentBag<Regex>();

            // Act
            var result = sut.IsValid(filename, BlocklistType.Blacklist, patterns, regexes);

            // Assert
            result.ShouldBe(!shouldBeBlocked);
        }

        [Theory]
        [InlineData("test.txt", "test.txt", true)]  // Exact match
        [InlineData("test.txt", "*.txt", true)]     // End wildcard
        [InlineData("test.txt", "test.*", true)]    // Start wildcard
        [InlineData("test.txt", "*test*", true)]    // Both wildcards
        [InlineData("test.txt", "other.txt", false)] // No match
        public void Whitelist_ShouldMatchPatterns(string filename, string pattern, bool shouldBeAllowed)
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string> { pattern };
            var regexes = new ConcurrentBag<Regex>();

            // Act
            var result = sut.IsValid(filename, BlocklistType.Whitelist, patterns, regexes);

            // Assert
            result.ShouldBe(shouldBeAllowed);
        }

        [Theory]
        [InlineData("TEST.TXT", "test.txt")]
        [InlineData("test.txt", "TEST.TXT")]
        public void ShouldBeCaseInsensitive(string filename, string pattern)
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string> { pattern };
            var regexes = new ConcurrentBag<Regex>();

            // Act
            var result = sut.IsValid(filename, BlocklistType.Blacklist, patterns, regexes);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public void MultiplePatterns_ShouldMatchAny()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string> 
            { 
                "other.txt",
                "*.pdf",
                "test.*"
            };
            var regexes = new ConcurrentBag<Regex>();

            // Act
            var result = sut.IsValid("test.txt", BlocklistType.Blacklist, patterns, regexes);

            // Assert
            result.ShouldBeFalse();
        }
    }

    public class RegexTests : FilenameEvaluatorTests
    {
        public RegexTests(FilenameEvaluatorFixture fixture) : base(fixture) { }

        [Fact]
        public void WhenNoRegexes_ShouldReturnTrue()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string>();
            var regexes = new ConcurrentBag<Regex>();

            // Act
            var result = sut.IsValid("test.txt", BlocklistType.Blacklist, patterns, regexes);

            // Assert
            result.ShouldBeTrue();
        }

        [Theory]
        [InlineData(@"test\d+\.txt", "test123.txt", true)]
        [InlineData(@"test\d+\.txt", "test.txt", false)]
        public void Blacklist_ShouldMatchRegexes(string pattern, string filename, bool shouldBeBlocked)
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string>();
            var regexes = new ConcurrentBag<Regex> { new Regex(pattern, RegexOptions.IgnoreCase) };

            // Act
            var result = sut.IsValid(filename, BlocklistType.Blacklist, patterns, regexes);

            // Assert
            result.ShouldBe(!shouldBeBlocked);
        }

        [Theory]
        [InlineData(@"test\d+\.txt", "test123.txt", true)]
        [InlineData(@"test\d+\.txt", "test.txt", false)]
        public void Whitelist_ShouldMatchRegexes(string pattern, string filename, bool shouldBeAllowed)
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string>();
            var regexes = new ConcurrentBag<Regex> { new Regex(pattern, RegexOptions.IgnoreCase) };

            // Act
            var result = sut.IsValid(filename, BlocklistType.Whitelist, patterns, regexes);

            // Assert
            result.ShouldBe(shouldBeAllowed);
        }

        [Theory]
        [InlineData(@"TEST\d+\.TXT", "test123.txt")]
        [InlineData(@"test\d+\.txt", "TEST123.TXT")]
        public void ShouldBeCaseInsensitive(string pattern, string filename)
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string>();
            var regexes = new ConcurrentBag<Regex> { new Regex(pattern, RegexOptions.IgnoreCase) };

            // Act
            var result = sut.IsValid(filename, BlocklistType.Blacklist, patterns, regexes);

            // Assert
            result.ShouldBeFalse();
        }
    }

    public class CombinedTests : FilenameEvaluatorTests
    {
        public CombinedTests(FilenameEvaluatorFixture fixture) : base(fixture) { }

        [Fact]
        public void WhenBothPatternsAndRegexes_ShouldMatchBoth()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string> { "*.txt" };
            var regexes = new ConcurrentBag<Regex> { new Regex(@"test\d+", RegexOptions.IgnoreCase) };

            // Act
            var result = sut.IsValid("test123.txt", BlocklistType.Blacklist, patterns, regexes);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public void WhenPatternMatchesButRegexDoesNot_ShouldReturnFalse()
        {
            // Arrange
            var sut = _fixture.CreateSut();
            var patterns = new ConcurrentBag<string> { "*.txt" };
            var regexes = new ConcurrentBag<Regex> { new Regex(@"test\d+", RegexOptions.IgnoreCase) };

            // Act
            var result = sut.IsValid("other.txt", BlocklistType.Whitelist, patterns, regexes);

            // Assert
            result.ShouldBeFalse();
        }
    }
}