using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// Reads one <c>&lt;building&gt;</c> entry's optional upgrade attributes. Every attribute
		/// is optional and an entry with none of them yields an undefined chain, which is what
		/// every design that shipped before this system existed has and why none of them changed
		/// behaviour.
		/// <para>
		/// Hostile input disables itself rather than half-registering: an attribute that names a
		/// cost, a time, a crew, or a stage without naming anything to grow into is an error, not
		/// a chain with a default successor, because guessing at the successor would improve a
		/// building into something its author never wrote.
		/// </para>
		/// </summary>
		/// <param name="Key">The design's own key, for the error text and the self-reference
		/// check.</param>
		/// <param name="UpgradesTo">Registry key of the successor design, or null.</param>
		/// <param name="UpgradeCost">Drams to charge, or null to compute.</param>
		/// <param name="UpgradeTicks">Ticks to take, or null to compute.</param>
		/// <param name="UpgradeCrew">Free hands to require, or null to compute.</param>
		/// <param name="UpgradeMinStage">Stage to wait for, or null to inherit the successor's.
		/// </param>
		/// <param name="Chain">The parsed chain. Undefined, never null, when this returns true
		/// for an entry that named no successor.</param>
		/// <param name="Error">Set to a log-facing reason when this returns false. The chain is
		/// null then, and the caller must register nothing.</param>
		/// <returns>False only for malformed input.</returns>
		public static bool TryParseUpgradeAttributes(string Key, string UpgradesTo, string UpgradeCost, string UpgradeTicks, string UpgradeCrew, string UpgradeMinStage, out UpgradeChain Chain, out string Error)
		{
			Chain = null;
			Error = null;
			string key = string.IsNullOrEmpty(Key) ? "(unnamed)" : Key;
			bool namesSomething = !string.IsNullOrEmpty(UpgradeCost) || !string.IsNullOrEmpty(UpgradeTicks)
				|| !string.IsNullOrEmpty(UpgradeCrew) || !string.IsNullOrEmpty(UpgradeMinStage);
			if (string.IsNullOrEmpty(UpgradesTo))
			{
				if (namesSomething)
				{
					Error = "building " + key + " has upgrade attributes but no UpgradesTo";
					return false;
				}
				Chain = new UpgradeChain();
				return true;
			}
			if (UpgradesTo == Key)
			{
				Error = "building " + key + " upgrades into itself";
				return false;
			}
			int cost = Unset;
			if (!string.IsNullOrEmpty(UpgradeCost) && (!int.TryParse(UpgradeCost, out cost) || cost < 0))
			{
				Error = "building " + key + " has a bad UpgradeCost";
				return false;
			}
			long ticks = UnsetTicks;
			if (!string.IsNullOrEmpty(UpgradeTicks) && (!long.TryParse(UpgradeTicks, out ticks) || ticks <= 0L))
			{
				Error = "building " + key + " has a bad UpgradeTicks";
				return false;
			}
			int crew = Unset;
			if (!string.IsNullOrEmpty(UpgradeCrew) && (!int.TryParse(UpgradeCrew, out crew) || crew < 0))
			{
				Error = "building " + key + " has a bad UpgradeCrew";
				return false;
			}
			GrowthStage stage = GrowthStage.Camp;
			bool hasStage = !string.IsNullOrEmpty(UpgradeMinStage);
			if (hasStage && (!System.Enum.TryParse<GrowthStage>(UpgradeMinStage, ignoreCase: true, out stage) || !KingdomRules.IsKnownStage(stage)))
			{
				// Enum.TryParse takes any number the underlying type can hold, so "7" parses into
				// a stage no settlement reaches; StageRequired would then gate the improvement
				// above every real stage and the founder would be told to wait for a city forever.
				Error = "building " + key + " has a bad UpgradeMinStage";
				return false;
			}
			Chain = new UpgradeChain
			{
				SuccessorKey = UpgradesTo,
				CostDramsOverride = (string.IsNullOrEmpty(UpgradeCost) ? Unset : cost),
				BuildTicksOverride = (string.IsNullOrEmpty(UpgradeTicks) ? UnsetTicks : ticks),
				CrewOverride = (string.IsNullOrEmpty(UpgradeCrew) ? Unset : crew),
				HasMinStageOverride = hasStage,
				MinStageOverride = stage
			};
			return true;
		}
	}
}
