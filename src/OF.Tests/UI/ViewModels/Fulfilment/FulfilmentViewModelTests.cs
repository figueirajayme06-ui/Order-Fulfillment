using OF.Data.Database;
using OF.UI.ViewModels.Asset;

namespace OF.Tests.UI.ViewModels.Fulfilment
{
    public class FulfilmentViewModelTests
    {
        [Fact]
        public void DisplayActivation_Should_Be_True_When_Only_Orphaned_Quote_Lines_Remain()
        {
            // Arrange
            var header = new Header
            {
                Id = 1,
                AgreementNumber = "A12345", // Agreement number (activatable)
                FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.PartiallyFulfilled // Not fully fulfilled at header level
            };

            var lines = new[]
            {
                new Line
                {
                    Id = 1,
                    AgreementLineNumber = "Q001", // Orphaned quote line
                    RequiresFulfilment = true,
                    IsDeleted = false,
                    FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.Unfulfilled
                },
                new Line
                {
                    Id = 2,
                    AgreementLineNumber = "L001", // Regular line - fulfilled
                    RequiresFulfilment = true,
                    IsDeleted = false,
                    FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.FullyFulfiled
                }
            };

            var viewModel = new FulfilmentViewModel
            {
                Header = header,
                Lines = lines
            };

            // Act & Assert
            Assert.True(viewModel.IsActivatable, "Should be activatable for agreement");
            Assert.True(viewModel.DisplayActivation, "Should display activation when only orphaned quote lines remain unfulfilled");
        }

        [Fact]
        public void DisplayActivation_Should_Be_False_When_Non_Orphaned_Lines_Are_Unfulfilled()
        {
            // Arrange
            var header = new Header
            {
                Id = 1,
                AgreementNumber = "A12345", // Agreement number (activatable)
                FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.PartiallyFulfilled
            };

            var lines = new[]
            {
                new Line
                {
                    Id = 1,
                    AgreementLineNumber = "Q001", // Orphaned quote line
                    RequiresFulfilment = true,
                    IsDeleted = false,
                    FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.Unfulfilled
                },
                new Line
                {
                    Id = 2,
                    AgreementLineNumber = "L001", // Regular line - NOT fulfilled
                    RequiresFulfilment = true,
                    IsDeleted = false,
                    FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.Unfulfilled
                }
            };

            var viewModel = new FulfilmentViewModel
            {
                Header = header,
                Lines = lines
            };

            // Act & Assert
            Assert.True(viewModel.IsActivatable, "Should be activatable for agreement");
            Assert.False(viewModel.DisplayActivation, "Should NOT display activation when non-orphaned lines are unfulfilled");
        }

        [Fact]
        public void DisplayActivation_Should_Be_True_When_Header_Is_Fully_Fulfilled()
        {
            // Arrange
            var header = new Header
            {
                Id = 1,
                AgreementNumber = "A12345", // Agreement number (activatable)
                FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.FullyFulfiled // Fully fulfilled at header level
            };

            var lines = new[]
            {
                new Line
                {
                    Id = 1,
                    AgreementLineNumber = "L001", // Regular line
                    RequiresFulfilment = true,
                    IsDeleted = false,
                    FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.FullyFulfiled
                }
            };

            var viewModel = new FulfilmentViewModel
            {
                Header = header,
                Lines = lines
            };

            // Act & Assert
            Assert.True(viewModel.IsActivatable, "Should be activatable for agreement");
            Assert.True(viewModel.DisplayActivation, "Should display activation when header is fully fulfilled");
        }
    }
}
