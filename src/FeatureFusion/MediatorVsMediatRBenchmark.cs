using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FeatureFusion.Infrastructure.CQRS;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace MediatorBenchmarks
{
	public class Program
	{
		public static void Main(string[] args)
		{
			BenchmarkRunner.Run<MediatorBenchmark>();
		}
	}

	[MemoryDiagnoser]
	[ThreadingDiagnoser]
	public class MediatorBenchmark
	{
		private IServiceProvider _customMediatorServices;
		private IServiceProvider _mediatRServices;
		private IMediator _mediatR;
		private IMediator _customMediator;
		private readonly SampleCommand _command = new SampleCommand();
		private const int ConcurrentRequests = 100;

		[GlobalSetup]
		public void Setup()
		{
			// Setup Custom Mediator
			var customServices = new ServiceCollection();
			customServices.AddSingleton<IMediator, CustomMediator>();
			customServices.AddSingleton<IRequestHandler<SampleCommand, SampleResponse>, SampleCommandHandler>();
			customServices.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
			_customMediatorServices = customServices.BuildServiceProvider();
			_customMediator = _customMediatorServices.GetRequiredService<IMediator>();

			// Setup MediatR
			var mediatRServices = new ServiceCollection();
			mediatRServices.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
			mediatRServices.AddTransient(typeof(IPipelineBehavior<,>), typeof(MediatRLoggingBehavior<,>));
			_mediatRServices = mediatRServices.BuildServiceProvider();
			_mediatR = _mediatRServices.GetRequiredService<IMediator>();
		}

		[Benchmark(Baseline = true)]
		public async Task CustomMediator_100ConcurrentRequests()
		{
			await ExecuteConcurrentRequests(_customMediator);
		}

		[Benchmark]
		public async Task MediatR_100ConcurrentRequests()
		{
			await ExecuteConcurrentRequests(_mediatR);
		}

		private async Task ExecuteConcurrentRequests(IMediator mediator)
		{
			var tasks = new Task[ConcurrentRequests];
			for (int i = 0; i < ConcurrentRequests; i++)
			{
				tasks[i] = Task.Run(async () =>
				{
					var response = await mediator.Send(_command);
					if (response == null) throw new Exception("Null response");
				});
			}
			await Task.WhenAll(tasks);
		}

		// Sample command and handler
		public class SampleCommand : IRequest<SampleResponse> { }
		public class SampleResponse { }

		public class SampleCommandHandler :
			IRequestHandler<SampleCommand, SampleResponse>,
			MediatR.IRequestHandler<SampleCommand, SampleResponse>
		{
			public Task<SampleResponse> Handle(SampleCommand request, CancellationToken cancellationToken)
			{
				return Task.FromResult(new SampleResponse());
			}

			Task<SampleResponse> MediatR.IRequestHandler<SampleCommand, SampleResponse>.Handle(
				SampleCommand request, CancellationToken cancellationToken)
			{
				return Task.FromResult(new SampleResponse());
			}
		}

		// Custom Mediator pipeline behavior
		public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
			where TRequest : IRequest<TResponse>
		{
			public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
			{
				// Simulate logging
				await Task.Delay(1);
				return await next();
			}
		}

		// MediatR pipeline behavior
		public class MediatRLoggingBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
			where TRequest : MediatR.IRequest<TResponse>
		{
			public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
			{
				// Simulate logging
				await Task.Delay(1);
				return await next();
			}
		}
	}
}