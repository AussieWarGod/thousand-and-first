using System;

namespace ThousandAndFirst
{
	public enum SuccessionSelectionReason
	{
		Seniority = 0,
		Chosen = 1,
		ChosenMissing = 2,
		ChosenIneligible = 3,
		ChosenAmbiguous = 4,
		ChosenAgreesWithLaw = 5,
		Groomed = 6,
		GroomedMissing = 7,
		GroomedIneligible = 8,
		GroomedAmbiguous = 9,
		GroomedUnready = 10
	}

	public readonly struct KingdomSuccessionSelection
	{
		public readonly int HeirIndex;
		public readonly int LawHeirIndex;
		public readonly HeirChoice Choice;
		public readonly bool CostsTheSeat;
		public readonly SuccessionSelectionReason Reason;

		internal KingdomSuccessionSelection(int heirIndex, int lawHeirIndex,
			HeirChoice choice, bool costsTheSeat, SuccessionSelectionReason reason)
		{
			HeirIndex = heirIndex;
			LawHeirIndex = lawHeirIndex;
			Choice = choice;
			CostsTheSeat = costsTheSeat;
			Reason = reason;
		}
	}

	public static partial class KingdomSuccessionRules
	{
		/// <summary>The chosen life may claim the Charter only after vanilla regards them as
		/// trusted. This mirrors <c>KingdomExileRules.RegardLiked</c>.</summary>
		public const int ChosenSeatReturnRegard = KingdomExileRules.RegardLiked;

		public static bool ChosenSeatMayReturn(bool SeatClimbActive, int Regard)
		{
			return !SeatClimbActive || Regard >= ChosenSeatReturnRegard;
		}

