using Microsoft.AspNetCore.Mvc;
using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Controllers.Dtos.Product;
using Refacto.DotNet.Domain.Entities;

namespace Refacto.DotNet.Controllers.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderRepository _orders;

        public OrdersController(
            IOrderProcessingService orderProcessingService,
            IOrderRepository orders)
        {
            _orderProcessingService = orderProcessingService;
            _orders = orders;
        }

        [HttpPost("{orderId}/processOrder")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProcessOrderResponse>> ProcessOrder(
            long orderId,
            CancellationToken cancellationToken)
        {
            Order? order = await _orders.GetWithProductsAsync(orderId, cancellationToken);
            if (order is null)
            {
                return NotFound();
            }

            await _orderProcessingService.ProcessOrderAsync(order, cancellationToken);
            return new ProcessOrderResponse(order.Id);
        }
    }
}
