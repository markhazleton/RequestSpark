namespace RequestSpark.Domain.Tests.Models;

/// <summary>
/// Tests for <see cref="CompareInstance"/> validation and value normalization.
/// </summary>
[TestClass]
public class CompareInstanceTests
{
        /// <summary>
    /// Verifies that an instance formats its name and URL in <see cref="object.ToString"/>.
    /// </summary>
    [TestMethod]
    public void ToString_WithNameAndUrl_ReturnsFormattedString()
    {
        var instance = new CompareInstance
        {
            BaseUrl = "https://www.controlorigins.com",
            Name = "Production"
        };

        var result = instance.ToString();

        Assert.AreEqual("Production:https://www.controlorigins.com", result);
    }

        /// <summary>
    /// Verifies that an instance with a name and base URL is valid.
    /// </summary>
    [TestMethod]
    public void IsValid_WithNameAndBaseUrl_ReturnsTrue()
    {
        var instance = new CompareInstance
        {
            Name = "Local",
            BaseUrl = "https://localhost:7001/"
        };

        Assert.IsTrue(instance.IsValid());
    }

        /// <summary>
    /// Verifies that an instance without a name is invalid.
    /// </summary>
    [TestMethod]
    public void IsValid_MissingName_ReturnsFalse()
    {
        var instance = new CompareInstance { BaseUrl = "https://localhost:7001/" };

        Assert.IsFalse(instance.IsValid());
    }

        /// <summary>
    /// Verifies that an instance without a base URL is invalid.
    /// </summary>
    [TestMethod]
    public void IsValid_MissingBaseUrl_ReturnsFalse()
    {
        var instance = new CompareInstance { Name = "Local" };

        Assert.IsFalse(instance.IsValid());
    }

        /// <summary>
    /// Verifies that an instance without required fields is invalid.
    /// </summary>
    [TestMethod]
    public void IsValid_BothMissing_ReturnsFalse()
    {
        var instance = new CompareInstance();

        Assert.IsFalse(instance.IsValid());
    }

        /// <summary>
    /// Verifies that assigning an invalid base URL throws an exception.
    /// </summary>
    [TestMethod]
    public void BaseUrl_InvalidUrl_ThrowsArgumentException()
    {
        try
        {
            _ = new CompareInstance { BaseUrl = "not-a-valid-url" };
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException) { }
    }

        /// <summary>
    /// Verifies that assigning a valid absolute base URL stores the value.
    /// </summary>
    [TestMethod]
    public void BaseUrl_ValidAbsoluteUrl_Stores()
    {
        var instance = new CompareInstance { BaseUrl = "https://example.com/" };

        Assert.AreEqual("https://example.com/", instance.BaseUrl);
    }

        /// <summary>
    /// Verifies that whitespace-only names are stored as null.
    /// </summary>
    [TestMethod]
    public void Name_WhitespaceOnly_StoresNull()
    {
        var instance = new CompareInstance { Name = "   " };

        Assert.IsNull(instance.Name);
    }

        /// <summary>
    /// Verifies that names are trimmed before storage.
    /// </summary>
    [TestMethod]
    public void Name_WithWhitespace_IsTrimmed()
    {
        var instance = new CompareInstance { Name = "  Demo  " };

        Assert.AreEqual("Demo", instance.Name);
    }
}

