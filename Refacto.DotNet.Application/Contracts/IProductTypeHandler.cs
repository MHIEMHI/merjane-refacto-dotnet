using Refacto.DotNet.Domain.Entities;
using Refacto.DotNet.Domain.Products;

namespace Refacto.DotNet.Application.Contracts
{
    public interface IProductTypeHandler
    {
        ProductType ProductType { get; }

        ProductNotification? Handle(Product product, DateOnly currentDate);
    }
}
