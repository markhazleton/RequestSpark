namespace RequestSpark.Domain.Tests.Models
{
        /// <summary>
    /// Tests for <see cref="CompareResult"/> defaults, formatting, and error classification.
    /// </summary>
    [TestClass]
    public class CompareResultTests
    {
                /// <summary>
        /// Verifies that object initializer values are stored on the result.
        /// </summary>
        [TestMethod]
        public void CompareResult_Constructor_SetsProperties()
        {
            var result = new CompareResult
            {
                SessionId = "session1",
                StatusDescription = "OK",
                Success = true,
                Duration = 100,
                Hash = 123,
                Instance = "Local",
                Request = "api/status",
                ResultCode = "200",
                Verb = "GET",
                UserName = "testuser"
            };

            Assert.AreEqual("session1", result.SessionId);
            Assert.AreEqual("OK", result.StatusDescription);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(100, result.Duration);
            Assert.AreEqual(123, result.Hash);
            Assert.AreEqual("Local", result.Instance);
            Assert.AreEqual("api/status", result.Request);
            Assert.AreEqual("200", result.ResultCode);
            Assert.AreEqual("GET", result.Verb);
            Assert.AreEqual("testuser", result.UserName);
        }

                /// <summary>
        /// Verifies that a successful result without a status description is not an error.
        /// </summary>
        [TestMethod]
        public void IsError_SuccessWithNoStatusDescription_ReturnsFalse()
        {
            var result = new CompareResult { Success = true, StatusDescription = null };

            Assert.IsFalse(result.IsError);
        }

                /// <summary>
        /// Verifies that an unsuccessful result is classified as an error.
        /// </summary>
        [TestMethod]
        public void IsError_NotSuccessful_ReturnsTrue()
        {
            var result = new CompareResult { Success = false };

            Assert.IsTrue(result.IsError);
        }

                /// <summary>
        /// Verifies that a successful result with a status description is classified as an error.
        /// </summary>
        [TestMethod]
        public void IsError_SuccessWithStatusDescription_ReturnsTrue()
        {
            // A status description (e.g. error message) combined with Success=true is still considered an error
            var result = new CompareResult { Success = true, StatusDescription = "Partial content" };

            Assert.IsTrue(result.IsError);
        }

                /// <summary>
        /// Verifies that successful result text includes core request details.
        /// </summary>
        [TestMethod]
        public void ToString_SuccessfulResult_ContainsSuccessAndMethod()
        {
            var result = new CompareResult
            {
                Success = true,
                Verb = "GET",
                Request = "api/status",
                ResultCode = "200",
                Duration = 42
            };

            var text = result.ToString();

            Assert.IsTrue(text.Contains("SUCCESS"));
            Assert.IsTrue(text.Contains("GET"));
            Assert.IsTrue(text.Contains("api/status"));
            Assert.IsTrue(text.Contains("200"));
            Assert.IsTrue(text.Contains("42ms"));
        }

                /// <summary>
        /// Verifies that failed result text contains the failed status marker.
        /// </summary>
        [TestMethod]
        public void ToString_FailedResult_ContainsFailed()
        {
            var result = new CompareResult { Success = false, Verb = "POST", Request = "api/items", ResultCode = "500" };

            Assert.IsTrue(result.ToString().Contains("FAILED"));
        }

                /// <summary>
        /// Verifies that a new result defaults to a recent UTC run date.
        /// </summary>
        [TestMethod]
        public void LastRunDate_DefaultsToRecentUtcTime()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var result = new CompareResult();
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.IsTrue(result.LastRunDate >= before && result.LastRunDate <= after);
        }
    }
}

