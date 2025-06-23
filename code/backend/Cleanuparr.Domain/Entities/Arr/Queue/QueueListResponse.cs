﻿namespace Data.Models.Arr.Queue;

public record QueueListResponse
{
    public required int TotalRecords { get; init; }
    public required IReadOnlyList<QueueRecord> Records { get; init; }
}