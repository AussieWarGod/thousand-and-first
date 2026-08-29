using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Registers the capital's visualization-only, hosted Great Archive.</summary>
	public static partial class KingdomGreatArchive
	{
		public const string LotKey = "greatarchive";
		public const string ViewKey = "greatarchive";
		private static bool registrationAttempted;

		public static void EnsureRegistered()
		{
			if (registrationAttempted) return;
			registrationAttempted = true;
			string failure;
			if (!KingdomHostedArcologyRules.RegisterHostedLot(
				new KingdomHostedLotDefinition {
					Key = LotKey, DisplayName = "great archive",
					InteriorCell = "TAFGreatArchive", ReadOnly = true,
					KnowledgeView = ViewKey
				}, out failure))
			{
				KingdomLog.Log("great archive: registration refused (" + failure + ")");
				return;
			}
			if (!KingdomHostedArcology.RegisterKnowledgeView(ViewKey, Eligible, Draw,
				out failure))
				KingdomLog.Log("great archive: view registration refused (" + failure + ")");
		}

		private static bool Eligible(KingdomSystem System, Zone HostZone,
			GameObject HostRoot, out string Refusal)
		{
			Refusal = null;
			if (System == null || !System.Founded || HostZone == null
				|| !GameObject.Validate(HostRoot) || HostRoot.CurrentZone != HostZone
				|| KingdomUpgrade.DesignKeyOf(HostRoot) != KingdomHostedArcology.ArcologyKey
				|| System.City == null || System.City.SettlementId !=
					System.SettlementIdForOwnedZone(HostZone.ZoneID)
				|| !KingdomHostedArcology.Operational(HostRoot))
			{
				Refusal = "The great archive opens only in the exact crowned arcology of the capital.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(HostZone, System);
			bool shelf = false;
			bool press = false;
			for (int i = 0; i < survey.Built.Count; i++)
			{
				GameObject work = survey.Built[i];
				if (KingdomUpgrade.DesignKeyOf(work) == "bookshelf") shelf = true;
				if (work.GetStringProperty(KingdomYards.YardKeyProperty) == "vellumpress")
					press = true;
			}
			if (shelf && press) return true;
			Refusal = !shelf && !press
				? "The capital needs its keeper's shelf and a household's vellum press before the great archive can be read."
				: !shelf ? "The capital needs its keeper's shelf before the great archive can be read."
				: "The capital needs a household's vellum press before the great archive can be read.";
			return false;
		}
	}
}
