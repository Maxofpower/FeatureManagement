using FeatureFusion.Infrastructure.CQRS;
using FeatureManagementFilters.Models;
using static FeatureFusion.Controllers.V2.OrderController;
using static FeatureFusion.Features.Orders.Commands.CreateOrderCommandHandler;
using StackExchange.Redis;
using FeatureFusion.Domain.Entities;
namespace FeatureFusion.Features.Orders.Commands
{
	public class CreateOrderCommandVoidHandler : IRequestHandler<CreateOrderCommandVoid>
	{
		public Task Handle(CreateOrderCommandVoid request, CancellationToken cancellationToken)
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

			var response = new OrderResponse
			{
				OrderId = orderId,
				CustomerName = customer.Name,
				ProductName = product.Name,
				Quantity = request.Quantity,
				TotalAmount = orderTotal,
				OrderDate = DateTime.UtcNow,
				Message = "Order created successfully."
			};
			return Task.CompletedTask;

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
