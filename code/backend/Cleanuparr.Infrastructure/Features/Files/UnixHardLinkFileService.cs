﻿using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mono.Unix.Native;

namespace Cleanuparr.Infrastructure.Features.Files;

public class UnixHardLinkFileService : IHardLinkFileService, IDisposable
{
    private readonly ILogger<UnixHardLinkFileService> _logger;
    private readonly ConcurrentDictionary<ulong, int> _inodeCounts = new();
    
    public UnixHardLinkFileService(ILogger<UnixHardLinkFileService> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public long GetHardLinkCount(string filePath, bool ignoreRootDir)
    {
        try
        {
            if (Syscall.stat(filePath, out Stat stat) != 0)
            {
                _logger.LogDebug("failed to stat file {file}", filePath);
                return -1;
            }

            if (!ignoreRootDir)
            {
                _logger.LogDebug("stat file | hardlinks: {nlink} | {file}", stat.st_nlink, filePath);
                return (long)stat.st_nlink == 1 ? 0 : 1;
            }

            // get the number of hardlinks in the same root directory
            int linksInIgnoredDir = _inodeCounts.TryGetValue(stat.st_ino, out int count) 
                ? count
                : 1; // default to 1 if not found
            
            _logger.LogDebug("stat file | hardlinks: {nlink} | ignored: {ignored} | {file}", stat.st_nlink, linksInIgnoredDir, filePath);
            return (long)stat.st_nlink - linksInIgnoredDir;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "failed to stat file {file}", filePath);
            return -1;
        }
    }
    
    /// <inheritdoc/>
    public void PopulateFileCounts(string directoryPath)
    {
        try
        {
            // traverse all files in the ignored path and subdirectories
            foreach (string file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                AddInodeToCount(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to populate inode counts from {dir}", directoryPath);
            throw;
        }
    }

    private void AddInodeToCount(string path)
    {
        try
        {
            if (Syscall.stat(path, out Stat stat) == 0)
            {
                _inodeCounts.AddOrUpdate(stat.st_ino, 1, (_, count) => count + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "could not stat {path} during inode counting", path);
            throw;
        }
    }

    public void Dispose()
    {
        _inodeCounts.Clear();
    }
}