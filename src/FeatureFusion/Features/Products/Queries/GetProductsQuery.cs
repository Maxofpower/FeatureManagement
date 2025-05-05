﻿using FeatureFusion.Dtos;
using FeatureFusion.Infrastructure.CQRS;
using FeatureFusion.Infrastructure.CursorPagination;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FeatureFusion.Features.Products.Queries
{
	public sealed record GetProductsQuery : IRequest<Result<PagedResult<ProductDto>>>
	{
		[SwaggerParameter(Description = "Maximum number of items to return")]
		[Range(1, 100)]
		public int Limit { get; init; } = 20;

		[SwaggerParameter(Required = false, Description = "Pagination cursor")]
		public string Cursor { get; init; } = string.Empty;

		[SwaggerParameter(Description = "Field to sort by")]
		public ProductSortField SortBy { get; init; } = ProductSortField.Id;

		[SwaggerParameter(Description = "Sort direction")]
		public SortDirection SortDirection { get; init; } = SortDirection.Ascending;
	}

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum ProductSortField
	{
		Id,
		Name,
		Price,
		CreatedAt
	}

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum SortDirection
	{
		Ascending,
		Descending
	}
}
