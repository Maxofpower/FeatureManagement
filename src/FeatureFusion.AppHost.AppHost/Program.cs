using Aspire.Hosting;
using FeatureFusion.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aspire.StackExchange.Redis;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddForwardedHeaders();

var memcached= builder.AddContainer("cache", "memcached", "alpine")
	.WithEndpoint( 11211, targetPort: 11211, name: "memcached");

var redis = builder.AddRedis("redis")
	   .WithEndpoint(6379, targetPort: 6379, name: "redis")
	   .WithDataVolume("redis_data")
		.WithPersistence(
					   interval: TimeSpan.FromMinutes(5),
					   keysChangedThreshold: 100)
	   .WithRedisInsight()
	   .WithRedisCommander();

var rabbitMq = builder.AddRabbitMQ("eventbus")
	.WithEnvironment("RABBITMQ_LOGS", "-")
	.WithVolume("rabbitmq-data", "/var/lib/rabbitmq")
	.WithEndpoint(5672, targetPort: 5672, name: "amqp") 
	.WithEndpoint(15672 ,targetPort: 15672, name: "management")
    .WithLifetime(ContainerLifetime.Persistent);


var username = builder.AddParameter("username", secret: true, value: "username");
var password = builder.AddParameter("password", secret: true, value: "password");
var postgres = builder.AddPostgres(name:"postgres", userName:username, password:password)
	 .WithPgAdmin(container =>
	 {
		 container.WithEnvironment("PGADMIN_DEFAULT_EMAIL", "guest@admin.com");
		 container.WithEnvironment("PGADMIN_DEFAULT_PASSWORD", "guest");
	 })
	.WithLifetime(ContainerLifetime.Persistent)
	.WithEndpoint (5432, targetPort: 5432, name: "postgres");



var catalogDb = postgres.AddDatabase("catalogdb");

builder.AddProject<Projects.FeatureFusion>("featurefusion")
	   .WithEndpoint(7762, targetPort: 5002, scheme: "https", name: "featurefusion-https")
	   .WaitFor(memcached)
	   .WaitFor(redis)
	   .WithReference(rabbitMq)
	   .WaitFor(rabbitMq)
	   .WaitFor(postgres)

	  .WithReference(catalogDb);

builder.Build().Run();



