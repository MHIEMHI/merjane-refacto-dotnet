using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Controllers.Dtos.Product;
using Refacto.DotNet.Domain.Entities;
using Refacto.DotNet.Infrastructure.Persistence;

namespace Refacto.Dotnet.Controllers.Tests.Controllers
{
    [Collection("Sequential")]
    public class MyControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly AppDbContext _context;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private static readonly DateTime TestNow = new(2025, 5, 10, 14, 30, 0);

        public MyControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockDateTimeProvider.SetupGet(provider => provider.Now).Returns(TestNow);

            _factory = factory.WithWebHostBuilder(builder =>
            {
                _ = builder.ConfigureServices(services =>
                {
                    _ = services.RemoveAll<IDateTimeProvider>();
                    _ = services.AddSingleton(_mockDateTimeProvider.Object);
                    _ = services.AddSingleton(_mockNotificationService.Object);

                    ServiceDescriptor? descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                    {
                        _ = services.Remove(descriptor);
                    }

                    // Add ApplicationDbContext using an in-memory database for testing
                    _ = services.AddDbContext<AppDbContext>(options =>
                    {
                        _ = options.UseInMemoryDatabase($"InMemoryDbForTesting-{GetType()}");
                    });
                });
            });

            IServiceScope scope = _factory.Services.CreateScope();
            _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
        }

        [Fact]
        public async Task ProcessOrderShouldReturn()
        {
            HttpClient client = _factory.CreateClient();

            List<Product> allProducts = CreateProducts();
            DateTime milkExpiryDate = allProducts.Single(p => p.Name == "Milk").ExpiryDate!.Value;
            HashSet<Product> orderItems = new(allProducts);
            Order order = CreateOrder(orderItems);
            await _context.Products.AddRangeAsync(allProducts);
            _ = await _context.Orders.AddAsync(order);
            _ = await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            HttpResponseMessage response = await client.PostAsync($"/orders/{order.Id}/processOrder", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            ProcessOrderResponse? responseBody = await response.Content.ReadFromJsonAsync<ProcessOrderResponse>();
            Assert.NotNull(responseBody);
            Assert.Equal(order.Id, responseBody?.id);

            foreach (Product product in allProducts)
            {
                int expectedAvailability = product.Name switch
                {
                    "USB Cable" or "Butter" or "Watermelon" => product.Available - 1,
                    "Milk" => 0,
                    _ => product.Available
                };

                Product persistedProduct = await _context.Products.AsNoTracking()
                    .SingleAsync(p => p.Id == product.Id);
                Assert.Equal(expectedAvailability, persistedProduct.Available);
            }

            _mockNotificationService.Verify(
                service => service.SendDelayNotification(10, "USB Dongle"),
                Times.Once);
            _mockNotificationService.Verify(
                service => service.SendOutOfStockNotification("Grapes"),
                Times.Once);
            _mockNotificationService.Verify(
                service => service.SendExpirationNotification(
                    "Milk",
                    milkExpiryDate),
                Times.Once);
        }

        [Fact]
        public async Task ProcessOrderForMissingOrderReturnsNotFound()
        {
            HttpClient client = _factory.CreateClient();

            HttpResponseMessage response = await client.PostAsync("/orders/-1/processOrder", null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ProcessOrderWithNonNumericIdReturnsBadRequest()
        {
            HttpClient client = _factory.CreateClient();

            HttpResponseMessage response = await client.PostAsync("/orders/not-a-number/processOrder", null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ProcessOrderTreatsSeasonEndAndExpiryAsDateOnlyBoundaries()
        {
            HttpClient client = _factory.CreateClient();
            Product endingSeason = new()
            {
                Available = 2,
                Type = "SEASONAL",
                Name = "Season ends today",
                SeasonStartDate = TestNow.Date.AddDays(-1).AddHours(23),
                SeasonEndDate = TestNow.Date.AddHours(23).AddMinutes(59)
            };
            Product expiring = new()
            {
                Available = 2,
                Type = "EXPIRABLE",
                Name = "Expires today",
                ExpiryDate = TestNow.Date.AddHours(23).AddMinutes(59)
            };
            Product startsToday = new()
            {
                Available = 2,
                LeadTime = 0,
                Type = "SEASONAL",
                Name = "Season starts today",
                SeasonStartDate = TestNow.Date.AddHours(23).AddMinutes(59),
                SeasonEndDate = TestNow.Date.AddDays(10)
            };
            Order order = CreateOrder(new HashSet<Product> { endingSeason, expiring, startsToday });
            await _context.Products.AddRangeAsync(endingSeason, expiring, startsToday);
            _ = await _context.Orders.AddAsync(order);
            _ = await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            HttpResponseMessage response = await client.PostAsync($"/orders/{order.Id}/processOrder", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await ReadProduct(endingSeason.Id)).Available);
            Assert.Equal(0, (await ReadProduct(expiring.Id)).Available);
            Assert.Equal(2, (await ReadProduct(startsToday.Id)).Available);
            _mockNotificationService.Verify(
                service => service.SendOutOfStockNotification("Season ends today"),
                Times.Once);
            _mockNotificationService.Verify(
                service => service.SendExpirationNotification("Expires today", expiring.ExpiryDate!.Value),
                Times.Once);
            _mockNotificationService.Verify(
                service => service.SendDelayNotification(0, "Season starts today"),
                Times.Once);
        }

        private async Task<Product> ReadProduct(long id)
        {
            return await _context.Products.AsNoTracking().SingleAsync(product => product.Id == id);
        }

        private static Order CreateOrder(HashSet<Product> products)
        {
            return new Order { Items = products };
        }

        private static List<Product> CreateProducts()
        {
            return new List<Product>
            {
                new Product { LeadTime = 15, Available = 30, Type = "NORMAL", Name = "USB Cable" },
                new Product { LeadTime = 10, Available = 0, Type = "NORMAL", Name = "USB Dongle" },
                new Product { LeadTime = 15, Available = 30, Type = "EXPIRABLE", Name = "Butter", ExpiryDate = TestNow.AddDays(26) },
                new Product { LeadTime = 90, Available = 6, Type = "EXPIRABLE", Name = "Milk", ExpiryDate = TestNow.AddDays(-2) },
                new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Watermelon", SeasonStartDate = TestNow.AddDays(-2), SeasonEndDate = TestNow.AddDays(58) },
                new Product { LeadTime = 15, Available = 30, Type = "SEASONAL", Name = "Grapes", SeasonStartDate = TestNow.AddDays(180), SeasonEndDate = TestNow.AddDays(240) }
            };
        }
    }
}
