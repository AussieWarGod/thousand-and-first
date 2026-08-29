namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>Real present-tense requirements measured before an improvement begins.</summary>
		public struct ImprovementDemand
		{
			/// <summary>Whether the stockpiles cover the improvement's material.</summary>
			public bool MaterialsInHand;

			/// <summary>Whether the settlement's craft and learning reach the successor.</summary>
			public bool CraftMet;

			/// <summary>Exact missing knowledge or technology name from zoning.</summary>
			public string CraftDetail;

			/// <summary>True for a Knowledge refusal; false for a MinTech refusal.</summary>
			public bool KnowledgeMissing;

			/// <summary>Fail-open compatibility value for callers with no measurement.</summary>
			public static ImprovementDemand None
			{
				get
				{
					ImprovementDemand none = default(ImprovementDemand);
					none.MaterialsInHand = true;
					none.CraftMet = true;
					return none;
				}
			}
		}
	}
}
