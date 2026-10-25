using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Domain.Entities;
using Refacto.DotNet.Domain.Products;

namespace Refacto.DotNet.Application.Services
{
    public sealed class ProductService : IProductRulesService
    {
        private readonly IReadOnlyDictionary<ProductType, IProductTypeHandler> _handlers;

        public ProductService(IEnumerable<IProductTypeHandler> handlers)
        {
            _handlers = handlers.ToDictionary(handler => handler.ProductType);
        }

        public ProductNotification? ProcessProduct(Product product, DateOnly currentDate)
        {
            if (!ProductTypeParser.TryParse(product.Type, out ProductType productType))
            {
                return null;
            }

            if (!_handlers.TryGetValue(productType, out IProductTypeHandler? handler))
            {
                throw new InvalidOperationException($"No product handler is registered for '{productType}'.");
            }

            return handler.Handle(product, currentDate);
        }
    }
}
