namespace Refacto.DotNet.Domain.Entities
{
    public class Product
    {
        public long Id { get; set; }

        public int LeadTime { get; set; }

        public int Available { get; set; }

        public string? Type { get; set; }

        public string? Name { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public DateTime? SeasonStartDate { get; set; }

        public DateTime? SeasonEndDate { get; set; }
    }
}
