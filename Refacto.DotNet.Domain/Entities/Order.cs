namespace Refacto.DotNet.Domain.Entities
{
    public class Order
    {
        public long Id { get; set; }

        public ICollection<Product>? Items { get; set; }
    }
}
