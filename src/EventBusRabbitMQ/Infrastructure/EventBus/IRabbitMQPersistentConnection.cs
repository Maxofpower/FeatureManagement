using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBusRabbitMQ.Infrastructure.EventBus
{
	public interface IRabbitMQPersistentConnection : IDisposable
	{
		bool IsConnected { get; }
		Task<IModel> CreateModelAsync(CancellationToken cancellationToken = default);
		Task<bool> TryConnectAsync(CancellationToken cancellationToken = default);
	}
}
