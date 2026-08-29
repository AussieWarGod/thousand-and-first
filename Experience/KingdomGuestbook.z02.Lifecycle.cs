using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestbook
	{

		internal static void AppendGuestbookLine(KingdomSystem System, string Line)
		{
			if (System.GuestbookLines == null)
			{
				System.GuestbookLines = new List<string>();
			}
			System.GuestbookLines.Add(Line);
			if (System.GuestbookLines.Count > KingdomGuestRules.GuestbookMaxEntries)
			{
				System.GuestbookLines.RemoveAt(0);
			}
		}

		internal static void AppendLifecycleLine(KingdomSystem System, string Line)
		{
			if (!string.IsNullOrEmpty(Line)) AppendGuestbookLine(System, Line);
		}

		/// <summary>Creates an unplaced notable from one frozen lifecycle plan.</summary>
		internal static GameObject CreateLifecycleNotable(KingdomLifecycleOperation op,
			KingdomLifecycleProjection projection)
		{
			if (op == null || projection == null || op.Lane != KingdomLifecycleLane.NotableGuest
				|| op.Action != KingdomLifecycleAction.Spawn) return null;
			GameObject guest;
			try { guest = GameObject.Create(projection.Blueprint); }
			catch { return null; }
			if (!GameObject.Validate(guest)) return null;
			guest.SetIntProperty(NotableGuestProperty, 1);
			guest.SetIntProperty(HookKindProperty, op.Kind);
			guest.SetStringProperty(HookTextProperty, op.Detail ?? "a road still unwalked");
			guest.SetStringProperty(OriginProperty, op.Origin ?? "the road");
			if (!string.IsNullOrEmpty(op.ObjectName))
				guest.GiveProperName(op.ObjectName, Force: true);
			if (guest.HasTag(LegendaryTraderTag))
			{
				string title = op.DisplayFaction;
				if (!string.IsNullOrEmpty(title)) guest.RequirePart<Titles>().AddTitle(title, -40);
			}
			KingdomGuestRules.HookKind kind = (KingdomGuestRules.HookKind)op.Kind;
			// A third-party notable blueprint may already own a quest or conversation graph.
			// Qud's helper removes that part by default, so use it only on a genuinely blank body.
			if (guest.GetPart<ConversationScript>() == null)
			{
				Qud.API.ConversationsAPI.addSimpleConversationToObject(guest,
					KingdomGuestRules.ArrivalGreeting(kind), "Live and drink.",
					Question: "What are you really here for?", Answer: "There's " + op.Detail
						+ ", if I ever get around to it. For now I'm only walking.");
			}
			return guest;
		}

		/// <summary>One resident-row mutation enclosed by Lodge's domain lease. Re-entry recognizes
		/// exact already-enrolled evidence; it never adds a second row or consults compatibility lists.</summary>
		internal static bool ApplyLifecycleLodge(KingdomSystem system, GameObject guest,
			KingdomLifecycleOperation op)
		{
			if (system == null || op == null || !GameObject.Validate(guest)
				|| guest.ID != op.ObjectId || guest.Blueprint != op.Blueprint) return false;
			KingdomLifecycleResourceLease roster = op.ResourceLeases.Find(l =>
				l != null && l.Kind == KingdomLifecycleResourceKind.Roster);
			if (roster == null || roster.Before < 0L || roster.Before > int.MaxValue
				|| roster.After != roster.Before + 1L) return false;
			int before = (int)roster.Before;
			int onRoll = Simulation.City.KingdomResidents.OnRollCount(system);
			if (onRoll != before && onRoll != roster.After) return false;
			GameObject fineHouse = null;
			if (op.Target == 1)
			{
				fineHouse = string.IsNullOrEmpty(op.ObjectMarker)
					? null : GameObject.FindByID(op.ObjectMarker);
				if (!GameObject.Validate(fineHouse)
					|| fineHouse.CurrentZone == null || fineHouse.CurrentZone != guest.CurrentZone
					|| !string.Equals(KingdomUpgrade.DesignKeyOf(fineHouse), "finehouse",
						StringComparison.Ordinal)
					|| KingdomLodging.IsCondemned(fineHouse)
					|| !KingdomPlots.TryReadRect(fineHouse, out KingdomPlotRules.PlotRect rect)
					|| KingdomGuestRules.ClassifyRectTier(rect.Width, rect.Height)
						< KingdomGuestRules.LegendaryTraderFineHouseTier
					|| op.PlunderRequested < KingdomGuestRules.LegendaryTraderMinimumShopTier) return false;
				List<GameObject> residents = KingdomLodging.ResidentsOf(fineHouse.CurrentZone, fineHouse);
				for (int i = 0; i < residents.Count; i++)
					if (residents[i] != guest) return false;
			}
			string intent = "intent:" + op.Id;
			string receipt = guest.GetStringProperty(LodgeReceiptProperty);
			if (receipt != op.Id && receipt != intent)
			{
				if (guest.GetIntProperty(NotableGuestProperty) != 1) return false;
				guest.SetStringProperty(LodgeReceiptProperty, intent);
			}
			if (!string.IsNullOrEmpty(op.Creed))
			{
				string held = guest.GetStringProperty(KingdomCreed.CreedProperty);
				system.CreedCounts.TryGetValue(op.Creed, out int currentCreed);
				if (currentCreed != op.Count && currentCreed != op.Count + 1) return false;
				if (!string.IsNullOrEmpty(held)
					&& !string.Equals(held, op.Creed, StringComparison.Ordinal)) return false;
				guest.SetStringProperty(KingdomCreed.CreedProperty, op.Creed);
				if (currentCreed == op.Count) system.CreedCounts[op.Creed] = currentCreed + 1;
			}
			if (!KingdomFounding.EnrollCitizen(guest,
				KingdomCitizenshipEnrollmentReason.GuestAdoption,
				op.CreatedTick)) return false;
			guest.SetIntProperty("KingdomBorn", 1);
			guest.DisplayName = KingdomPresentation.Rich(op.ObjectName);
			guest.SetStringProperty("KingdomName", op.ObjectName);
			guest.SetStringProperty("KingdomOrigin", op.Origin ?? "");
			guest.SetIntProperty(NotableGuestProperty, 0);
			if (op.Target == 1)
			{
				guest.SetIntProperty(LegendaryTraderResidentProperty, 1);
				guest.SetStringProperty(KingdomLodging.HomePlotIdProperty,
					fineHouse.GetStringProperty(KingdomPlots.PlotIdProperty));
				if (!ConfigureLegendaryTraderShop(guest, op.PlunderRequested)) return false;
			}
			Simulation.City.KingdomCityBook residentBook;
			int residentId;
			if (!Simulation.City.KingdomResidents.TryEnsureRow(system, guest, op.Origin,
				op.Faction, op.CreatedTick, out residentBook, out residentId)) return false;
			guest.SetStringProperty(LodgeReceiptProperty, op.Id);
			// Lodging changes civic status, not the guest's native/owned conversation graph.
			if (op.Outbox != null && op.Outbox.ChronicleAccomplishment)
				system.FirstNotableGuestLodged = true;
			return true;
		}

		internal static bool LifecycleLodgeComplete(KingdomSystem system, GameObject guest,
			KingdomLifecycleOperation op)
		{
			GameObject fineHouse = op == null || string.IsNullOrEmpty(op.ObjectMarker)
				? null : GameObject.FindByID(op.ObjectMarker);
			Simulation.City.KingdomCityBook residentBook;
			int residentId;
			Simulation.City.KingdomCityState state;
			Simulation.City.KingdomCityFault fault;
			int rowIndex;
			Simulation.City.KingdomResidentRow row;
			bool exactRow = system != null && GameObject.Validate(guest)
				&& Simulation.City.KingdomResidents.TryLocate(system, guest, out residentBook,
					out residentId)
				&& residentBook.TryRead(out state, out fault)
				&& state.TryResidentIndex(residentId, out rowIndex)
				&& state.TryResident(rowIndex, out row)
				&& Simulation.City.KingdomResidentRules.OnTheRoll(row)
				&& string.Equals(row.Name, op?.ObjectName, StringComparison.Ordinal)
				&& string.Equals(row.Origin, op?.Origin ?? "", StringComparison.Ordinal)
				&& string.Equals(row.Arrived, op?.Faction ?? "", StringComparison.Ordinal);
			return system != null && op != null && GameObject.Validate(guest)
				&& guest.ID == op.ObjectId
				&& guest.GetStringProperty(LodgeReceiptProperty) == op.Id
				&& exactRow
				&& Simulation.City.KingdomResidents.OnRollCount(system) == op.Defence + 1
				&& (op.Target != 1 || (guest.GetIntProperty(LegendaryTraderResidentProperty) == 1
					&& GameObject.Validate(fineHouse)
					&& guest.GetStringProperty(KingdomLodging.HomePlotIdProperty)
						== fineHouse.GetStringProperty(KingdomPlots.PlotIdProperty)
					&& guest.GetIntProperty("VillageMerchant") == 1
					&& guest.GetIntProperty("InventoryTier") == op.PlunderRequested));
		}
	}
}
