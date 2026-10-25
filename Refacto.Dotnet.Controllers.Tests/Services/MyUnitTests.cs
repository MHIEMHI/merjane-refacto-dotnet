using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Application.Services;
using Refacto.DotNet.Application.Services.ProductHandlers;
using Refacto.DotNet.Domain.Entities;

namespace Refacto.DotNet.Controllers.Tests.Services
{
    public class MyUnitTests
    {
        private readonly ProductService _productService;

        public MyUnitTests()
        {
            _productService = new ProductService(new IProductTypeHandler[]
            {
                new NormalProductHandler(),
                new SeasonalProductHandler(),
                new ExpirableProductHandler()
            });
        }

        [Fact]
        public void Test()
        {
            // GIVEN
            Product product = new()
            {
                LeadTime = 15,
                Available = 0,
                Type = "NORMAL",
                Name = "RJ45 Cable"
            };

            // WHEN
            ProductNotification? notification =
                _productService.ProcessProduct(product, new DateOnly(2025, 5, 10));

            // THEN
            Assert.Equal(0, product.Available);
            Assert.Equal(15, product.LeadTime);
            Assert.Equal(ProductNotificationType.Delay, notification?.Type);
            Assert.Equal(product.Name, notification?.ProductName);
            Assert.Equal(product.LeadTime, notification?.LeadTime);
        }
    }
}
