using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Exact Core seam adapter for exile, return, and refounding polity authority.</summary>
	internal static class KingdomPolityRealmTransitionRuntime
	{
		internal static bool TryAdvanceExile(KingdomSystem System,
			KingdomRealmArchive Archive, out string Failure)
		{
			Failure = null;
			if (System == null || Archive == null || System.PolityLedger == null ||
				Archive.RealmId != System.RealmId || Archive.FactionName != System.KingdomFactionName)
			{
				Failure = "Core exile identity does not match polity authority"; return false;
			}
			if (System.PolityTransition == null)
				System.PolityTransition = new KingdomPolityRealmTransition();
			KingdomPolityRealmTransition transition = System.PolityTransition;
			if (transition.Phase == KingdomPolityRealmTransitionPhase.Quarantined)
			{
				Failure = transition.Fault ?? "polity realm transition is quarantined"; return false;
			}
			if (transition.Phase == KingdomPolityRealmTransitionPhase.None)
			{
				if (!KingdomPolityRealmLegacyFacts.TryCreate(Archive, System.PolityLedger,
					The.Player?.BaseDisplayNameStripped, out KingdomPolityRealmExileFacts facts,
					out Failure) || !KingdomPolityRules.TryPrepareRealmExile(System.PolityLedger,
					System.PolityLedger.Revision, facts, out KingdomPolityRealmTransition prepared,
					out KingdomPolityPublicationResult _, out Failure)) return false;
				System.PolityTransition = transition = prepared;
			}
			if (!ArchiveMatches(Archive, transition, out Failure)) return false;
			if (transition.Phase == KingdomPolityRealmTransitionPhase.Prepared)
			{
				if (!KingdomPolityFactionRuntime.TryApplyRealmExileTombstones(transition,
					out Failure) || !KingdomPolityRules.TryMarkRealmExileTombstoned(
					System.PolityLedger, System.PolityLedger.Revision, transition,
					transition.Revision, out KingdomPolityPublicationResult _, out Failure)) return false;
			}
			if (transition.Phase == KingdomPolityRealmTransitionPhase.Tombstoned &&
				!KingdomPolityRules.TryDetachRealmExile(System.PolityLedger,
					System.PolityLedger.Revision, transition, transition.Revision,
					out KingdomPolityPublicationResult _, out Failure)) return false;
			return transition.Phase == KingdomPolityRealmTransitionPhase.Detached ||
				Fail("polity exile did not reach detached authority", out Failure);
		}

		internal static bool TryRestoreReturn(KingdomSystem System,
			KingdomRealmArchive Archive, out string Failure)
		{
			Failure = null; KingdomPolityRealmTransition transition = System?.PolityTransition;
			if (System?.PolityLedger == null || Archive == null || transition == null ||
				!ArchiveMatches(Archive, transition, out Failure) ||
				!KingdomPolityFactionRuntime.TryRestoreRealmExileFactions(transition, out Failure))
				return false;
			return KingdomPolityRules.TryRestoreRealmReturn(System.PolityLedger,
				System.PolityLedger.Revision, transition, transition.Revision, Archive.RealmId,
				out KingdomPolityPublicationResult _, out Failure);
		}

		internal static bool TryCompleteReturn(KingdomSystem System, string RealmId,
			out string Failure)
		{
			Failure = null; KingdomPolityRealmTransition transition = System?.PolityTransition;
			if (transition == null) return false;
			if (transition.Phase == KingdomPolityRealmTransitionPhase.Restored &&
				!KingdomPolityFactionRuntime.TryReleaseRealmReturnMarkers(transition,
					out Failure)) return false;
			return KingdomPolityRules.TryCompleteRealmReturn(System.PolityLedger,
					System.PolityTransition, System.PolityTransition.Revision, RealmId,
					out KingdomPolityPublicationResult _, out Failure);
		}

		internal static bool TryFoundationLegacy(KingdomSystem System,
			out KingdomPolityLegacySnapshot Legacy, out bool FromTransition, out string Failure)
		{
			Legacy = null; FromTransition = false; Failure = null;
			KingdomPolityRealmTransition transition = System?.PolityTransition;
			if (transition == null || transition.Phase == KingdomPolityRealmTransitionPhase.None ||
				transition.Phase == KingdomPolityRealmTransitionPhase.Rebound ||
				transition.Phase == KingdomPolityRealmTransitionPhase.Restored) return true;
			if (transition.Phase != KingdomPolityRealmTransitionPhase.Detached)
				return Fail("polity realm transition blocks a new foundation", out Failure);
			FromTransition = KingdomPolityRules.TryGetRealmTransitionLegacy(transition,
				out Legacy, out Failure); return FromTransition;
		}

		internal static bool TryCommitRefound(KingdomSystem System, out string Failure)
		{
			Failure = null; KingdomPolityRealmTransition transition = System?.PolityTransition;
			if (transition == null || transition.Phase == KingdomPolityRealmTransitionPhase.None ||
				transition.Phase == KingdomPolityRealmTransitionPhase.Restored) return true;
			if (transition.Phase == KingdomPolityRealmTransitionPhase.Rebound)
			{
				if (!KingdomPolityFactionRuntime.TryReconcileRealmExileTombstones(
					transition, out Failure)) return false;
				if (KingdomPolityRules.TryCommitRealmRefound(System.PolityLedger, transition,
					transition.Revision, out KingdomPolityPublicationResult _, out Failure)) return true;
				KingdomPolityRules.Quarantine(System.PolityLedger,
					Failure ?? "refounded polity receipt differs from current authority"); return false;
			}
			if (transition.Phase != KingdomPolityRealmTransitionPhase.Detached)
				return Fail("polity transition is not ready to refound", out Failure);
			if (!KingdomPolityFactionRuntime.TryReconcileRealmExileTombstones(transition,
				out Failure)) return false;
			return KingdomPolityRules.TryCommitRealmRefound(System.PolityLedger, transition,
				transition.Revision, out KingdomPolityPublicationResult _, out Failure);
		}

		internal static bool RefoundObserved(KingdomSystem System)
		{
			KingdomPolityRealmTransition transition = System?.PolityTransition;
			if (transition == null || transition.Phase == KingdomPolityRealmTransitionPhase.None ||
				transition.Phase == KingdomPolityRealmTransitionPhase.Restored) return true;
			if (!KingdomPolityRules.TryValidateRealmTransition(transition, out string _) ||
				transition.Phase != KingdomPolityRealmTransitionPhase.Rebound ||
				transition.ReboundRealmId != System.RealmId || transition.ReturnLedgerEnvelope != null ||
				!KingdomPolityFactionRuntime.RealmExileTombstonesObserved(transition)) return false;
			for (int i = 0; i < System.PolityLedger.Polities.Count; i++)
			{
				KingdomPolityRecord p = System.PolityLedger.Polities[i];
				if (p.Source == KingdomPolitySource.ImportedLegacy)
					return p.PolityId == transition.ReboundPolityId &&
						p.ProjectedFactionId == transition.ReboundFactionId &&
						p.Lifecycle == KingdomPolityLifecycle.Active;
			}
			return false;
		}

		internal static void Normalize(KingdomSystem System)
		{
			if (System == null) return;
			if (System.PolityTransition == null)
				System.PolityTransition = new KingdomPolityRealmTransition();
			KingdomPolityRules.NormalizeRealmTransition(System.PolityTransition);
			if (System.PolityTransition.Phase == KingdomPolityRealmTransitionPhase.Quarantined &&
				System.PolityLedger != null)
				KingdomPolityRules.Quarantine(System.PolityLedger,
					System.PolityTransition.Fault ?? "realm transition is quarantined");
		}

		private static bool ArchiveMatches(KingdomRealmArchive A,
			KingdomPolityRealmTransition T, out string Failure)
		{
			Failure = null;
			return T != null && KingdomPolityRules.TryValidateRealmTransition(T, out Failure) &&
				A.RealmId == T.OldRealmId && A.FactionName == T.OldCurrentFactionId &&
				A.ClosedTick == T.ClosedTick || Fail(Failure ??
					"exile archive differs from polity transition", out Failure);
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason; return false;
		}
	}
}
