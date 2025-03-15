using FeatureManagementFilters.Models;

namespace FeatureManagementFilters.Pipeline;

public class ValidationPipeline
{
	private Func<User, Task<bool>> _pipeline = user => Task.FromResult(true); // Default: Always passes

	public void AddRule(Func<User, Task<bool>> rule)
	{
		var previous = _pipeline;
		_pipeline = async user => await previous(user) && await rule(user); // Chain with AND condition
	}

	public async Task<bool> ValidateAsync(User user) => await _pipeline(user);
}