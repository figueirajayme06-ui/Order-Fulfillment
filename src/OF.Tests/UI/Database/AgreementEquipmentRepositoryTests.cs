using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using static OF.Common.Enums;

namespace OF.Tests.UI.Database;

public class AgreementEquipmentRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 10, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("T100", 0, 0, true)]
    [InlineData("T100", 0, 3, false)]
    [InlineData("A100", 3, 3, true)]
    [InlineData("A100", 3, 0, false)]
    [InlineData("Q100", 0, 0, false)]
    public void ParentEligibility_RequiresStableHeaderAndCorrespondingRootLineState(
        string agreementNumber,
        int headerStatus,
        int lineStatus,
        bool expected)
    {
        var header = CreateHeader(agreementNumber, headerStatus);
        var line = CreateParent(lineStatus);

        AgreementEquipmentEligibility.CanChange(header).Should().Be(expected || agreementNumber != "Q100");
        AgreementEquipmentEligibility.IsEligibleParent(header, line).Should().Be(expected);
    }

    [Fact]
    public void AttributeParser_RejectsMoreThanOneValueForAGroup()
    {
        var parsed = AgreementEquipmentAttributeSelection.TryParse(
            ["Voltage:240V", "voltage:415V"],
            out var selections,
            out var error);

        parsed.Should().BeFalse();
        selections.Should().BeEmpty();
        error.Should().Contain("Only one value");
    }

    [Theory]
    [InlineData("division")]
    [InlineData("division-mismatch")]
    [InlineData("warehouse")]
    [InlineData("facility")]
    [InlineData("delivery")]
    [InlineData("from-date")]
    [InlineData("date-order")]
    [InlineData("line-type")]
    [InlineData("rate-type")]
    [InlineData("shifts")]
    public void ParentEligibility_RejectsIncompleteActivationLogistics(string missingField)
    {
        var header = CreateHeader("A100", (int)ActivationStatus.Activated);
        var line = CreateParent((int)ActivationStatus.Activated);
        switch (missingField)
        {
            case "division": line.Division = " "; break;
            case "division-mismatch": line.Division = "FR"; break;
            case "warehouse": line.Warehouse = " "; break;
            case "facility": line.Facility = " "; break;
            case "delivery": line.DeliveryDate = null; break;
            case "from-date": line.ValidFromDate = DateTime.MinValue; break;
            case "date-order": line.ValidToDate = line.ValidFromDate.AddDays(-1); break;
            case "line-type": line.AgreementLineType = " "; break;
            case "rate-type": line.RateType = " "; break;
            case "shifts": line.NumberOfShifts = " "; break;
            default: throw new ArgumentOutOfRangeException(nameof(missingField), missingField, null);
        }

        AgreementEquipmentEligibility.IsEligibleParent(header, line).Should().BeFalse();
    }

    [Fact]
    public void Catalog_ReturnsOnlyActiveAvailableProductsForDivision_WithGroupedAttributes()
    {
        using var context = new InMemoryDBContext();
        SeedCatalog(context, includeAttributes: true);
        context.CpqItems.Add(new CpqItem
        {
            Id = 31,
            GenericId = 20,
            ItemNumber = "OTHER-DIVISION",
            DescriptionIntl = "Other division item",
            DescriptionNam = "Other division item",
            Active = true,
            VerCol = [],
        });
        context.ProductItems.Add(new ProductItem
        {
            Warehouse = "FR1",
            ItemNumber = "OTHER-DIVISION",
            Division = "FR",
            Facility = "FR-FAC",
            Status = "ACTIVE",
        });
        context.SaveChanges();
        var subject = CreateSubject(context);

        var catalog = subject.GetCatalog("UK");
        var genericCatalog = subject.GetGenericCatalog("UK", 20, ["Voltage:240V"]);

        catalog.ProductLines.Should().ContainSingle().Which.FamilyDescription.Should().Be("Power");
        catalog.Generics.Should().ContainSingle().Which.Code.Should().Be("GEN60");
        genericCatalog.Failure.Should().Be(AgreementEquipmentFailure.None);
        genericCatalog.AttributeGroups.Should().ContainSingle()
            .Which.Values.Should().Equal("240V");
        genericCatalog.Items.Should().ContainSingle()
            .Which.ItemNumber.Should().Be("GEN60-240");
    }

    [Fact]
    public void CreateExactItem_CopiesOperationalMetadata_PersistsGeneric_AndAllocatesMonotonicSuffixes()
    {
        using var context = new InMemoryDBContext();
        SeedCatalog(context);
        var header = CreateHeader("A100", (int)ActivationStatus.Activated);
        header.FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled;
        context.Headers.Add(header);
        var parent = CreateParent((int)ActivationStatus.Activated);
        parent.HeaderId = header.Id;
        context.Lines.AddRange(
            parent,
            CreateExistingChild(header.Id, "A100-1.1", isDeleted: true),
            CreateExistingChild(header.Id, "A100-1.3"));
        context.SaveChanges();
        var identity = CreateIdentity();
        var subject = CreateSubject(context);

        var first = subject.Create(
            identity.Object,
            header.Id,
            "UK",
            new AgreementEquipmentCreateCommand(parent.Id, 20, "GEN60-240", [], 2));
        var second = subject.Create(
            identity.Object,
            header.Id,
            "UK",
            new AgreementEquipmentCreateCommand(parent.Id, 20, null, [], 1));

        first.Failure.Should().Be(AgreementEquipmentFailure.None);
        second.Failure.Should().Be(AgreementEquipmentFailure.None);
        first.Line!.AgreementLineNumber.Should().Be("A100-1.4");
        second.Line!.AgreementLineNumber.Should().Be("A100-1.5");
        first.Line.ItemNumber.Should().Be("GEN60-240");
        first.Line.GenericItemNumber.Should().Be("GEN60");
        second.Line.ItemNumber.Should().Be("GEN60");
        second.Line.GenericItemNumber.Should().Be("GEN60");
        first.Line.ActivationStatus.Should().Be((int)ActivationStatus.TODO);
        first.Line.FulfilmentStatus.Should().Be((int)FulfilmentStatus.Unfulfilled);
        first.Line.RequiresFulfilment.Should().BeTrue();
        first.Line.Quantity.Should().Be(2);
        first.Line.QuantityFulfilled.Should().Be(0);
        first.Line.LastUpdatedBy.Should().Be("planner@example.com");
        first.Line.LastUpdatedDate.Should().Be(Now.UtcDateTime);
        AssertInheritedFields(first.Line, parent);
        context.Headers.Single().FulfilmentStatus.Should().Be((int)FulfilmentStatus.PartiallyFulfilled);
    }

    [Fact]
    public void Create_RejectsInactiveUnavailableOrWrongGenericItemWithoutWriting()
    {
        using var context = new InMemoryDBContext();
        SeedCatalog(context);
        var header = CreateHeader("T100", (int)ActivationStatus.TODO);
        context.Headers.Add(header);
        var parent = CreateParent((int)ActivationStatus.TODO);
        parent.HeaderId = header.Id;
        context.Lines.Add(parent);
        context.CpqItems.Add(new CpqItem
        {
            Id = 31,
            GenericId = 20,
            ItemNumber = "INACTIVE",
            DescriptionIntl = "Inactive",
            DescriptionNam = "Inactive",
            Active = false,
            VerCol = [],
        });
        context.ProductItems.Add(new ProductItem
        {
            Warehouse = "UK1",
            ItemNumber = "INACTIVE",
            Division = "UK",
            Facility = "UK-FAC",
            Status = "ACTIVE",
        });
        context.SaveChanges();
        var subject = CreateSubject(context);

        var result = subject.Create(
            CreateIdentity().Object,
            header.Id,
            "UK",
            new AgreementEquipmentCreateCommand(parent.Id, 20, "INACTIVE", [], 1));

        result.Failure.Should().Be(AgreementEquipmentFailure.InvalidItem);
        context.Lines.Should().ContainSingle();
    }

    [Fact]
    public void Create_RejectsParentFromWrongStableState()
    {
        using var context = new InMemoryDBContext();
        SeedCatalog(context);
        var header = CreateHeader("A100", (int)ActivationStatus.Activated);
        context.Headers.Add(header);
        var parent = CreateParent((int)ActivationStatus.TODO);
        parent.HeaderId = header.Id;
        context.Lines.Add(parent);
        context.SaveChanges();

        var result = CreateSubject(context).Create(
            CreateIdentity().Object,
            header.Id,
            "UK",
            new AgreementEquipmentCreateCommand(parent.Id, 20, null, [], 1));

        result.Failure.Should().Be(AgreementEquipmentFailure.ParentNotEligible);
        context.Lines.Should().ContainSingle();
    }

    [Fact]
    public void Delete_RejectsReservations_ThenSoftDeletesPendingLocalLineAndRecalculatesHeader()
    {
        using var context = new InMemoryDBContext();
        var header = CreateHeader("T100", (int)ActivationStatus.TODO);
        header.FulfilmentStatus = (int)FulfilmentStatus.PartiallyFulfilled;
        context.Headers.Add(header);
        var parent = CreateParent((int)ActivationStatus.TODO);
        parent.HeaderId = header.Id;
        var child = CreateExistingChild(header.Id, "T100-1.1");
        context.Lines.AddRange(parent, child);
        var reservation = new Reservation
        {
            LineId = child.Id,
            AssetId = "ASSET-1",
            ItemNumber = "GEN60-240",
            Warehouse = "UK1",
            Quantity = 1,
        };
        context.Reservations.Add(reservation);
        context.SaveChanges();
        var subject = CreateSubject(context);

        var guarded = subject.Delete(CreateIdentity().Object, header.Id, "UK", child.Id);
        guarded.Failure.Should().Be(AgreementEquipmentFailure.ReservationsExist);
        child.IsDeleted.Should().BeFalse();

        context.Reservations.Remove(reservation);
        context.SaveChanges();
        var deleted = subject.Delete(CreateIdentity().Object, header.Id, "UK", child.Id);

        deleted.Failure.Should().Be(AgreementEquipmentFailure.None);
        child.IsDeleted.Should().BeTrue();
        child.LastUpdatedBy.Should().Be("planner@example.com");
        header.FulfilmentStatus.Should().Be((int)FulfilmentStatus.FullyFulfiled);
    }

    [Theory]
    [InlineData("T100-1", 0)]
    [InlineData("T100-1.1", 1)]
    [InlineData("T100-1.1", 2)]
    [InlineData("T100-1.1", 3)]
    public void Delete_RejectsRootOrNonTodoLine(string lineNumber, int activationStatus)
    {
        using var context = new InMemoryDBContext();
        var header = CreateHeader("T100", (int)ActivationStatus.TODO);
        context.Headers.Add(header);
        var line = CreateExistingChild(header.Id, lineNumber);
        line.ActivationStatus = activationStatus;
        context.Lines.Add(line);
        context.SaveChanges();

        var result = CreateSubject(context).Delete(
            CreateIdentity().Object,
            header.Id,
            "UK",
            line.Id);

        result.Failure.Should().Be(AgreementEquipmentFailure.LineNotDeletable);
        line.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void HeaderStatusRecalculation_ExcludesServiceLines()
    {
        using var context = new InMemoryDBContext();
        var header = CreateHeader("T100", (int)ActivationStatus.TODO);
        context.Headers.Add(header);
        var parent = CreateParent((int)ActivationStatus.TODO);
        parent.HeaderId = header.Id;
        var child = CreateExistingChild(header.Id, "T100-1.1");
        var service = CreateExistingChild(header.Id, "T100-2");
        service.ItemNumber = "SERVICE";
        service.FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled;
        context.Lines.AddRange(parent, child, service);
        context.CpqServices.Add(new CpqService
        {
            Id = 99,
            LineId = 10,
            ProductCode = "SERVICE",
            ProductFamily = string.Empty,
            ServiceCode = string.Empty,
            M3itemNumber = string.Empty,
            ProductName = string.Empty,
            ProductDescription = string.Empty,
            M3type = string.Empty,
            ShiftFactor = string.Empty,
            PricingMethod = string.Empty,
            ConfigurationType = string.Empty,
            ConfigurationEvent = string.Empty,
            OptionLayout = string.Empty,
            ChargeMethod = string.Empty,
            ChargeFrequency = string.Empty,
            LineChargeId = string.Empty,
            M3lineType = string.Empty,
            NonRentalCharge = string.Empty,
            ProposalSection = string.Empty,
            Configuration = string.Empty,
            ContractedServiceField = string.Empty,
            GeneratedValue = string.Empty,
            SbqqsortOrder = string.Empty,
            SbqqdefaultQuantity = string.Empty,
            SbqqoptionSelectionMethod = string.Empty,
            SbqqsubscriptionBase = string.Empty,
            SbqqsubscriptionType = string.Empty,
            RecordTypeId = string.Empty,
            ExternalId = string.Empty,
            ExternalId2 = string.Empty,
        });
        context.SaveChanges();

        var result = CreateSubject(context).Delete(
            CreateIdentity().Object,
            header.Id,
            "UK",
            child.Id);

        result.Failure.Should().Be(AgreementEquipmentFailure.None);
        header.FulfilmentStatus.Should().Be((int)FulfilmentStatus.FullyFulfiled);
    }

    private static AgreementEquipmentRepository CreateSubject(InMemoryDBContext context) =>
        new(context, new FakeTimeProvider(Now));

    private static Mock<IUserIdentity> CreateIdentity()
    {
        var identity = new Mock<IUserIdentity>();
        identity.Setup(service => service.GetIdentity()).Returns(new User
        {
            LoginName = "planner@example.com",
            FullName = "Fleet Planner",
            Division = "UK",
            DateFormat = "dd/MM/yyyy",
        });
        return identity;
    }

    private static Header CreateHeader(string agreementNumber, int activationStatus) => new()
    {
        Id = 42,
        AgreementNumber = agreementNumber,
        ActivationStatus = activationStatus,
        Division = "UK",
        Facility = "UK-FAC",
        OrderSource = "IPG",
    };

    private static Line CreateParent(int activationStatus) => new()
    {
        Id = 7,
        AgreementLineNumber = "A100-1",
        OrderLineNumber = "ORDER-LINE",
        QuoteLineNumber = "QUOTE-LINE",
        AgreementLineType = "Rental",
        Status = "Open",
        ValidFromDate = new DateTime(2026, 9, 1),
        ValidToDate = new DateTime(2026, 9, 30),
        DeliveryDate = new DateTime(2026, 8, 31, 8, 30, 0),
        TerminationDate = new DateTime(2026, 10, 1),
        CollectionDate = new DateTime(2026, 10, 2),
        Division = "UK",
        Warehouse = "UK1",
        Facility = "UK-FAC",
        PackageGroupNumber = "PKG-1",
        AgreementLineIndex = 1,
        OrderLineIndex = 2,
        QuoteLineIndex = 3,
        OrderSource = "IPG",
        AgreementNumbersOnly = "100",
        QuotePublicId = "QUOTE-PUBLIC",
        QuotePublicIdNumbersOnly = "123",
        NumberOfShifts = "1",
        RateType = "Daily",
        ChangeSequence = 9,
        ActivationStatus = activationStatus,
        RequiresFulfilment = true,
        ItemNumber = "PARENT",
        Quantity = 1,
        QuantityFulfilled = 1,
        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
    };

    private static Line CreateExistingChild(int headerId, string lineNumber, bool isDeleted = false) => new()
    {
        HeaderId = headerId,
        AgreementLineNumber = lineNumber,
        ItemNumber = "GEN60",
        GenericItemNumber = "GEN60",
        Division = "UK",
        Warehouse = "UK1",
        Facility = "UK-FAC",
        OrderSource = "IPG",
        ValidFromDate = new DateTime(2026, 9, 1),
        ValidToDate = new DateTime(2026, 9, 30),
        Quantity = 1,
        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
        ActivationStatus = (int)ActivationStatus.TODO,
        RequiresFulfilment = true,
        IsDeleted = isDeleted,
    };

    private static void SeedCatalog(InMemoryDBContext context, bool includeAttributes = false)
    {
        var family = new CpqFamily { Id = 1, FamilyDescription = "Power" };
        var productLine = new CpqLine { Id = 10, FamilyId = 1, Family = family, LineDescription = "Generators" };
        var generic = new CpqGeneric
        {
            Id = 20,
            LineId = 10,
            Line = productLine,
            GenericCode = "GEN60",
            GenericDescription = "Generator 60",
            Active = true,
            VerCol = [],
        };
        var item = new CpqItem
        {
            Id = 30,
            GenericId = 20,
            Generic = generic,
            ItemNumber = "GEN60-240",
            DescriptionIntl = "Generator 60 240V",
            DescriptionNam = "Generator 60 240V",
            Active = true,
            VerCol = [],
        };
        context.AddRange(family, productLine, generic, item);
        context.ProductItems.Add(new ProductItem
        {
            Warehouse = "UK1",
            ItemNumber = item.ItemNumber,
            Division = "UK",
            Facility = "UK-FAC",
            Status = "ACTIVE",
        });

        if (includeAttributes)
        {
            var attribute = new CpqAttribute
            {
                Id = 40,
                AttributeName = "Voltage",
                AttributeDescription = "Voltage",
                Cpqattribute1 = "Voltage",
                DataType = "Text",
                VerCol = [],
            };
            var purpose = new CpqPurpose { Id = 4, PurposeDescription = "FAM" };
            context.AddRange(
                attribute,
                purpose,
                new CpqLineAttributePurpose
                {
                    Id = 50,
                    LineId = productLine.Id,
                    AttributeId = attribute.Id,
                    PurposeId = purpose.Id,
                    Active = "TRUE",
                    VerCol = [],
                },
                new CpqItemAttributeValue
                {
                    Id = 60,
                    ItemId = item.Id,
                    AttributeId = attribute.Id,
                    Value = "240V",
                    VerCol = [],
                });
        }

        context.SaveChanges();
    }

    private static void AssertInheritedFields(Line actual, Line parent)
    {
        actual.HeaderId.Should().Be(parent.HeaderId);
        actual.OrderLineNumber.Should().Be(parent.OrderLineNumber);
        actual.QuoteLineNumber.Should().Be(parent.QuoteLineNumber);
        actual.AgreementLineType.Should().Be(parent.AgreementLineType);
        actual.Status.Should().Be(parent.Status);
        actual.ValidFromDate.Should().Be(parent.ValidFromDate);
        actual.ValidToDate.Should().Be(parent.ValidToDate);
        actual.DeliveryDate.Should().Be(parent.DeliveryDate);
        actual.TerminationDate.Should().Be(parent.TerminationDate);
        actual.CollectionDate.Should().Be(parent.CollectionDate);
        actual.Division.Should().Be(parent.Division);
        actual.Warehouse.Should().Be(parent.Warehouse);
        actual.Facility.Should().Be(parent.Facility);
        actual.PackageGroupNumber.Should().Be(parent.PackageGroupNumber);
        actual.AgreementLineIndex.Should().Be(parent.AgreementLineIndex);
        actual.OrderLineIndex.Should().Be(parent.OrderLineIndex);
        actual.QuoteLineIndex.Should().Be(parent.QuoteLineIndex);
        actual.OrderSource.Should().Be(parent.OrderSource);
        actual.AgreementNumbersOnly.Should().Be(parent.AgreementNumbersOnly);
        actual.QuotePublicId.Should().Be(parent.QuotePublicId);
        actual.QuotePublicIdNumbersOnly.Should().Be(parent.QuotePublicIdNumbersOnly);
        actual.NumberOfShifts.Should().Be(parent.NumberOfShifts);
        actual.RateType.Should().Be(parent.RateType);
        actual.ChangeSequence.Should().Be(parent.ChangeSequence);
    }
}
