namespace FeatureManagementFilters.Models
{
	public class User
	{
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public string Role { get; set; } = "User"; // Default role
		public bool HasActiveSubscription { get; set; } = false;
		public bool IsBetaTester { get; set; } = false;

		public User(string role, bool hasSubscription, bool isBetaTester)
		{
			Role = role;
			HasActiveSubscription = hasSubscription;
			IsBetaTester = isBetaTester;
		}
	}

}
