using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		internal static KingdomCityBook ResidenceBook(KingdomSystem System, Zone Ground)
			=> System == null || Ground == null ? null : BookFor(System, Ground.ZoneID);

		internal static bool TryResidence(KingdomSystem System, GameObject Body,
			out KingdomResidentRow Row, out KingdomResidence Home)
		{
			Row = default; Home = default;
			if (System == null || IdOf(Body) <= 0) return false;
			foreach (KingdomCityBook book in Books(System))
				if (book.HasValidSubsidenceStorage() && book.TryReadExact(out KingdomCityState state, out _)
					&& state.TryResidentIndex(IdOf(Body), out int index) && state.TryResident(index, out Row))
					return KingdomResidenceRules.TryDecode(Row.Residence, out Home);
			return false;
		}

		internal static bool TryObserveHousehold(GameObject Body, string ZoneId, string PlotId,
			string BedId, out string Wire)
		{
			Wire = null;
			if (!GameObject.Validate(Body)) return false;
			QolProfile profile = KingdomQol.ProfileOf(Body);
			return KingdomResidenceRules.TryEncode(new KingdomResidence(ZoneId ?? "", PlotId ?? "",
				BedId ?? "", Body.GetStringProperty(KingdomCreed.CreedProperty),
				new List<string>(profile.Needs), new List<string>(profile.Refuses),
				new List<string>(KingdomQolRules.SelfTags(profile))), out Wire);
		}

		// Reading a visitor refreshes their observed household traits, never replaces their home
		// with the visited map. Only Settle's complete physical housing reading can prove loss.
		private static KingdomResidentRow ReadResidence(KingdomCityState State,
			KingdomResidentRow Row, GameObject Body, string ZoneId, Dictionary<string, int> Homes)
		{
			int homeId = Row.HomeWorkId;
			if (!KingdomResidenceRules.TryDecode(Row.Residence, out KingdomResidence home))
			{
				string plot = Body.GetStringProperty(KingdomLodging.HomePlotIdProperty);
				string ground = null;
				if (!string.IsNullOrEmpty(plot) && homeId != 0)
				{
					for (int i = 0; i < State.WorkCount; i++)
						if (State.TryWork(i, out KingdomWorkRow work) && work.WorkId == homeId)
						{
							if (ground != null) return Row;
							ground = work.ZoneId;
						}
				}
				if (ground == null && !string.IsNullOrEmpty(plot) && Homes != null
					&& Homes.TryGetValue(plot, out int actual) && (homeId == 0 || homeId == actual))
				{ ground = ZoneId; homeId = actual; }
				if (ground == null && (!string.IsNullOrEmpty(plot) || homeId != 0)) return Row;
				if (!TryObserveHousehold(Body, ground, ground == null ? null : plot, "", out string legacy))
					return Row;
				return Row.WithResidence(legacy, homeId);
			}
			if (home.HasHome && home.ZoneId == ZoneId && Homes != null
				&& Homes.TryGetValue(home.PlotId, out int current)) homeId = current;
			if (!home.HasHome) homeId = 0;
			return TryObserveHousehold(Body, home.ZoneId, home.PlotId, home.BedId, out string wire)
				? Row.WithResidence(wire, homeId) : Row;
		}

		internal static bool TryAssignResidence(KingdomSystem System, Zone Ground,
			GameObject Body, GameObject Home)
		{
			if (!GameObject.Validate(Body) || Body.CurrentZone != Ground || Ground == null
				|| !KingdomCitizenship.BelongsTo(System, Body) || !TryEnsureRow(System, Body, out KingdomCityBook book, out int id)
				|| !ReferenceEquals(book, ResidenceBook(System, Ground))
				|| !book.TryRead(out KingdomCityState state, out _)
				|| !state.TryResidentIndex(id, out int index) || !state.TryResident(index, out KingdomResidentRow row))
				return false;
			string plot = Home?.GetStringProperty(KingdomPlots.PlotIdProperty);
			int homeId = Home == null ? 0 : KingdomCityRules.StableId(Home.IDIfAssigned);
			if (Home != null && (Home.CurrentZone != Ground || string.IsNullOrEmpty(plot) || homeId == 0)) return false;
			bool known = KingdomResidenceRules.TryDecode(row.Residence, out KingdomResidence prior);
			if (Home == null && known && prior.HasHome && prior.ZoneId != Ground.ZoneID) return false;
			string bed = known && KingdomResidenceRules.SameHome(prior, Ground.ZoneID, plot) ? prior.BedId : "";
			if (!TryObserveHousehold(Body, Home == null ? "" : Ground.ZoneID, plot, bed, out string wire)) return false;
			if (row.Residence != wire || row.HomeWorkId != homeId)
			{
				if (!state.TryWithResident(index, row.WithResidence(wire, homeId), out KingdomCityState changed, out _)
					|| !book.TryPublish(changed, out _)) return false;
			}
			// Publish authority first; an interrupted body projection can repeat this exact assignment.
			Body.SetStringProperty(KingdomLodging.HomePlotIdProperty, plot);
			return Body.GetStringProperty(KingdomLodging.HomePlotIdProperty) == plot;
		}

		internal static bool TryReconcileHomes(KingdomSystem System, Zone Ground, List<GameObject> Homes)
		{
			KingdomCityBook book = ResidenceBook(System, Ground);
			if (book == null || Homes == null || !book.TryRead(out KingdomCityState state, out _)) return false;
			var ids = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (GameObject home in Homes)
			{
				if (!GameObject.Validate(home) || home.CurrentZone != Ground) return false;
				string plot = home.GetStringProperty(KingdomPlots.PlotIdProperty);
				int id = KingdomCityRules.StableId(home.IDIfAssigned);
				if (string.IsNullOrEmpty(plot) || ids.ContainsKey(plot) || id == 0) return false;
				ids.Add(plot, id);
			}
			var rows = new KingdomResidentRow[state.ResidentCount];
			bool changed = false;
			for (int i = 0; i < rows.Length; i++)
			{
				state.TryResident(i, out KingdomResidentRow row); rows[i] = row;
				if (!KingdomResidentRules.OnTheRoll(row)
					|| !KingdomResidenceRules.TryDecode(row.Residence, out KingdomResidence home)
					|| !home.HasHome || home.ZoneId != Ground.ZoneID) continue;
				string wire = row.Residence;
				if (!ids.TryGetValue(home.PlotId, out int id))
				{
					if (!KingdomResidenceRules.TryEncode(new KingdomResidence("", "", "", home.Creed,
						home.Needs, home.Refuses, home.SelfTags), out wire)) return false;
					row = KingdomResidenceRules.ObserveHomeLoss(row, XRL.The.Game?.TimeTicks ?? 0L);
				}
				if (id == row.HomeWorkId && wire == row.Residence) continue;
				rows[i] = row.WithResidence(wire, id); changed = true;
			}
			return !changed || state.TryWithResidents(rows, out KingdomCityState next, out _)
				&& book.TryPublish(next, out _);
		}
	}
}
