using FeatureManagementFilters.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Grpc.Core.Metadata;

namespace FeatureFusion.Infrastructure.EntitiyConfiguration;

class ProductEntityTypeConfiguration
	: IEntityTypeConfiguration<Product>
{
	public void Configure(EntityTypeBuilder<Product> builder)
	{
		builder.ToTable("products");
		builder.HasKey(p => p.Id);

		builder.Property(p => p.Id)
			   .ValueGeneratedOnAdd(); 

		builder.Property(ci => ci.Name);


		builder.HasIndex(ci => ci.Name);
	}
}
