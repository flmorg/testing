﻿using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.ContentBlocker;

public interface IFilenameEvaluator
{
    bool IsValid(string filename, BlocklistType type, ConcurrentBag<string> patterns, ConcurrentBag<Regex> regexes);
}