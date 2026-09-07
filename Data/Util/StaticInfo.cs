namespace Data.Util
{
    public static class StaticInfo
    {
        public const string AdminRole = "Admin";
        public const string Job = "CEO";
        public const string Pending = "Pending";
        public const string Received = "Order Received";
        public const string NotYet = "Payment failed! Can't start delivery process";
        public const string GoAhead = "Payment was a success! delivery process can start";
        public const string Canceled = "Order Canceled!";
        public const string ItemCanceled = "Ordered item Canceled!";
        public const string Refund = "Buyer requested a refund on the item!";
        public const string MarkedForDeletion = "You can delete this order when you don't need it.";
    }
}