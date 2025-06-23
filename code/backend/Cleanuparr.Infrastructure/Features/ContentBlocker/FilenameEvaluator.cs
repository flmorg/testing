﻿using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.ContentBlocker;

public class FilenameEvaluator : IFilenameEvaluator
{
    private readonly ILogger<FilenameEvaluator> _logger;
    
    public FilenameEvaluator(ILogger<FilenameEvaluator> logger)
    {
        _logger = logger;
    }
    
    // TODO create unit tests
    public bool IsValid(string filename, BlocklistType type, ConcurrentBag<string> patterns, ConcurrentBag<Regex> regexes)
    {
        return IsValidAgainstPatterns(filename, type, patterns) && IsValidAgainstRegexes(filename, type, regexes);
    }

    private static bool IsValidAgainstPatterns(string filename, BlocklistType type, ConcurrentBag<string> patterns)
    {
        if (patterns.Count is 0)
        {
            return true;
        }

        return type switch
        {
            BlocklistType.Blacklist => !patterns.Any(pattern => MatchesPattern(filename, pattern)),
            BlocklistType.Whitelist => patterns.Any(pattern => MatchesPattern(filename, pattern)),
        };
    }

    private static bool IsValidAgainstRegexes(string filename, BlocklistType type, ConcurrentBag<Regex> regexes)
    {
        if (regexes.Count is 0)
        {
            return true;
        }
        
        return type switch
        {
            BlocklistType.Blacklist => !regexes.Any(regex => regex.IsMatch(filename)),
            BlocklistType.Whitelist => regexes.Any(regex => regex.IsMatch(filename)),
        };
    }
    
    private static bool MatchesPattern(string filename, string pattern)
    {
        bool hasStartWildcard = pattern.StartsWith('*');
        bool hasEndWildcard = pattern.EndsWith('*');

        if (hasStartWildcard && hasEndWildcard)
        {
            return filename.Contains(
                pattern.Substring(1, pattern.Length - 2),
                StringComparison.InvariantCultureIgnoreCase
            );
        }

        if (hasStartWildcard)
        {
            return filename.EndsWith(pattern.Substring(1), StringComparison.InvariantCultureIgnoreCase);
        }

        if (hasEndWildcard)
        {
            return filename.StartsWith(
                pattern.Substring(0, pattern.Length - 1),
                StringComparison.InvariantCultureIgnoreCase
            );
        }

        return filename.Equals(pattern, StringComparison.InvariantCultureIgnoreCase);
    }
}