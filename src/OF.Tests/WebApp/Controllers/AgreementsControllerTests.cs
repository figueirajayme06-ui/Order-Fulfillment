using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;
using OF.WebApp.Features.Agreements;

namespace OF.Tests.WebApp.Controllers;

public class AgreementsControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();

    [Fact]
    public void GetAgreements_ReturnsUnauthorizedWithoutReadingAgreements_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetAgreements();

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(
            repository => repository.GetAgreements(It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public void GetAgreements_PassesExactFulfilmentAndHistoricalFlagsToRepository(
        bool hideFulfilled,
        bool showHistorical,
        bool expectedShowFulfilled)
    {
        SetIdentity(null, isSuperAdmin: true);
        SetAgreements();

        var result = CreateSubject().GetAgreements(
            hideFulfilled: hideFulfilled,
            showHistorical: showHistorical);

        GetOkAgreements(result).Should().BeEmpty();
        _repository.Verify(
            repository => repository.GetAgreements(expectedShowFulfilled, showHistorical),
            Times.Once);
    }

    [Fact]
    public void GetAgreements_UsesAllAssignedDivisionsIgnoringCaseWhitespaceAndDuplicates()
    {
        SetIdentity(" uk, IE,uk ");
        SetAgreements(
            CreateAgreement(1, "UK", new DateTime(2026, 1, 1)),
            CreateAgreement(2, "ie", new DateTime(2026, 2, 1)),
            CreateAgreement(3, "FR", new DateTime(2026, 3, 1)));

        var result = CreateSubject().GetAgreements();

        GetOkAgreements(result).Select(agreement => agreement.Id).Should().Equal(2, 1);
    }

    [Fact]
    public void GetAgreements_ExplicitDivisionsCanOnlyNarrowAssignedDivisions()
    {
        SetIdentity("UK, IE");
        SetAgreements(
            CreateAgreement(1, "UK", new DateTime(2026, 1, 1)),
            CreateAgreement(2, "ie", new DateTime(2026, 2, 1)),
            CreateAgreement(3, "FR", new DateTime(2026, 3, 1)));

        var result = CreateSubject().GetAgreements(division: " ie, FR ");

        GetOkAgreements(result).Select(agreement => agreement.Id).Should().Equal(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void GetAgreements_ReturnsEmpty_WhenNormalUserHasNoAssignedDivisions(string? assignedDivisions)
    {
        SetIdentity(assignedDivisions);
        SetAgreements(CreateAgreement(1, "UK"));

        var result = CreateSubject().GetAgreements(division: "UK");

        GetOkAgreements(result).Should().BeEmpty();
    }

    [Fact]
    public void GetAgreements_ReturnsEmpty_WhenExplicitDivisionsAreOutsideAssignedDivisions()
    {
        SetIdentity("UK");
        SetAgreements(CreateAgreement(1, "UK"), CreateAgreement(2, "FR"));

        var result = CreateSubject().GetAgreements(division: "FR");

        GetOkAgreements(result).Should().BeEmpty();
    }

    [Fact]
    public void GetAgreements_ReturnsAllDivisionsForSuperAdmin_WhenNoDivisionIsRequested()
    {
        SetIdentity(null, isSuperAdmin: true);
        SetAgreements(
            CreateAgreement(1, "UK", new DateTime(2026, 1, 1)),
            CreateAgreement(2, "FR", new DateTime(2026, 2, 1)),
            CreateAgreement(3, null, new DateTime(2026, 3, 1)));

        var result = CreateSubject().GetAgreements();

        GetOkAgreements(result).Select(agreement => agreement.Id).Should().Equal(3, 2, 1);
    }

    [Fact]
    public void GetAgreements_UsesRequestedDivisionsForSuperAdmin()
    {
        SetIdentity("UK", isSuperAdmin: true);
        SetAgreements(
            CreateAgreement(1, "UK", new DateTime(2026, 1, 1)),
            CreateAgreement(2, "fr", new DateTime(2026, 2, 1)));

        var result = CreateSubject().GetAgreements(division: " FR ");

        GetOkAgreements(result).Select(agreement => agreement.Id).Should().Equal(2);
    }

    [Fact]
    public void GetAgreements_GenericSearchIncludesOpportunityAndLastUpdatedByName()
    {
        SetIdentity(null, isSuperAdmin: true);
        SetAgreements(
            new VwHeader
            {
                Id = 1,
                Division = "UK",
                OrderSource = "NOF",
                OnHireDate = new DateTime(2026, 1, 1),
                OpportunityName = "Needle renewal project",
            },
            new VwHeader
            {
                Id = 2,
                Division = "UK",
                OrderSource = "NOF",
                OnHireDate = new DateTime(2026, 2, 1),
                LastUpdatedByName = "Needle Manager",
            },
            CreateAgreement(3, "UK", new DateTime(2026, 3, 1)));

        var result = CreateSubject().GetAgreements(search: " nEeDlE ");

        GetOkAgreements(result).Select(agreement => agreement.Id).Should().Equal(2, 1);
    }

    [Fact]
    public void GetAgreements_FiltersByAnyRequestedOrderTypeAndFulfilmentStatus()
    {
        SetIdentity(null, isSuperAdmin: true);
        var quote = CreateAgreement(1, "UK", new DateTime(2026, 1, 1));
        quote.AgreementNumber = "Q-1";
        quote.FulfilmentStatus = 0;
        var temporaryAgreement = CreateAgreement(2, "UK", new DateTime(2026, 2, 1));
        temporaryAgreement.AgreementNumber = "T-2";
        temporaryAgreement.FulfilmentStatus = 1;
        var agreement = CreateAgreement(3, "UK", new DateTime(2026, 3, 1));
        agreement.AgreementNumber = "A-3";
        agreement.FulfilmentStatus = 3;
        SetAgreements(quote, temporaryAgreement, agreement);

        var result = CreateSubject().GetAgreements(orderTypes: " quote, agreement ", statuses: "0,3");

        GetOkAgreements(result).Select(item => item.Id).Should().Equal(3, 1);
    }

    [Fact]
    public void GetAgreements_PreservesSingularOrderTypeAndStatusFilters()
    {
        SetIdentity(null, isSuperAdmin: true);
        var quote = CreateAgreement(1, "UK");
        quote.AgreementNumber = "Q-1";
        quote.FulfilmentStatus = 1;
        var agreement = CreateAgreement(2, "UK");
        agreement.AgreementNumber = "A-2";
        agreement.FulfilmentStatus = 1;
        SetAgreements(quote, agreement);

        var result = CreateSubject().GetAgreements(orderType: "quote", status: 1);

        GetOkAgreements(result).Select(item => item.Id).Should().Equal(1);
    }

    [Fact]
    public void GetAgreements_OrdersByOnHireDateDescendingBeforeApplyingPositiveTake()
    {
        SetIdentity(null, isSuperAdmin: true);
        SetAgreements(
            CreateAgreement(1, "UK", new DateTime(2026, 1, 1)),
            CreateAgreement(2, "UK", new DateTime(2026, 3, 1)),
            CreateAgreement(3, "UK", new DateTime(2026, 2, 1)));

        var result = CreateSubject().GetAgreements(take: 2);

        GetOkAgreements(result).Select(agreement => agreement.Id).Should().Equal(2, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetAgreements_DoesNotLimitResults_WhenTakeIsNotPositive(int take)
    {
        SetIdentity(null, isSuperAdmin: true);
        SetAgreements(
            CreateAgreement(1, "UK", new DateTime(2026, 1, 1)),
            CreateAgreement(2, "UK", new DateTime(2026, 3, 1)),
            CreateAgreement(3, "UK", new DateTime(2026, 2, 1)));

        var result = CreateSubject().GetAgreements(take: take);

        GetOkAgreements(result).Select(agreement => agreement.Id).Should().Equal(2, 3, 1);
    }

    [Fact]
    public void GetAgreements_SerializesLegacyGridFieldsAndNoteCountsToWebJsonContract()
    {
        SetIdentity(null, isSuperAdmin: true);
        SetAgreements(
            new VwHeader
            {
                Id = 42,
                AgreementNumber = "A-100",
                CustomerName = "Customer Ltd",
                CustomerNumber = "C-1",
                Division = "UK",
                Warehouse = "ED1",
                FulfilmentStatus = 2,
                OnHireDate = new DateTime(2026, 1, 2),
                OffHireDate = new DateTime(2026, 2, 3),
                IsDeleted = true,
                OrderSource = "NOF",
                LineCount = 3,
                DeliveryDate = new DateTime(2025, 12, 30),
                ValidFromDate = new DateTime(2026, 1, 1),
                ValidToDate = new DateTime(2026, 12, 31),
                TerminationDate = new DateTime(2026, 11, 30),
                CollectionDate = new DateTime(2027, 1, 2),
                CustomerAddress = "1 Test Street",
                LastUpdatedByName = "Test User",
                OpportunityName = "Opportunity 1",
                FromDate = new DateTime(2026, 1, 4),
                ToDate = new DateTime(2026, 10, 31),
                LastUpdatedDate = new DateTime(2026, 1, 5),
                OpportunityStage = "Proposal",
                Probability = 75.5f,
                MinFulfilmentStatus = 1,
                MaxFulfilmentStatus = 3,
            },
            new VwHeader
            {
                Id = 7,
                Division = null!,
                OrderSource = null!,
            });
        _repository.Setup(repository => repository.GetNotes("agreement")).Returns(new[]
        {
            new Note
            {
                Id = 1,
                ParentId = "42",
                NoteType = "agreement",
                Note1 = "Follow up",
                LastUpdatedBy = "test.user@example.com",
                LastUpdatedDate = new DateTime(2026, 1, 7),
            },
        }.AsQueryable());

        var response = GetOkAgreements(CreateSubject().GetAgreements());

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Be(
            "[{\"id\":42,\"agreementNumber\":\"A-100\",\"customerName\":\"Customer Ltd\"," +
            "\"customerNumber\":\"C-1\",\"division\":\"UK\",\"warehouse\":\"ED1\"," +
            "\"fulfilmentStatus\":2,\"onHireDate\":\"2026-01-02T00:00:00\"," +
            "\"offHireDate\":\"2026-02-03T00:00:00\",\"isDeleted\":true,\"orderSource\":\"NOF\"," +
            "\"lineCount\":3,\"deliveryDate\":\"2025-12-30T00:00:00\"," +
            "\"validFromDate\":\"2026-01-01T00:00:00\",\"validToDate\":\"2026-12-31T00:00:00\"," +
            "\"terminationDate\":\"2026-11-30T00:00:00\",\"collectionDate\":\"2027-01-02T00:00:00\"," +
            "\"customerAddress\":\"1 Test Street\",\"lastUpdatedByName\":\"Test User\"," +
            "\"opportunityName\":\"Opportunity 1\",\"fromDate\":\"2026-01-04T00:00:00\"," +
            "\"toDate\":\"2026-10-31T00:00:00\",\"lastUpdatedDate\":\"2026-01-05T00:00:00\"," +
            "\"opportunityStage\":\"Proposal\",\"probability\":75.5,\"minFulfilmentStatus\":1," +
            "\"maxFulfilmentStatus\":3,\"noteCount\":1},{\"id\":7,\"agreementNumber\":null," +
            "\"customerName\":null,\"customerNumber\":null,\"division\":null,\"warehouse\":null," +
            "\"fulfilmentStatus\":0,\"onHireDate\":null,\"offHireDate\":null,\"isDeleted\":false," +
            "\"orderSource\":null,\"lineCount\":null,\"deliveryDate\":null,\"validFromDate\":null," +
            "\"validToDate\":null,\"terminationDate\":null,\"collectionDate\":null," +
            "\"customerAddress\":null,\"lastUpdatedByName\":null,\"opportunityName\":null," +
            "\"fromDate\":null,\"toDate\":null,\"lastUpdatedDate\":null,\"opportunityStage\":null," +
            "\"probability\":null,\"minFulfilmentStatus\":null,\"maxFulfilmentStatus\":null,\"noteCount\":0}]");
        using var document = JsonDocument.Parse(json);
        document.RootElement[0].EnumerateObject().Select(property => property.Name).Should().Equal(
            "id",
            "agreementNumber",
            "customerName",
            "customerNumber",
            "division",
            "warehouse",
            "fulfilmentStatus",
            "onHireDate",
            "offHireDate",
            "isDeleted",
            "orderSource",
            "lineCount",
            "deliveryDate",
            "validFromDate",
            "validToDate",
            "terminationDate",
            "collectionDate",
            "customerAddress",
            "lastUpdatedByName",
            "opportunityName",
            "fromDate",
            "toDate",
            "lastUpdatedDate",
            "opportunityStage",
            "probability",
            "minFulfilmentStatus",
            "maxFulfilmentStatus",
            "noteCount");
        document.RootElement[0].GetProperty("noteCount").GetInt32().Should().Be(1);
        document.RootElement[1].EnumerateObject().Should().HaveCount(28);
        document.RootElement[1].GetProperty("noteCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public void GetAgreement_ReturnsUnauthorizedWithoutRepositoryAccess_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetAgreement(42);

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetHeader(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.GetLines(It.IsAny<int>()), Times.Never);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetAgreement_ReturnsNotFoundWithoutReadingLines_WhenAgreementDoesNotExist()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeader(42)).Returns((Header?)null);

        var result = CreateSubject().GetAgreement(42);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetHeader(42), Times.Once);
        _repository.Verify(repository => repository.GetLines(It.IsAny<int>()), Times.Never);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetAgreement_ReturnsNotFoundWithoutReadingLines_WhenAgreementIsOutsideCallerDivision()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader("FR"));

        var result = CreateSubject().GetAgreement(42);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetHeader(42), Times.Once);
        _repository.Verify(repository => repository.GetLines(It.IsAny<int>()), Times.Never);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetAgreement_ReturnsNotFoundWithoutReadingLines_WhenNormalUserHasNoDivision()
    {
        SetIdentity("");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader("UK"));

        var result = CreateSubject().GetAgreement(42);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetHeader(42), Times.Once);
        _repository.Verify(repository => repository.GetLines(It.IsAny<int>()), Times.Never);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetAgreement_ReadsHeaderBeforeLinesAndReturnsConcreteDtosInRepositoryLineOrder()
    {
        SetIdentity("uk,IE");
        var header = CreateHeader("UK");
        var lines = new[] { CreateLine(8), CreateLine(7) };
        var repositoryCalls = new List<string>();
        _repository.Setup(repository => repository.GetHeader(42))
            .Callback(() => repositoryCalls.Add("header"))
            .Returns(header);
        _repository.Setup(repository => repository.GetLines(42))
            .Callback(() => repositoryCalls.Add("lines"))
            .Returns(lines.AsQueryable());

        var result = CreateSubject().GetAgreement(42);

        var response = GetOkDetail(result);
        repositoryCalls.Should().Equal("header", "lines");
        response.Header.GetType().Should().Be(typeof(AgreementHeaderDetailResponse));
        response.Lines.GetType().Should().Be(typeof(List<AgreementLineDetailResponse>));
        response.Header.Id.Should().Be(header.Id);
        response.Header.Division.Should().Be(header.Division);
        response.Lines.Select(line => line.Id).Should().Equal(8, 7);
        ((object)response.Header).Should().NotBeSameAs(header);
        _repository.Verify(repository => repository.GetHeader(42), Times.Once);
        _repository.Verify(repository => repository.GetLines(42), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetAgreement_SerializesFullScalarContractAndOmitsLinkedEntityGraph()
    {
        SetIdentity("uk,IE");
        var header = CreateFullyPopulatedHeader();
        var line = CreateFullyPopulatedLine();
        var changeOrder = new ChangeOrder
        {
            Id = 91,
            HeaderId = header.Id,
            Status = 0,
            CreatedBy = "planner@example.com",
            CreatedDate = new DateTime(2026, 1, 1),
            Header = header,
        };
        header.ChangeOrders.Add(changeOrder);
        header.ChangeOrderHeaders.Add(new ChangeOrderHeader
        {
            ChangeOrderId = changeOrder.Id,
            HeaderId = header.Id,
            ChangeOrder = changeOrder,
            Header = header,
        });
        header.Lines.Add(line);
        line.Header = header;
        line.ChangeOrderLines.Add(new ChangeOrderLine
        {
            Id = 92,
            ChangeOrderId = changeOrder.Id,
            Warehouse = "ED1",
            ChangeOrder = changeOrder,
            Line = line,
        });
        header.CurrentChangeOrder.Should().BeSameAs(changeOrder);
        _repository.Setup(repository => repository.GetHeader(42)).Returns(header);
        _repository.Setup(repository => repository.GetLines(42)).Returns(new[] { line }.AsQueryable());

        var response = GetOkDetail(CreateSubject().GetAgreement(42));

        response.Header.IsActivated.Should().BeTrue();
        response.Lines.Single().IsSubline.Should().BeTrue();
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Be(
            "{\"header\":{\"isActivated\":true,\"id\":42,\"quotePublicId\":\"Q-PUBLIC-1\"," +
            "\"agreementNumber\":\"A-100\",\"onHireDate\":\"2026-01-01T00:00:00\"," +
            "\"offHireDate\":\"2026-12-31T00:00:00\",\"status\":\"Active\"," +
            "\"customerName\":\"Customer Ltd\",\"customerAddress\":\"1 Test Street\"," +
            "\"customerNumber\":\"CUST-1\",\"division\":\"UK\",\"customerAddressCode\":\"ADDR-1\"," +
            "\"orderSource\":\"NOF\",\"changeSequence\":1234567890123,\"isDeleted\":true," +
            "\"fulfilmentStatus\":2,\"lastUpdatedBy\":\"planner@example.com\"," +
            "\"lastUpdatedDate\":\"2026-01-04T05:06:07\",\"facility\":\"FAC1\"," +
            "\"opportunityNumber\":\"OPP-1\",\"orderNumber\":\"ORD-1\",\"quoteNumber\":\"QUOTE-1\"," +
            "\"agreementNumbersOnly\":\"100\",\"quotePublicIdNumbersOnly\":\"200\"," +
            "\"overviewOfService\":\"Generator hire\",\"probability\":0.75," +
            "\"armcontactName\":\"Alex Contact\",\"armcontactEmail\":\"alex@example.com\"," +
            "\"armcontactPhone\":\"01234\",\"activationStatus\":2," +
            "\"activationErrors\":\"Header warning\",\"activationInstanceId\":\"header-instance\"," +
            "\"rentalDepot\":\"ED1\",\"opportunityName\":\"Opportunity 1\"," +
            "\"opportunityStage\":\"Negotiation\",\"isSkeleton\":true},\"lines\":[{" +
            "\"isSubline\":true,\"id\":7,\"headerId\":42,\"orderLineNumber\":\"O1\"," +
            "\"agreementLineNumber\":\"A1.1\",\"itemNumber\":\"ITEM-1\"," +
            "\"deliveryDate\":\"2026-01-02T00:00:00\",\"validToDate\":\"2026-12-30T00:00:00\"," +
            "\"terminationDate\":\"2026-12-31T00:00:00\",\"quantity\":3.5," +
            "\"agreementLineType\":\"Rental\",\"warehouse\":\"ED1\",\"status\":\"Active\"," +
            "\"division\":\"UK\",\"packageGroupNumber\":\"PKG-1\",\"attributes\":\"Power=50kVA\"," +
            "\"localizedAttributes\":\"Power=50 kVA\",\"validFromDate\":\"2026-01-01T00:00:00\"," +
            "\"changeSequence\":987654321012,\"genericItemNumber\":\"GEN-1\",\"isDeleted\":true," +
            "\"fulfilmentStatus\":2,\"quantityFulfilled\":1.25," +
            "\"lastUpdatedBy\":\"line.planner@example.com\",\"lastUpdatedDate\":\"2026-01-05T06:07:08\"," +
            "\"agreementLineIndex\":4,\"facility\":\"FAC1\",\"orderLineIndex\":5," +
            "\"quoteLineIndex\":6,\"quoteLineNumber\":\"QL-1\",\"orderSource\":\"NOF\"," +
            "\"agreementNumbersOnly\":\"100\",\"quotePublicId\":\"Q-PUBLIC-1\"," +
            "\"quotePublicIdNumbersOnly\":\"200\",\"numberOfShifts\":\"2\"," +
            "\"activationErrors\":\"Line warning\",\"rateType\":\"Daily\"," +
            "\"collectionDate\":\"2027-01-02T00:00:00\"," +
            "\"descriptionWithAttributes\":\"Generator, 50 kVA\",\"activationStatus\":1," +
            "\"activationInstanceId\":\"line-instance\",\"itemDescription\":\"Diesel generator\"," +
            "\"requiresFulfilment\":true}]}");

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name).Should().Equal("header", "lines");
        var headerJson = document.RootElement.GetProperty("header");
        var lineJson = document.RootElement.GetProperty("lines")[0];
        headerJson.EnumerateObject().Should().HaveCount(36);
        lineJson.EnumerateObject().Should().HaveCount(43);
        foreach (var graphProperty in new[] { "currentChangeOrder", "changeOrderHeaders", "changeOrders", "lines" })
        {
            headerJson.TryGetProperty(graphProperty, out _).Should().BeFalse();
        }

        foreach (var graphProperty in new[] { "header", "changeOrderLines" })
        {
            lineJson.TryGetProperty(graphProperty, out _).Should().BeFalse();
        }
    }

    [Fact]
    public void GetAgreement_SerializesNullAndDefaultScalarContractForSuperAdmin()
    {
        SetIdentity(null, isSuperAdmin: true);
        var header = new Header
        {
            Division = null!,
            OrderSource = null!,
            Facility = null!,
        };
        var lines = new[]
        {
            new Line
            {
                Warehouse = null!,
                Division = null!,
                Facility = null!,
                OrderSource = null!,
            },
        };
        _repository.Setup(repository => repository.GetHeader(42)).Returns(header);
        _repository.Setup(repository => repository.GetLines(42)).Returns(lines.AsQueryable());

        var response = GetOkDetail(CreateSubject().GetAgreement(42));

        response.Header.IsActivated.Should().BeFalse();
        response.Lines.Single().IsSubline.Should().BeFalse();
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Be(
            "{\"header\":{\"isActivated\":false,\"id\":0,\"quotePublicId\":null," +
            "\"agreementNumber\":null,\"onHireDate\":null,\"offHireDate\":null,\"status\":null," +
            "\"customerName\":null,\"customerAddress\":null,\"customerNumber\":null,\"division\":null," +
            "\"customerAddressCode\":null,\"orderSource\":null,\"changeSequence\":0,\"isDeleted\":false," +
            "\"fulfilmentStatus\":0,\"lastUpdatedBy\":null,\"lastUpdatedDate\":null,\"facility\":null," +
            "\"opportunityNumber\":null,\"orderNumber\":null,\"quoteNumber\":null," +
            "\"agreementNumbersOnly\":null,\"quotePublicIdNumbersOnly\":null,\"overviewOfService\":null," +
            "\"probability\":null,\"armcontactName\":null,\"armcontactEmail\":null,\"armcontactPhone\":null," +
            "\"activationStatus\":0,\"activationErrors\":null,\"activationInstanceId\":null," +
            "\"rentalDepot\":null,\"opportunityName\":null,\"opportunityStage\":null,\"isSkeleton\":null}," +
            "\"lines\":[{\"isSubline\":false,\"id\":0,\"headerId\":null,\"orderLineNumber\":null," +
            "\"agreementLineNumber\":null,\"itemNumber\":null,\"deliveryDate\":null," +
            "\"validToDate\":\"0001-01-01T00:00:00\",\"terminationDate\":null,\"quantity\":0," +
            "\"agreementLineType\":null,\"warehouse\":null,\"status\":null,\"division\":null," +
            "\"packageGroupNumber\":null,\"attributes\":null,\"localizedAttributes\":null," +
            "\"validFromDate\":\"0001-01-01T00:00:00\",\"changeSequence\":0,\"genericItemNumber\":null," +
            "\"isDeleted\":false,\"fulfilmentStatus\":0,\"quantityFulfilled\":0," +
            "\"lastUpdatedBy\":null,\"lastUpdatedDate\":null,\"agreementLineIndex\":null,\"facility\":null," +
            "\"orderLineIndex\":null,\"quoteLineIndex\":null,\"quoteLineNumber\":null,\"orderSource\":null," +
            "\"agreementNumbersOnly\":null,\"quotePublicId\":null,\"quotePublicIdNumbersOnly\":null," +
            "\"numberOfShifts\":null,\"activationErrors\":null,\"rateType\":null,\"collectionDate\":null," +
            "\"descriptionWithAttributes\":null,\"activationStatus\":0,\"activationInstanceId\":null," +
            "\"itemDescription\":null,\"requiresFulfilment\":false}]}");
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("header").EnumerateObject().Should().HaveCount(36);
        document.RootElement.GetProperty("lines")[0].EnumerateObject().Should().HaveCount(43);
        _repository.Verify(repository => repository.GetHeader(42), Times.Once);
        _repository.Verify(repository => repository.GetLines(42), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    private AgreementsController CreateSubject() => new(_repository.Object, _identity.Object);

    private void SetIdentity(string? division, bool isSuperAdmin = false)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(new User
        {
            LoginName = "test.user@example.com",
            FullName = "Test User",
            Division = division!,
            IsSuperAdmin = isSuperAdmin,
            DateFormat = "dd/MM/yyyy",
        });
    }

    private void SetAgreements(params VwHeader[] agreements)
    {
        _repository.Setup(repository => repository.GetAgreements(It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(agreements.AsQueryable());
    }

    private static VwHeader CreateAgreement(int id, string? division, DateTime? onHireDate = null) => new()
    {
        Id = id,
        Division = division!,
        OrderSource = "NOF",
        OnHireDate = onHireDate,
    };

    private static Header CreateHeader(string division) => new()
    {
        Id = 42,
        Division = division,
        OrderSource = "NOF",
        Facility = "FAC1",
    };

    private static Header CreateFullyPopulatedHeader() => new()
    {
        Id = 42,
        QuotePublicId = "Q-PUBLIC-1",
        AgreementNumber = "A-100",
        OnHireDate = new DateTime(2026, 1, 1),
        OffHireDate = new DateTime(2026, 12, 31),
        Status = "Active",
        CustomerName = "Customer Ltd",
        CustomerAddress = "1 Test Street",
        CustomerNumber = "CUST-1",
        Division = "UK",
        CustomerAddressCode = "ADDR-1",
        OrderSource = "NOF",
        ChangeSequence = 1234567890123,
        IsDeleted = true,
        FulfilmentStatus = 2,
        LastUpdatedBy = "planner@example.com",
        LastUpdatedDate = new DateTime(2026, 1, 4, 5, 6, 7),
        Facility = "FAC1",
        OpportunityNumber = "OPP-1",
        OrderNumber = "ORD-1",
        QuoteNumber = "QUOTE-1",
        AgreementNumbersOnly = "100",
        QuotePublicIdNumbersOnly = "200",
        OverviewOfService = "Generator hire",
        Probability = 0.75,
        ArmcontactName = "Alex Contact",
        ArmcontactEmail = "alex@example.com",
        ArmcontactPhone = "01234",
        ActivationStatus = 2,
        ActivationErrors = "Header warning",
        ActivationInstanceId = "header-instance",
        RentalDepot = "ED1",
        OpportunityName = "Opportunity 1",
        OpportunityStage = "Negotiation",
        IsSkeleton = true,
    };

    private static Line CreateFullyPopulatedLine() => new()
    {
        Id = 7,
        HeaderId = 42,
        OrderLineNumber = "O1",
        AgreementLineNumber = "A1.1",
        ItemNumber = "ITEM-1",
        DeliveryDate = new DateTime(2026, 1, 2),
        ValidToDate = new DateTime(2026, 12, 30),
        TerminationDate = new DateTime(2026, 12, 31),
        Quantity = 3.5f,
        AgreementLineType = "Rental",
        Warehouse = "ED1",
        Status = "Active",
        Division = "UK",
        PackageGroupNumber = "PKG-1",
        Attributes = "Power=50kVA",
        LocalizedAttributes = "Power=50 kVA",
        ValidFromDate = new DateTime(2026, 1, 1),
        ChangeSequence = 987654321012,
        GenericItemNumber = "GEN-1",
        IsDeleted = true,
        FulfilmentStatus = 2,
        QuantityFulfilled = 1.25,
        LastUpdatedBy = "line.planner@example.com",
        LastUpdatedDate = new DateTime(2026, 1, 5, 6, 7, 8),
        AgreementLineIndex = 4,
        Facility = "FAC1",
        OrderLineIndex = 5,
        QuoteLineIndex = 6,
        QuoteLineNumber = "QL-1",
        OrderSource = "NOF",
        AgreementNumbersOnly = "100",
        QuotePublicId = "Q-PUBLIC-1",
        QuotePublicIdNumbersOnly = "200",
        NumberOfShifts = "2",
        ActivationErrors = "Line warning",
        RateType = "Daily",
        CollectionDate = new DateTime(2027, 1, 2),
        DescriptionWithAttributes = "Generator, 50 kVA",
        ActivationStatus = 1,
        ActivationInstanceId = "line-instance",
        ItemDescription = "Diesel generator",
        RequiresFulfilment = true,
    };

    private static Line CreateLine(int id) => new()
    {
        Id = id,
        HeaderId = 42,
        Division = "UK",
        Facility = "FAC1",
        Warehouse = "ED1",
        OrderSource = "NOF",
    };

    private static AgreementDetailResponse GetOkDetail(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AgreementDetailResponse>().Subject;
    }

    private static List<AgreementListItemResponse> GetOkAgreements(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<List<AgreementListItemResponse>>().Subject;
    }
}
