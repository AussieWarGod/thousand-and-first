using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomZoning
	{
		/// <summary>
		/// Composes a refusal in the settlement's own voice. Every branch names the lack AND the
		/// act that lifts it &mdash; the whole point of the gate is to teach the founder what the
		/// realm is short of, and a refusal that only says "no" teaches nothing.
		/// </summary>
		private static string Refusal(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry, ZoningJudgement Judgement)
		{
			string seat = (System != null) ? System.SeatName : "the settlement";
			string name = (Entry != null) ? Entry.Name : "that";
			switch (Judgement.Verdict)
			{
			case ZoningVerdict.RefusedUnlearned:
				return "Nobody at " + seat + " knows how to raise " + XRL.Language.Grammar.A(name) + ". It wants {{C|" + Judgement.Detail
					+ "}}. Teach the keepers from a data disk you carry, certify one hauled home, or take in people who already do the work.";
			case ZoningVerdict.RefusedTechLevel:
				return seat + " builds at the level of {{C|" + KingdomZoningRules.TechName(Tech(System)) + "}}, and "
					+ XRL.Language.Grammar.A(name) + " wants {{C|" + Judgement.Detail
					+ "}}. Teach the keepers more designs and certify more machines hauled home; the craft rises with the work, not with the asking.";
			case ZoningVerdict.RefusedTerritory:
				return XRL.Language.Grammar.A(name) + " wants a realm of at least {{C|" + Judgement.Detail + "}}, and " + seat
					+ " holds {{C|" + ((System != null && System.ClaimedZones != null) ? System.ClaimedZones.Count : 0) + "}}. Claim more ground and ask again.";
			case ZoningVerdict.RefusedStratum:
				// One verdict, two refusals, and they want different sentences: the weather is a
				// fact about the rock and the set is a fact about the catalogue. Asked in the same
				// order Judge asks them, so the words always match the reason.
				if (!KingdomZoningRules.StratumAccepts(StratumOf(ZoneID), WantsSky(Entry)))
				{
					return XRL.Language.Grammar.A(name) + " wants weather — sun, wind, or rain — and there is none under the rock. Raise it on ground under {{C|"
						+ Judgement.Detail + "}}.";
				}
				return XRL.Language.Grammar.A(name) + " belongs to {{C|" + Judgement.Detail + "}}, and this ground is {{C|"
					+ KingdomZoningRules.StratumName(KingdomZoningRules.StratumOfGround(StratumOf(ZoneID)))
					+ "}}. Claim ground there and raise it there — a claim reaches the stratum directly above or below the one you hold.";
			case ZoningVerdict.RefusedUnaligned:
				return "Nobody at " + seat + " holds with {{C|" + KingdomCreed.CreedName(Judgement.Detail) + "}}, and nobody here ever has. "
					+ XRL.Language.Grammar.A(name) + " is raised by people who believe it, or who once did. Take in people who hold with them, or let the creed spread here.";
			case ZoningVerdict.RefusedCreedShare:
			{
				string creed = KingdomCreed.CreedName(Judgement.Detail);
				int holding = (System != null && System.CreedCounts != null && System.CreedCounts.TryGetValue(Judgement.Detail, out var held)) ? held : 0;
				int people = (System != null) ? System.Population : 0;
				int wanted = (Entry != null) ? GateFor(Entry.Key).EffectiveCreedShare : KingdomCreedRules.DominantSharePercent;
				return XRL.Language.Grammar.A(name) + " wants {{C|" + wanted + "%}} of the city holding with {{C|" + creed
					+ "}}, and " + seat + " has {{C|" + holding + "}} of {{C|" + people + "}} ("
					+ KingdomZoningRules.ShareHeld(holding, people) + "%, and never fewer than "
					+ KingdomCreedRules.MinBelievers + " of them). A creed-work waits on a congregation, not on a convert.";
			}
			case ZoningVerdict.RefusedBuilders:
				return XRL.Language.Grammar.A(name) + " is raised by {{C|" + Judgement.Detail + "}}, and there is nobody at " + seat
					+ " who answers to that. Grow, take in people from further off, or wait for somebody who does.";
			case ZoningVerdict.RefusedDistrict:
			{
				string here = DistrictOf(System, ZoneID);
				string standing = string.IsNullOrEmpty(here)
					? "This ground carries no district"
					: ("This ground is the {{C|" + KingdomRules.DistrictName(here) + "}}");
				return standing + ", and " + XRL.Language.Grammar.A(name) + " is raised in {{C|" + Judgement.Detail
					+ "}}. Name this ground from the Charter, or walk to ground that already carries it.";
			}
			case ZoningVerdict.RefusedMegastructure:
				// The Judgement carries the KEY; the founder is owed the NAME. Composed here, where
				// the catalogue can be asked, so the rules layer never has to know it exists.
				return KingdomLabRules.PurposeRefusalLine(KingdomUpgrade.DisplayNameOf(Judgement.Detail));
			case ZoningVerdict.RefusedSatellite:
				// The Detail is the PARENT's key: the founder is told which great work is missing
				// and that it may be raised in any of their cities, not only this one.
				return KingdomSatelliteRules.NoParentRefusalLine(KingdomUpgrade.DisplayNameOf(Judgement.Detail));
			case ZoningVerdict.RefusedSatelliteKept:
				// A different verdict rather than a second reading of the same Detail, because this
				// Detail is the KEPT outpost and the one above is the PARENT, and a composer that
				// had to guess which would guess wrong.
				return KingdomSatelliteRules.CityKeepsRefusalLine(KingdomUpgrade.DisplayNameOf(Judgement.Detail));
			case ZoningVerdict.RefusedUncrowned:
				// The Detail is already a CITY, so nothing is looked up: a city's name is the
				// founder's own word for it.
				return KingdomLabRules.UncrownedRefusalLine(Judgement.Detail);
			case ZoningVerdict.RefusedCovenantStanding:
			{
				int standing = (System == null || string.IsNullOrEmpty(Judgement.Detail))
					? 0 : System.GetRegardForRealm(Judgement.Detail);
				int wanted = (Entry == null) ? 0 : Entry.CovenantMinStanding;
				return XRL.Language.Grammar.A(name) + " is opened by covenant with {{C|"
					+ CovenantName(Judgement.Detail) + "}}. " + seat + " holds {{C|" + standing
					+ "}} standing with them and needs {{C|" + wanted
					+ "}}. Keep their charter, answer their petitions, and improve the realm's standing before asking again.";
			}
			default:
				return XRL.Language.Grammar.A(name) + " cannot be raised here.";
			}
		}

		private static string CovenantName(string FactionName)
		{
			if (string.IsNullOrEmpty(FactionName))
			{
				return "that covenant";
			}
			Faction faction = Factions.GetIfExists(FactionName);
			return (faction == null) ? FactionName : faction.GetFormattedName();
		}

	}
}
