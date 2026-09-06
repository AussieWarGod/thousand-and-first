using System;
using XRL;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceOptionRuntime
	{
		internal static bool TryRestore(KingdomSystem system, KingdomCityBook city, string stepWire,
			out KingdomSubsidenceOptionObservation observed, out string refusal)
		{
			observed = null;
			refusal = InvalidOwner;
			XRLGame game = The.Game;
			if (game == null || system == null || city == null || !ReferenceEquals(system.City, city)
				|| !ReferenceEquals(game.GetSystem<KingdomSystem>(), system)
				|| city.SubsidenceModel != stepWire || !city.HasValidSubsidenceStorage()
				|| !KingdomSubsidenceStepCodec.TryDecode(stepWire, out KingdomSubsidenceStepBook book)
				|| book.RealmId != system.CurrentRealmId || book.SettlementId != city.SettlementId
				|| !KingdomSubsidenceOptionIntentRules.TryDecode(book.OptionModel,
					out KingdomSubsidenceOptionIntent intent)
				|| !KingdomSubsidenceOptionIntentRules.Matches(intent, book)
				|| !KingdomSubsidenceOptionIntentRules.TrySnapshot(intent,
					out KingdomSubsidenceOptionRules.Snapshot snapshot)) return false;
			if (snapshot.Decision.Record.MasterResumeToken > system.MasterAppliedResumeToken
				|| snapshot.Decision.Record.ObservedTick > game.TimeTicks)
			{ refusal = InvalidDecision; return false; }
			if (!TryTables(game, out refusal)) return false;
			string key = KingdomSubsidence.OptionStatePrefix + book.SettlementId;
			KingdomSubsidenceOptionTables prior = new KingdomSubsidenceOptionTables(
				new KingdomDurableKeyObservation { HasString = snapshot.Present, String = snapshot.PriorWire });
			KingdomSubsidenceOptionObservation candidate = new KingdomSubsidenceOptionObservation(
				game, system, city, book.RealmId, book.SettlementId, key,
				system.MasterAppliedResumeToken, prior, snapshot);
			KingdomDurableKeyObservation current = Observe(game, key);
			if (!KingdomSubsidenceOptionRules.ProvesPublished(snapshot, current)
				&& !KingdomSubsidenceOptionRules.CanPublish(snapshot, current, out string _))
			{ refusal = ForeignBytes; return false; }
			if (!ReprovesExact(candidate, out refusal) || city.SubsidenceModel != stepWire) return false;
			observed = candidate;
			return true;
		}

		internal static bool TryConfirm(KingdomSubsidenceOptionObservation observed,
			out KingdomDurableKeyObservation row)
		{
			row = default(KingdomDurableKeyObservation);
			if (observed == null || !ReprovesExact(observed, out string _)) return false;
			KingdomDurableKeyObservation current = Observe(observed.Game, observed.Key);
			if (!ReprovesExact(observed, out string _)
				|| !KingdomSubsidenceOptionRules.ProvesPublished(observed.Snapshot, current)) return false;
			row = current;
			return true;
		}
	}
}
