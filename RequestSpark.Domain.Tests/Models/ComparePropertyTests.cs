namespace RequestSpark.Domain.Tests.Models;

/// <summary>
/// Tests for <see cref="CompareProperty"/> construction behavior.
/// </summary>
[TestClass]
public class ComparePropertyTests
{
        /// <summary>
    /// Verifies that the constructor stores every supplied property value.
    /// </summary>
    [TestMethod]
    public void CompareProperty_Constructor_SetsAllProperties()
    {
        var prop = new CompareProperty("key1", "value1", "string", "Display Name", "A description");

        Assert.AreEqual("key1", prop.Key);
        Assert.AreEqual("value1", prop.Value);
        Assert.AreEqual("string", prop.Type);
        Assert.AreEqual("Display Name", prop.Name);
        Assert.AreEqual("A description", prop.Description);
    }

        /// <summary>
    /// Verifies that an omitted description defaults to an empty string.
    /// </summary>
    [TestMethod]
    public void CompareProperty_Constructor_NullDescription_DefaultsToEmpty()
    {
        var prop = new CompareProperty("key1", "value1", "string", "Display Name");

        Assert.AreEqual(string.Empty, prop.Description);
    }

        /// <summary>
    /// Verifies that a null key is rejected by the constructor.
    /// </summary>
    [TestMethod]
    public void CompareProperty_Constructor_NullKey_ThrowsArgumentNullException()
    {
        try
        {
            _ = new CompareProperty(null!, "value", "string", "Name");
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException) { }
    }

        /// <summary>
    /// Verifies that a null value is rejected by the constructor.
    /// </summary>
    [TestMethod]
    public void CompareProperty_Constructor_NullValue_ThrowsArgumentNullException()
    {
        try
        {
            _ = new CompareProperty("key", null!, "string", "Name");
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException) { }
    }

        /// <summary>
    /// Verifies that a null type is rejected by the constructor.
    /// </summary>
    [TestMethod]
    public void CompareProperty_Constructor_NullType_ThrowsArgumentNullException()
    {
        try
        {
            _ = new CompareProperty("key", "value", null!, "Name");
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException) { }
    }

        /// <summary>
    /// Verifies that a null display name is rejected by the constructor.
    /// </summary>
    [TestMethod]
    public void CompareProperty_Constructor_NullName_ThrowsArgumentNullException()
    {
        try
        {
            _ = new CompareProperty("key", "value", "string", null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException) { }
    }
}

