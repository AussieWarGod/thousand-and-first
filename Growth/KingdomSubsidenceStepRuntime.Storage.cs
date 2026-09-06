using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private sealed class Snapshot
		{
			internal readonly KingdomCityBook City;
			internal readonly string Wire;
			internal readonly KingdomSubsidenceStepBook Step;
			internal Snapshot(KingdomCityBook city, string wire, KingdomSubsidenceStepBook step)
			{ City = city; Wire = wire; Step = step; }
		}

		private static bool TryReadOwned(KingdomSystem system, out List<Snapshot> snapshots)
		{
			snapshots = null;
			string realm = system?.CurrentRealmId;
			if (!KingdomIdentityRules.IsRealmId(realm)) return false;
			List<KingdomCityBook> books = system.OwnedCityBooks();
			if (books.Count != 1 + system.NonSeatSettlementCount
				|| books.Count > KingdomIdentityRules.MaxSettlements) return false;
			HashSet<KingdomCityBook> references = new HashSet<KingdomCityBook>();
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			List<Snapshot> found = new List<Snapshot>();
			int pending = 0;
			foreach (KingdomCityBook city in books)
			{
				if (city == null || !references.Add(city) || !identities.Add(city.SettlementId)
					|| !KingdomIdentityRules.IsSettlementId(city.SettlementId)
					|| !city.HasValidSubsidenceStorage()) return false;
				string wire = city.SubsidenceModel;
				if (!KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook step)
					|| step.Admission == KingdomSubsidenceAdmission.Admitted
						&& (step.RealmId != realm || step.SettlementId != city.SettlementId)) return false;
				if (step.Active != null && step.Active.PendingDepartureId != "" && ++pending > 1) return false;
				found.Add(new Snapshot(city, wire, step));
			}
			snapshots = found; return true;
		}

		private static bool TryMatch(KingdomSystem system, KingdomResidentDepartureOperation departure,
			out Snapshot owner, out bool associated)
		{
			owner = null; associated = false;
			if (!KingdomResidentDepartureRules.Valid(departure)
				|| system?.CurrentRealmId != departure.RealmId
				|| !TryReadOwned(system, out List<Snapshot> books)) return false;
			foreach (Snapshot item in books)
			{
				if (item.City.SettlementId == departure.SettlementId) owner = item;
				KingdomSubsidenceStepOperation active = item.Step.Active;
				if (active == null || active.PendingDepartureId == "") continue;
				if (item.City.SettlementId != departure.SettlementId
					|| active.PendingDepartureId != departure.OperationId
					|| !active.PendingIdentity.Matches(departure)
					|| active.Phase == KingdomSubsidenceStepPhase.Quarantined) return false;
				associated = true;
			}
			return owner != null;
		}

		private static bool Publish(KingdomSystem system, Snapshot prior, KingdomSubsidenceStepBook next)
		{
			if (prior == null || next == null || next.RealmId != prior.Step.RealmId
				|| next.SettlementId != prior.Step.SettlementId
				|| !KingdomSubsidenceStepCodec.TryEncode(next, out string wire)
				|| !TryReadOwned(system, out List<Snapshot> current)) return false;
			foreach (Snapshot item in current)
			{
				if (!ReferenceEquals(item.City, prior.City)) continue;
				if (item.Wire != prior.Wire || !KingdomResidentDeathRuntime.CanProceed(system, out _)) return false;
				item.City.SubsidenceModel = wire;
				return item.City.SubsidenceModel == wire;
			}
			return false;
		}
	}
}
