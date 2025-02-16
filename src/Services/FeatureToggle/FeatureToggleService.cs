
using FeatureManagementFilters.Models;
using FeatureManagementFilters.Pipeline;
using Microsoft.FeatureManagement;

namespace FeatureManagementFilters.Services.FeatureToggleService
{
	public class FeatureToggleService : IFeatureToggleService
	{
		private readonly ValidationPipeline _validateRules ;
		private readonly IFeatureManagerSnapshot _featureManager;

		public FeatureToggleService(IFeatureManagerSnapshot featureManager)
		{
			_validateRules = new ValidationPipeline();
			_featureManager = featureManager;		
		}

		public async Task<bool> CanAccessFeatureAsync(User user)
		{
			_validateRules.AddRule(user => Task.FromResult(user.Role == "Amin"));
			_validateRules.AddRule(user => Task.FromResult(user.HasActiveSubscription));
			_validateRules.AddRule(async user => await _featureManager.IsEnabledAsync("CustomGreeting"));

			return await _featureManager.IsEnabledAsync("CustomGreeting") && await _validateRules.ValidateAsync(user);
		}
	}
	public interface IFeatureToggleService
	{
		Task<bool> CanAccessFeatureAsync(User user);
	}
}
