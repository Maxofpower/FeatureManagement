namespace FeatureFusion.Features.Order.Types
{
	public class Result<T>
	{
		public bool IsSuccess { get; }
		public string ErrorMessage { get; }
		public T Data { get; }

		private Result(T data, bool isSuccess, string errorMessage = null)
		{
			Data = data;
			IsSuccess = isSuccess;
			ErrorMessage = errorMessage;
		}
		public static Result<T> Success(T data)
		{
			return new Result<T>(data, true);
		}
		public static Task<Result<T>> Failure(string errorMessage)
		{
			return Task.FromResult(new Result<T>(default, false, errorMessage));
		}
	}

}
