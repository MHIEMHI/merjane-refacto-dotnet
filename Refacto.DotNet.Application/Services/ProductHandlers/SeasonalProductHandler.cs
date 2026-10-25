using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Domain.Entities;
using Refacto.DotNet.Domain.Products;

namespace Refacto.DotNet.Application.Services.ProductHandlers
{
    public sealed class SeasonalProductHandler : IProductTypeHandler
    {
        public ProductType ProductType => ProductType.Seasonal;

        public ProductNotification? Handle(Product product, DateOnly currentDate)
        {
            DateOnly? seasonStartDate = ToDateOnly(product.SeasonStartDate);
            DateOnly? seasonEndDate = ToDateOnly(product.SeasonEndDate);

            if (seasonStartDate.HasValue
                && seasonEndDate.HasValue
                && currentDate > seasonStartDate.Value
                && currentDate < seasonEndDate.Value
                && product.Available > 0)
            {
                product.Available -= 1;
                return null;
            }
            else if (seasonEndDate.HasValue
                && currentDate.AddDays(product.LeadTime) >= seasonEndDate.Value)
            {
                product.Available = 0;
                return new ProductNotification(ProductNotificationType.OutOfStock, product.Name);
            }
            else if (seasonStartDate.HasValue && seasonStartDate.Value > currentDate)
            {
                return new ProductNotification(ProductNotificationType.OutOfStock, product.Name);
            }

            return new ProductNotification(ProductNotificationType.Delay, product.Name, product.LeadTime);
        }

        private static DateOnly? ToDateOnly(DateTime? value)
        {
            return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
        }
    }
}
