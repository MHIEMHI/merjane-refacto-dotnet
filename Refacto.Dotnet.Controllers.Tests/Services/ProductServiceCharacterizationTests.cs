using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Application.Services;
using Refacto.DotNet.Application.Services.ProductHandlers;
using Refacto.DotNet.Controllers.Controllers;
using Refacto.DotNet.Domain.Entities;
using Refacto.DotNet.Domain.Products;
using Refacto.DotNet.Infrastructure.Persistence;

namespace Refacto.Dotnet.Controllers.Tests.Services
{
    public class ProductServiceCharacterizationTests
    {

        private AppDbContext _context = null!;
        private Mock<INotificationService> _notifications = null!;
        private Mock<IDateTimeProvider> _dateTimeProvider = null!;
        private ProductService _service = null!;
        private readonly DateTime _now = new(2025, 5, 10, 14, 30, 0);

        public ProductServiceCharacterizationTests()
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
            _notifications = new Mock<INotificationService>();
            _dateTimeProvider = new Mock<IDateTimeProvider>();
            _dateTimeProvider.SetupGet(provider => provider.Now).Returns(_now);
            _service = CreateProductService();
        }


        [Fact]
        public void NotifyDelaySavesTheLeadTimeAndSendsDelayNotification()
        {
            Product product = AddProduct(new Product { LeadTime = 15, Available = 0, Type = "NORMAL", Name = "RJ45 Cable" });

            ProcessProduct(product);

            Assert.Equal(15, product.LeadTime);
            _notifications.Verify(service => service.SendDelayNotification(15, "RJ45 Cable"), Times.Once);
        }

        [Theory]
        [InlineData("NORMAL", ProductType.Normal)]
        [InlineData("SEASONAL", ProductType.Seasonal)]
        [InlineData("EXPIRABLE", ProductType.Expirable)]
        public void ProductTypeParserRecognizesSupportedValues(string value, ProductType expected)
        {
            Assert.True(ProductTypeParser.TryParse(value, out ProductType actual));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("OTHER")]
        [InlineData("normal")]
        public void ProductTypeParserRejectsUnsupportedValues(string? value)
        {
            Assert.False(ProductTypeParser.TryParse(value, out _));
        }

        [Fact]
        public void ProductServiceDelegatesToHandlerForParsedType()
        {
            Mock<IProductTypeHandler> handler = new();
            _ = handler.SetupGet(candidate => candidate.ProductType).Returns(ProductType.Normal);
            ProductService service = new(new[] { handler.Object });
            Product product = new() { Type = "NORMAL", Name = "USB Cable" };
            DateOnly currentDate = DateOnly.FromDateTime(_now);

            service.ProcessProduct(product, currentDate);

            handler.Verify(candidate => candidate.Handle(product, currentDate), Times.Once);
        }

        [Fact]
        public void ProductServiceRejectsDuplicateHandlersForSameType()
        {
            Mock<IProductTypeHandler> firstHandler = new();
            Mock<IProductTypeHandler> secondHandler = new();
            _ = firstHandler.SetupGet(handler => handler.ProductType).Returns(ProductType.Normal);
            _ = secondHandler.SetupGet(handler => handler.ProductType).Returns(ProductType.Normal);

            Assert.Throws<ArgumentException>(() =>
                new ProductService(new[] { firstHandler.Object, secondHandler.Object }));
        }

