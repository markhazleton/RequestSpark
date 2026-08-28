using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;

namespace RequestSpark.Web.Tests.Services;

/// <summary>
/// Tests for <see cref="ExecutionHistoryStore"/> persistence, caching, deletion, and aggregation.
/// </summary>
[TestClass]
public class ExecutionHistoryStoreTests
{
        /// <summary>
    /// Verifies that saving history persists the record and caches it.
    /// </summary>
    [TestMethod]
    public async Task SaveAsync_PersistsHistory_AndCachesRecord()
    {
        var fileStorage = new Mock<IFileStorageService>();
        fileStorage
            .Setup(storage => storage.SaveLogAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("c:\\temp\\history_exec-1.json");

        var store = new ExecutionHistoryStore(fileStorage.Object, Mock.Of<ILogger<ExecutionHistoryStore>>());
        var history = CreateHistory("exec-1", "config-1");

        await store.SaveAsync(history);
        var loaded = await store.GetAsync("exec-1");

        Assert.IsNotNull(loaded);
        Assert.AreEqual("c:\\temp\\history_exec-1.json", loaded.LogFilePath);
        fileStorage.Verify(storage => storage.SaveLogAsync("history_exec-1.json", It.IsAny<string>()), Times.Once);
        fileStorage.Verify(storage => storage.ReadFileAsync(It.IsAny<string>()), Times.Never);
    }

        /// <summary>
    /// Verifies that history is loaded from storage when it is not already cached.
    /// </summary>
    [TestMethod]
    public async Task GetAsync_LoadsHistoryFromStorage_WhenCacheMisses()
    {
        var fileStorage = new Mock<IFileStorageService>();
        var history = CreateHistory("exec-2", "config-2");
        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });

        fileStorage.Setup(storage => storage.GetDirectoryPath("logs")).Returns("c:\\logs");
        fileStorage
            .Setup(storage => storage.ReadFileAsync("c:\\logs\\history_exec-2.json"))
            .ReturnsAsync(json);

        var store = new ExecutionHistoryStore(fileStorage.Object, Mock.Of<ILogger<ExecutionHistoryStore>>());

        var loaded = await store.GetAsync("exec-2");
        var cached = await store.GetAsync("exec-2");

        Assert.IsNotNull(loaded);
        Assert.AreEqual("exec-2", loaded.Id);
        Assert.AreEqual("exec-2", cached?.Id);
        fileStorage.Verify(storage => storage.ReadFileAsync("c:\\logs\\history_exec-2.json"), Times.Once);
    }

        /// <summary>
    /// Verifies that deleting history removes associated result and log files.
    /// </summary>
    [TestMethod]
    public async Task DeleteAsync_RemovesAssociatedFiles_WhenHistoryExists()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var resultsPath = Path.Combine(tempDirectory, "results.csv");
            var logPath = Path.Combine(tempDirectory, "history_exec-3.json");
            await File.WriteAllTextAsync(resultsPath, "results");
            await File.WriteAllTextAsync(logPath, "log");

            var history = CreateHistory("exec-3", "config-3");
            history.ResultsFilePath = resultsPath;
            history.LogFilePath = logPath;

            var fileStorage = new Mock<IFileStorageService>();
            fileStorage
                .Setup(storage => storage.SaveLogAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(logPath);

            var store = new ExecutionHistoryStore(fileStorage.Object, Mock.Of<ILogger<ExecutionHistoryStore>>());
            await store.SaveAsync(history);

            var deleted = await store.DeleteAsync("exec-3");

            Assert.IsTrue(deleted);
            Assert.IsFalse(File.Exists(resultsPath));
            Assert.IsFalse(File.Exists(logPath));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

        /// <summary>
    /// Verifies that aggregation sums matching execution history statistics.
    /// </summary>
    [TestMethod]
    public async Task GetAggregatedStatisticsAsync_SumsMatchingExecutionHistory()
    {
        var fileStorage = new Mock<IFileStorageService>();
        fileStorage
            .SetupSequence(storage => storage.SaveLogAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("c:\\temp\\history_exec-4.json")
            .ReturnsAsync("c:\\temp\\history_exec-5.json");

        var store = new ExecutionHistoryStore(fileStorage.Object, Mock.Of<ILogger<ExecutionHistoryStore>>());
        await store.SaveAsync(CreateHistory("exec-4", "config-4", totalRequests: 3, successfulRequests: 2, failedRequests: 1));
        await store.SaveAsync(CreateHistory("exec-5", "config-4", totalRequests: 2, successfulRequests: 1, failedRequests: 1));

        var stats = await store.GetAggregatedStatisticsAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), "config-4");

        Assert.AreEqual(5, stats.TotalRequests);
        Assert.AreEqual(3, stats.SuccessfulRequests);
        Assert.AreEqual(2, stats.FailedRequests);
    }

    private static ExecutionHistory CreateHistory(
        string executionId,
        string configurationId,
        int totalRequests = 1,
        int successfulRequests = 1,
        int failedRequests = 0)
    {
        var statistics = new ExecutionStatistics();
        for (int i = 0; i < totalRequests; i++)
        {
            statistics.IncrementTotalRequests();
        }

        for (int i = 0; i < successfulRequests; i++)
        {
            statistics.IncrementSuccessfulRequests();
        }

        for (int i = 0; i < failedRequests; i++)
        {
            statistics.IncrementFailedRequests();
        }

        statistics.AddResponseTime(10);
        statistics.AddResponseTime(20);
        statistics.FinalizeStatistics();

        return new ExecutionHistory
        {
            Id = executionId,
            ConfigurationId = configurationId,
            ConfigurationName = $"Configuration {configurationId}",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(1),
            Status = ExecutionStatus.Completed,
            Statistics = statistics
        };
    }
}
