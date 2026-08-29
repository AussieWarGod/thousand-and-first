using System;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		/// <summary>Atomically observes the three persisted presentation policies. Active leases
		/// remain source-owned: an option transition blocks new emission immediately, but cannot
		/// erase accounting for a prepared, live, or frozen projection.</summary>
		public static bool TryObserveOptions(KingdomExperienceLedger Ledger, long ExpectedRevision,
			bool StoryEnabled, bool KnowledgeEnabled, bool AmbientEnabled, long Tick,
			out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!Ledger.IdentityBound)
				return Fail("experience option authority is not realm-bound", out Failure);
			if (ExpectedRevision != Ledger.Revision)
				return Fail("experience option revision conflict", out Failure);
			if (Tick < 0L || Tick < Ledger.Story.ObservedTick
				|| Tick < Ledger.Knowledge.ObservedTick || Tick < Ledger.Ambient.ObservedTick)
				return Fail("experience option observation regresses time", out Failure);

			bool changes = Changes(Ledger.Story, StoryEnabled)
				|| Changes(Ledger.Knowledge, KnowledgeEnabled)
				|| Changes(Ledger.Ambient, AmbientEnabled);
			if (!changes) return true;
			if (Ledger.Revision == long.MaxValue)
				return Fail("experience revision is exhausted", out Failure);
			if ((StoryEnabled && Ledger.Story.State != KingdomExperienceOptionState.Enabled
					&& Ledger.Story.EnableEpoch == long.MaxValue)
				|| (KnowledgeEnabled && Ledger.Knowledge.State != KingdomExperienceOptionState.Enabled
					&& Ledger.Knowledge.EnableEpoch == long.MaxValue)
				|| (AmbientEnabled && Ledger.Ambient.State != KingdomExperienceOptionState.Enabled
					&& Ledger.Ambient.EnableEpoch == long.MaxValue))
				return Fail("experience option enable epoch is exhausted", out Failure);

			KingdomExperienceLedger candidate = Clone(Ledger);
			Observe(candidate.Story, StoryEnabled, Tick);
			Observe(candidate.Knowledge, KnowledgeEnabled, Tick);
			Observe(candidate.Ambient, AmbientEnabled, Tick);
			candidate.Revision++;
			if (!TryValidate(candidate, out Failure)) return false;
			Ledger.CopyFrom(candidate); return true;
		}

		public static bool CanEmit(KingdomExperienceLedger Ledger,
			KingdomExperienceOptionKind Kind, long CauseTick)
		{
			if (!TryValidate(Ledger, out string _) || CauseTick < 0L) return false;
			KingdomExperienceOptionReceipt option = OptionFor(Ledger, Kind);
			return option != null && option.State == KingdomExperienceOptionState.Enabled
				&& CauseTick >= option.FutureCauseFloorTick;
		}

		public static bool TryGetEnableEpoch(KingdomExperienceLedger Ledger,
			KingdomExperienceOptionKind Kind, long CauseTick, out long EnableEpoch,
			out string Failure)
		{
			EnableEpoch = 0L; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			KingdomExperienceOptionReceipt option = OptionFor(Ledger, Kind);
			if (CauseTick < 0L || option == null || option.State !=
				KingdomExperienceOptionState.Enabled || CauseTick < option.FutureCauseFloorTick)
				return Fail("experience cause is not enabled in the current option epoch",
					out Failure);
			EnableEpoch = option.EnableEpoch; return true;
		}

		private static bool Changes(KingdomExperienceOptionReceipt O, bool Enabled)
		{
			KingdomExperienceOptionState desired = Enabled
				? KingdomExperienceOptionState.Enabled : KingdomExperienceOptionState.Disabled;
			return O.State != desired;
		}

		private static void Observe(KingdomExperienceOptionReceipt O, bool Enabled, long Tick)
		{
			KingdomExperienceOptionState desired = Enabled
				? KingdomExperienceOptionState.Enabled : KingdomExperienceOptionState.Disabled;
			if (O.State == desired) return;
			if (Enabled)
			{
				O.EnableEpoch++; O.FutureCauseFloorTick = Tick;
			}
			else O.FutureCauseFloorTick = long.MaxValue;
			O.State = desired; O.ObservedTick = Tick;
		}

	}
}
