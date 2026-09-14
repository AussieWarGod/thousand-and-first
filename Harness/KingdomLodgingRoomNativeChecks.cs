using System;
using System.Reflection;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed partial class KingdomLodgingRoomNativeFixture
	{
		private void Check(string Name, KingdomLodgingRules.Closeness Quarters,
			int Places, int Floor, bool Arrival)
		{
			RequireWorld();
			Require(Root.IDIfAssigned == RootId && Root.CurrentCell == At(6, 1), "adoption root identity moved");
			Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope, out string failure), failure);
			string detail = "case=" + Name;
			bool accepted = false;
			try
			{
				using (scope)
				{
					var survey = KingdomSurvey.ActiveFor(Zone);
					KingdomBenefitIndex benefits = null;
					Require(survey != null && survey.TryBenefits(out benefits, out failure),
						Name + " benefit index refused: " + failure);
					var reading = benefits.RoomReadingForRoot(RootId);
					int roof = benefits.AmountForRoot(RootId, "roof");
					detail += "; roof=" + roof + "; places=" + reading.SleepingPlaces
						+ "; rooms=" + reading.SleepingRooms + "; floor=" + reading.UsableFloorCells
						+ "; quarters=" + reading.Quarters;
					Require(roof == (Arrival ? 1 : 0) && benefits.Total("roof") == roof,
						detail + "; wrong roof credit or another home masked the scenario");
					Require(reading.SleepingPlaces == Places && reading.UsableFloorCells == Floor
						&& reading.Quarters == Quarters, detail + "; physical room measurement differs");
					MethodInfo quartersOf = typeof(KingdomLodging).GetMethod("QuartersOf",
						BindingFlags.NonPublic | BindingFlags.Static);
					Require(quartersOf != null && (KingdomLodgingRules.Closeness)quartersOf.Invoke(null,
						new object[] { Root, benefits }) == Quarters, detail + "; lodging bypassed physical privacy");
					bool wouldTake = KingdomLodging.ObservePreparedArrival(System, Zone, Newcomer,
						out var reason, out string hash);
					detail += "; arrival=" + wouldTake + "; reason=" + reason;
					Require(wouldTake == Arrival && !string.IsNullOrEmpty(hash), detail + "; arrival decision differs");
					Require(KingdomLodging.ObservePreparedArrival(System, Zone, Newcomer, out _,
						out string repeat) == wouldTake && repeat == hash,
						detail + "; read-only arrival observation was unstable");
				}
				RequireWorld(); Passed.Add(Name); accepted = true;
			}
			finally
			{
				KingdomScenarioJournal.Append("room-" + Name, accepted, detail);
			}
		}

		private void CheckOccupied()
		{
			Require(KingdomQuickstartRules.TryDecode(Game.GetStringGameState(KingdomQuickstartRules.ReceiptState),
				out var receipt), "Quickstart founder receipt absent");
			GameObject citizen = Zone.FindObjectByID(receipt.FounderObjectIds[0]);
			Require(GameObject.Validate(citizen) && citizen.IsAlive && citizen.IsCreature
				&& citizen.CurrentZone == Zone && citizen.GetIntProperty("KingdomCitizen") != 0,
				"original citizen absent");
			Cell original = citizen.CurrentCell;
			string id = citizen.IDIfAssigned;
			try
			{
				original.RemoveObject(citizen); Place(citizen, At(4, 2));
				Check("occupied-room", KingdomLodgingRules.Closeness.Private, 1, 24, true);
			}
			finally
			{
				citizen.CurrentCell?.RemoveObject(citizen); Place(citizen, original);
				Require(citizen.IDIfAssigned == id && citizen.CurrentCell == original,
					"borrowed founder was not restored exactly");
			}
		}
	}
}
