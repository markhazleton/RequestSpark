
namespace RequestSpark.Domain.Tests.Models;

/// <summary>
/// Tests for <see cref="CompareRequest"/> defaults, validation, and HTTP body rules.
/// </summary>
[TestClass]
public class CompareRequestTests
{
        /// <summary>
    /// Verifies that the default constructor initializes expected default values.
    /// </summary>
    [TestMethod]
    public void CompareRequest_DefaultConstructor_CreatesInstance()
    {
        var request = new CompareRequest();

        Assert.IsNotNull(request);
        Assert.IsNull(request.Path);
        Assert.AreEqual(HttpVerb.GET, request.RequestMethod);
        Assert.IsFalse(request.RequiresClientToken);
    }

        /// <summary>
    /// Verifies that request paths are trimmed before storage.
    /// </summary>
    [TestMethod]
    public void CompareRequest_Path_TrimsWhitespace()
    {
        var request = new CompareRequest { Path = "  api/test  " };

        Assert.AreEqual("api/test", request.Path);
    }

        /// <summary>
    /// Verifies that whitespace-only paths are stored as null.
    /// </summary>
    [TestMethod]
    public void CompareRequest_Path_WhitespaceOnly_StoresNull()
    {
        var request = new CompareRequest { Path = "   " };

        Assert.IsNull(request.Path);
    }

        /// <summary>
    /// Verifies that a request with a valid path is valid.
    /// </summary>
    [TestMethod]
    public void IsValid_WithValidPath_ReturnsTrue()
    {
        var request = new CompareRequest { Path = "api/status" };

        Assert.IsTrue(request.IsValid());
    }

        /// <summary>
    /// Verifies that a request without a path is invalid.
    /// </summary>
    [TestMethod]
    public void IsValid_WithNullPath_ReturnsFalse()
    {
        var request = new CompareRequest();

        Assert.IsFalse(request.IsValid());
    }

        /// <summary>
    /// Verifies that an overlong request path is invalid.
    /// </summary>
    [TestMethod]
    public void IsValid_PathExceedsMaxLength_ReturnsFalse()
    {
        var request = new CompareRequest { Path = new string('a', 3000) };

        Assert.IsFalse(request.IsValid());
    }

        /// <summary>
    /// Verifies that POST requests require a body.
    /// </summary>
    [TestMethod]
    public void RequiresBody_PostMethod_ReturnsTrue()
    {
        var request = new CompareRequest { Path = "api/test", RequestMethod = HttpVerb.POST };

        Assert.IsTrue(request.RequiresBody());
    }

        /// <summary>
    /// Verifies that PUT requests require a body.
    /// </summary>
    [TestMethod]
    public void RequiresBody_PutMethod_ReturnsTrue()
    {
        var request = new CompareRequest { Path = "api/test", RequestMethod = HttpVerb.PUT };

        Assert.IsTrue(request.RequiresBody());
    }

        /// <summary>
    /// Verifies that PATCH requests require a body.
    /// </summary>
    [TestMethod]
    public void RequiresBody_PatchMethod_ReturnsTrue()
    {
        var request = new CompareRequest { Path = "api/test", RequestMethod = HttpVerb.PATCH };

        Assert.IsTrue(request.RequiresBody());
    }

        /// <summary>
    /// Verifies that GET requests do not require a body.
    /// </summary>
    [TestMethod]
    public void RequiresBody_GetMethod_ReturnsFalse()
    {
        var request = new CompareRequest { Path = "api/test", RequestMethod = HttpVerb.GET };

        Assert.IsFalse(request.RequiresBody());
    }

        /// <summary>
    /// Verifies that DELETE requests do not require a body.
    /// </summary>
    [TestMethod]
    public void RequiresBody_DeleteMethod_ReturnsFalse()
    {
        var request = new CompareRequest { Path = "api/test", RequestMethod = HttpVerb.DELETE };

        Assert.IsFalse(request.RequiresBody());
    }
}

