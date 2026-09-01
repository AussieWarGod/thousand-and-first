using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConversion
	{
		private static bool Transition(KingdomSystem System, Zone Z, GameObject Settler,
			string Creed, ConversionChannel Channel, string GovernanceVerb, bool theological)
		{
			string was = Settler.GetStringProperty(KingdomCreed.CreedProperty);
			if (was == Creed) return false;
			string roll = RollNameOf(Settler);
			string named = string.IsNullOrEmpty(roll) ? Settler.BaseDisplayNameStripped : roll;
			int hostility = KingdomCreed.HostilityBetween(was, Creed);
			KingdomCreed.Forget(System, Settler);
			if (!string.IsNullOrEmpty(GovernanceVerb) && !KingdomGovernanceScope.HasCommitted)
				KingdomGovernanceScope.Commit(GovernanceVerb);
			KingdomCreed.Record(System, Settler, Creed);
			KingdomCreed.RememberPast(System, Settler, was);
			if (roll != null)
			{
				System.ConversionShared.Remove(roll);
				System.ConversionToward.Remove(roll);
				System.ConversionResented.Remove(roll);
			}
			KingdomBrink.Lift(Settler, BrinkKind.Creed);
			string affiliation = KingdomCreed.CreedName(Creed);
			string shownName = KingdomPresentation.Rich(named);
			string telling = theological
				? KingdomConversionRules.ConversionTelling(Channel, shownName, affiliation)
				: KingdomCreedKindRules.AdoptionTelling(shownName, affiliation);
			string rumour = theological
				? KingdomConversionRules.ConversionRumour(Channel, shownName, affiliation)
				: KingdomCreedKindRules.AdoptionRumour(shownName, affiliation);
			if (KingdomConversionRules.Contested(hostility))
				KingdomChronicle.RecordDisputed(System, telling, rumour);
			else KingdomChronicle.Record(System, telling);
			string note = theological
				? KingdomConversionRules.ConversionNote(shownName, affiliation)
				: KingdomCreedKindRules.AdoptionNote(shownName, affiliation);
			System.Ledger.Note("{{G|" + note + "}}");
			KingdomLog.Log((theological ? "conversion: " : "affiliation adoption: ") + named
				+ " " + (string.IsNullOrEmpty(was) ? "(none)" : was) + " -> " + Creed
				+ " via " + Channel + " hostility=" + hostility);
			return true;
		}
	}
}
