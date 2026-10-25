using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Domain.Entities;
using Refacto.DotNet.Domain.Products;

namespace Refacto.DotNet.Application.Services.ProductHandlers
{
    public sealed class NormalProductHandler : IProductTypeHandler
    {
        public ProductType ProductType => ProductType.Normal;

        public ProductNotification? Handle(Product product, DateOnly currentDate)
        {
            if (product.Available > 0)
            {
                product.Available -= 1;
                return null;
            }

            return product.LeadTime > 0
                ? new ProductNotification(ProductNotificationType.Delay, product.Name, product.LeadTime)
                : null;
        }
    }
}
