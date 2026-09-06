using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		// Called only after exact founding seal, reservations, markers and retired custody.
		// A native tombstone proves a fresh cut; the persisted Removed phase survives its pooling.
		private static bool ExactFoundingHeartRetirementProof(Zone Z,
			FoundingHeartContext Context, string PredecessorId)
		{
			KingdomPhysicalLookupState state = FindGraveyardTombstone(PredecessorId,
				out GameObject tombstone);
			if (state == KingdomPhysicalLookupState.Exact)
				return FoundingHeartTombstoneIdentity(tombstone, Context.Plan,
					KingdomFoundingHeartRules.WorksSlot);
			if (state != KingdomPhysicalLookupState.Absent) return false;
			string raw = Z?.GetZoneProperty(FoundingHeartTerminalProperty, null);
			if (!KingdomFoundingHeartTerminalRules.TryDecode(raw, out var terminal)
				|| !FoundingHeartTerminalBinding(Context, terminal)
				|| terminal.PredecessorId != PredecessorId
				|| !string.IsNullOrEmpty(Z.GetZoneProperty(FoundingHeartTerminalFailureProperty, null)))
				return false;
			bool exactFinal = ExactFoundingHeartRecordedFinal(Z, Context, terminal, raw);
			return KingdomFoundingHeartTerminalRules.CanUseRecordedRemoval(terminal, exactFinal,
				ExactFoundingHeartLiveAbsence(PredecessorId), state);
		}

		private static bool ExactFoundingHeartRecordedFinal(Zone Z, FoundingHeartContext Context,
			KingdomFoundingHeartTerminalPlan Terminal, string Raw)
		{
			if (FindGlobalFoundingHeartId(Terminal.FinalId, out GameObject final, out bool graveyard)
					!= KingdomPhysicalLookupState.Exact || graveyard
				|| !ExactPreparedFoundingHeartFinal(final, Z, Context, Terminal)
				|| final.CurrentCell == null || final.CurrentCell != Z.GetCell(Terminal.X, Terminal.Y)
				|| !ExactFoundingHeartFinalTruth(final, Context.Stake)
				|| !ExactFoundingHeartString(final, KingdomUpgrade.BuildKeyProperty, Terminal.BuildKey)
				|| !ExactFoundingHeartString(final, PlotIdProperty, Terminal.PlotId)
				|| !ExactFoundingHeartString(final, FoundingHeartTerminalProperty, Raw)
				|| final.HasIntProperty(FoundingHeartTerminalFailureProperty)
				|| !string.IsNullOrEmpty(final.GetStringProperty(FoundingHeartTerminalFailureProperty))
				|| !r_KingdomScaffold.HasRemovalProof(final, Terminal.PredecessorId)) return false;
			// Do not call ExactSettledFoundingHeartFinal here: its full component verifier can
			// quarantine on failure. The terminal driver still runs that audit after this proof.
			FoundingHeartReservationStore store = new FoundingHeartReservationStore();
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
				if (KingdomScenarioStateShape.Classify(store.Observe(FoundingHeartRootKey(Context.Plan, slot)), out _)
					!= KingdomDurableKeyShape.Absent) return false;
			var root = store.Observe(FoundingHeartFinalRootKey(Context.Plan));
			if (root == null || root.HasString || root.HasInt || root.HasInt64 || root.HasBoolean)
				return false;
			bool rooted = ExactFoundingHeartFinalObjectGameState(Context.Plan, final, true);
			return store.Current && (Terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled
				? rooted || !root.HasObject && ExactFoundingHeartFinalObjectGameState(Context.Plan, final, false)
				: rooted);
		}
	}
}
