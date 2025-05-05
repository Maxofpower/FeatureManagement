using FeatureFusion.Infrastructure.CQRS;
using static FeatureFusion.Controllers.V2.OrderController;
using static FeatureFusion.Features.Orders.Commands.CreateOrderCommandHandler;

namespace FeatureFusion.Features.Order.Queries
{
	public record GetOrderQuery(Ulid OrderId) : IRequest<OrderResponse>;
}
