using Moq;

namespace RequestSpark.Web.Tests.Controllers;

/// <summary>
/// Tests for <see cref="OpenApiController"/> upload actions.
/// </summary>
[TestClass]
public class OpenApiControllerTests
{
        /// <summary>
    /// Verifies that the upload GET action returns an upload view model.
    /// </summary>
    [TestMethod]
    public void Upload_Get_ReturnsUploadViewModel()
    {
        var controller = CreateController();

        var result = controller.Upload() as ViewResult;

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OpenApiUploadViewModel>(result.Model);
    }

        /// <summary>
    /// Verifies that an invalid upload POST redisplays the upload view.
    /// </summary>
    [TestMethod]
    public async Task Upload_PostInvalidModelState_ReturnsView()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("Title", "Required");
        var viewModel = new OpenApiUploadViewModel();

        var result = await controller.Upload(viewModel) as ViewResult;

        Assert.IsNotNull(result);
        Assert.AreSame(viewModel, result.Model);
    }

    private static OpenApiController CreateController()
    {
        return new OpenApiController(
            Mock.Of<IOpenApiService>(),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<OpenApiController>>());
    }
}
