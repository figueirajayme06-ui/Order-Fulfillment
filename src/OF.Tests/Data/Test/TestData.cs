using OF.Data.Database;
using OF.Data;
using static OF.Common.Enums;

namespace OF.Tests.Data.Test
{
    internal class TestData
    {
        public static class ActivateHeader
        {
            public static Header IgnoreIfAlreadyActivatedByANumber(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                return header;
            }
            public static Header IgnoreIfAlreadyActivatedByStatus(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Activated
                };

                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                return header;
            }

            public static Header IgnoreIfPartiallyFulfilleds(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.TODO,
                    FulfilmentStatus = (int)FulfilmentStatus.PartiallyFulfilled
                };

                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                return header;
            }

            public static Header IgnoreIfNotFulfilleds(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.TODO,
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                return header;
            }

            public static Header IgnoreIfWithFailedLines(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Requested,
                    FulfilmentStatus = (int)FulfilmentStatus.OverFulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.Requested,
                        FulfilmentStatus = (int)FulfilmentStatus.OverFulfilled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = "Failed",
                        ActivationStatus = (int)ActivationStatus.Failed,
                        FulfilmentStatus = (int)FulfilmentStatus.OverFulfilled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header ActivateWhenAllGoodWithExcludedItem(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Requested,
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.Activated,
                        FulfilmentStatus = (int)FulfilmentStatus.OverFulfilled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = "Failed",
                        ActivationStatus = (int)ActivationStatus.Activated,
                        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = false,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-3",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0126GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        IsDeleted = false,
                        ActivationErrors = "Failed",
                        ActivationStatus = (int)ActivationStatus.TODO,
                        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header ActivateWhenAllGoodWithDeletedItem(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Requested,
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.Activated,
                        FulfilmentStatus = (int)FulfilmentStatus.OverFulfilled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = "Failed",
                        ActivationStatus = (int)ActivationStatus.Activated,
                        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-3",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        IsDeleted = true,
                        ActivationErrors = "Failed",
                        ActivationStatus = (int)ActivationStatus.TODO,
                        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }


            public static Header ActivateWhenAllGood(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Requested,
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.Activated,
                        FulfilmentStatus = (int)FulfilmentStatus.OverFulfilled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = "Failed",
                        ActivationStatus = (int)ActivationStatus.Activated,
                        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header RetryIfWithLinesStillGoing(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Requested,
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.Requested,
                        FulfilmentStatus = (int)FulfilmentStatus.OverFulfilled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = "Failed",
                        ActivationStatus = (int)ActivationStatus.Activated,
                        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header IgnoreIfWithTodoLines(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Requested,
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.Requested,
                        FulfilmentStatus = (int)FulfilmentStatus.OverFulfilled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = "Failed",
                        ActivationStatus = (int)ActivationStatus.TODO,
                        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }
        }

        public static class Activation
        {
            public static Header MiscLines(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    QuotePublicId = "Q-502928",
                    AgreementNumber = "T719875",
                    OnHireDate = DateTime.Parse("2024-07-03 00:00:00.0000000"),
                    OffHireDate = DateTime.Parse("2024-08-07 00:00:00.0000000"),
                    Status = "05",
                    CustomerName = "Bernd Group Inc, The",
                    CustomerAddress = "400 Main street, East Hartford, CT 06108-0968",
                    CustomerNumber = "US00076878",
                    Division = "200",
                    CustomerAddressCode = "900002",
                    OrderSource = "ORF",
                    ChangeSequence = 1721219839492,
                    IsDeleted = false,
                    FulfilmentStatus = 3,
                    LastUpdatedBy = "robert.wyroski@aggreko.com",
                    LastUpdatedDate = DateTime.Parse("2024-07-17 18:29:36.5186597"),
                    Facility = "USN",
                    OpportunityNumber = "006080000151IwDAAU",
                    OrderNumber = "8010800000SahEyAAJ",
                    QuoteNumber = "a260800000CEoOhAAL",
                    AgreementNumbersOnly = "719875",
                    QuotePublicIdNumbersOnly = "502928",
                    OverviewOfService = @"...",
                    Probability = 100,
                    ArmcontactName = "James Samonsky",
                    ArmcontactEmail = "james.samonsky@prattwhitney.com",
                    ArmcontactPhone = null,
                    ActivationStatus = 1,
                    ActivationErrors = "20240717190627: Fail: Error in requesting activation of Header '4065', Sequence contains no matching element;",
                    ActivationInstanceId = null
                };

                var lines = new List<Line>
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfNEAA0",
                        AgreementLineNumber = "T719875-14",
                        ItemNumber = "XGCT1000G_NA",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 1,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = null,
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239126454,
                        GenericItemNumber = "XGCT1000G_NA",
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 1,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 07, 18, 515),
                        AgreementLineIndex = 14,
                        Facility = "USN",
                        OrderLineIndex = 2,
                        QuoteLineIndex = 2,
                        QuoteLineNumber = "a220800000CIN2nAAH",
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = "Cooling Tower 1000 ton - GT40",
                        ActivationStatus = 3,
                        ActivationInstanceId = "a0cc7d92-23ba-41c5-b311-34903c1112bb"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfNFAA0",
                        AgreementLineNumber = "T719875-21",
                        ItemNumber = "XGHS0008TEE",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 2,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = "Connection Type:Flanged",
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239127171,
                        GenericItemNumber = "XGHS0008TEE",
                        IsDeleted = true,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 2,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 27, 30, 394),
                        AgreementLineIndex = 21,
                        Facility = "USN",
                        OrderLineIndex = 15,
                        QuoteLineIndex = 15,
                        QuoteLineNumber = "a220800000CIN2oAAH",
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = "20240717190305: Fail[Delete]: (Line: 35445) -> NotFound:{\"errorMessage\":\"Agreement line [T719875-21] does not exist.\"};",
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = "Hose 8 in (200 mm) Tee<br>Connection Type: Flanged",
                        ActivationStatus = 1,
                        ActivationInstanceId = null
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfN4AAK",
                        AgreementLineNumber = "T719875-17",
                        ItemNumber = "XGCBTFM_NA",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 4,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = "Category:Tail, Female, Multi Conductor;Additional information:Cable Tail, Female, Multi Conductor",
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239145110,
                        GenericItemNumber = "XGCBTFM_NA",
                        IsDeleted = true,
                        FulfilmentStatus = 0,
                        QuantityFulfilled = 0,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 15, 07, 562),
                        AgreementLineIndex = 17,
                        Facility = "USN",
                        OrderLineIndex = 5,
                        QuoteLineIndex = 5,
                        QuoteLineNumber = "a220800000CIN2dAAH",
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = "20240717190310: Fail[Delete]: (Line: 35446) -> NotFound:{\"errorMessage\":\"Agreement line [T719875-17] does not exist.\"};",
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = "Cable Tail, Female, Multi Conductor",
                        ActivationStatus = 1,
                        ActivationInstanceId = null
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfNGAA0",
                        AgreementLineNumber = "T719875-22",
                        ItemNumber = "XGHS0010RED",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 2,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = "Connection Type:Flanged",
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239138431,
                        GenericItemNumber = "XGHS0010RED",
                        IsDeleted = true,
                        FulfilmentStatus = 0,
                        QuantityFulfilled = 0,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 27, 33, 476),
                        AgreementLineIndex = 22,
                        Facility = "USN",
                        OrderLineIndex = 16,
                        QuoteLineIndex = 16,
                        QuoteLineNumber = "a220800000CIN2pAAH",
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = "20240717190316: Fail[Delete]: (Line: 35447) -> NotFound:{\"errorMessage\":\"Agreement line [T719875-22] does not exist.\"};",
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = "Hose 10 in (250 mm) Reducer<br>Connection Type: Flanged",
                        ActivationStatus = 1,
                        ActivationInstanceId = null
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfN0AAK",
                        AgreementLineNumber = "T719875-13",
                        ItemNumber = "XGCT1000TR_NA",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 1,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = "Quantity (Pieces):1;Additional information:Rapid Deploy Cooling Tower - GT40",
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239133940,
                        GenericItemNumber = "XGCT1000TR_NA",
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 1,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 28, 31, 750),
                        AgreementLineIndex = 13,
                        Facility = "USN",
                        OrderLineIndex = 1,
                        QuoteLineIndex = 1,
                        QuoteLineNumber = "a220800000CIN2ZAAX",
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = "Rapid Deploy Cooling Tower - GT40<br>",
                        ActivationStatus = 3,
                        ActivationInstanceId = "b7be4648-2303-45d4-b060-22735641a537"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfN2AAK",
                        AgreementLineNumber = "T719875-15",
                        ItemNumber = "XGHS0006T15",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 12,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = "Connection Type:Flanged;Length (ft):25;Additional information:Hose 6 in (150 mm) Standard, 150psi",
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239154819,
                        GenericItemNumber = "XGHS0006T15",
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 16,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 05, 13, 388),
                        AgreementLineIndex = 15,
                        Facility = "USN",
                        OrderLineIndex = 3,
                        QuoteLineIndex = 3,
                        QuoteLineNumber = "a220800000CIN2bAAH",
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = "Hose 6 in (150 mm) Standard, 150psi<br>Connection Type: Flanged; Length (ft): 25",
                        ActivationStatus = 3,
                        ActivationInstanceId = "fd4fa036-3658-47f0-8344-794b883a7766"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfN3AAK",
                        AgreementLineNumber = "T719875-16",
                        ItemNumber = "XGCB04/0_NA",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 8,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = "Calculated Amperage:405;Length (ft):25;Cable Connection Type:Cam-Lok;Additional information:25 Feet x Cam-Lok Cable 4/0 AWG",
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239129483,
                        GenericItemNumber = "XGCB04/0_NA",
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 8,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 15, 44, 362),
                        AgreementLineIndex = 16,
                        Facility = "USN",
                        OrderLineIndex = 4,
                        QuoteLineIndex = 4,
                        QuoteLineNumber = "a220800000CIN2cAAH",
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = "25 Feet x Cam-Lok Cable 4/0 AWG",
                        ActivationStatus = 3,
                        ActivationInstanceId = "dc785693-9f0f-4172-a16d-88cf98df323c"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfN2AAK",
                        AgreementLineNumber = "T719875-27",
                        ItemNumber = "HS0006F15025FT",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 10,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = null,
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721220032375,
                        GenericItemNumber = null,
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 10,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 04, 27, 862),
                        AgreementLineIndex = 15,
                        Facility = "USN",
                        OrderLineIndex = 3,
                        QuoteLineIndex = 3,
                        QuoteLineNumber = null,
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = null,
                        ActivationStatus = 3,
                        ActivationInstanceId = "86c0b96b-66a9-4e2f-9b94-7d0366de263c"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfN3AAK",
                        AgreementLineNumber = "T719875-28",
                        ItemNumber = "CB04/0TLF008FT",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 4,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = null,
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721220040664,
                        GenericItemNumber = null,
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 4,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 14, 41, 753),
                        AgreementLineIndex = 16,
                        Facility = "USN",
                        OrderLineIndex = 4,
                        QuoteLineIndex = 4,
                        QuoteLineNumber = null,
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = null,
                        ActivationStatus = 3,
                        ActivationInstanceId = "1ef3b764-da0a-4d73-b282-11b90b7822bc"
                    },
                    new Line
                    {
                        RequiresFulfilment = false,
                        OrderLineNumber = "8020800000nEfN2AAK",
                        AgreementLineNumber = "T719875-15.2",
                        ItemNumber = "XX56",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 2,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = null,
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239154819,
                        GenericItemNumber = null,
                        IsDeleted = false,
                        FulfilmentStatus = 0,
                        QuantityFulfilled = 0,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 21, 56, 996),
                        AgreementLineIndex = 15,
                        Facility = "USN",
                        OrderLineIndex = 3,
                        QuoteLineIndex = 3,
                        QuoteLineNumber = null,
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = null,
                        ActivationStatus = 2,
                        ActivationInstanceId = null
                    },
                    new Line
                    {
                        RequiresFulfilment =  true,
                        OrderLineNumber = "8020800000nEfN2AAK",
                        AgreementLineNumber = "T719875-15.3",
                        ItemNumber = "XXMISCHS",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 40,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = null,
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239154819,
                        GenericItemNumber = null,
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 40,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 26, 01, 765),
                        AgreementLineIndex = 15,
                        Facility = "USN",
                        OrderLineIndex = 3,
                        QuoteLineIndex = 3,
                        QuoteLineNumber = null,
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = null,
                        ActivationStatus = 2,
                        ActivationInstanceId = null
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfN2AAK",
                        AgreementLineNumber = "T719875-15.4",
                        ItemNumber = "XXMISCHS",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 2,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = null,
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239154819,
                        GenericItemNumber = null,
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 2,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 29, 26, 873),
                        AgreementLineIndex = 15,
                        Facility = "USN",
                        OrderLineIndex = 3,
                        QuoteLineIndex = 3,
                        QuoteLineNumber = null,
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = null,
                        ActivationStatus = 2,
                        ActivationInstanceId = null
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        OrderLineNumber = "8020800000nEfN2AAK",
                        AgreementLineNumber = "T719875-15.5",
                        ItemNumber = "XXMISCHS",
                        DeliveryDate = new DateTime(2024, 07, 19),
                        ValidToDate = new DateTime(2024, 08, 07),
                        TerminationDate = null,
                        Quantity = 40,
                        AgreementLineType = "5",
                        Warehouse = "BQ0",
                        Status = "05",
                        Division = "200",
                        PackageGroupNumber = "1",
                        Attributes = null,
                        ValidFromDate = new DateTime(2024, 07, 19),
                        ChangeSequence = 1721239154819,
                        GenericItemNumber = null,
                        IsDeleted = false,
                        FulfilmentStatus = 3,
                        QuantityFulfilled = 40,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = new DateTime(2024, 07, 17, 18, 29, 36, 495),
                        AgreementLineIndex = 15,
                        Facility = "USN",
                        OrderLineIndex = 3,
                        QuoteLineIndex = 3,
                        QuoteLineNumber = null,
                        OrderSource = "ORF",
                        AgreementNumbersOnly = "719875",
                        QuotePublicId = "Q-502928",
                        QuotePublicIdNumbersOnly = "502928",
                        NumberOfShifts = "1",
                        ActivationErrors = null,
                        RateType = "4",
                        CollectionDate = new DateTime(2024, 08, 07),
                        DescriptionWithAttributes = null,
                        ActivationStatus = 2,
                        ActivationInstanceId = null
                    }
                };

                dbContext.Lines.AddRange(lines);

                foreach (var line in lines)
                {
                    header.Lines.Add(line);
                }

                dbContext.Headers.Add(header);

                dbContext.SaveChanges();

                var reservations = new List<Reservation>
                {
                    new Reservation
                    {
                        AssetId = "YCHZ038100-0",
                        Notes = null,
                        ItemNumber = "CT1000MDRGCT_NA",
                        Quantity = 1,
                        Warehouse = "DM0",
                        LineId = lines[0].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 17:57:22.7265719"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 1,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    },
                    new Reservation
                    {
                        AssetId = "T5449",
                        Notes = null,
                        ItemNumber = "TR5300FDLAX2",
                        Quantity = 1,
                        Warehouse = "DM0",
                        LineId = lines[4].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 18:00:41.5732738"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 1,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    },
                    new Reservation
                    {
                        AssetId = "HS0006F15025FT",
                        Notes = null,
                        ItemNumber = "HS0006F15025FT",
                        Quantity = 16,
                        Warehouse = "BQ0",
                        LineId = lines[5].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 18:02:55.2758147"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 12,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    },
                    new Reservation
                    {
                        AssetId = "HS0006F15025FT",
                        Notes = null,
                        ItemNumber = "HS0006F15025FT",
                        Quantity = 10,
                        Warehouse = "DM0",
                        LineId = lines[7].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 18:04:27.8480532"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 10,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    },
                    new Reservation
                    {
                        AssetId = "CB04/0CNN050FT",
                        Notes = null,
                        ItemNumber = "CB04/0CNN050FT",
                        Quantity = 8,
                        Warehouse = "BQ0",
                        LineId = lines[6].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 18:08:44.0945206"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 8,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    },
                    new Reservation
                    {
                        AssetId = "CB04/0TLF008FT",
                        Notes = null,
                        ItemNumber = "CB04/0TLF008FT",
                        Quantity = 4,
                        Warehouse = "BQ0",
                        LineId = lines[8].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 18:14:41.7345487"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 4,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    },
                    new Reservation
                    {
                        AssetId = "XXMISCHS",
                        Notes = null,
                        ItemNumber = "XXMISCHS",
                        Quantity = 40,
                        Warehouse = "BQ0",
                        LineId = lines[10].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 18:26:01.7510644"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 40,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    },
                    new Reservation
                    {
                        AssetId = "XXMISCHS",
                        Notes = null,
                        ItemNumber = "XXMISCHS",
                        Quantity = 2,
                        Warehouse = "BQ0",
                        LineId = lines[11].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 18:29:26.8567683"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 2,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    },
                    new Reservation
                    {
                        AssetId = "XXMISCHS",
                        Notes = null,
                        ItemNumber = "XXMISCHS",
                        Quantity = 40,
                        Warehouse = "BQ0",
                        LineId = lines[12].Id,
                        LastUpdatedBy = "robert.wyroski@aggreko.com",
                        LastUpdatedDate = DateTime.Parse("2024-07-17 18:29:36.4773305"),
                        IsDepotFulfilled = false,
                        IsRehire = false,
                        EffectiveQuantity = 40,
                        IsConfirmed = false,
                        ActualAssetId = null,
                        ActualItemNumber = null,
                        ActualQuantity = null
                    }
                };

                dbContext.Reservations.AddRange(reservations);

                dbContext.SaveChanges();

                return header;
            }

            public static Header TwoLinesMultipleWarehouseReservationAndSingleReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.TODO,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        OrderLineNumber = "8027a000007OxW7AAK",
                        OrderLineIndex = 1
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.TODO,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447",
                        QuoteLineNumber = "8017a000003GAesAAG",
                        OrderLineNumber = "8027a000007OxW8AAK",
                        OrderLineIndex = 2
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
            {
                new Reservation()
                {
                    AssetId = "CB04/4BAE025FT",
                    ItemNumber = "CB04/4BAE025FT",
                    Quantity = 2,
                    Warehouse = "BD0",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                },
                new Reservation()
                {
                    AssetId = "XAPP007",
                    ItemNumber = "GN0125GHPCAN",
                    Quantity = 1,
                    Warehouse = "BD0",
                    EffectiveQuantity = 1,
                    LineId = lines[1].Id
                },
                new Reservation()
                {
                    AssetId = "CB04/4BAE050FT",
                    ItemNumber = "CB04/4BAE050FT",
                    Quantity = 3,
                    Warehouse = "ED1",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                },
                new Reservation()
                {
                    AssetId = "CB04/4BAE075FT",
                    ItemNumber = "CB04/4BAE075FT",
                    Quantity = 4,
                    Warehouse = "ED1",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                }
            };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header TwoLinesMultipleWarehouseReservationAndSingleReservationWithQuantityGt1(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 9,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.TODO,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 1,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.TODO,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
            {
                new Reservation()
                {
                    AssetId = "CB04/4BAE025FT",
                    ItemNumber = "CB04/4BAE025FT",
                    Quantity = 2,
                    Warehouse = "BD0",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                },
                new Reservation()
                {
                    AssetId = "XAPP007",
                    ItemNumber = "GN0125GHPCAN",
                    Quantity = 1,
                    Warehouse = "BD0",
                    EffectiveQuantity = 1,
                    LineId = lines[1].Id
                },
                new Reservation()
                {
                    AssetId = "CB04/4BAE050FT",
                    ItemNumber = "CB04/4BAE050FT",
                    Quantity = 3,
                    Warehouse = "ED1",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                },
                new Reservation()
                {
                    AssetId = "CB04/4BAE075FT",
                    ItemNumber = "CB04/4BAE075FT",
                    Quantity = 4,
                    Warehouse = "ED1",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                }
            };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header TwoLinesMultipleWarehouseReservationAndSingleReservationWithQuantityGt1AndDeletedSubline(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 9,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.TODO,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGGN0125",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        QuantityFulfilled = 1,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.TODO,
                        ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1.1",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        GenericItemNumber = "XGCB04/0_NA",
                        ItemNumber = "XGCB04/0_NA",
                        Quantity = 9,
                        QuantityFulfilled = 9,
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        Header = header,
                        ActivationErrors = null,
                        ActivationStatus = (int)ActivationStatus.TODO,
                        ActivationInstanceId = "1427f9b0-6489-4ea8-b4fc-445cb9390447",
                        IsDeleted = true
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
            {
                new Reservation()
                {
                    AssetId = "CB04/4BAE025FT",
                    ItemNumber = "CB04/4BAE025FT",
                    Quantity = 2,
                    Warehouse = "BD0",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                },
                new Reservation()
                {
                    AssetId = "XAPP007",
                    ItemNumber = "GN0125GHPCAN",
                    Quantity = 1,
                    Warehouse = "BD0",
                    EffectiveQuantity = 1,
                    LineId = lines[1].Id
                },
                new Reservation()
                {
                    AssetId = "CB04/4BAE050FT",
                    ItemNumber = "CB04/4BAE050FT",
                    Quantity = 3,
                    Warehouse = "ED1",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                },
                new Reservation()
                {
                    AssetId = "CB04/4BAE075FT",
                    ItemNumber = "CB04/4BAE075FT",
                    Quantity = 4,
                    Warehouse = "ED1",
                    EffectiveQuantity = 1,
                    LineId = lines[0].Id
                }
            };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header TwoLinesSingleReservationOneDeleted(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-1",
                        AgreementNumbersOnly = "712628",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        Quantity = 1,
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension"
                    },

                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "T712628-2",
                        AgreementNumbersOnly = "712628",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        Quantity = 1,
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        IsDeleted = true
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "CB04/4BAE025FT",
                        ItemNumber = "CB04/4BAE025FT",
                        Quantity = 1,
                        Warehouse = "BD1",
                        EffectiveQuantity = 1,
                        LineId = lines[0].Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            /// <summary>
            /// Represents the retry scenario: an activation previously failed in M3 (e.g. customer on block),
            /// the user subsequently deleted one of the lines from the agreement, and is now retrying.
            /// Line 1 is active with a reservation and should be processed normally.
            /// Line 2 is deleted AND has Failed status, meaning it was never created in M3 during the first
            /// attempt. Calling DeleteLine for it in M3 would fail because the line does not exist there.
            /// </summary>
            public static Header TwoLinesSingleReservationOneDeletedAfterFailedActivation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                    ActivationStatus = (int)ActivationStatus.Failed,
                    ActivationErrors = "20240101120000: Fail[LineAcknowledge]: Customer is on block in M3;",
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 1,
                        AgreementLineNumber = "T712628-1",
                        AgreementNumbersOnly = "712628",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        Quantity = 1,
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        IsDeleted = false,
                        ActivationStatus = (int)ActivationStatus.Failed,
                        ActivationErrors = "20240101120000: Fail[LineAcknowledge]: Customer is on block in M3;",
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 2,
                        AgreementLineNumber = "T712628-2",
                        AgreementNumbersOnly = "712628",
                        AgreementLineType = "5",
                        DeliveryDate = DateTime.Now,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        NumberOfShifts = "1",
                        Quantity = 1,
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        IsDeleted = true,
                        ActivationStatus = (int)ActivationStatus.Failed,
                        ActivationErrors = "20240101120000: Fail[LineAcknowledge]: Customer is on block in M3;",
                    }
                };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                // Only Line 1 has a reservation - Line 2's reservation was removed when the user deleted it
                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "CB04/4BAE025FT",
                        ItemNumber = "CB04/4BAE025FT",
                        Quantity = 1,
                        Warehouse = "BD1",
                        EffectiveQuantity = 1,
                        LineId = lines[0].Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header SingleLineSingleRehireReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 5,
                    AgreementLineNumber = "T712628-1",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                };

                dbContext.Lines.Add(line);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "XHAH",
                        ItemNumber = "XHAH",
                        Quantity = 1,
                        Warehouse = "BD1",
                        EffectiveQuantity = 1,
                        LineId = line.Id,
                        IsRehire = true,
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header SingleLineSingleReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 5,
                    AgreementLineNumber = "T712628-1",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                };

                dbContext.Lines.Add(line);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "CB04/4BAE025FT",
                        ItemNumber = "CB04/4BAE025FT",
                        Quantity = 1,
                        Warehouse = "BD1",
                        EffectiveQuantity = 1,
                        LineId = line.Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header ActivatedHeaderByNumberSingleLineSingleReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 5,
                    AgreementLineNumber = "T712628-1",
                    AgreementNumbersOnly = "712628",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Quantity = 1,
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                };

                dbContext.Lines.Add(line);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "CB04/4BAE025FT",
                        ItemNumber = "CB04/4BAE025FT",
                        Quantity = 1,
                        Warehouse = "BD1",
                        EffectiveQuantity = 1,
                        LineId = line.Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header ActivatedHeaderByStatusSingleLineSingleReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Activated
                };

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 5,
                    AgreementLineNumber = "T712628-1",
                    AgreementNumbersOnly = "712628",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Quantity = 1,
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                };

                dbContext.Lines.Add(line);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "CB04/4BAE025FT",
                        ItemNumber = "CB04/4BAE025FT",
                        Quantity = 1,
                        Warehouse = "BD1",
                        EffectiveQuantity = 1,
                        LineId = line.Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header ActivatedHeaderByStatusSingleLineWithExcludedLineSingleReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Activated
                };

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 5,
                    AgreementLineNumber = "T712628-1",
                    AgreementNumbersOnly = "712628",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Quantity = 1,
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                };

                var quoteLine = new Line
                {
                    RequiresFulfilment = false,
                    AgreementLineIndex = 4,
                    AgreementLineNumber = "T712628-2",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Quantity = 1,
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "1427f9b0-6489-4ea8-b4fc-445cb9390447"
                };


                dbContext.Lines.Add(line);
                dbContext.Lines.Add(quoteLine);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "CB04/4BAE025FT",
                        ItemNumber = "CB04/4BAE025FT",
                        Quantity = 1,
                        Warehouse = "BD1",
                        EffectiveQuantity = 1,
                        LineId = line.Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header ActivatedHeaderByStatusSingleLineWithQuoteLineSingleReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    AgreementNumbersOnly = "712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Activated
                };

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 5,
                    AgreementLineNumber = "T712628-1",
                    AgreementNumbersOnly = "712628",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Quantity = 1,
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                };

                var quoteLine = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 4,
                    AgreementLineNumber = "Q712628-1",
                    AgreementLineType = "-1",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Quantity = 1,
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "1427f9b0-6489-4ea8-b4fc-445cb9390447"
                };


                dbContext.Lines.Add(line);
                dbContext.Lines.Add(quoteLine);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "CB04/4BAE025FT",
                        ItemNumber = "CB04/4BAE025FT",
                        Quantity = 1,
                        Warehouse = "BD1",
                        EffectiveQuantity = 1,
                        LineId = line.Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }
        }

        public static class Agreements
        {
            public static Header SetupHeaderWithItemAndDeletedItem(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900009",
                    CustomerAddress = "Fake place"
                };

                dbContext.Headers.Add(header);

                dbContext.SaveChanges();

                return header;
            }

            public static Header HeaderNotExistsWithFullyFulfilledLinesGoesFulfilled(ApplicationDbContext dbContext)
            {
                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 5,
                    AgreementLineNumber = "A712565-1",
                    AgreementNumbersOnly = "712565",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Quantity = 1,
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };
                var line2 = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 5,
                    AgreementLineNumber = "A712565-2",
                    AgreementNumbersOnly = "712565",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Quantity = 1,
                    NumberOfShifts = "1",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    ActivationErrors = null,
                    ActivationStatus = (int)ActivationStatus.TODO,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Lines.Add(line);
                dbContext.Lines.Add(line2);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "CB04/4BAE025FT",
                            ItemNumber = "CB04/4BAE025FT",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = line.Id,
                            IsConfirmed = true
                        },
                        new Reservation()
                        {
                            AssetId = "CB04/4BAE025FT",
                            ItemNumber = "CB04/4BAE025FT",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = line2.Id,
                            IsConfirmed = true
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return new Header();
            }
        }

        public static class AgreementLines
        {

            public static Header View_Headers_Line_Counts_With_Excluded_Lines(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "A712844-1",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712844-2",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },

                        new Line
                        {
                            RequiresFulfilment = false,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712844-3",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                var header2 = new Header
                {
                    AgreementNumber = "T712843",
                    AgreementNumbersOnly = "712843",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.PartiallyFulfilled
                };

                dbContext.Headers.Add(header2);

                var lines2 = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "T712843-1",
                            AgreementNumbersOnly = "712843",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        },
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "T712843-2",
                            AgreementNumbersOnly = "712843",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "T712843-4",
                            AgreementNumbersOnly = "712843",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },

                        new Line
                        {
                            RequiresFulfilment = false,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712843-3",
                            AgreementNumbersOnly = "712843",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines2)
                {
                    dbContext.Lines.Add(line);
                    header2.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header PartiallyFulfilledHeader_All_Lines_Reserved_For_Calculating_Status_With_Excluded(ApplicationDbContext dbContext, int quantity)
            {
                var header = new Header
                {
                    AgreementNumber = "A712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.PartiallyFulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "A712844-1",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712844-2",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        },

                        new Line
                        {
                            RequiresFulfilment = false,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712844-3",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();


                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XAPP007",
                            ItemNumber = "GN0125GHPCAN",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        },
                        new Reservation()
                        {
                            AssetId = "XAPP007",
                            ItemNumber = "GN0125GHPCAN",
                            Quantity = quantity,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[1].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header PartiallyFulfilledHeader_All_Lines_Reserved_For_Calculating_Status(ApplicationDbContext dbContext, int quantity)
            {
                var header = new Header
                {
                    AgreementNumber = "A712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.PartiallyFulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "A712844-1",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712844-2",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();


                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XAPP007",
                            ItemNumber = "GN0125GHPCAN",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        },

                        new Reservation()
                        {
                            AssetId = "XAPP007",
                            ItemNumber = "GN0125GHPCAN",
                            Quantity = quantity,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[1].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header UnFulfilledHeaderAndQuoteLineWithExcludedLines(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "Q712844-1",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGCE1500",
                            GenericItemNumber = "XGCE1500",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            OrderLineNumber = "Q017a000003GAerrBBG",
                            QuoteLineNumber = "8017a000003GAerAAG",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        },
                    };

                int count = 2;

                foreach (var item in new string [] { "FSLLABOUR", "FUEL OUT/IN", "MTR123", "XX123", "XH123", "TX123", "BD123", "BF123", "PF123", "SERV123", "YDEF OUT/IN" })
                {
                    lines.Add(new Line
                    {
                        RequiresFulfilment = false,
                        AgreementLineIndex = count,
                        AgreementLineNumber = "Q712844-" + count,
                        DeliveryDate = DateTime.Now,
                        ItemNumber = item,
                        GenericItemNumber = item,
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "BD0",
                        NumberOfShifts = "1",
                        Quantity = 1,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                    });

                    count++;
                }

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header FulfilledOrderAndLine(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "T712844-1",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGCE1500",
                            GenericItemNumber = "XGCE1500",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "ED0",
                            NumberOfShifts = "1",
                            Quantity = 2,
                            OrderLineNumber = "Q017a000003GAerrBBG",
                            QuoteLineNumber = "8017a000003GAerAAG",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XGCB0535_NA",
                            ItemNumber = "XGCB0535_NA",
                            Quantity = 1,
                            Warehouse = "BD0",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id,
                            IsRehire = true
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header SetupHeaderWithMatched(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "A712844-1",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();


                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XAPP007",
                            ItemNumber = "GN0125GHPCAN",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header UnFulfilledHeaderAndQuoteLine(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "Q712844-1",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGCE1500",
                            GenericItemNumber = "XGCE1500",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            OrderLineNumber = "Q017a000003GAerrBBG",
                            QuoteLineNumber = "8017a000003GAerAAG",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header UnFulfilledHeaderAndLine(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900001",
                    CustomerAddress = "Fake Street",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "T712844-1",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGCE1500",
                            GenericItemNumber = "XGCE1500",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header QuoteConvertedToTAgreementWithMultipleWarehouseReservations(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "T712844-1",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGCE1500",
                            GenericItemNumber = "XGCE1500",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "ED1",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            OrderLineNumber = "Q017a000003GAerrBBG",
                            QuoteLineNumber = "8017a000003GAerAAG",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XBBE234",
                            ItemNumber = "XBBE234",
                            Quantity = 1,
                            Warehouse = "ED1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        },
                        new Reservation()
                        {
                            AssetId = "XBBE349",
                            ItemNumber = "XBBE349",
                            Quantity = 1,
                            Warehouse = "BD0",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header QuoteConvertedToTAgreementWithSiblingOnDifferentHeader(ApplicationDbContext dbContext)
            {
                // Simulate the real scenario: Q header still owns the sibling line (header mismatch after Q→T rename)
                var quoteHeader = new Header
                {
                    AgreementNumber = "Q-438254",
                    AgreementNumbersOnly = "438254",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };
                dbContext.Headers.Add(quoteHeader);

                var tHeader = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };
                dbContext.Headers.Add(tHeader);

                // Sibling line is on the QUOTE header (simulates Q→T rename without header reassignment)
                var siblingLine = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 1,
                    AgreementLineNumber = "T712844-1",
                    AgreementNumbersOnly = "712844",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    ItemNumber = "XGCE1500",
                    GenericItemNumber = "XGCE1500",
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Warehouse = "ED1",
                    NumberOfShifts = "1",
                    Quantity = 1,
                    OrderLineNumber = "Q017a000003GAerrBBG",
                    QuoteLineNumber = "8017a000003GAerAAG",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Lines.Add(siblingLine);
                quoteHeader.Lines.Add(siblingLine);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "XBBE234",
                        ItemNumber = "XBBE234",
                        Quantity = 1,
                        Warehouse = "ED1",
                        EffectiveQuantity = 1,
                        LineId = siblingLine.Id
                    },
                    new Reservation()
                    {
                        AssetId = "XBBE349",
                        ItemNumber = "XBBE349",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = siblingLine.Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return tHeader;
            }

            public static Header QuoteConvertedToTAgreementWithSameWarehouseReservations(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 1,
                    AgreementLineNumber = "T712844-1",
                    AgreementNumbersOnly = "712844",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    ItemNumber = "XGCE1500",
                    GenericItemNumber = "XGCE1500",
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Warehouse = "BD0",
                    NumberOfShifts = "1",
                    Quantity = 1,
                    OrderLineNumber = "Q017a000003GAerrBBG",
                    QuoteLineNumber = "8017a000003GAerAAG",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Lines.Add(line);
                header.Lines.Add(line);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "XBBE234",
                        ItemNumber = "XBBE234",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = line.Id
                    },
                    new Reservation()
                    {
                        AssetId = "XBBE349",
                        ItemNumber = "XBBE349",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = line.Id
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header OldOFHeaderSingleLineAndReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T713033",
                    AgreementNumbersOnly = "713033",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "T713033-2",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGCE1500",
                            GenericItemNumber = "XGCE1500",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            OrderLineNumber = "Q017a000003GAerrBBG",
                            QuoteLineNumber = "8017a000003GAerAAG",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                            AgreementNumbersOnly = "713033",
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XGCB0535_NA",
                            ItemNumber = "XGCB0535_NA",
                            Quantity = 1,
                            Warehouse = "BD0",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id,
                            IsRehire = true
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header OldOFHeaderNoLinesOrReservations(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T713033",
                    AgreementNumbersOnly = "713033",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                return header;
            }

            public static Header FulfilledHeaderAndLine(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                            RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "A712844-1",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGCE1500",
                            GenericItemNumber = "XGCE1500",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XAPP007",
                            ItemNumber = "GN0155GHPCAN",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }
            public static Header InvalidExpected(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712794",
                    AgreementNumbersOnly = "712794",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.PartiallyFulfilled
                };

                dbContext.Headers.Add(header);
                dbContext.SaveChanges(true);

                return header;
            }

            public static Header UnFulfilledHeaderAndSerializedLineReadyForDelete(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712794",
                    AgreementNumbersOnly = "712794",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.PartiallyFulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "A712794-1",
                            AgreementNumbersOnly = "712794",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGDP0100CBL",
                            GenericItemNumber = "XGDP0100CBL",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            OrderLineNumber = "8027a000007OxtQAAS",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712794-2",
                            AgreementNumbersOnly = "712794",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGDP0200CBL",
                            GenericItemNumber = "XGDP0200CBL",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            OrderLineNumber = "8027a000007OxtQAAS",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XAPP007",
                            ItemNumber = "GN0155GHPCAN",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header UnFulfilledHeaderAndSerializedLineSplitsWhenQuantityisGt1(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T713033",
                    AgreementNumbersOnly = "713033",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "T713033-1",
                            AgreementNumbersOnly = "713033",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGDP0100CBL",
                            GenericItemNumber = "XGDP0100CBL",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            OrderLineNumber = "8027a000007OxtQAAS",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header FulfilledHeaderAndLineGoesToPartiallyFulfilled(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A713020",
                    AgreementNumbersOnly = "713020",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 1,
                            AgreementLineNumber = "A713020-1",
                            AgreementNumbersOnly = "713020",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            ItemNumber = "XGCB0535_NA",
                            GenericItemNumber = "XGCB0535_NA",
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            Warehouse  = "BD0",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XGCB0535_NA",
                            ItemNumber = "XGCB0535_NA",
                            Quantity = 1,
                            Warehouse = "BD0",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header SetupHeaderWithMatchedItemAndReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712844-2",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();


                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "XAPP007",
                            ItemNumber = "GN0155GHPCAN",
                            Quantity = 2,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header SetupHeaderWithMatchedItemAndNoReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 2,
                            AgreementLineNumber = "A712844-2",
                            AgreementNumbersOnly = "712844",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                return header;
            }

            public static Header SetupHeaderWithUnmatchedItemAndDeletedItem(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 5,
                            AgreementLineNumber = "A712565-1",
                            AgreementNumbersOnly = "712565",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            Quantity = 1,
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },

                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 5,
                            AgreementLineNumber = "A712565-2",
                            AgreementNumbersOnly = "712565",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Quantity = 1,
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            IsDeleted = true,
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "CB04/4BAE025FT",
                            ItemNumber = "CB04/4BAE025FT",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header SetupHeaderWithItemAndDeletedItem(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Headers.Add(header);

                var lines = new List<Line>()
                    {
                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 5,
                            AgreementLineNumber = "A712565-3",
                            AgreementNumbersOnly = "712565",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Quantity = 1,
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        },

                        new Line
                        {
                        RequiresFulfilment = true,
                            AgreementLineIndex = 5,
                            AgreementLineNumber = "A712565-2",
                            AgreementNumbersOnly = "712565",
                            AgreementLineType = "5",
                            DeliveryDate = DateTime.Now,
                            RateType = "4",
                            Division = "200",
                            Facility = "USG",
                            NumberOfShifts = "1",
                            ValidToDate = DateTime.Now.AddDays(3),
                            ValidFromDate = DateTime.Now.AddDays(1),
                            Quantity = 1,
                            Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                            IsDeleted = true,
                            FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                        }
                    };

                foreach (var line in lines)
                {
                    dbContext.Lines.Add(line);
                    header.Lines.Add(line);
                }

                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "CB04/4BAE025FT",
                            ItemNumber = "CB04/4BAE025FT",
                            Quantity = 1,
                            Warehouse = "BD1",
                            EffectiveQuantity = 1,
                            LineId = lines[0].Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header SiblingWithConfirmedReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };
                dbContext.Headers.Add(header);

                // Sibling line with a CONFIRMED reservation (on-hire)
                var siblingLine = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 1,
                    AgreementLineNumber = "T712844-1",
                    AgreementNumbersOnly = "712844",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    ItemNumber = "XGCE1500",
                    GenericItemNumber = "XGCE1500",
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Warehouse = "BD0",
                    NumberOfShifts = "1",
                    Quantity = 1,
                    OrderLineNumber = "Q017a000003GAerrBBG",
                    QuoteLineNumber = "8017a000003GAerAAG",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Lines.Add(siblingLine);
                header.Lines.Add(siblingLine);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "XBBE234",
                        ItemNumber = "XBBE234",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = siblingLine.Id,
                        IsConfirmed = true  // ON-HIRE - should NOT be moved
                    },
                    new Reservation()
                    {
                        AssetId = "XBBE349",
                        ItemNumber = "XBBE349",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = siblingLine.Id,
                        IsConfirmed = false  // NOT confirmed - should be moved
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            public static Header SiblingWithDuplicateReservation(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };
                dbContext.Headers.Add(header);

                // Target line that already has a reservation
                var targetLine = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 2,
                    AgreementLineNumber = "A712844-2",
                    AgreementNumbersOnly = "712844",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    ItemNumber = "XGCE1500",
                    GenericItemNumber = "XGCE1500",
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Warehouse = "BD0",
                    NumberOfShifts = "1",
                    Quantity = 2,
                    OrderLineNumber = "8027a000007OuQhAAK",
                    QuoteLineNumber = "8017a000003GAerAAG",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                // Sibling line with reservations
                var siblingLine = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 1,
                    AgreementLineNumber = "T712844-1",
                    AgreementNumbersOnly = "712844",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    ItemNumber = "XGCE1500",
                    GenericItemNumber = "XGCE1500",
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Warehouse = "BD0",
                    NumberOfShifts = "1",
                    Quantity = 1,
                    OrderLineNumber = "Q017a000003GAerrBBG",
                    QuoteLineNumber = "8017a000003GAerAAG",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                dbContext.Lines.Add(targetLine);
                dbContext.Lines.Add(siblingLine);
                header.Lines.Add(targetLine);
                header.Lines.Add(siblingLine);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                {
                    // Target line already has this asset
                    new Reservation()
                    {
                        AssetId = "XBBE234",
                        ItemNumber = "XBBE234",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = targetLine.Id,
                        IsConfirmed = false
                    },
                    // Sibling has duplicate asset - should NOT be moved
                    new Reservation()
                    {
                        AssetId = "XBBE234",
                        ItemNumber = "XBBE234",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = siblingLine.Id,
                        IsConfirmed = false
                    },
                    // Sibling has unique asset - SHOULD be moved
                    new Reservation()
                    {
                        AssetId = "XBBE999",
                        ItemNumber = "XBBE999",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = siblingLine.Id,
                        IsConfirmed = false
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }

            /// <summary>
            /// Sibling line has multiple unconfirmed reservations in the same warehouse.
            /// Used to test that when allocation data is present, sibling reservations are NOT moved
            /// (preventing the destructive upsert from deleting them).
            /// </summary>
            public static Header SiblingWithMultipleReservationsAndAllocationOnNewLine(ApplicationDbContext dbContext)
            {
                var header = new Header
                {
                    AgreementNumber = "T712844",
                    AgreementNumbersOnly = "712844",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };
                dbContext.Headers.Add(header);

                // Sibling line (original quote line) with multiple reservations
                var siblingLine = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineIndex = 1,
                    AgreementLineNumber = "T712844-1",
                    AgreementNumbersOnly = "712844",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    ItemNumber = "XGCE1500",
                    GenericItemNumber = "XGCE1500",
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    Warehouse = "BD0",
                    NumberOfShifts = "1",
                    Quantity = 2,
                    OrderLineNumber = "Q017a000003GAerrBBG",
                    QuoteLineNumber = "8017a000003GAerAAG",
                    ValidToDate = DateTime.Now.AddDays(3),
                    ValidFromDate = DateTime.Now.AddDays(1),
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };

                dbContext.Lines.Add(siblingLine);
                header.Lines.Add(siblingLine);
                dbContext.SaveChanges();

                // Sibling has 2 unconfirmed reservations in BD0 (one for each asset on the quote)
                var reservations = new List<Reservation>()
                {
                    new Reservation()
                    {
                        AssetId = "XAPP004",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = siblingLine.Id,
                        IsConfirmed = false
                    },
                    new Reservation()
                    {
                        AssetId = "XAPP005",
                        ItemNumber = "GN0125GHPCAN",
                        Quantity = 1,
                        Warehouse = "BD0",
                        EffectiveQuantity = 1,
                        LineId = siblingLine.Id,
                        IsConfirmed = false
                    }
                };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }
        }

        public static class AcknowledgeHeader
        {
            public static Header SetupHeaderWithLineWithSubLineAndActivation(ApplicationDbContext dbContext, string? agreementLineNumberSuffix = null, ActivationStatus initialStatus = ActivationStatus.Requested)
            {
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)initialStatus,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                };

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineNumber = "A712565-1" + agreementLineNumberSuffix,
                    AgreementNumbersOnly = "712565",
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    NumberOfShifts = "1",
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null
                };

                header.Lines.Add(line);
                dbContext.Lines.Add(line);
                dbContext.SaveChanges();

                return header;
            }
        }

        public static class AcknowledgeLine
        {
            public static Header SetupLineWithSubLineAndActivationAndReservations(ApplicationDbContext dbContext, string? agreementLineNumberSuffix = null, ActivationStatus initialStatus = ActivationStatus.Requested)
            {
                var header = new Header
                {
                    AgreementNumber = "T712628",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000"
                };

                var line = new Line
                {
                    RequiresFulfilment = true,
                    AgreementLineNumber = "T712628-1" + agreementLineNumberSuffix,
                    AgreementLineType = "5",
                    DeliveryDate = DateTime.Now,
                    RateType = "4",
                    Division = "200",
                    Facility = "USG",
                    NumberOfShifts = "1",
                    ValidFromDate = DateTime.Now.AddDays(1),
                    Attributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok;Additional information:30 Meter x Cam-Lok Cable 2/0 AWG Extension",
                    Header = header,
                    ActivationErrors = null,
                    ActivationStatus = (int)initialStatus,
                    ActivationInstanceId = "0427f9b0-6489-4ea8-b4fc-445cb9390447"
                };

                dbContext.Lines.Add(line);
                dbContext.SaveChanges();

                var reservations = new List<Reservation>()
                    {
                        new Reservation()
                        {
                            AssetId = "CB04/4BAE025FT",
                            ItemNumber = "CB04/4BAE025FT",
                            Quantity = 1,
                            Warehouse = "BD0",
                            EffectiveQuantity = 1,
                            LineId = line.Id
                        },
                        new Reservation()
                        {
                            AssetId = "CB04/4BAE050FT",
                            ItemNumber = "CB04/4BAE050FT",
                            Quantity = 1,
                            Warehouse = "ED1",
                            EffectiveQuantity = 1,
                            LineId = line.Id
                        }
                    };

                dbContext.Reservations.AddRange(reservations);
                dbContext.SaveChanges();

                return header;
            }
        }

        public static class RingfenceData
        {
            public static (Ringfence, Ringfence) CreateRingfence(ApplicationDbContext dbContext)
            {
                var ringfence1 = new Ringfence("stephen.hogan@aggreko.com")
                {
                    Title = "Test Ringfence 1",
                    FromDate = DateTime.Now.AddDays(-10),
                    ToDate = DateTime.Now.AddDays(10),
                    Owner = "stephen.hogan@aggreko.co.uk",
                    Warehouse = "WH1",
                    Divisions = "110"
                };

                var ringfence2 = new Ringfence("stephen.hogan@aggreko.com")
                {
                    Title = "Test Ringfence 2",
                    FromDate = DateTime.Now.AddDays(-5),
                    ToDate = DateTime.Now.AddDays(5),
                    Owner = "stephen.hogan@aggreko.co.uk",
                    Warehouse = "WH1",
                    Divisions = "110"
                };

                dbContext.Ringfences.AddRange(ringfence1, ringfence2);
                dbContext.SaveChanges();

                return (ringfence1, ringfence2);
            }

            public static List<RingfenceItem> CreateRingfenceItems(ApplicationDbContext dbContext, int ringfenceId)
            {
                var ringfenceItems = new List<RingfenceItem>
                {
                    new RingfenceItem("test.owner")
                    {
                        RingfenceId = ringfenceId,
                        AssetId = "Asset1"
                    },
                    new RingfenceItem("test.owner")
                    {
                        RingfenceId = ringfenceId,
                        AssetId = "Asset2"
                    }
                };

                dbContext.RingfenceItems.AddRange(ringfenceItems);
                dbContext.SaveChanges();

                return ringfenceItems;
            }
        }

        public static class Assets
        {
            public static List<VwAssetItem> AssetItemsWithSomeRemovedStock =>
                new List<VwAssetItem>
                {
                    new VwAssetItem()
                    {
                        Id = "1",
                        Status = "InTransit",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "2",
                        Status = "InService",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "3",
                        Status = "Assess",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "4",
                        Status = "Available",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "5",
                        Status = "Repair",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "6",
                        Status = "OnHire",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "7",
                        Status = "Collection",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "8",
                        Status = "RemovedStock",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "9",
                        Status = "Scrap",
                        WarehouseName = string.Empty,
                    },
                    new VwAssetItem()
                    {
                        Id = "10",
                        Status = "Sold",
                        WarehouseName = string.Empty,
                    },
                };

            public static List<VwAssetItem> AssetItemsWithDaysOffHire =>
                new List<VwAssetItem>
                {
                    new VwAssetItem()
                    {
                        Id = "long-off",
                        Status = "Available",
                        WarehouseName = string.Empty,
                        DaysOffHire = 33,
                    },
                    new VwAssetItem()
                    {
                        Id = "recent-off",
                        Status = "Available",
                        WarehouseName = string.Empty,
                        DaysOffHire = 5,
                    },
                    new VwAssetItem()
                    {
                        Id = "on-hire",
                        Status = "OnHire",
                        WarehouseName = string.Empty,
                        DaysOffHire = 0,
                    },
                    new VwAssetItem()
                    {
                        Id = "never-on-hire",
                        Status = "Available",
                        WarehouseName = string.Empty,
                        DaysOffHire = null,
                    },
                };
        }
    }
}
