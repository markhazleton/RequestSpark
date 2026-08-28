namespace RequestSpark.Domain.Tests.Models;

/// <summary>
/// Tests for <see cref="CompareUser"/> defaults and property storage.
/// </summary>
[TestClass]
public class CompareUserTests
{
        /// <summary>
    /// Verifies that a default user has empty optional values and property storage.
    /// </summary>
    [TestMethod]
    public void CompareUser_DefaultConstructor_HasEmptyProperties()
    {
        var user = new CompareUser();

        Assert.IsNotNull(user.Properties);
        Assert.AreEqual(0, user.Properties.Count);
        Assert.IsNull(user.UserName);
        Assert.IsNull(user.Password);
    }

        /// <summary>
    /// Verifies that assigning a user name stores the value.
    /// </summary>
    [TestMethod]
    public void CompareUser_SetUserName_StoresValue()
    {
        var user = new CompareUser { UserName = "testuser" };

        Assert.AreEqual("testuser", user.UserName);
    }

        /// <summary>
    /// Verifies that assigning a password stores the value.
    /// </summary>
    [TestMethod]
    public void CompareUser_SetPassword_StoresValue()
    {
        var user = new CompareUser { Password = RequestSpark.Domain.Constants.DomainConstants.PlaceholderPassword };

        Assert.AreEqual(RequestSpark.Domain.Constants.DomainConstants.PlaceholderPassword, user.Password);
    }

        /// <summary>
    /// Verifies that arbitrary user properties can be added and read.
    /// </summary>
    [TestMethod]
    public void CompareUser_Properties_CanAddKeyValuePairs()
    {
        var user = new CompareUser();
        user.Properties.Add("email", "user@example.com");
        user.Properties.Add("role", "tester");

        Assert.AreEqual(2, user.Properties.Count);
        Assert.AreEqual("user@example.com", user.Properties["email"]);
        Assert.AreEqual("tester", user.Properties["role"]);
    }
}

