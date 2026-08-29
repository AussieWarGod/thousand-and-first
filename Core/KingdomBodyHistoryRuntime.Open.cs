#if !TAF_TESTS
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomBodyHistoryRuntime
	{
		/// <summary>Builds a view from the current loaded body and one C18 section lease.</summary>
		public static bool TryBuildCurrent(GameObject Actor, KingdomSystem System,
			out string View, out string Failure)
		{
			View = null;
			Failure = null;
			if (!KingdomBodyHistoryRulerLifeRuntime.TryReadCurrent(System, Actor,
				out KingdomRulerLifeSnapshot life, out Failure)) return false;
			long tick = The.Game?.TimeTicks ?? -1L;
			if (!KingdomBodyHistoryRuntime.TryReadLoaded(Actor, life.RulerLifeId,
				life.BodyObjectId, tick, out KingdomLiveAnatomySnapshot anatomy,
				out Failure)) return false;
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (memory == null)
				return KingdomBodyHistoryViewRules.TryComposeWithoutHistory(anatomy,
					"Civic memory is unavailable.", out View, out Failure);
			if (!memory.TryReadSection(KingdomCivicMemoryLimits.SectionBodyHistory,
				out KingdomCivicMemorySectionLease lease, out Failure))
			{
				string readFailure = Failure;
				return KingdomBodyHistoryViewRules.TryComposeWithoutHistory(anatomy,
					readFailure, out View, out Failure);
			}
			KingdomBodyHistoryEnvelope history = KingdomBodyHistoryStore.ReadForRealm(
				lease.Present ? lease.Payload() : null, life.RealmId, out Failure);
			if (Failure != null || history == null || history.Quarantined
				|| history.IsOpaqueFuture)
			{
				string historyFailure = Failure ?? "Body history is not readable by this build.";
				return KingdomBodyHistoryViewRules.TryComposeWithoutHistory(anatomy,
					historyFailure, out View, out Failure);
			}
			return KingdomBodyHistoryViewRules.TryCompose(anatomy, history.Book,
				out View, out Failure);
		}

		/// <summary>Player-facing, mutation-free opener for Charter/UI fan-in.</summary>
		public static void OpenCurrent(GameObject Actor, KingdomSystem System)
		{
			if (TryBuildCurrent(Actor, System, out string view, out string failure))
				Popup.Show(view);
			else Popup.Show("Current body history is unavailable.\n\n"
				+ (failure ?? "Exact loaded evidence could not be proved."));
		}

	}
}
#endif
