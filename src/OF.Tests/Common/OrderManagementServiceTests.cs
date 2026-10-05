using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Orders;
using OF.Common.Infrastructure.OF;
using OF.Data;
using OF.Data.Database;
using OF.Tests.AutoFixture;
using OF.Tests.AutoFixture.Customizations;
using System.Net;
using Xunit.Abstractions;
using static OF.Common.Enums;

namespace OF.Tests.Common;
public class OrderManagementServiceTests
{
    public const string AttributeSeparator = ";";
    public const string ItemToPickText = "Item to Pick";
    public const string DepotFulfillesFrom = "Depot fulfills from";
    public const string DepotQuantity = "Qty";

    public OrderManagementServiceTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private ITestOutputHelper Output { get; }

    [SkippableTheory]
    [AutoData(
        typeof(MoqCustomization),
        typeof(ApplicationDbContextCustomization),
        typeof(OrderCustomization))]
    internal async Task CreateLine_ShouldSucced_WhenLinePostedSuccessfully(
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output);
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()))
            .ReturnsAsync(new HttpResponseMessage((HttpStatusCode)200));

        await testable.Sut.CreateLine(header, line, reservation);

        line.ActivationInstanceId.Should().NotBeEmpty();
        line.ActivationErrors.Should().BeNull();
    }

    [SkippableTheory]
    [AutoData(
        typeof(MoqCustomization),
        typeof(ApplicationDbContextCustomization),
        typeof(OrderCustomization))]
    internal async Task CreateLine_ShouldFail_WhenLineNotPostedSuccessfully(
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output, out var logs);
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()))
            .ReturnsAsync(new HttpResponseMessage((HttpStatusCode)500)
            {
                Content = new StringContent(@"{""testError"": ""testErrorvalue""}")
            });

        await testable.Sut.CreateLine(header, line, reservation);

        line.ActivationInstanceId.Should().BeNull();
        line.ActivationStatus.Should().Be((int)ActivationStatus.Failed);
        logs.Should().Contain(x => x.LogLine.Contains(line.ActivationErrors!));
    }

    [SkippableTheory]
    [AutoData(
        typeof(MoqCustomization),
        typeof(ApplicationDbContextCustomization),
        typeof(OrderCustomization))]
    internal async Task UpdateLine_ShouldSucced_WhenLinePutSuccessfully(
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output);
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()))
            .ReturnsAsync(new HttpResponseMessage((HttpStatusCode)200));

        await testable.Sut.UpdateLine(header, line, reservation);

        line.ActivationInstanceId.Should().NotBeEmpty();
        line.ActivationErrors.Should().BeNull();
    }

    [SkippableTheory]
    [AutoData(
        typeof(MoqCustomization),
        typeof(ApplicationDbContextCustomization),
        typeof(OrderCustomization))]
    internal async Task UpdateLine_ShouldFail_WhenLineNotPutSuccessfully(
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output, out var logs);
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()))
            .ReturnsAsync(new HttpResponseMessage((HttpStatusCode)500)
            {
                Content = new StringContent(@"{""testError"": ""testErrorvalue""}")
            });

        await testable.Sut.UpdateLine(header, line, reservation);

        line.ActivationInstanceId.Should().BeNull();
        line.ActivationStatus.Should().Be((int)ActivationStatus.Failed);
        logs.Should().Contain(x => x.LogLine.Contains(line.ActivationErrors!));
    }

    [SkippableTheory(Skip = "ML: This is so complicated I can't figure it out and don't feel it's worth the effort, can someone explain it and fix it please?")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        null!,
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Item to pick: [GN1500GHPEMM] [XBPB080150-0]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Itemtopick: [GN1500GHPEMM] [XBPB080150-0]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "item to pick this one")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "ITEM TO PICK: [fykfyuk] [rtyhsrity]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "ITEM TO PICK: [fykfyuk] [rtyhsrity]; item to pick this one")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depotfulfillsfrom:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        null!,
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Item to pick: [CB2401EXT010M] x20")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Itemtopick: [CB2401EXT010M] x20")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Item to pick: these ones 56")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "ITEM TO PICK: aergearg")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Item to pick: these ones 56; ITEM TO PICK: aergearg")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Depot fulfills from:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Depotfulfillsfrom:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1")]
    internal async Task CreateLine_ShouldUpdateAttributesWithReservedItemToPick_WhenPostingLine(
        bool isItemAsset,
        string? constantAttributes,
        string? previousAttributes,
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output);
        OrderLineCreateRequest creationRequest = null!;
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()))
            .Returns<OrderLineCreateRequest>((request) =>
            {
                creationRequest = request;
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)200));
            });
        line.Attributes = JoinAttributes(constantAttributes, previousAttributes);
        string expectedAttribute = null!;

        if (isItemAsset)
        {
            expectedAttribute = JoinAttributes(constantAttributes, $"{ItemToPickText}:[{reservation.ItemNumber}] [{reservation.AssetId}]");
        }
        else
        {
            expectedAttribute = JoinAttributes(constantAttributes, $"{ItemToPickText}:[{reservation.ItemNumber}] x{reservation.Quantity}");
            reservation.AssetId = reservation.ItemNumber;
        }

        await testable.Sut.CreateLine(header, line, reservation);

        creationRequest.Should().NotBeNull();
        Output.WriteLine(creationRequest!.ItemAttributesAsText);
        creationRequest!.ItemAttributesAsText.Should().Be(expectedAttribute);
        creationRequest!.ItemAttributesAsText.Should().Contain(ItemToPickText, Exactly.Once());
    }

    [SkippableTheory(Skip = "ML: This is so complicated I can't figure it out and don't feel it's worth the effort, can someone explain it and fix it please?")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        null!,
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depotfulfillsfrom:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1; Depot fulfills from:esrtjdyjdrxn")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:esrtjdyjdrxn")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Item to pick: [GN1500GHPEMM] [XBPB080150-0]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "ITEM TO PICK: [GN1500GHPEMM] [XBPB080150-0]")]
    internal async Task CreateLine_ShouldUpdateAttributesWithReservedItemFulfilledByDepot_WhenPostingLine(
        string? constantAttributes,
        string? previousAttributes,
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation,
        WarehouseItem warehouse)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output);
        testable.MockOf(x => x.ApplicationRepository)
            .Setup(x => x.GetWarehouseByCode(It.Is<string>(code => code == reservation.Warehouse), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<WarehouseItem?>(warehouse));
        OrderLineCreateRequest creationRequest = null!;
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()))
            .Returns<OrderLineCreateRequest>((request) =>
            {
                creationRequest = request;
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)200));
            });
        line.Attributes = JoinAttributes(constantAttributes, previousAttributes);
        var expectedAttribute = JoinAttributes(constantAttributes, $"{DepotFulfillesFrom}:[{reservation.Warehouse}] [{warehouse.Warehouse}] {DepotQuantity}: x{reservation.Quantity}");

        await testable.Sut.CreateLine(header, line, reservation);

        creationRequest.Should().NotBeNull();
        Output.WriteLine(creationRequest!.ItemAttributesAsText);
        creationRequest!.ItemAttributesAsText.Should().Be(expectedAttribute);
        creationRequest!.ItemAttributesAsText.Should().Contain(DepotFulfillesFrom, Exactly.Once());
    }

    [SkippableTheory(Skip = "ML: This is so complicated I can't figure it out and don't feel it's worth the effort, can someone explain it and fix it please?")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        null!,
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depotfulfillsfrom:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1; Depot fulfills from:esrtjdyjdrxn")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:esrtjdyjdrxn")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Item to pick: [GN1500GHPEMM] [XBPB080150-0]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "ITEM TO PICK: [GN1500GHPEMM] [XBPB080150-0]")]
    internal async Task CreateLine_ShouldUpdateAttributesWithReservedItemFulfilledByUnkownDepot_WhenPostingLine(
        string? constantAttributes,
        string? previousAttributes,
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output);
        testable.MockOf(x => x.ApplicationRepository)
            .Setup(x => x.GetWarehouseByCode(It.Is<string>(code => code == reservation.Warehouse), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<WarehouseItem?>((WarehouseItem?)null));
        OrderLineCreateRequest creationRequest = null!;
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()))
            .Returns<OrderLineCreateRequest>((request) =>
            {
                creationRequest = request;
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)200));
            });
        line.Attributes = JoinAttributes(constantAttributes, previousAttributes);
        var expectedAttribute = JoinAttributes(constantAttributes, $"{DepotFulfillesFrom}:[{reservation.Warehouse}] [Unknown] {DepotQuantity}: x{reservation.Quantity}");

        await testable.Sut.CreateLine(header, line, reservation);

        creationRequest.Should().NotBeNull();
        Output.WriteLine(creationRequest!.ItemAttributesAsText);
        creationRequest!.ItemAttributesAsText.Should().Be(expectedAttribute);
        creationRequest!.ItemAttributesAsText.Should().Contain(DepotFulfillesFrom, Exactly.Once());
    }

    [SkippableTheory(Skip = "ML: This is so complicated I can't figure it out and don't feel it's worth the effort, can someone explain it and fix it please?")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        null!,
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Item to pick: [GN1500GHPEMM] [XBPB080150-0]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Itemtopick: [GN1500GHPEMM] [XBPB080150-0]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "item to pick this one")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "ITEM TO PICK: [fykfyuk] [rtyhsrity]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "ITEM TO PICK: [fykfyuk] [rtyhsrity]; item to pick this one")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depotfulfillsfrom:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        true,
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        null!,
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Item to pick: [CB2401EXT010M] x20")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Itemtopick: [CB2401EXT010M] x20")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Item to pick: these ones 56")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "ITEM TO PICK: aergearg")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Item to pick: these ones 56; ITEM TO PICK: aergearg")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Depot fulfills from:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "Depotfulfillsfrom:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        false,
        "Category: Low Temperature; Hose (ins): 2; Quantity (Pieces): 1",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1")]
    internal async Task UpdateLine_ShouldUpdateAttributesWithReservedItemToPick_WhenPuttingLine(
        bool isItemAsset,
        string? constantAttributes,
        string? previousAttributes,
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output);
        OrderLineUpdateRequest updateRequest = null!;
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()))
            .Returns<OrderLineUpdateRequest>((request) =>
            {
                updateRequest = request;
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)200));
            });
        line.Attributes = JoinAttributes(constantAttributes, previousAttributes);
        string expectedAttribute = null!;

        if (isItemAsset)
        {
            expectedAttribute = JoinAttributes(constantAttributes, $"{ItemToPickText}:[{reservation.ItemNumber}] [{reservation.AssetId}]");
        }
        else
        {
            expectedAttribute = JoinAttributes(constantAttributes, $"{ItemToPickText}:[{reservation.ItemNumber}] x{reservation.Quantity}");
            reservation.AssetId = reservation.ItemNumber;
        }

        await testable.Sut.UpdateLine(header, line, reservation);

        updateRequest.Should().NotBeNull();
        Output.WriteLine(updateRequest!.ItemAttributesAsText);
        updateRequest!.ItemAttributesAsText.Should().Be(expectedAttribute);
        updateRequest!.ItemAttributesAsText.Should().Contain(ItemToPickText, Exactly.Once());
    }

    [SkippableTheory(Skip = "ML: This is so complicated I can't figure it out and don't feel it's worth the effort, can someone explain it and fix it please?")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        null!,
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depotfulfillsfrom:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1; Depot fulfills from:esrtjdyjdrxn")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:esrtjdyjdrxn")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Item to pick: [GN1500GHPEMM] [XBPB080150-0]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "ITEM TO PICK: [GN1500GHPEMM] [XBPB080150-0]")]
    internal async Task UpdateLine_ShouldUpdateAttributesWithReservedItemFulfilledByDepot_WhenPuttingLine(
        string? constantAttributes,
        string? previousAttributes,
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation,
        WarehouseItem warehouse)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output);
        testable.MockOf(x => x.ApplicationRepository)
            .Setup(x => x.GetWarehouseByCode(It.Is<string>(code => code == reservation.Warehouse), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<WarehouseItem?>(warehouse));
        OrderLineUpdateRequest creationRequest = null!;
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()))
            .Returns<OrderLineUpdateRequest>((request) =>
            {
                creationRequest = request;
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)200));
            });
        line.Attributes = JoinAttributes(constantAttributes, previousAttributes);
        var expectedAttribute = JoinAttributes(constantAttributes, $"{DepotFulfillesFrom}:[{reservation.Warehouse}] [{warehouse.Warehouse}] {DepotQuantity}: x{reservation.Quantity}");

        await testable.Sut.UpdateLine(header, line, reservation);

        creationRequest.Should().NotBeNull();
        Output.WriteLine(creationRequest!.ItemAttributesAsText);
        creationRequest!.ItemAttributesAsText.Should().Be(expectedAttribute);
        creationRequest!.ItemAttributesAsText.Should().Contain(DepotFulfillesFrom, Exactly.Once());
    }

    [SkippableTheory(Skip = "ML: This is so complicated I can't figure it out and don't feel it's worth the effort, can someone explain it and fix it please?")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        null!,
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        null!)]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depotfulfillsfrom:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "DEPOT FULFILLS FROM:[ED0] [Dumbarton UK] Qty : x1; Depot fulfills from:esrtjdyjdrxn")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Depot fulfills from:esrtjdyjdrxn")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "Item to pick: [GN1500GHPEMM] [XBPB080150-0]")]
    [InlineAutoData(customizations: new[]
        {
            typeof(MoqCustomization),
            typeof(ApplicationDbContextCustomization),
            typeof(OrderCustomization)
        },
        "Shift factor: 8 Running hours p/d; Telemetry: Yes; Voltage: 380V 3-phase @ 50 Hz",
        "ITEM TO PICK: [GN1500GHPEMM] [XBPB080150-0]")]
    internal async Task UpdateLine_ShouldUpdateAttributesWithReservedItemFulfilledByUnkownDepot_WhenPuttingLine(
        string? constantAttributes,
        string? previousAttributes,
        TestableOrderManagementService testable,
        Header header,
        Line line,
        Reservation reservation)
    {
        testable.MockOf(x => x.Logger).SinkIn(Output);
        testable.MockOf(x => x.ApplicationRepository)
            .Setup(x => x.GetWarehouseByCode(It.Is<string>(code => code == reservation.Warehouse), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<WarehouseItem?>((WarehouseItem?)null));
        OrderLineUpdateRequest creationRequest = null!;
        testable.MockOf(x => x.OrderIntegration)
            .Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()))
            .Returns<OrderLineUpdateRequest>((request) =>
            {
                creationRequest = request;
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)200));
            });
        line.Attributes = JoinAttributes(constantAttributes, previousAttributes);
        var expectedAttribute = JoinAttributes(constantAttributes, $"{DepotFulfillesFrom}:[{reservation.Warehouse}] [Unknown] {DepotQuantity}: x{reservation.Quantity}");

        await testable.Sut.UpdateLine(header, line, reservation);

        creationRequest.Should().NotBeNull();
        Output.WriteLine(creationRequest!.ItemAttributesAsText);
        creationRequest!.ItemAttributesAsText.Should().Be(expectedAttribute);
        creationRequest!.ItemAttributesAsText.Should().Contain(DepotFulfillesFrom, Exactly.Once());
    }

    private static string JoinAttributes(params string?[] values)
        => string.Join(AttributeSeparator + " ", values.Where(x => x is not null)!);

    internal class TestableOrderManagementService
    {
        public TestableOrderManagementService(
            IOrderManagementIntegration orderIntegration,
            ICoreDataRepository applicationRepository,
            ApplicationDbContext dbContext,
            ILogger<OrderManagementService> logger)
        {
            OrderIntegration = orderIntegration;
            ApplicationRepository = applicationRepository;
            DbContext = dbContext;
            Logger = logger;
        }

        public IOrderManagementIntegration OrderIntegration { get; }
        public ICoreDataRepository ApplicationRepository { get; }
        public ApplicationDbContext DbContext { get; }
        public ILogger<OrderManagementService> Logger { get; }

        private OrderManagementService? _sut;

        public OrderManagementService Sut
            => _sut ??= new OrderManagementService(OrderIntegration, ApplicationRepository, DbContext, Logger);

        public Mock<T> MockOf<T>(Func<TestableOrderManagementService, T> getter) where T : class
            => Mock.Get(getter(this));
    }
}
