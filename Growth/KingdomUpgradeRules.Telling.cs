using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// The dip, disclosed in full before the founder consents to it. Names the rate, the
		/// duration of the LABOUR, the whole loss, and how far past the reserve it goes &mdash;
		/// because a founder who forces this is spending the settlement's cushion and must be able
		/// to see how much of it.
		/// </summary>
		/// <param name="PredecessorName">What is standing there now.</param>
		/// <param name="SuccessorName">What it would become.</param>
		/// <param name="SupportPerDay">Drams a day the work sustains.</param>
		/// <param name="BuildTicks">The improvement's build time.</param>
		/// <param name="Margin">From <see cref="AbsorptionMargin"/>; negative here.</param>
		public static string DipLine(string PredecessorName, string SuccessorName, int SupportPerDay, long BuildTicks, int Margin)
		{
			string predecessor = string.IsNullOrEmpty(PredecessorName) ? "work" : PredecessorName;
			string successor = string.IsNullOrEmpty(SuccessorName) ? "something better" : SuccessorName;
			int days = BuildDays(BuildTicks);
			int lost = OutputLost(SupportPerDay, BuildTicks);
			int under = (Margin < 0) ? (-Margin) : 0;
			return "Raising the " + predecessor + " into " + Article(successor) + " takes " + SupportPerDay
				+ " " + ((SupportPerDay == 1) ? "dram" : "drams") + " a day off the settlement's books for "
				+ days + " " + ((days == 1) ? "day" : "days") + " -- " + lost + " "
				+ ((lost == 1) ? "dram" : "drams") + " in all, and " + under + " "
				+ ((under == 1) ? "dram" : "drams") + " further into the reserve than the stores can carry.";
		}

		/// <summary>The line the founder reads once they have forced a held offer, so the ledger
		/// and the chronicle both record that this was a decision and not the settlement's own
		/// judgement.</summary>
		public static string ForcedLine(string PredecessorName, string SuccessorName, int Margin)
		{
			string predecessor = string.IsNullOrEmpty(PredecessorName) ? "work" : PredecessorName;
			string successor = string.IsNullOrEmpty(SuccessorName) ? "something better" : SuccessorName;
			int under = (Margin < 0) ? (-Margin) : 0;
			return "The " + predecessor + " is being raised into " + Article(successor)
				+ " on your word, and the settlement will go " + under + " "
				+ ((under == 1) ? "dram" : "drams") + " into its reserve to do it.";
		}

		/// <summary>Reported when a blueprint declares no liquid capacity, which is different
		/// from declaring a capacity of nothing. Negative because Qud's own open pools use a
		/// negative MaxVolume for "unbounded", and neither case is a reason to refuse.</summary>
		public const int UnknownCapacity = -1;

	}
}
