using System;
using System.Collections.Generic;
using HarmonyLib;
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
			int citizens = 0, housed = 0, shelters = 0, beds = 0, rooms = 0, clearFloor = 0;
			var priorSurvey = KingdomSurvey.ActiveFor(Zone);
			try
			{
				Require(KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled && KingdomLodging.Enabled,
					"settlement, arrivals or lodging are disabled; default gameplay is not being tested");
				Require(KingdomQuickstartRules.TryDecode(Game.GetStringGameState(
					KingdomQuickstartRules.ReceiptState), out KingdomQuickstartReceipt receipt)
					&& KingdomQuickstartRules.IsTerminal(receipt)
					&& receipt.FoundersDisposition == KingdomQuickstartFoundersDisposition.Seeded
					&& receipt.ZoneId == Zone.ZoneID, "no completed four-citizen Quickstart receipt");
				Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope, out string surveyFailure),
					surveyFailure);
				using (scope)
				{
					LogSettlementState(Zone, System, receipt, Stage);
					Require(KingdomQuickstartEncounterDiagnostics.Check(Game, Zone, Stage),
						"initial camp encounter reservation was absent or escaped its generation scope");
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
								&& rect.X2 == expected.X2 && rect.Y2 == expected.Y2)
							{
								standing++;
								var survey = KingdomSurvey.ActiveFor(Zone);
								KingdomBenefitIndex benefits = null;
								Require(survey != null && survey.TryBenefits(out benefits, out string _),
									"completed shelter benefit index refused");
								var room = benefits.RoomReadingForRoot(item.IDIfAssigned);
								Require(room.SleepingRooms == 1 && room.SleepingPlaces == 3
									&& room.ExposedPlaces == 0 && room.UnusablePlaces == 0
									&& room.UsableFloorCells == 17 && room.Quarters == KingdomLodgingRules.Closeness.Close,
									"reserved shelter " + i + " lacks usable enclosed housing: rooms=" + room.SleepingRooms
									+ ", places=" + room.SleepingPlaces + ", exposed=" + room.ExposedPlaces
									+ ", unusable=" + room.UnusablePlaces + ", floor=" + room.UsableFloorCells
									+ ", quarters=" + room.Quarters);
								rooms += room.SleepingRooms;
								clearFloor += room.UsableFloorCells;
							}
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
				Require(ReferenceEquals(KingdomSurvey.ActiveFor(Zone), priorSurvey),
					"settlement observer did not restore the previous survey scope");
			}
			catch (Exception error) { Failure = error.GetType().Name + ": " + error.Message; }
			string message = "stage=" + Stage + "; citizens=" + citizens + "; housed=" + housed
				+ "; shelters=" + shelters + "; beds=" + beds + "; rooms=" + rooms
				+ "; clear-floor=" + clearFloor + "; turns=" + Game.Turns
				+ "; synthetic-residents=false; forced-housing=false";
			if (Failure != null) message += "; failure=" + Failure;
			KingdomScenarioJournal.Append(Row, Failure == null, message);
			return Failure == null;
		}

		// Failure diagnostics never manufacture a replacement body or infer death from absence.
		private static void LogSettlementState(Zone Zone, KingdomSystem System,
			KingdomQuickstartReceipt Receipt, string Stage)
		{
			foreach (string id in Receipt.FounderObjectIds)
			{
				GameObject body = Zone.FindObjectByID(id);
				KingdomLog.Log("quickstart witness " + Stage + ": founder=" + id
					+ "; live=" + GameObject.Validate(body) + "; resident=" + KingdomResidents.IdOf(body)
					+ "; citizen=" + (body?.GetIntProperty("KingdomCitizen") ?? 0));
				if (Zone.Graveyard?.Objects == null) continue;
				foreach (GameObject dead in Zone.Graveyard.Objects)
					if (dead?.IDIfAssigned == id)
						KingdomLog.Log("quickstart witness " + Stage + ": grave=" + id
							+ "; blueprint=" + dead.Blueprint);
			}
			if (System.City.TryRead(out var state, out var fault))
				for (int i = 0; i < state.ResidentCount; i++)
					if (state.TryResident(i, out var resident))
						KingdomLog.Log("quickstart witness " + Stage + ": row=" + resident.ResidentId
							+ "; name=" + resident.Name + "; standing=" + resident.Standing
							+ "; cause=" + resident.Cause + "; bound=" + resident.BoundZoneId
							+ "; home=" + resident.HomeWorkId);
		}

		private static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure);
		}
	}

	// Observe the engine's actual fatal event; never veto damage or protect a test citizen.
	[HarmonyPatch(typeof(BeforeDeathRemovalEvent), nameof(BeforeDeathRemovalEvent.Send))]
	internal static class KingdomQuickstartFounderDeathDiagnostics
	{
		[HarmonyPrefix]
		internal static void Before(GameObject Dying, GameObject Killer, GameObject Weapon,
			GameObject Projectile, string Reason, string ThirdPersonReason, bool Accidental)
		{
			if (!KingdomQuickstartBootTest.LifecycleRequested || The.Game == null || Dying == null
				|| !KingdomQuickstartRules.TryDecode(The.Game.GetStringGameState(
					KingdomQuickstartRules.ReceiptState), out var receipt)) return;
			bool original = false;
			foreach (string id in receipt.FounderObjectIds)
				if (id == Dying.IDIfAssigned) original = true;
			if (!original) return;
			KingdomLog.Log("quickstart fatal event: turns=" + The.Game.Turns
				+ "; victim=" + Describe(Dying) + "; killer=" + Describe(Killer)
				+ "; weapon=" + Describe(Weapon) + "; projectile=" + Describe(Projectile)
				+ "; accidental=" + Accidental + "; category=" + Dying.Physics?.LastDeathCategory
				+ "; reason=" + KingdomScenarioRules.Bounded(ThirdPersonReason ?? Reason ?? "unstated"));
		}

		private static string Describe(GameObject Body)
		{
			return Body == null ? "none" : Body.Blueprint + "#" + Body.IDIfAssigned
				+ "@" + Body.CurrentCell?.X + "," + Body.CurrentCell?.Y
				+ "; zone=" + Body.CurrentZone?.ZoneID
				+ "; citizen=" + Body.GetIntProperty("KingdomCitizen");
		}
	}


	[HarmonyPatch(typeof(KingdomQuickstartAmbientEncounterPatch), "Before")]
	internal static class KingdomQuickstartEncounterDiagnostics
	{
		private static XRLGame ObservedGame;
		private static string ObservedZone;
		private static int Reservations;
		private static bool Invalid;

		[HarmonyPostfix]
		internal static void After(Zone Z, bool __result)
		{
			if (!KingdomQuickstartBootTest.LifecycleRequested || __result) return;
			if (!ReferenceEquals(ObservedGame, The.Game))
			{
				ObservedGame = The.Game; ObservedZone = Z?.ZoneID; Reservations = 0; Invalid = false;
			}
			Reservations++;
			Invalid |= Z == null || Z.ZoneID != ObservedZone
				|| !KingdomQuickstartEncounterScope.Reserves(Z);
		}

		internal static bool Check(XRLGame Game, Zone Zone, string Stage)
		{
			// Call only the production interception decision, never the encounter builder.
			// Once generation finishes, even this same camp must take the ordinary path.
			bool result = false;
			bool ordinary = KingdomQuickstartAmbientEncounterPatch.Before(Zone, ref result);
			bool observed = Stage == "loaded" ? Reservations == 0
				: ReferenceEquals(ObservedGame, Game) && ObservedZone == Zone.ZoneID && Reservations > 0;
			KingdomLog.Log("quickstart encounter witness: stage=" + Stage + "; reserved=" + Reservations
				+ "; observed=" + observed + "; outside-scope-ordinary=" + ordinary);
			return observed && !Invalid && ordinary && !result && !KingdomQuickstartEncounterScope.Reserves(Zone);
		}
	}

}
