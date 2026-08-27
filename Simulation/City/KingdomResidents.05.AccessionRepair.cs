using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		internal static KingdomAccessionOutcome TryRepairAccession(KingdomSystem System,
			GameObject Body, int ResidentId, bool Seated, string Name, long ArrivedTick,
			string KeptCreeds, out KingdomResidentRow FormerRow)
		{
			FormerRow = default(KingdomResidentRow);
			if (System == null || System.Bindings == null || !GameObject.Validate(Body)
				|| !Body.IsAlive || ResidentId == 0 || string.IsNullOrEmpty(Name))
			{
				return KingdomAccessionOutcome.RepairRequired;
			}
			KingdomSettlement away = Seated ? null : System.Away;
			KingdomCityBook book = Seated ? System.City : away?.City;
			KingdomCityState city;
			KingdomBindingTable bindings;
			KingdomCityFault fault;
			if (book == null || !book.TryRead(out city, out fault)
				|| !System.Bindings.TryRead(out bindings, out fault))
			{
				return KingdomAccessionOutcome.RepairRequired;
			}

			int rowIndex;
			bool hasRow = city.TryResidentIndex(ResidentId, out rowIndex);
			if (hasRow)
			{
				if (!city.TryResident(rowIndex, out FormerRow)
					|| FormerRow.Standing != KingdomResidentStanding.Resident
					|| FormerRow.Name != Name || FormerRow.ArrivedTick != ArrivedTick
					|| (FormerRow.KeptCreeds ?? "") != (KeptCreeds ?? ""))
				{
					return KingdomAccessionOutcome.RepairRequired;
				}
			}
			else
			{
				FormerRow = new KingdomResidentRow(ResidentId, Name, 0, 0, ArrivedTick,
					0, 0, 0, KingdomDayShape.Hearth, KingdomResidentStanding.Resident,
					KingdomStandingCause.None, Body.CurrentZone?.ZoneID,
					KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0, KeptCreeds);
			}

			KingdomBinding held;
			bool hasBinding = bindings.TryGet(ResidentId, KingdomBindingKind.Resident, out held);
			string bodyZone = Body.CurrentZone?.ZoneID;
			if (hasBinding && (string.IsNullOrEmpty(bodyZone)
				|| held.ObjectId != Body.ID || held.ZoneId != bodyZone))
			{
				return KingdomAccessionOutcome.RepairRequired;
			}

			Dictionary<string, int> creedCounts = Seated ? System.CreedCounts : away?.CreedCounts;
			Dictionary<string, int> creedPastCounts = Seated ? System.CreedPastCounts : away?.CreedPastCounts;
			if ((!Seated && away == null) || creedCounts == null || creedPastCounts == null)
			{
				return KingdomAccessionOutcome.RepairRequired;
			}
			Dictionary<string, int> nextCreedCounts = new Dictionary<string, int>(creedCounts);
			Dictionary<string, int> nextCreedPastCounts = new Dictionary<string, int>(creedPastCounts);
			string rollName = Body.GetStringProperty("KingdomName");
			if (rollName != Name)
			{
				return KingdomAccessionOutcome.RepairRequired;
			}
			string citizenshipFailure;
			if (!KingdomCitizenship.CanRemove(System, Body, out citizenshipFailure))
			{
				KingdomLog.Log("binding: accession repair cannot remove citizenship exactly ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			DropCount(nextCreedCounts, Body.GetStringProperty(KingdomCreed.CreedProperty));
			List<string> pastCreeds = KingdomCreedRules.DecodeKept(
				Body.GetStringProperty(KingdomCreed.CreedPastProperty));
			for (int i = 0; i < pastCreeds.Count; i++) DropCount(nextCreedPastCounts, pastCreeds[i]);

			if (hasRow)
			{
				KingdomCityState nextCity;
				KingdomResidentRow removed;
				if (!KingdomResidentRules.TryRemove(city, ResidentId, out nextCity, out removed,
						out fault)
					|| !SafePublish(book, nextCity, "accession repair city"))
				{
					return KingdomAccessionOutcome.RepairRequired;
				}
			}
			if (hasBinding)
			{
				KingdomBindingTable nextBindings;
				KingdomBinding evicted;
				if (!bindings.TryUnbind(ResidentId, KingdomBindingKind.Resident,
					KingdomUnbindCause.Accession, out nextBindings, out evicted, out fault)
					|| !SafePublish(System.Bindings, nextBindings, "accession repair registry"))
				{
					return KingdomAccessionOutcome.RepairRequired;
				}
			}
			if (!AccessionAbsent(book, System.Bindings, ResidentId))
			{
				return KingdomAccessionOutcome.RepairRequired;
			}
			if (Seated)
			{
				System.CreedCounts = nextCreedCounts;
				System.CreedPastCounts = nextCreedPastCounts;
			}
			else
			{
				away.CreedCounts = nextCreedCounts;
				away.CreedPastCounts = nextCreedPastCounts;
			}
			ProjectCompatibility(System);
			if (!KingdomCitizenship.TryRemove(System, Body,
				KingdomCitizenshipRemovalReason.Accession, out citizenshipFailure))
			{
				KingdomLog.Log("binding: accession repair left citizenship pending ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			FinishAccessionBody(Body, FormerRow, ResidentId);
			return KingdomAccessionOutcome.Committed;
		}

		private static KingdomAccessionOutcome PublishAccessionCarriers(KingdomCityBook Book,
			KingdomBindingRegistry Registry, KingdomCityState OriginalCity,
			KingdomCityState AdvancedCity, KingdomBindingTable OriginalBindings,
			KingdomBindingTable AdvancedBindings)
		{
			SafePublish(Book, AdvancedCity, "accession city");
			for (int attempt = 0; attempt < 4; attempt++)
			{
				KingdomAccessionCarrierState state = ReadAccessionCarriers(Book, Registry,
					OriginalCity, AdvancedCity, OriginalBindings, AdvancedBindings);
				switch (state)
				{
				case KingdomAccessionCarrierState.Original:
					if (attempt == 0) return KingdomAccessionOutcome.RefusedClean;
					SafePublish(Book, AdvancedCity, "accession city retry");
					break;
				case KingdomAccessionCarrierState.CityAdvanced:
					SafePublish(Registry, AdvancedBindings, "accession registry");
					break;
				case KingdomAccessionCarrierState.BindingAdvanced:
					SafePublish(Book, AdvancedCity, "accession city completion");
					break;
				case KingdomAccessionCarrierState.Committed:
					return KingdomAccessionOutcome.Committed;
				default:
					return KingdomAccessionOutcome.RepairRequired;
				}
			}
			return ReadAccessionCarriers(Book, Registry, OriginalCity, AdvancedCity,
				OriginalBindings, AdvancedBindings) == KingdomAccessionCarrierState.Committed
				? KingdomAccessionOutcome.Committed : KingdomAccessionOutcome.RepairRequired;
		}

		private static KingdomAccessionCarrierState ReadAccessionCarriers(KingdomCityBook Book,
			KingdomBindingRegistry Registry, KingdomCityState OriginalCity,
			KingdomCityState AdvancedCity, KingdomBindingTable OriginalBindings,
			KingdomBindingTable AdvancedBindings)
		{
			try
			{
				KingdomCityState city;
				KingdomBindingTable bindings;
				KingdomCityFault fault;
				if (!Book.TryRead(out city, out fault) || !Registry.TryRead(out bindings, out fault))
				{
					return KingdomAccessionCarrierState.Unknown;
				}
				return KingdomResidentRules.AccessionCarriers(
					KingdomResidentRules.SameCity(city, OriginalCity),
					KingdomResidentRules.SameCity(city, AdvancedCity),
					SameBindings(bindings, OriginalBindings), SameBindings(bindings, AdvancedBindings));
			}
			catch (Exception ex)
			{
				KingdomLog.Log("binding: accession carrier reproof threw " + ex.GetType().Name);
				return KingdomAccessionCarrierState.Unknown;
			}
		}

		private static bool AccessionAbsent(KingdomCityBook Book,
			KingdomBindingRegistry Registry, int ResidentId)
		{
			KingdomCityState city;
			KingdomBindingTable bindings;
			KingdomCityFault fault;
			int row;
			KingdomBinding binding;
			return Book.TryRead(out city, out fault) && Registry.TryRead(out bindings, out fault)
				&& !city.TryResidentIndex(ResidentId, out row)
				&& !bindings.TryGet(ResidentId, KingdomBindingKind.Resident, out binding);
		}

		private static bool SafePublish(KingdomCityBook Book, KingdomCityState State, string Context)
		{
			try
			{
				KingdomCityFault fault;
				if (Book.TryPublish(State, out fault)) return true;
				Refuse(Context, fault);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("binding: " + Context + " threw " + ex.GetType().Name);
			}
			return false;
		}

		private static bool SafePublish(KingdomBindingRegistry Registry,
			KingdomBindingTable State, string Context)
		{
			try
			{
				KingdomCityFault fault;
				if (Registry.TryPublish(State, out fault)) return true;
				Refuse(Context, fault);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("binding: " + Context + " threw " + ex.GetType().Name);
			}
			return false;
		}

		private static void FinishAccessionBody(GameObject Body, KingdomResidentRow FormerRow,
			int ResidentId)
		{
			try
			{
				KingdomStations.Post(Body, 0, KingdomWorkKind.Other);
				Body.RemoveIntProperty(ResidentIdProperty);
				Body.RemoveIntProperty("KingdomCitizen");
				Body.RemoveIntProperty("KingdomBorn");
				Body.RemoveStringProperty("KingdomName");
				Body.RemoveStringProperty(KingdomLodging.HomePlotIdProperty);
				KingdomLog.Log("binding: " + (FormerRow.Name ?? "-") + " (" + ResidentId
					+ ") left the resident roll by accession");
			}
			catch (Exception ex)
			{
				KingdomLog.Log("binding: accession body cleanup remains idempotently pending ("
					+ ex.GetType().Name + ")");
			}
		}

		private static bool SameBindings(KingdomBindingTable A, KingdomBindingTable B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++)
			{
				KingdomBinding a;
				KingdomBinding b;
				if (!A.TryAt(i, out a) || !B.TryAt(i, out b)
					|| a.BindingKey != b.BindingKey || a.Kind != b.Kind || a.ZoneId != b.ZoneId
					|| a.ObjectId != b.ObjectId || a.MintedTick != b.MintedTick) return false;
			}
			return true;
		}

		/// <summary>Removes one person from a per-city tally without leaving zero rows behind.</summary>
		private static void DropCount(Dictionary<string, int> Counts, string Key)
		{
			if (Counts == null || Key == null || !Counts.TryGetValue(Key, out int count))
			{
				return;
			}
			if (count > 1)
			{
				Counts[Key] = count - 1;
			}
			else
			{
				Counts.Remove(Key);
			}
		}
	}
}
