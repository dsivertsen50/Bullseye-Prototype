using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Buffered JSONL writer for research telemetry. Serialization happens on
/// the main thread in batches; disk writes are deferred until Flush.
/// </summary>
public sealed class ResearchTelemetryWriter
{
    public const int MaxPendingLines = 4096;

    private readonly List<PendingLine> pending = new(256);
    private readonly Dictionary<string, StringBuilder> batchBuilders = new();
    private readonly object ioLock = new();
    private string directory;
    private int droppedCount;

    public int DroppedCount => droppedCount;
    public int PendingCount => pending.Count;
    public string Directory => directory;
    public bool HasDirectory => !string.IsNullOrEmpty(directory);

    public void BeginSession(string sessionDirectory)
    {
        directory = sessionDirectory;
        if (string.IsNullOrEmpty(directory))
            return;

        try
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Research telemetry could not create output folder: {exception.Message}");
            directory = null;
        }
    }

    public void Enqueue(string fileName, string jsonLine)
    {
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(jsonLine))
        {
            droppedCount++;
            return;
        }

        if (pending.Count >= MaxPendingLines)
        {
            droppedCount++;
            return;
        }

        pending.Add(new PendingLine { FileName = fileName, Line = jsonLine });
    }

    public void Flush()
    {
        if (pending.Count == 0 || string.IsNullOrEmpty(directory))
            return;

        foreach (KeyValuePair<string, StringBuilder> pair in batchBuilders)
            pair.Value.Clear();

        for (int i = 0; i < pending.Count; i++)
        {
            PendingLine line = pending[i];
            if (!batchBuilders.TryGetValue(line.FileName, out StringBuilder builder))
            {
                builder = new StringBuilder(2048);
                batchBuilders[line.FileName] = builder;
            }

            builder.Append(line.Line);
            if (line.Line.Length == 0 || line.Line[line.Line.Length - 1] != '\n')
                builder.Append('\n');
        }

        pending.Clear();

        lock (ioLock)
        {
            foreach (KeyValuePair<string, StringBuilder> pair in batchBuilders)
            {
                if (pair.Value.Length == 0)
                    continue;

                try
                {
                    File.AppendAllText(Path.Combine(directory, pair.Key), pair.Value.ToString());
                }
                catch (Exception exception)
                {
                    droppedCount++;
                    Debug.LogWarning($"Research telemetry write dropped ({pair.Key}): {exception.Message}");
                }
            }
        }
    }

    public void EndSession()
    {
        Flush();
        directory = null;
    }

    private struct PendingLine
    {
        public string FileName;
        public string Line;
    }
}
