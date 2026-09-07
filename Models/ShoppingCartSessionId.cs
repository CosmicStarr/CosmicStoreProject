namespace Models
{
    public class ShoppingCartSessionId
    {
        public int Id { get; set; }
        public required string ActualShoppingCartId { get; set; }
        public required string ApplicationUser { get; set; }
        public DateTime TimeToStayAlive { get; set; } = DateTime.Now;
        public TimeSpan GetTime()
        {
            var timeInfo = DateTime.Now - TimeToStayAlive;
            return timeInfo;
        }

    }
}