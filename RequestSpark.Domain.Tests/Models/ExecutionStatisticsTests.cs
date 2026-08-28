namespace RequestSpark.Domain.Tests.Models;

/// <summary>
/// Tests for <see cref="ExecutionStatistics"/> counters, timing, and derived metrics.
/// </summary>
[TestClass]
public class ExecutionStatisticsTests
{
        /// <summary>
    /// Verifies that a new statistics instance starts with zeroed counters.
    /// </summary>
    [TestMethod]
    public void ExecutionStatistics_DefaultState_HasZeroCounters()
    {
        var stats = new ExecutionStatistics();

        Assert.AreEqual(0, stats.TotalRequests);
        Assert.AreEqual(0, stats.SuccessfulRequests);
        Assert.AreEqual(0, stats.FailedRequests);
        Assert.AreEqual(0, stats.MinResponseTime);
        Assert.AreEqual(0, stats.MaxResponseTime);
    }

        /// <summary>
    /// Verifies that total request increments update the total counter.
    /// </summary>
    [TestMethod]
    public void IncrementTotalRequests_IncrementsCounter()
    {
        var stats = new ExecutionStatistics();

        stats.IncrementTotalRequests();
        stats.IncrementTotalRequests();

        Assert.AreEqual(2, stats.TotalRequests);
    }

        /// <summary>
    /// Verifies that successful request increments update the success counter.
    /// </summary>
    [TestMethod]
    public void IncrementSuccessfulRequests_IncrementsCounter()
    {
        var stats = new ExecutionStatistics();

        stats.IncrementSuccessfulRequests();

        Assert.AreEqual(1, stats.SuccessfulRequests);
    }

        /// <summary>
    /// Verifies that failed request increments update the failure counter.
    /// </summary>
    [TestMethod]
    public void IncrementFailedRequests_IncrementsCounter()
    {
        var stats = new ExecutionStatistics();

        stats.IncrementFailedRequests();

        Assert.AreEqual(1, stats.FailedRequests);
    }

        /// <summary>
    /// Verifies that response times update minimum and maximum values.
    /// </summary>
    [TestMethod]
    public void AddResponseTime_UpdatesMinAndMax()
    {
        var stats = new ExecutionStatistics();

        stats.AddResponseTime(100);
        stats.AddResponseTime(50);
        stats.AddResponseTime(200);

        Assert.AreEqual(50, stats.MinResponseTime);
        Assert.AreEqual(200, stats.MaxResponseTime);
    }

        /// <summary>
    /// Verifies that current average response time is calculated from recorded timings.
    /// </summary>
    [TestMethod]
    public void CurrentAverageResponseTime_ReturnsCorrectAverage()
    {
        var stats = new ExecutionStatistics();
        stats.IncrementTotalRequests();
        stats.IncrementTotalRequests();
        stats.AddResponseTime(100);
        stats.AddResponseTime(200);

        Assert.AreEqual(150.0, stats.CurrentAverageResponseTime, 0.001);
    }

        /// <summary>
    /// Verifies that current average response time is zero when no requests exist.
    /// </summary>
    [TestMethod]
    public void CurrentAverageResponseTime_WithNoRequests_ReturnsZero()
    {
        var stats = new ExecutionStatistics();

        Assert.AreEqual(0.0, stats.CurrentAverageResponseTime, 0.001);
    }

        /// <summary>
    /// Verifies that success rate is calculated from mixed successful and failed results.
    /// </summary>
    [TestMethod]
    public void SuccessRate_WithMixedResults_ReturnsCorrectPercentage()
    {
        var stats = new ExecutionStatistics();
        stats.IncrementTotalRequests();
        stats.IncrementTotalRequests();
        stats.IncrementTotalRequests();
        stats.IncrementTotalRequests();
        stats.IncrementSuccessfulRequests();
        stats.IncrementSuccessfulRequests();
        stats.IncrementSuccessfulRequests();

        Assert.AreEqual(75.0, stats.SuccessRate, 0.001);
    }

        /// <summary>
    /// Verifies that success rate is zero when no requests exist.
    /// </summary>
    [TestMethod]
    public void SuccessRate_WithNoRequests_ReturnsZero()
    {
        var stats = new ExecutionStatistics();

        Assert.AreEqual(0.0, stats.SuccessRate, 0.001);
    }

        /// <summary>
    /// Verifies that response time percentiles return expected values.
    /// </summary>
    [TestMethod]
    public void GetResponseTimePercentile_ReturnsCorrectValue()
    {
        var stats = new ExecutionStatistics();
        foreach (var t in new long[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
            stats.AddResponseTime(t);

        Assert.AreEqual(50, stats.GetResponseTimePercentile(50));
        Assert.AreEqual(100, stats.GetResponseTimePercentile(100));
    }

        /// <summary>
    /// Verifies that response time percentiles return zero when no timings exist.
    /// </summary>
    [TestMethod]
    public void GetResponseTimePercentile_EmptyCollection_ReturnsZero()
    {
        var stats = new ExecutionStatistics();

        Assert.AreEqual(0, stats.GetResponseTimePercentile(50));
    }

        /// <summary>
    /// Verifies that percentiles above 100 are rejected.
    /// </summary>
    [TestMethod]
    public void GetResponseTimePercentile_PercentileAbove100_ThrowsArgumentOutOfRangeException()
    {
        var stats = new ExecutionStatistics();
        try
        {
            stats.GetResponseTimePercentile(101);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException) { }
    }

        /// <summary>
    /// Verifies that negative percentiles are rejected.
    /// </summary>
    [TestMethod]
    public void GetResponseTimePercentile_NegativePercentile_ThrowsArgumentOutOfRangeException()
    {
        var stats = new ExecutionStatistics();
        try
        {
            stats.GetResponseTimePercentile(-1);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException) { }
    }

        /// <summary>
    /// Verifies that finalization sets the average response time.
    /// </summary>
    [TestMethod]
    public void FinalizeStatistics_SetsAverageResponseTime()
    {
        var stats = new ExecutionStatistics { StartTime = DateTime.UtcNow };
        stats.AddResponseTime(100);
        stats.AddResponseTime(200);

        stats.FinalizeStatistics();

        Assert.AreEqual(150.0, stats.AverageResponseTime, 0.001);
    }

        /// <summary>
    /// Verifies that finalization sets an end timestamp.
    /// </summary>
    [TestMethod]
    public void FinalizeStatistics_SetsEndTime()
    {
        var before = DateTime.UtcNow;
        var stats = new ExecutionStatistics { StartTime = before };

        stats.FinalizeStatistics();

        Assert.IsTrue(stats.EndTime >= before);
    }

        /// <summary>
    /// Verifies that total duration is calculated from start and end times.
    /// </summary>
    [TestMethod]
    public void TotalDuration_ReturnsEndMinusStart()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddSeconds(10);
        var stats = new ExecutionStatistics { StartTime = start, EndTime = end };

        Assert.AreEqual(TimeSpan.FromSeconds(10), stats.TotalDuration);
    }

        /// <summary>
    /// Verifies that formatted statistics text includes key metrics.
    /// </summary>
    [TestMethod]
    public void ToString_ContainsKeyMetrics()
    {
        var stats = new ExecutionStatistics();
        stats.IncrementTotalRequests();
        stats.IncrementSuccessfulRequests();
        stats.AddResponseTime(100);
        stats.FinalizeStatistics();

        var result = stats.ToString();

        Assert.IsTrue(result.Contains("Total Requests"));
        Assert.IsTrue(result.Contains("Success Rate"));
        Assert.IsTrue(result.Contains("Avg Response"));
    }
}

