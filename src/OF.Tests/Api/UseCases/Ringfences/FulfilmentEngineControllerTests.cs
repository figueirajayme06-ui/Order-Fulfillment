using Moq;
using Microsoft.AspNetCore.Mvc;
using OF.UI.Controllers;
using OF.Tests.Data.Test;
using OF.Tests.Common;
using OF.UI.Models;
using OF.UI.Identity;
using Xunit.Abstractions;
using OF.UI.Database;
using OF.UI.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using OF.Data.Database;

namespace OF.Tests.Api.UseCases.Ringfences
{
    [Collection("DatabaseCollection")]
    public class FulfilmentEngineControllerTests : CommonDBTest
    {
        private ITestOutputHelper TestOutput { get; }
        public FulfilmentEngineControllerTests(ITestOutputHelper testOutput, DatabaseFixture databaseFixture) : base(databaseFixture)
        {
            TestOutput = testOutput;
        }

        

        [SkippableFact]
        public async Task RingfenceOverlap_ShouldReturnExpectedResult()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndActRingFence(TestData.RingfenceData.CreateRingfence);
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var (ringfence1, ringfence2) = TestData.RingfenceData.CreateRingfence(dbContext);
            var ringfenceItems1 = TestData.RingfenceData.CreateRingfenceItems(dbContext, ringfence1.Id);
            var ringfenceItems2 = TestData.RingfenceData.CreateRingfenceItems(dbContext, ringfence2.Id);

            var request = new RingfenceRequest
            {
                RingfenceId = ringfence1.Id,
                AssetIds = ringfenceItems1.Select(ri => ri.AssetId).ToArray()
            };

            var mockDbRepository = new Mock<IDataRepository>();
            var mockUserIdentity = new Mock<IUserIdentity>();
            var mockFulfilmentEngine = new Mock<IFulfilmentEngine>();
            var mockLogger = TestOutput.ToLogger<FulfilmentEngineController>().Object;
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            var controller = new FulfilmentEngineController(
                mockDbRepository.Object,
                mockUserIdentity.Object,
                mockFulfilmentEngine.Object,
                mockLogger,
                mockHttpContextAccessor.Object
            );
            var admin = new User
        {
            FullName = "Test User",
            LoginName = "testuser@aggreko.com",
            IsAdmin = true
        };

            mockUserIdentity.Setup(ui => ui.GetIdentity()).Returns(admin);
            mockDbRepository.Setup(repo => repo.GetRingfences()).Returns(dbContext.Ringfences);
            mockDbRepository.Setup(repo => repo.GetOverlappingRingfenceDetailsAsync(
            ringfence1.Id,
            request.AssetIds,
            ringfence1.FromDate,
            ringfence1.ToDate,
            It.IsAny<CancellationToken>()
        )).Returns(Task.FromResult<IReadOnlyList<RingfenceAssestDetails>>(new List<RingfenceAssestDetails>()));

        var expectedResponse = new OkObjectResult(new RingfenceOverlap
        {
            OverlappingRingfences = new List<RingfenceAssestDetails>(),
            IsSuccess = true,
            ErrorMessage = string.Empty
        });

            // Act
            var result = await controller.RingfenceOverlap(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RingfenceOverlap>(okResult.Value);
            Assert.True(response.IsSuccess);
            Assert.Empty(response.OverlappingRingfences);
            Assert.Equal(string.Empty, response.ErrorMessage);
        }
    }
}