using System;

namespace ThousandAndFirst
{
	/// <summary>Bounded read model of one exact assenting-moot receipt; never policy authority.</summary>
	public readonly struct KingdomDecisionTagView
	{
		public readonly int Version;
		public readonly int SourceVersion;
		public readonly string SourceId;
		public readonly string MembershipFingerprint;
		public readonly int Assents;
		public readonly int Exemptions;

		internal KingdomDecisionTagView(string sourceId, string fingerprint,
			int assents, int exemptions)
		{
			Version = 1; SourceVersion = KingdomAssentingMootRules.CurrentReceiptVersion;
			SourceId = sourceId; MembershipFingerprint = fingerprint;
			Assents = assents; Exemptions = exemptions;
		}
	}

	public static class KingdomDecisionTagRules
	{
		public static bool TryDerive(KingdomAssentingMootReceipt Receipt,
			out KingdomDecisionTagView View)
		{
			View = default(KingdomDecisionTagView);
			if (Receipt == null) return false;
			KingdomAssentingMootReceipt copy = Receipt.Copy();
			if (!KingdomAssentingMootRules.Validate(copy, out string _)
				|| copy.Phase == KingdomAssentingMootPhase.None
				|| copy.Phase == KingdomAssentingMootPhase.Quarantined
				|| copy.AssentResidentIds.Count + copy.ExemptResidentIds.Count == 0)
				return false;
			View = new KingdomDecisionTagView(copy.AuthorityId, copy.MembershipFingerprint,
				copy.AssentResidentIds.Count, copy.ExemptResidentIds.Count);
			return true;
		}

		public static string CreedScene(KingdomAssentingMootReceipt Receipt)
		{
			return TryDerive(Receipt, out KingdomDecisionTagView tag)
				? "At the declaration table, the moot's exact " + tag.Assents
					+ " assents and " + tag.Exemptions
					+ " exemptions are recalled. They do not decide this declaration."
				: "";
		}

		public static string CovenantScene(KingdomAssentingMootReceipt Receipt)
		{
			return TryDerive(Receipt, out KingdomDecisionTagView tag)
				? "At the basin, the moot's exact " + tag.Assents + " assents and "
					+ tag.Exemptions
					+ " exemptions are recalled. They do not decide this covenant."
				: "";
		}
	}
}
