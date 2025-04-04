namespace csgame.Models
{
    public class Invoice
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string InvoiceNumber { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public int Traffic { get; set; }
        public int Duration { get; set; }
        public decimal BasePrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public int DiscountPercent { get; set; }
        public decimal FinalPrice { get; set; }
        public string? RemarkName { get; set; }
        public string? PaymentToken { get; set; } = Guid.NewGuid().ToString();
        public bool IsComplate { get; set; }


        // فیلدهای جدید برای تمدید
        public bool IsRenewal { get; set; }
        public string? RenewalSubscriptionId { get; set; }

        // تغییر از IsPaid به Status
        public string Status { get; set; } = "در انتظار پرداخت"; // مقادیر: "در انتظار پرداخت"، "پرداخت شده"، "لغو شده"

        public DateTime? PaidDate { get; set; }
        public long? PaymentRefId { get; set; }

        // Navigation properties
        public virtual User User { get; set; }
        public virtual Subscription Subscription { get; set; }
    }
}
