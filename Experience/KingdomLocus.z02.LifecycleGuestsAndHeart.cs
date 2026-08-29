using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomLocus
	{
		/// <summary>Creates an unplaced body from one frozen lifecycle plan. Placement, identity,
		/// marker, and post-scan proof remain owned by <see cref="KingdomGuestLifecycle"/>.</summary>
		internal static GameObject CreateLifecycleGuest(KingdomLifecycleOperation op,
			KingdomLifecycleProjection projection)
		{
			if (op == null || projection == null || op.Lane != KingdomLifecycleLane.PlainGuest
				|| op.Action != KingdomLifecycleAction.Spawn) return null;
			GameObject guest;
			try { guest = GameObject.Create(projection.Blueprint); }
			catch { return null; }
			if (!GameObject.Validate(guest)) return null;
			guest.SetIntProperty("KingdomGuest", 1);
			guest.SetStringProperty("KingdomOrigin", op.Origin ?? "the road");
			if (!string.IsNullOrEmpty(op.ObjectName))
				guest.GiveProperName(op.ObjectName, Force: true);
			if (string.Equals(op.Creed, "causal-pilgrim", StringComparison.Ordinal))
			{
				string detail = op.Detail ?? "a story from the city";
				string shownDetail = KingdomPresentation.Rich(detail);
				guest.SetIntProperty(CausalPilgrimProperty, 1);
				guest.SetIntProperty(PilgrimSequenceProperty, op.Kind);
				guest.SetStringProperty(PilgrimCauseProperty, detail);
				Description description = guest.GetPart<Description>();
				if (description != null)
					description.Short = "Road dust worked into ceremonial folds. This pilgrim came "
						+ "because " + shownDetail + ".";
				Qud.API.ConversationsAPI.addSimpleConversationToObject(guest,
					KingdomLocusRules.PilgrimGreeting(shownDetail), "Live and drink.",
					Question: "What drew you here?", Answer: "The roads kept telling of "
						+ shownDetail + ". I wanted to stand where it happened before I went on.");
			}
			else
			{
				Qud.API.ConversationsAPI.addSimpleConversationToObject(guest,
					"Live and drink, if you have it to spare. I'm not staying — just passing through.",
					"Live and drink.", Question: "Where are you bound?",
					Answer: "Wherever the road goes next. I heard there was water shared here, and wanted to see it for myself.");
			}
			return guest;
		}

		private static GameObject FindCausalPilgrim(KingdomSurvey Survey,
			Simulation.City.KingdomCityBook Book)
		{
			if (Survey == null) return null;
			if (!string.IsNullOrEmpty(Book.PilgrimObjectId))
			{
				GameObject global = GameObject.FindByID(Book.PilgrimObjectId);
				if (GameObject.Validate(global) && global.GetIntProperty(CausalPilgrimProperty) == 1
					&& ReferenceEquals(global.CurrentZone, Survey.Ground)
					&& Survey.CausalPilgrims.Contains(global)
					&& global.GetIntProperty(PilgrimSequenceProperty) == Book.PilgrimSequence)
					return global;
			}
			for (int i = 0; i < Survey.CausalPilgrims.Count; i++)
			{
				GameObject item = Survey.CausalPilgrims[i];
				if (GameObject.Validate(item) && item.GetIntProperty(CausalPilgrimProperty) == 1
					&& item.GetIntProperty(PilgrimSequenceProperty) == Book.PilgrimSequence)
					return item;
			}
			return null;
		}

		/// <summary>Rite cell first, then deterministic Chebyshev rings. No draw and no distant
		/// random empty cell: blockage is a real, retryable state.</summary>
		internal static Cell HeartArrivalCell(Zone Z)
		{
			if (!KingdomPlots.TryRiteGround(Z, out int riteX, out int riteY)) return null;
			for (int radius = 0; radius <= 3; radius++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					for (int dx = -radius; dx <= radius; dx++)
					{
						if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
						int x = riteX + dx;
						int y = riteY + dy;
						if (x < 0 || x >= Z.Width || y < 0 || y >= Z.Height) continue;
						Cell cell = Z.GetCell(x, y);
						if (cell == null || !cell.IsPassable()
							|| cell.HasObjectWithPart("LiquidVolume")) continue;
						bool living = false;
						List<GameObject> objects = cell.GetObjects();
						for (int i = 0; i < objects.Count; i++)
							if (GameObject.Validate(objects[i]) && objects[i].IsCreature)
							{
								living = true;
								break;
							}
						if (!living) return cell;
					}
				}
			}
			return null;
		}

		private static bool ResolvePilgrim(KingdomSystem System,
			Simulation.City.KingdomCityBook Book, GameObject Pilgrim, bool Greeted,
			long DepartTick)
		{
			string name = !string.IsNullOrEmpty(Book.PilgrimName) ? Book.PilgrimName
				: (GameObject.Validate(Pilgrim) ? PlainGuestName(Pilgrim) : "A pilgrim");
			if (string.IsNullOrEmpty(name) || name.Length > KingdomLocusRules.MaxPilgrimNameChars)
				name = "A pilgrim";
			if (string.IsNullOrEmpty(Book.PilgrimName) && GameObject.Validate(Pilgrim)
				&& name.Length <= KingdomLocusRules.MaxPilgrimNameChars)
				Book.PilgrimName = name;
			string cause = Book.PilgrimCause;
			string shownName = KingdomPresentation.Rich(name);
			string shownCause = KingdomPresentation.Rich(cause);
			string shownPlace = KingdomPresentation.Rich(Book.PilgrimPlaceName);
			int sequence = Book.PilgrimSequence;
			string line = KingdomLocusRules.PilgrimChronicleLine(shownName,
				shownPlace, shownCause, Book.PilgrimGreeted == 1 || Greeted);
			string note = Greeted ? shownName + " received water and went on speaking of "
				+ shownCause + "."
				: KingdomLocusRules.PilgrimLedgerNote(shownName, shownCause,
					KingdomRules.ElapsedDays(The.Game.TimeTicks - DepartTick));
			long next = KingdomLocusRules.NextGuestDueTick(The.Game.TimeTicks);
			if (!GameObject.Validate(Pilgrim))
			{
				long before = System.NextGuestTick > 0L ? System.NextGuestTick : 0L;
				if (before == next && next < long.MaxValue) next++;
				return KingdomGuestLifecycle.PublishMissedCausal(System,
					The.Player?.CurrentZone ?? null, The.Game.TimeTicks, before, next, sequence,
					name, cause, Book.PilgrimPlaceName, line, note);
			}
			return KingdomGuestLifecycle.PublishDeparture(System, Pilgrim,
				KingdomLifecycleLane.PlainGuest, The.Game.TimeTicks, next, Greeted,
				line, note, Greeted ? "{{C|" + shownName
					+ " received the settlement's water.}}" : null,
				null, Greeted && !System.FirstGuestGreeted);
		}

		private static GameObject FindGuest(KingdomSurvey Survey)
		{
			return Survey != null && Survey.Guests.Count > 0 ? Survey.Guests[0] : null;
		}

		/// <summary>Puts one traveller on the ground at the tick they walked up. False when there
		/// was nowhere to stand them, which is the caller's signal to leave their arrival unspent
		/// rather than losing them.</summary>
		private static bool SpawnGuest(KingdomSystem System, Zone Z, Cell cell, long ArrivalTick)
		{
			if (cell == null) return false;
			KingdomSemanticPersonPlan plan;
			string planFailure;
			if (!KingdomGuestLifecycle.TryPrepareSpawnPlan(System,
				KingdomLifecycleLane.PlainGuest, "r_KingdomGuests", "r_KingdomGuest",
				out plan, out planFailure))
			{
				KingdomLog.Log("plain guest waits: " + planFailure);
				return false;
			}
			long depart = KingdomLocusRules.GuestDepartTickFor(ArrivalTick);
			string shownName = KingdomPresentation.Rich(plan.Name);
			string chronicle = shownName + " came to "
				+ KingdomPresentation.Rich(System.KingdomDisplayName)
				+ " by the road and waited at its rite ground";
			string ledger = shownName + " is waiting at the rite ground.";
			string message = "{{C|" + shownName
				+ " has come to the rite ground as a guest.}}";
			return KingdomGuestLifecycle.PublishSpawn(System, Z,
				KingdomLifecycleLane.PlainGuest, cell, The.Game.TimeTicks, depart,
				plan.Blueprint, plan.Name, plan.Origin, 0, 0, null, null, null, chronicle,
				ledger, message, null, semanticPlan: plan);
		}

	}
}
