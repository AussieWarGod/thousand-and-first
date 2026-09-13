using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Read-only Quickstart acceptance: the original citizens must survive long enough
	/// to occupy real completed housing. A paid fire alone cannot prove the opening is playable.
	/// Runs at startup, after ordinary construction turns, and after the separate cold load.</summary>
	internal static class KingdomQuickstartSettlementChecks
	{
		internal const string Row = "quickstart-settlement";

		internal static bool Observe(XRLGame Game, Zone Zone, KingdomSystem System,
			string Stage, out string Failure)
		{
			Failure = null;
			// The same lifecycle verbs also support ordinary founding without the Quickstart kit.
			if (!KingdomQuickstartRules.IsMode(Game.gameMode)) return true;
			int citizens = 0, housed = 0, shelters = 0, beds = 0;
			try
			{
				Require(KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled && KingdomLodging.Enabled,
					"settlement, arrivals or lodging are disabled; default gameplay is not being tested");
				Require(KingdomQuickstartRules.TryDecode(Game.GetStringGameState(
					KingdomQuickstartRules.ReceiptState), out KingdomQuickstartReceipt receipt)
					&& KingdomQuickstartRules.IsTerminal(receipt)
					&& receipt.FoundersDisposition == KingdomQuickstartFoundersDisposition.Seeded
					&& receipt.ZoneId == Zone.ZoneID, "no completed four-citizen Quickstart receipt");
				var ids = new HashSet<string>(StringComparer.Ordinal);
				for (int i = 0; i < KingdomQuickstartRules.FounderCount; i++)
				{
					string id = receipt.FounderObjectIds[i];
					Require(!string.IsNullOrEmpty(id) && ids.Add(id), "founder identities are absent or repeated");
					GameObject body = null;
					foreach (GameObject item in Zone.GetObjects())
					{
						if (!GameObject.Validate(item) || item.IDIfAssigned != id) continue;
						Require(body == null, "duplicate physical founder identity " + id);
						body = item;
					}
					Require(body != null && body.CurrentZone == Zone && body.IsCreature && !body.IsPlayer(),
						"original founder no longer stands in the settlement: " + id);
					var citizenship = body.GetPart<r_KingdomCitizenship>();
					Require(body.GetIntProperty("KingdomCitizen") == 1 && citizenship != null
						&& citizenship.Phase == KingdomCitizenshipPhase.Applied
						&& citizenship.OwnerRealmId == System.RealmId && citizenship.BodyObjectId == id
						&& citizenship.EnrollmentReason == (int)KingdomCitizenshipEnrollmentReason.Founding,
						"original founder lacks applied citizenship: " + id);
					Require(KingdomResidents.TryResident(System.City, KingdomResidents.IdOf(body), out var resident)
						&& KingdomResidentRules.OnTheRoll(resident), "original founder left the living roll: " + id);
					citizens++;
					if (KingdomLodging.HomeDesignKeyOf(Zone, body) == KingdomQuickstartRules.ShelterBuildKey)
						housed++;
				}
				for (int i = 0; i < KingdomQuickstartRules.ShelterLotCount; i++)
				{
					KingdomPlotRules.PlotRect expected = KingdomQuickstartRules.ShelterLot(i);
					int standing = 0;
					foreach (GameObject item in Zone.GetObjects())
					{
						if (!GameObject.Validate(item) || !KingdomUpgrade.IsFunctionallyBuilt(item)
							|| item.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != KingdomQuickstartRules.ShelterBuildKey
							|| !KingdomPlots.TryReadRect(item, out var rect)) continue;
						if (rect.X1 == expected.X1 && rect.Y1 == expected.Y1
							&& rect.X2 == expected.X2 && rect.Y2 == expected.Y2) standing++;
					}
					Require(standing <= 1, "duplicate completed shelter on reserved lot " + i);
					shelters += standing;
				}
				Require(KingdomGrowth.TryCountBeds(Zone, out beds, out string roofFailure),
					"physical roof census refused: " + roofFailure);
				if (Stage == "startup")
					Require(shelters == 0 && housed == 0, "starter housing completed before ordinary turns");
				else
				{
					Require(shelters == KingdomQuickstartRules.ShelterLotCount,
						"starter tent rows did not finish on their reserved lots: " + shelters);
					Require(beds >= KingdomQuickstartRules.FounderCount, "completed homes provide too few real beds: " + beds);
					Require(housed == KingdomQuickstartRules.FounderCount,
						"original citizens are not all assigned to the completed tent rows: " + housed);
				}
			}
			catch (Exception error) { Failure = error.GetType().Name + ": " + error.Message; }
			string message = "stage=" + Stage + "; citizens=" + citizens + "; housed=" + housed
				+ "; shelters=" + shelters + "; beds=" + beds + "; turns=" + Game.Turns
				+ "; synthetic-residents=false; forced-housing=false";
			if (Failure != null) message += "; failure=" + Failure;
			KingdomScenarioJournal.Append(Row, Failure == null, message);
			return Failure == null;
		}

		private static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure);
		}
	}
}
