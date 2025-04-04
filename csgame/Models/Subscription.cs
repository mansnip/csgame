namespace csgame.Models
{
    public class Subscription
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public int Traffic { get; set; }
        public int Duration { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int RemainingTraffic { get; set; }
        public string InvoiceId { get; set; }
        public string VpnServerID { get; set; }
        public string VpnEmailName { get; set; }
        public string? RemarkName { get; set; }
        public string VpnId { get; set; }

        // Navigation properties
        public virtual User User { get; set; }
        public virtual Invoice Invoice { get; set; }
    }
}
