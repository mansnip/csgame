namespace csgame.Context.Entitis
{
    public class Payment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime CreateDateTime { get; set; } = DateTime.Now;

        public string? RefId { get; set; }

        public int Price { get; set; }

        public string FactorID { get; set; }

        public bool IsPaymented { get; set; } = false;

    }
}
