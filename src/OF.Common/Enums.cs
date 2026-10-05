namespace OF.Common
{
    public class Enums
    {
        public enum SendToM3Status
        {
            Pending = 1,
            Successful = 2,
            Failed = 3,
        }

        public enum FinaliseMethod
        {
            AcceptSuggestion,
            ReviewSuggestion,
            ManualSelection,
        }

        public enum LineType
        {
            Unknown = 0,
            SaleByThirdPartyDeliveredByAggreko = 1,
            SaleByThirdPartyDeliveredDirectByThirdParty = 2,
            Package = 4,
            StandardRental = 5,
            StandardSale = 6,
            RehireFromThirdPartyDeliveredByAggreko = 7,
            RehireFromThirdPartyDeliveredDirectByThirdParty = 8,
        }

        public enum OrderLineSource
        {
            SalesForce = 0,
            M3Other = 1,
            M3Spartan = 2,
        }

        public enum OrderAssignmentStatus
        {
            Unfulfilled = 1,
            PartiallyFulfilled = 2,
            FullyFulfilled = 3,
            Finished = 4,
        }

        public enum ActivationStatus
        {
            TODO = 0,
            Failed = 1,
            Requested = 2,
            Activated = 3,
        }
    }
}
