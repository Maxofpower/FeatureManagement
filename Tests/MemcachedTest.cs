using System;
using System.Threading.Tasks;
using Xunit;
using Enyim.Caching;
using Microsoft.Extensions.Logging;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Enyim.Caching.Configuration;
using FeatureManagementFilters.Infrastructure.Caching;

public sealed class MemcachedCacheManagerTests : IClassFixture<MemcachedCacheManagerTests.MemcachedFixture>, IAsyncLifetime
{
	private readonly MemcachedFixture _fixture;
	private MemcachedCacheManager _cacheManager=null!;

	public MemcachedCacheManagerTests(MemcachedFixture fixture)
	{
		_fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
	}

	public async Task InitializeAsync()
	{
		_cacheManager = _fixture.CreateCacheManager();
		await _fixture.ClearCacheAsync();
	}

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task GetAsync_ShouldRetrieveData_FromMemcached()
	{
		// Arrange
		var key = new CacheKey("test-key");
		var expectedValue = "hello-world";

		await _fixture.MemcachedClient.SetAsync(key.Key, expectedValue, TimeSpan.FromMinutes(1));

		// Act
		var result = await _cacheManager.GetAsync(key, () => Task.FromResult("db-data"));

		// Assert
		Assert.Equal(expectedValue, result);
	}

	[Fact]
	public async Task GetAsync_ShouldFallbackToDB_WhenCacheMiss()
	{
		// Arrange
		var key = new CacheKey("missing-key");

		// Act
		var result = await _cacheManager.GetAsync(key, () => Task.FromResult("db-data"));

		// Assert
		Assert.Equal("db-data", result);
	}

	public class MemcachedFixture : IAsyncLifetime
	{
		private readonly IContainer _memcachedContainer;
		private MemcachedClient _memcachedClient = null!;
		private readonly ILoggerFactory _loggerFactory;

		public MemcachedClient MemcachedClient => _memcachedClient;
		public ILogger<MemcachedCacheManager> Logger { get; private set; }

		public MemcachedFixture()
		{
			_loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
			Logger = _loggerFactory.CreateLogger<MemcachedCacheManager>();

			_memcachedContainer = new ContainerBuilder()
				.WithImage("memcached:latest")
				.WithPortBinding(11211, assignRandomHostPort: true)
				.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(11211))
				.Build();
		}

		public async Task InitializeAsync()
		{
			await _memcachedContainer.StartAsync();
			var port = _memcachedContainer.GetMappedPublicPort(11211);

			var options = Options.Create(new MemcachedClientOptions
			{
				Servers = { new Server { Address = "localhost", Port = port } }
			});

			var config = new MemcachedClientConfiguration(
				_loggerFactory,
				options,
				configuration: null,
				transcoder: null,
				keyTransformer: null);

			_memcachedClient = new MemcachedClient(_loggerFactory, config);
		}

		public async Task DisposeAsync()
		{
			_memcachedClient?.Dispose();
			await _memcachedContainer.DisposeAsync();
		}

		public MemcachedCacheManager CreateCacheManager() =>
			new MemcachedCacheManager(_memcachedClient, Logger);

		public async Task ClearCacheAsync() =>
			await _memcachedClient.FlushAllAsync();
	}
}