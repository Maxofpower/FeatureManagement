using FeatureFusion.Infrastructure.CQRS;
using FeatureManagementFilters.Models;
using static FeatureFusion.Controllers.V2.OrderController;
using FeatureFusion.Models;
using static FeatureFusion.Features.Order.Commands.CreateOrderCommandHandler;
using FeatureFusion.Features.Order.Types;

namespace FeatureFusion.Features.Order.Commands
{
	public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<OrderResponse>>
	{
		public Task<Result<OrderResponse>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
		{
			
			// Static in-memory product
			var product = new Product
			{
				Id = 12345,
				Name = "Smartphone",
				Published = true,
				Deleted = false,
				VisibleIndividually = true,
				Price = 599.99m
			};

			// Static in-memory customer
			var customer = new Person
			{
				Name = "John Doe",
				Age = 11111,
			};

			var orderId = Ulid.NewUlid();

			var orderTotal = product.Price * request.Quantity;

			var response = new OrderResponse {
				OrderId = orderId,
				CustomerName = customer.Name,
				ProductName = product.Name,
				Quantity = request.Quantity,
				TotalAmount = orderTotal,
				OrderDate = DateTime.UtcNow,
				Message = "Order created successfully."
			};
			return Result<OrderResponse>.Success(response);
			
		}
	
	public class OrderResponse
		{
			public Ulid OrderId { get; set; }
			public string CustomerName { get; set; }
			public string ProductName { get; set; }
			public int Quantity { get; set; }
			public decimal TotalAmount { get; set; }
			public DateTime OrderDate { get; set; }
			public string Message { get; set; }
		}
	}
}