        [Fact]
        public void SeasonalProductWhoseLeadTimeExceedsSeasonEndIsDepletedAndReportedOutOfStock()
        {
            Product product = AddProduct(new Product
            {
                LeadTime = 20,
                Available = 8,
                Type = "SEASONAL",
                Name = "Watermelon",
                SeasonStartDate = _now.Date,
                SeasonEndDate = _now.AddDays(10)
            });

            ProcessProduct(product);

            Assert.Equal(0, product.Available);
            _notifications.Verify(service => service.SendOutOfStockNotification("Watermelon"), Times.Once);
            _notifications.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SeasonalProductBeforeItsStartIsReportedOutOfStockWithoutChangingAvailability()
        {
            Product product = AddProduct(new Product
            {
                LeadTime = 2,
                Available = 8,
                Type = "SEASONAL",
                Name = "Grapes",
                SeasonStartDate = _now.AddDays(30),
                SeasonEndDate = _now.AddDays(60)
            });

            ProcessProduct(product);

            Assert.Equal(8, product.Available);
            _notifications.Verify(service => service.SendOutOfStockNotification("Grapes"), Times.Once);
            _notifications.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SeasonalProductWithReachableSeasonUsesDelayNotification()
        {
            Product product = AddProduct(new Product
            {
                LeadTime = 2,
                Available = 8,
                Type = "SEASONAL",
                Name = "Peaches",
                SeasonStartDate = _now.Date,
                SeasonEndDate = _now.AddDays(10)
            });

            ProcessProduct(product);

            Assert.Equal(8, product.Available);
            _notifications.Verify(service => service.SendDelayNotification(2, "Peaches"), Times.Once);
            _notifications.Verify(service => service.SendOutOfStockNotification(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SeasonalProductWhoseLeadTimeReachesSeasonEndIsReportedOutOfStock()
        {
            Product product = AddProduct(new Product
            {
                LeadTime = 1,
                Available = 8,
                Type = "SEASONAL",
                Name = "Peaches",
                SeasonStartDate = _now.Date,
                SeasonEndDate = _now.Date.AddDays(1).AddHours(23).AddMinutes(59)
            });

            ProcessProduct(product);

            Assert.Equal(0, product.Available);
            _notifications.Verify(service => service.SendOutOfStockNotification("Peaches"), Times.Once);
            _notifications.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void SeasonalProductWithNonPositiveLeadTimeStillUsesDelayNotification(int leadTime)
        {
            Product product = AddProduct(new Product
            {
                LeadTime = leadTime,
                Available = 8,
                Type = "SEASONAL",
                Name = "Peaches",
                SeasonStartDate = _now.Date,
                SeasonEndDate = _now.AddDays(10)
            });

            ProcessProduct(product);

            _notifications.Verify(service => service.SendDelayNotification(leadTime, "Peaches"), Times.Once);
        }

        [Fact]
        public void UnexpiredProductInStockIsDecrementedWithoutNotification()
        {
            Product product = AddProduct(new Product
            {
                Available = 3,
                Type = "EXPIRABLE",
                Name = "Butter",
                ExpiryDate = _now.AddDays(2)
            });

            ProcessProduct(product);

            Assert.Equal(2, product.Available);
            _notifications.Verify(service => service.SendExpirationNotification(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public void ProductExpiringTodayIsZeroedAndReportedExpired()
        {
            Product product = AddProduct(new Product
            {
                Available = 3,
                Type = "EXPIRABLE",
                Name = "Milk",
                ExpiryDate = _now.Date
            });

            ProcessProduct(product);

            Assert.Equal(0, product.Available);
            _notifications.Verify(service => service.SendExpirationNotification("Milk", _now.Date), Times.Once);
        }

        [Fact]
        public void ProductWithExpiryLaterTodayIsStillExpired()
        {
            DateTime expiryDate = _now.Date.AddHours(23).AddMinutes(59);
            Product product = AddProduct(new Product
            {
                Available = 3,
                Type = "EXPIRABLE",
                Name = "Milk",
                ExpiryDate = expiryDate
            });

            ProcessProduct(product);

            Assert.Equal(0, product.Available);
            _notifications.Verify(service => service.SendExpirationNotification("Milk", expiryDate), Times.Once);
        }

        [Fact]
        public void OutOfStockProductIsReportedExpiredEvenWhenItsExpiryDateIsInTheFuture()
        {
            DateTime expiryDate = _now.AddDays(2);
            Product product = AddProduct(new Product
            {
                Available = 0,
                Type = "EXPIRABLE",
                Name = "Cheese",
                ExpiryDate = expiryDate
            });

            ProcessProduct(product);

            Assert.Equal(0, product.Available);
            _notifications.Verify(service => service.SendExpirationNotification("Cheese", expiryDate), Times.Once);
        }

        [Fact]
        public void OutOfStockProductWithoutExpiryDateThrowsBeforeSendingNotification()
        {
            Product product = AddProduct(new Product { Available = 0, Type = "EXPIRABLE", Name = "Unknown expiry" });

            Assert.Throws<InvalidOperationException>(() => ProcessProduct(product));

            _notifications.Verify(service => service.SendExpirationNotification(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public void InStockProductWithoutExpiryDateIsZeroedAndThrowsBeforeSendingNotification()
        {
            Product product = AddProduct(new Product { Available = 2, Type = "EXPIRABLE", Name = "Unknown expiry" });

            Assert.Throws<InvalidOperationException>(() => ProcessProduct(product));

            Assert.Equal(2, product.Available);
            _notifications.Verify(service => service.SendExpirationNotification(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task ProcessOrderDecrementsAvailableProductAndReturnsOrderId()
        {
            Product product = new() { Available = 2, Type = "NORMAL", Name = "USB Cable" };
            Order order = AddOrder(product);

            ActionResult<Refacto.DotNet.Controllers.Dtos.Product.ProcessOrderResponse> result =
                await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            Assert.Equal(order.Id, result.Value!.id);
            Assert.Equal(1, ReadProduct(product.Id).Available);
            _notifications.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessOrderNotifiesForUnavailableNormalProductOnlyWhenLeadTimeIsPositive()
        {
            Product product = new() { Available = 0, LeadTime = 12, Type = "NORMAL", Name = "USB Dongle" };
            Order order = AddOrder(product);

            _ = await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            _notifications.Verify(service => service.SendDelayNotification(12, "USB Dongle"), Times.Once);
            Assert.Equal(0, ReadProduct(product.Id).Available);
        }

        [Fact]
        public async Task ProcessOrderIgnoresUnavailableNormalProductWithoutPositiveLeadTime()
        {
            Product product = new() { Available = 0, LeadTime = 0, Type = "NORMAL", Name = "USB Dongle" };
            Order order = AddOrder(product);

            _ = await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            _notifications.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessOrderIgnoresUnavailableNormalProductWithNegativeLeadTime()
        {
            Product product = new() { Available = 0, LeadTime = -1, Type = "NORMAL", Name = "USB Dongle" };
            Order order = AddOrder(product);

            _ = await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            _notifications.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessOrderOnSeasonStartDateFallsBackToZeroDayDelay()
        {
            Product product = new()
            {
                Available = 2,
                LeadTime = 0,
                Type = "SEASONAL",
                Name = "Watermelon",
                SeasonStartDate = _now.Date,
                SeasonEndDate = _now.Date.AddDays(10)
            };
            Order order = AddOrder(product);

            _ = await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            Assert.Equal(2, ReadProduct(product.Id).Available);
            _notifications.Verify(service => service.SendDelayNotification(0, "Watermelon"), Times.Once);
        }

        [Fact]
        public async Task ProcessOrderOnSeasonEndDateReportsOutOfStockAndSetsAvailabilityToZero()
        {
            Product product = new()
            {
                Available = 2,
                LeadTime = 0,
                Type = "SEASONAL",
                Name = "Watermelon",
                SeasonStartDate = _now.Date.AddDays(-10),
                SeasonEndDate = _now.Date
            };
            Order order = AddOrder(product);

            _ = await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            Assert.Equal(0, ReadProduct(product.Id).Available);
            _notifications.Verify(service => service.SendOutOfStockNotification("Watermelon"), Times.Once);
        }

        [Fact]
        public async Task ProcessOrderOnExpiryDateReportsExpiredAndSetsAvailabilityToZero()
        {
            Product product = new()
            {
                Available = 2,
                Type = "EXPIRABLE",
                Name = "Milk",
                ExpiryDate = _now.Date
            };
            Order order = AddOrder(product);

            _ = await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            Assert.Equal(0, ReadProduct(product.Id).Available);
            _notifications.Verify(service => service.SendExpirationNotification("Milk", _now.Date), Times.Once);
        }

        [Fact]
        public async Task ProcessOrderAppliesCurrentSeasonAndExpiryRules()
        {
            Product inSeason = new()
            {
                Available = 2,
                Type = "SEASONAL",
                Name = "Watermelon",
                SeasonStartDate = _now.Date.AddDays(-1),
                SeasonEndDate = _now.Date.AddDays(1)
            };
            Product futureSeason = new()
            {
                Available = 2,
                LeadTime = 90,
                Type = "SEASONAL",
                Name = "Grapes",
                SeasonStartDate = _now.AddDays(30),
                SeasonEndDate = _now.AddDays(60)
            };
            Product unexpired = new()
            {
                Available = 2,
                Type = "EXPIRABLE",
                Name = "Butter",
                ExpiryDate = _now.AddDays(2)
            };
            Product expired = new()
            {
                Available = 2,
                Type = "EXPIRABLE",
                Name = "Milk",
                ExpiryDate = _now.Date.AddDays(-1)
            };
            Order order = AddOrder(inSeason, futureSeason, unexpired, expired);

            ActionResult<Refacto.DotNet.Controllers.Dtos.Product.ProcessOrderResponse> result =
                await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            Assert.Equal(order.Id, result.Value!.id);
            Assert.Equal(1, ReadProduct(inSeason.Id).Available);
            Assert.Equal(0, ReadProduct(futureSeason.Id).Available);
            Assert.Equal(1, ReadProduct(unexpired.Id).Available);
            Assert.Equal(0, ReadProduct(expired.Id).Available);
            _notifications.Verify(service => service.SendOutOfStockNotification("Grapes"), Times.Once);
            _notifications.Verify(service => service.SendExpirationNotification("Milk", expired.ExpiryDate!.Value), Times.Once);
        }

        [Fact]
        public async Task ProcessOrderWithUnknownProductTypeLeavesAvailabilityUnchanged()
        {
            Product product = new() { Available = 5, Type = "OTHER", Name = "Unclassified" };
            Order order = AddOrder(product);

            _ = await CreateController().ProcessOrder(order.Id, CancellationToken.None);

            Assert.Equal(5, ReadProduct(product.Id).Available);
            _notifications.Verify(service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            _notifications.Verify(service => service.SendOutOfStockNotification(It.IsAny<string>()), Times.Never);
            _notifications.Verify(service => service.SendExpirationNotification(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task ProcessOrderForMissingOrderReturnsNotFound()
        {
            ActionResult<Refacto.DotNet.Controllers.Dtos.Product.ProcessOrderResponse> result =
                await CreateController().ProcessOrder(-1, CancellationToken.None);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        private Product AddProduct(Product product)
        {
            _context.Products.Add(product);
            _context.SaveChanges();
            _context.ChangeTracker.Clear();
            return _context.Products.Single(p => p.Name == product.Name);
        }

        private Product ReadProduct(long id)
        {
            _context.ChangeTracker.Clear();
            return _context.Products.Find(id)!;
        }

        private Order AddOrder(params Product[] products)
        {
            Order order = new() { Items = products.ToList() };
            _context.Orders.Add(order);
            _context.SaveChanges();
            _context.ChangeTracker.Clear();
            return order;
        }

        private OrdersController CreateController()
        {
            EfOrderRepository repository = new(_context);
            IOrderProcessingService orderProcessingService =
                new OrderProcessingService(repository, _service, _dateTimeProvider.Object, _notifications.Object);
            return new OrdersController(orderProcessingService, repository);
        }

        private ProductService CreateProductService()
        {
            return new ProductService(new IProductTypeHandler[]
            {
                new NormalProductHandler(),
                new SeasonalProductHandler(),
                new ExpirableProductHandler()
            });
        }

        private void ProcessProduct(Product product)
        {
            ProductNotification? notification = _service.ProcessProduct(product, DateOnly.FromDateTime(_now));
            _ = _context.SaveChanges();
            if (notification is null)
            {
                return;
            }

            switch (notification.Type)
            {
                case ProductNotificationType.Delay:
                    _notifications.Object.SendDelayNotification(notification.LeadTime!.Value, notification.ProductName!);
                    break;
                case ProductNotificationType.OutOfStock:
                    _notifications.Object.SendOutOfStockNotification(notification.ProductName!);
                    break;
                case ProductNotificationType.Expired:
                    _notifications.Object.SendExpirationNotification(
                        notification.ProductName!,
                        notification.ExpiryDate!.Value);
                    break;
            }
        }
    }
}
