namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMootRules
	{
		/// <summary>Exact named-membership delta shown before commit and copied by civic voices.</summary>
		public static string MembershipPreview(KingdomAssentingMootReceipt Receipt,
			KingdomAssentingMootRole Role, bool Add, string ResidentName)
		{
			int oldAssents = RoleCount(Receipt, KingdomAssentingMootRole.Assent);
			int oldExemptions = RoleCount(Receipt, KingdomAssentingMootRole.Exemption);
			int newAssents = oldAssents + (Role == KingdomAssentingMootRole.Assent
				? (Add ? 1 : -1) : 0);
			int newExemptions = oldExemptions + (Role == KingdomAssentingMootRole.Exemption
				? (Add ? 1 : -1) : 0);
			string action = (Add ? "Record " : "Withdraw ")
				+ (Role == KingdomAssentingMootRole.Assent ? "assent" : "exemption")
				+ " for {{W|" + (ResidentName ?? "the named resident") + "}}?";
			return action + "\n\nFacts: assents " + oldAssents + " to " + newAssents
				+ "; exemptions " + oldExemptions + " to " + newExemptions
				+ "; named-member ward strength " + StrengthFor(oldAssents, oldExemptions)
				+ " to " + StrengthFor(newAssents, newExemptions)
				+ ". The exact resident identity is retained across saves.";
		}
	}
}
