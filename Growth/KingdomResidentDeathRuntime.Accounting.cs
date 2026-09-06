using System;
using System.Collections.Generic;
using System.Globalization;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDeathRuntime
	{
		private static bool Accounts(Frame f, int index, ref KingdomResidentDeathReceipt r)
		{
			if (!DeadExact(f, r)) return false;
			if (r.Phase == KingdomResidentDeathPhase.RolesSettled)
			{
				foreach (var other in f.Journal.Entries)
					if (other.Before.ResidentId != r.Before.ResidentId && (other.Phase == KingdomResidentDeathPhase.AccountingPrepared
						|| other.Phase == KingdomResidentDeathPhase.Accounted)) return false;
				string[] before = ObserveAccounts(f);
				if (!KingdomResidentDeathRules.TryPrepareAccounts(r, before, out var prepared) || !DeadExact(f, r)
					|| !Equal(before, ObserveAccounts(f)) || !Save(f, index, prepared)) return false;
				r = prepared;
			}
			if (r.Phase != KingdomResidentDeathPhase.AccountingPrepared) return r.Phase >= KingdomResidentDeathPhase.Accounted;
			int cut = KingdomResidentDeathRules.Prefix(r.BeforeAccounts, r.AfterAccounts, ObserveAccounts(f));
			if (cut < 0) return false;
			for (int i = cut; i < 6; i++)
			{
				if (!DeadExact(f, r) || KingdomResidentDeathRules.Prefix(r.BeforeAccounts, r.AfterAccounts, ObserveAccounts(f)) < i) return false;
				if (i < 5)
				{
					var next = KingdomResidentDeathCodec.ReadMap(r.AfterAccounts[i]);
					if (!DeadExact(f, r)) return false;
					PutMap(f, i, next);
				}
				else if (!PutHistory(f, r.AfterAccounts[5])) return false;
				if (!DeadExact(f, r) || KingdomResidentDeathRules.Prefix(r.BeforeAccounts, r.AfterAccounts, ObserveAccounts(f)) < i + 1) return false;
			}
			// Compatibility is a deterministic projection of the actual dead row, never a decrement.
			if (!KingdomResidents.ProjectCompatibility(f.System, Exact: true) || !DeadExact(f, r)
				|| !Equal(r.AfterAccounts, ObserveAccounts(f))) return false;
			var accounted = r.Copy(); accounted.Phase = KingdomResidentDeathPhase.Accounted;
			if (!Save(f, index, accounted)) return false; r = accounted; return true;
		}
		private static bool Seat(Frame f, out KingdomSettlement other)
		{
			if (!f.System.TryFindSettlement(f.City, out bool seated, out other)) throw new InvalidOperationException();
			return seated;
		}
		private static string[] ObserveAccounts(Frame f)
		{
			var values = new string[6];
			for (int i = 0; i < 5; i++) values[i] = KingdomResidentDeathCodec.Map(GetMap(f, i));
			values[5] = History(f); return values;
		}
		private static Dictionary<string, int> GetMap(Frame f, int index)
		{
			bool seat = Seat(f, out var other); var s = f.System;
			switch (index)
			{
			case 0: return seat ? s.CultureCounts : other.CultureCounts;
			case 1: return seat ? s.SpeciesCounts : other.SpeciesCounts;
			case 2: return seat ? s.IdentityCounts : other.IdentityCounts;
			case 3: return seat ? s.CreedCounts : other.CreedCounts;
			case 4: return seat ? s.CreedPastCounts : other.CreedPastCounts;
			default: throw new ArgumentOutOfRangeException();
			}
		}
		private static void PutMap(Frame f, int index, Dictionary<string, int> map)
		{
			bool seat = Seat(f, out var other); var s = f.System;
			switch (index)
			{
			case 0: if (seat) s.CultureCounts = map; else other.CultureCounts = map; break;
			case 1: if (seat) s.SpeciesCounts = map; else other.SpeciesCounts = map; break;
			case 2: if (seat) s.IdentityCounts = map; else other.IdentityCounts = map; break;
			case 3: if (seat) s.CreedCounts = map; else other.CreedCounts = map; break;
			case 4: if (seat) s.CreedPastCounts = map; else other.CreedPastCounts = map; break;
			default: throw new ArgumentOutOfRangeException();
			}
		}
		private static string History(Frame f)
		{
			bool seat = Seat(f, out var o); var s = f.System;
			var names = seat ? s.DeadNames : o.DeadNames; var origins = seat ? s.DeadOrigins : o.DeadOrigins;
			var arrived = seat ? s.DeadArrived : o.DeadArrived; var causes = seat ? s.DeadCauses : o.DeadCauses;
			int count = seat ? s.Dead : o.Dead;
			if (count < 0 || names == null || names.Count > 4096 || origins?.Count != names.Count
				|| arrived?.Count != names.Count || causes?.Count != names.Count) throw new InvalidOperationException();
			var fields = new List<string> { count.ToString(CultureInfo.InvariantCulture) };
			for (int i = 0; i < names.Count; i++) { fields.Add(names[i]); fields.Add(origins[i]); fields.Add(arrived[i]); fields.Add(causes[i]); }
			return KingdomResidentDeathCodec.Fields(fields.ToArray());
		}
		private static bool PutHistory(Frame f, string wire)
		{
			string[] values = KingdomResidentDeathCodec.ReadFields(wire); int dead = int.Parse(values[0], CultureInfo.InvariantCulture);
			var names = new List<string>(); var origins = new List<string>(); var arrived = new List<string>(); var causes = new List<string>();
			for (int i = 1; i < values.Length; i += 4) { names.Add(values[i]); origins.Add(values[i + 1]); arrived.Add(values[i + 2]); causes.Add(values[i + 3]); }
			if (!Exact(f)) return false;
			bool seat = Seat(f, out var o); var s = f.System;
			if (seat) { s.DeadNames = names; s.DeadOrigins = origins; s.DeadArrived = arrived; s.DeadCauses = causes; s.Dead = dead; }
			else { o.DeadNames = names; o.DeadOrigins = origins; o.DeadArrived = arrived; o.DeadCauses = causes; o.Dead = dead; }
			return Exact(f) && History(f) == wire;
		}
		private static bool Equal(string[] a, string[] b)
		{
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true;
		}
	}
}
