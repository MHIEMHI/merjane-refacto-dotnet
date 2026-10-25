using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Domain.Entities;
using Refacto.DotNet.Domain.Products;

namespace Refacto.DotNet.Application.Services.ProductHandlers
{
    public sealed class ExpirableProductHandler : IProductTypeHandler
    {
        public ProductType ProductType => ProductType.Expirable;

        public ProductNotification? Handle(Product product, DateOnly currentDate)
        {
            DateOnly? expiryDate = ToDateOnly(product.ExpiryDate);
            if (product.Available > 0 && expiryDate.HasValue && expiryDate.Value > currentDate)
            {
                product.Available -= 1;
                return null;
            }

            if (!product.ExpiryDate.HasValue)
            {
                throw new InvalidOperationException("An expirable product must have an expiry date.");
            }

            product.Available = 0;
            return new ProductNotification(
                ProductNotificationType.Expired,
                product.Name,
                ExpiryDate: product.ExpiryDate.Value);
        }

        private static DateOnly? ToDateOnly(DateTime? value)
        {
            return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
        }
    }
}
