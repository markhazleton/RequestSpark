namespace RequestSpark.Domain.Tests.Models;

/// <summary>
/// Tests for <see cref="CompareRunner"/> defaults, validation, and test-count calculation.
/// </summary>
[TestClass]
public class CompareRunnerTests
{
    private static CompareInstance ValidInstance() =>
        new() { Name = "Test", BaseUrl = "https://example.com/" };

    private static CompareRequest ValidRequest() =>
        new() { Path = "api/test" };

        /// <summary>
    /// Verifies that the default constructor initializes collections and session data.
    /// </summary>
    [TestMethod]
    public void CompareRunner_DefaultConstructor_CreatesInstance()
    {
        var runner = new CompareRunner();

        Assert.IsNotNull(runner);
        Assert.IsNotNull(runner.Instances);
        Assert.IsNotNull(runner.Requests);
        Assert.IsNotNull(runner.Users);
        Assert.IsNotNull(runner.SessionId);
    }

        /// <summary>
    /// Verifies that a runner with one valid instance and request is valid.
    /// </summary>
    [TestMethod]
    public void IsValid_WithInstanceAndRequest_ReturnsTrue()
    {
        var runner = new CompareRunner();
        runner.Instances.Add(ValidInstance());
        runner.Requests.Add(ValidRequest());

        Assert.IsTrue(runner.IsValid());
    }

        /// <summary>
    /// Verifies that a runner without instances is invalid.
    /// </summary>
    [TestMethod]
    public void IsValid_NoInstances_ReturnsFalse()
    {
        var runner = new CompareRunner();
        runner.Requests.Add(ValidRequest());

        Assert.IsFalse(runner.IsValid());
    }

        /// <summary>
    /// Verifies that a runner without requests is invalid.
    /// </summary>
    [TestMethod]
    public void IsValid_NoRequests_ReturnsFalse()
    {
        var runner = new CompareRunner();
        runner.Instances.Add(ValidInstance());

        Assert.IsFalse(runner.IsValid());
    }

        /// <summary>
    /// Verifies that a runner with an invalid instance is invalid.
    /// </summary>
    [TestMethod]
    public void IsValid_InvalidInstance_ReturnsFalse()
    {
        var runner = new CompareRunner();
        runner.Instances.Add(new CompareInstance()); // missing name and url
        runner.Requests.Add(ValidRequest());

        Assert.IsFalse(runner.IsValid());
    }

        /// <summary>
    /// Verifies that one instance, request, and user produces one total test.
    /// </summary>
    [TestMethod]
    public void GetTotalTestCount_OneEach_ReturnsOne()
    {
        var runner = new CompareRunner();
        runner.Instances.Add(ValidInstance());
        runner.Requests.Add(ValidRequest());
        runner.Users.Add(new CompareUser { UserName = "u1" });

        Assert.AreEqual(1, runner.GetTotalTestCount());
    }

        /// <summary>
    /// Verifies that total test count multiplies instances, requests, and users.
    /// </summary>
    [TestMethod]
    public void GetTotalTestCount_MultipleInstances_MultipliesCorrectly()
    {
        var runner = new CompareRunner();
        runner.Instances.Add(ValidInstance());
        runner.Instances.Add(new CompareInstance { Name = "Test2", BaseUrl = "https://example2.com/" });
        runner.Requests.Add(ValidRequest());
        runner.Requests.Add(new CompareRequest { Path = "api/other" });
        runner.Users.Add(new CompareUser { UserName = "u1" });

        // 2 instances × 2 requests × 1 user = 4
        Assert.AreEqual(4, runner.GetTotalTestCount());
    }

        /// <summary>
    /// Verifies that a runner with no users still uses a single-user multiplier.
    /// </summary>
    [TestMethod]
    public void GetTotalTestCount_NoUsers_UsesSingleUserMultiplier()
    {
        var runner = new CompareRunner();
        runner.Instances.Add(ValidInstance());
        runner.Requests.Add(ValidRequest());
        // No users — Math.Max(0, 1) = 1

        Assert.AreEqual(1, runner.GetTotalTestCount());
    }

        /// <summary>
    /// Verifies that the default iteration count is 100.
    /// </summary>
    [TestMethod]
    public void DefaultIterations_Is100()
    {
        var runner = new CompareRunner();

        Assert.AreEqual(100, runner.Iterations);
    }

        /// <summary>
    /// Verifies that the default maximum concurrency is 10.
    /// </summary>
    [TestMethod]
    public void DefaultMaxConcurrency_Is10()
    {
        var runner = new CompareRunner();

        Assert.AreEqual(10, runner.MaxConcurrency);
    }
}

