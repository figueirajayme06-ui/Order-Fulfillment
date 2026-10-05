namespace OF.Common
{
    public static class Constants
    {
        public static class Topics
        {
            public const string OrderActivationAcknowledgment = "order-activation-ack";
            public const string OrderHeaderSync = "order-header-sync";
            public const string OrderLineAcknowledgment = "order-line-ack";
            public const string OrderLineSync = "order-line-sync";
        }

        public static class Queues
        {
            public const string UpdateByAgreement = "updatebyagreement";
            public const string ActivateAgreement = "activateagreement";
            public const string ActivateHeader = "activateheader";
            public const string UpsertQuote = "upsertquote";
            public const string ChangeNofity = "changenotify";
            public const string ChangeApproval = "changeapproval";
            public const string ChangeComplete = "changecomplete";
            public const string AgreementSync = "agreement-sync";
        }

        public static class AgreementSync
        {
            public const string QueueOperationKey = "Operation";

            public static class Operations
            {
                public const string OrderActivationAcknowledgment = "order-activation-ack";
                public const string OrderHeaderSync = "order-header-sync";
                public const string OrderLineAcknowledgment = "order-line-ack";
                public const string OrderLineSync = "order-line-sync";
            }
        }

        public static class DataRefresh
        {
            public static class Quotes
            {
                public const string Key = "Quotes";
                public const string Description = "Salesforce quotes and lines. Every 20, 50 minutes past the hour.";
            }

            public static class Assets
            {
                public const string Key = "Assets";
                public const string Description = "Serialized assets from ION. Once every 30 minutes at 10 and 40 minutes past the hour.";
            }

            public static class Warehouses
            {
                public const string Key = "Warehouse";
                public const string Description = "Warehouses from MDP. Once every hour at 20 minutes past.";
            }

            public static class NonSerialised
            {
                public const string Key = "NonSerialised";
                public const string Description = "Product items from ION. Once a day at 12 UTC.";
            }
        }

        public static class Operations
        {
            public const string Delete = "delete";
            public const string Ignore = "ignore";
        }

        public static class OpportunityStage
        {
            public const string Develop = "Develop";
            public const string Build = "Build";
            public const string Negotiate = "Negotiate";
            public const string ClosedLost = "Closed Lost";
            public const string ClosedWon = "Closed Won";
            public const string NotAccepted = "Not Accepted";
        }

        public static class Lines
        {
            public static string[] Excludes = new[] 
            {
                "FSLLABOUR","FUEL OUT/IN","MTR","XX","XH","TX","BD","BF","PF","SERV","YDEF OUT/IN"
            };
        }

        public static class Warehouses
        {
            public static readonly IReadOnlySet<string> Excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "EA0","EC0","EH0","EK0","EL0","EU0","EW0","EX0","FC0","FD0","FE0","FF0","FG0","FH0","FK0","FL0","FM0","FV0","FU0","GR0","JK0","HK0","YG0","GA0","GB0"
            };

            public static bool IsExcluded(string? warehouseCode) =>
                !string.IsNullOrWhiteSpace(warehouseCode) && Excludes.Contains(warehouseCode.Trim());
        }

        public static class IPG
        {
            public static string Company = "1";
            public static string OrderSource = "NOF";
            public static string AgreementLineSuffix = "0";
            public static string AgreementVersion = "0";
            public static string DepotFulfil = "DEPOTFULFIL";

            public static class ProcessStatus
            {
                public const int HeaderReady = 130;
                public const int LineReady = 131;
                public const int HeaderHasLinesInProgress = 140;
                public const int LineInProgress = 141;
            }
        }

        public static class M3LineText
        {
            public const string AttributeSeparator = ";";
            public const string ItemToPickText = "Item to Pick";
            public const string DepotFulfillesFrom = "Depot fulfills from";
            public const string DepotQuantity = "Qty";
            public const string DeliverySlot = "Delivery slot";
            public const string CollectionSlot = "Pickup slot";
            public const string Rehire = "Rehire: [Requires no action]";
        }

        public static class M3LineStatus
        {
            public const int OnHire = 50;
            public const int Terminated = 40;
            public const int Invoiced = 90;
            public const int Closed = 99;
        }

        public static class HeaderStatus
        {
            public const string Initial = "00";
            public const string Created = "05";
            public const string CustomerOnStop = "12";
            public const string LineCreated = "20";
            public const string Terminated = "40";
            public const string OnHire = "50";
            public const string Invoiced = "90";
            public const string Completed = "99";

            public static readonly HashSet<string> HistoricalStatuses = new()
            {
                Terminated,
                Invoiced,
                Completed
            };
        }

        public static class M3LineDeliveryStatus
        {
            public const int Delivered = 99;
        }

        public static class Infrastructure
        {
            public static class Http
            {
                public const string CorrelatableOptionsKey = "CorrelatableKey";
            }
        }

        public static class Message
        {
            public static class Properties
            {
                public const string ActivationTimestampKey = "NOF:Activation:Timestamp";
                public const string ActivationRetryCounterKey = "NOF:Activation:Retry:Count";
            }
        }

        public static class Roles
        {
            public const string ChangeOrder = "ChangeOrder";
            public const string ChangeApproval = "ChangeApproval";
            public const string NewFrontendPreview = "NewFrontendPreview";
            public const string ReadOnly = "ReadOnly";
        }
    }
}
