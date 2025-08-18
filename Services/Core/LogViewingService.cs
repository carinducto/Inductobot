using Inductobot.Abstractions.Services;
using Inductobot.Models.Debug;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Inductobot.Services.Core;

/// <summary>
/// Service for viewing and managing log files
/// </summary>
public class LogViewingService : ILogViewingService
{
    private readonly IConfigurationService _config;
    private readonly DebugConfiguration _debugConfig;
    private readonly ILogger<LogViewingService> _logger;
    
    // Pattern to match log entries (simplified format)
    private static readonly Regex LogEntryPattern = new(
        @"^(\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d{3})\s+(\w+)\s+(.+?)\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public LogViewingService(IConfigurationService config, DebugConfiguration debugConfig, ILogger<LogViewingService> logger)
    {
        _config = config;
        _debugConfig = debugConfig;
        _logger = logger;
    }

    public async Task<LogFileInfo[]> GetLogFilesAsync()
    {
        try
        {
            var logDirectory = _debugConfig.LogDirectory;
            if (!Directory.Exists(logDirectory))
            {
                _logger.LogWarning("Log directory does not exist: {LogDirectory}", logDirectory);
                return Array.Empty<LogFileInfo>();
            }

            var logFiles = Directory.GetFiles(logDirectory, "inductobot_*.log")
                .Select(filePath => new FileInfo(filePath))
                .OrderByDescending(f => f.LastWriteTime)
                .Select(fileInfo => new LogFileInfo
                {
                    FilePath = fileInfo.FullName,
                    FileName = fileInfo.Name,
                    CreatedAt = fileInfo.CreationTime,
                    ModifiedAt = fileInfo.LastWriteTime,
                    SizeBytes = fileInfo.Length,
                    ApproximateEntryCount = EstimateEntryCount(fileInfo.FullName)
                })
                .ToArray();

            _logger.LogDebug("Found {FileCount} log files in {LogDirectory}", logFiles.Length, logDirectory);
            return logFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting log files from {LogDirectory}", _debugConfig.LogDirectory);
            return Array.Empty<LogFileInfo>();
        }
    }

    public async Task<LogEntry[]> ReadLogFileAsync(string filePath, LogLevel? minLevel = null, string? searchText = null, int maxEntries = 1000)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Log file does not exist: {FilePath}", filePath);
                return Array.Empty<LogEntry>();
            }

            var lines = await File.ReadAllLinesAsync(filePath);
            var entries = new List<LogEntry>();
            
            foreach (var line in lines.Reverse().Take(maxEntries * 2)) // Read more to allow for filtering
            {
                var entry = ParseLogEntry(line, Path.GetFileName(filePath));
                if (entry == null) continue;
                
                // Apply level filter
                if (minLevel.HasValue && entry.Level < minLevel.Value) continue;
                
                // Apply search filter
                if (!string.IsNullOrEmpty(searchText) && 
                    !entry.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !entry.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase)) continue;
                
                entries.Add(entry);
                if (entries.Count >= maxEntries) break;
            }

            _logger.LogDebug("Read {EntryCount} log entries from {FilePath}", entries.Count, filePath);
            return entries.OrderByDescending(e => e.Timestamp).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading log file: {FilePath}", filePath);
            return Array.Empty<LogEntry>();
        }
    }

    public async Task<LogEntry[]> GetRecentEntriesAsync(int maxEntries = 100, LogLevel? minLevel = null)
    {
        try
        {
            var logFiles = await GetLogFilesAsync();
            var allEntries = new List<LogEntry>();
            
            // Read from multiple recent files to get a good mix
            foreach (var logFile in logFiles.Take(3))
            {
                var entries = await ReadLogFileAsync(logFile.FilePath, minLevel, null, maxEntries / 2);
                allEntries.AddRange(entries);
            }
            
            return allEntries
                .OrderByDescending(e => e.Timestamp)
                .Take(maxEntries)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent log entries");
            return Array.Empty<LogEntry>();
        }
    }

    public async Task<bool> DeleteLogFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Cannot delete log file, does not exist: {FilePath}", filePath);
                return false;
            }

            File.Delete(filePath);
            _logger.LogInformation("Deleted log file: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting log file: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<string?> ExportLogsAsync(LogEntry[] entries, string format = "txt")
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"inductobot_logs_{DateTime.Now:yyyyMMdd_HHmmss}.{format}");
            
            if (format.ToLower() == "json")
            {
                var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await File.WriteAllTextAsync(tempPath, json);
            }
            else
            {
                var lines = entries.Select(entry => 
                    $"{entry.FormattedTimestamp} [{entry.FormattedLevel}] {entry.Category}: {entry.Message}" +
                    (string.IsNullOrEmpty(entry.Exception) ? "" : $"\n    Exception: {entry.Exception}"));
                
                await File.WriteAllLinesAsync(tempPath, lines);
            }
            
            _logger.LogInformation("Exported {EntryCount} log entries to {FilePath}", entries.Length, tempPath);
            return tempPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs");
            return null;
        }
    }

    private int EstimateEntryCount(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            // Rough estimate: average log line is ~150 characters
            return (int)(fileInfo.Length / 150);
        }
        catch
        {
            return 0;
        }
    }

    private LogEntry? ParseLogEntry(string line, string sourceFile)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            
            // Try to match the standard log format
            var match = LogEntryPattern.Match(line);
            if (!match.Success)
            {
                // If regex doesn't match, try simple parsing
                return ParseSimpleLogEntry(line, sourceFile);
            }

            var timestamp = DateTime.Parse(match.Groups[1].Value);
            var levelString = match.Groups[2].Value;
            var category = match.Groups[3].Value;
            var message = match.Groups[4].Value;
            
            if (!Enum.TryParse<LogLevel>(levelString, true, out var level))
            {
                level = LogLevel.Information;
            }

            return new LogEntry
            {
                Timestamp = timestamp,
                Level = level,
                Category = category.Trim(),
                Message = message.Trim(),
                SourceFile = sourceFile
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to parse log entry: {Line}", line);
            return null;
        }
    }
    
    private LogEntry? ParseSimpleLogEntry(string line, string sourceFile)
    {
        try
        {
            // For lines that don't match the standard format, create a simple entry
            return new LogEntry
            {
                Timestamp = DateTime.Now, // Default to now if we can't parse timestamp
                Level = LogLevel.Information,
                Category = "Unknown",
                Message = line.Trim(),
                SourceFile = sourceFile
            };
        }
        catch
        {
            return null;
        }
    }
}