		public static string ConfigurationEventId(string RealmId, int Revision)
		{
			if (string.IsNullOrEmpty(RealmId) || Revision < 0) return null;
			string hash = SuccessionQuestHash("succession-custom", RealmId,
				Revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
			return hash == null ? null : "taf:succession:custom:v1:" + hash;
		}

		public static string ConfigurationChronicle(HeirChoice Choice, string ChosenName,
			int ChosenResidentId, bool SeatCostEnabled)
		{
			if (Choice == HeirChoice.Law)
				return "the Charter declared seniority: the longest-serving eligible resident would inherit";
			string name = string.IsNullOrWhiteSpace(ChosenName) ? "a named resident"
				: ChosenName.Trim();
			if (name.Length > 256) name = name.Substring(0, 255) + "…";
			if (Choice == HeirChoice.Groomed)
				return "the Charter nominated " + name + " (resident " + ChosenResidentId
					+ ") for service and schooling as the realm's lawful successor; seniority would answer until the preparation was proved";
			return "the Charter named " + name + " (resident " + ChosenResidentId
				+ ") to carry the next life; " + (SeatCostEnabled
					? "the senior heir would keep the Charter until the chosen life earned the realm's trust"
					: "the chosen life would also inherit the Charter");
		}

		public static string SelectionChronicle(KingdomSuccessionSelectionReceipt Receipt)
		{
			string heir = BoundSelectionName(Receipt.HeirName);
			string law = BoundSelectionName(Receipt.LawHeirName);
			if (Receipt.Choice == HeirChoice.Chosen)
				return Receipt.CostsTheSeat
					? "The founder's custom raised " + heir + " into the next life, while "
						+ law + " kept the Charter."
					: "The founder's custom raised " + heir
						+ " into the next life with the Charter.";
			if (Receipt.Choice == HeirChoice.Groomed)
				return "The realm's groomed successor " + heir
					+ " carried the next life and the Charter.";
			if (Receipt.Reason == SuccessionSelectionReason.ChosenMissing
				|| Receipt.Reason == SuccessionSelectionReason.ChosenIneligible
				|| Receipt.Reason == SuccessionSelectionReason.ChosenAmbiguous)
				return "The named identity failed its exact roll proof; seniority raised "
					+ heir + " without a chosen-seat cost.";
			if (Receipt.Reason == SuccessionSelectionReason.GroomedMissing
				|| Receipt.Reason == SuccessionSelectionReason.GroomedIneligible
				|| Receipt.Reason == SuccessionSelectionReason.GroomedAmbiguous)
				return "The groomed identity failed its exact roll proof; seniority raised "
					+ heir + ".";
			if (Receipt.Reason == SuccessionSelectionReason.GroomedUnready)
				return "The designee's preparation was unfinished; seniority raised " + heir + ".";
			return "The realm's seniority law raised " + heir + ".";
		}

		private static string BoundSelectionName(string Value)
		{
			string value = string.IsNullOrWhiteSpace(Value) ? "the heir" : Value.Trim();
			return value.Length <= 256 ? value : value.Substring(0, 255) + "…";
		}

		/// <summary>Resolves an exact configured resident against the complete two-city roll.
		/// Missing, departed, dead, or duplicate identities fall back only to seniority. Once
		/// selected, later body resolution must use <see cref="KingdomSuccessionSelection.HeirIndex"/>
		/// and may not substitute.</summary>
		public static bool TryResolveConfiguredHeir(KingdomHeir[] Candidates,
			KingdomSuccessionConfiguration Configuration,
			out KingdomSuccessionSelection Selection)
		{
			return TryResolveConfiguredHeir(Candidates, Configuration,
				default(KingdomGroomingRecord), false, out Selection);
		}

		/// <summary>Resolves the law against one optional exact grooming proof. Groomed failures
		/// are lawful seniority fallbacks and never inherit the chosen-life seat consequence.</summary>
		public static bool TryResolveConfiguredHeir(KingdomHeir[] Candidates,
			KingdomSuccessionConfiguration Configuration, KingdomGroomingRecord Grooming,
			bool HasGrooming, out KingdomSuccessionSelection Selection)
		{
			Selection = default(KingdomSuccessionSelection);
			KingdomSuccessionConfiguration proved;
			if (string.IsNullOrEmpty(KingdomSuccessionConfiguration.Encode(Configuration))
				|| !KingdomSuccessionConfiguration.TryCreate(Configuration.RealmId,
					Configuration.Choice, Configuration.ChosenResidentId,
					Configuration.SeatCostEnabled, Configuration.Revision, out proved)) return false;
			int law;
			if (!TryChooseHeir(Candidates, SuccessionLaw.Seniority, null, out law)) return false;
			if (Configuration.Choice == HeirChoice.Law)
			{
				Selection = Law(law, SuccessionSelectionReason.Seniority);
				return true;
			}
			bool groomed = Configuration.Choice == HeirChoice.Groomed;
			if (groomed && (!HasGrooming
				|| string.IsNullOrEmpty(KingdomGroomingRecord.Encode(Grooming))
				|| !string.Equals(Grooming.RealmId, Configuration.RealmId,
					StringComparison.Ordinal)
				|| Grooming.ResidentId != Configuration.ChosenResidentId))
			{
				Selection = Law(law, SuccessionSelectionReason.GroomedMissing);
				return true;
			}
			int found = -1;
			int count = 0;
			for (int i = 0; Candidates != null && i < Candidates.Length; i++)
			{
				if (Candidates[i].ResidentId == Configuration.ChosenResidentId)
				{
					found = i;
					count++;
				}
			}
			if (count == 0)
			{
				Selection = Law(law, groomed ? SuccessionSelectionReason.GroomedMissing
					: SuccessionSelectionReason.ChosenMissing);
				return true;
			}
			if (count != 1)
			{
				Selection = Law(law, groomed ? SuccessionSelectionReason.GroomedAmbiguous
					: SuccessionSelectionReason.ChosenAmbiguous);
				return true;
			}
			if (!Eligible(Candidates[found]))
			{
				Selection = Law(law, groomed ? SuccessionSelectionReason.GroomedIneligible
					: SuccessionSelectionReason.ChosenIneligible);
				return true;
			}
			if (groomed)
			{
				if (!Grooming.Ready)
				{
					Selection = Law(law, SuccessionSelectionReason.GroomedUnready);
					return true;
				}
				Selection = new KingdomSuccessionSelection(found, law, HeirChoice.Groomed,
					false, SuccessionSelectionReason.Groomed);
				return true;
			}
			if (found == law)
			{
				Selection = Law(law, SuccessionSelectionReason.ChosenAgreesWithLaw);
				return true;
			}
			Selection = new KingdomSuccessionSelection(found, law, HeirChoice.Chosen,
				CostsTheSeat(HeirChoice.Chosen, Configuration.SeatCostEnabled),
				SuccessionSelectionReason.Chosen);
			return true;
		}

		private static KingdomSuccessionSelection Law(int Index,
			SuccessionSelectionReason Reason)
		{
			return new KingdomSuccessionSelection(Index, Index, HeirChoice.Law, false, Reason);
		}
	}
}
