using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityFactionRuntime
	{
		internal const string RealmTransitionProperty =
			"r_TAF_PolityRealmTransition_v1";
		internal const string RealmTransitionDigestProperty =
			"r_TAF_PolityRealmTransitionDigest_v1";
		internal const string RealmTransitionTombstoneProperty =
			"r_TAF_PolityRealmTransitionTombstone_v1";

		internal static bool TryApplyRealmExileTombstones(
			KingdomPolityRealmTransition Transition, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidateRealmTransition(Transition, out Failure) ||
				Transition.Phase != KingdomPolityRealmTransitionPhase.Prepared ||
				!KingdomPolityRules.TryTransitionLedger(Transition,
					out KingdomPolityLedger source)) return false;
			if (!TrySetCurrentFaction(Transition, Hidden: true, out Failure)) return false;
			return TrySetImportedFaction(Transition, source, HiddenForReturn: true, out Failure) &&
				RealmExileTombstonesObserved(Transition);
		}

		internal static bool TryRestoreRealmExileFactions(
			KingdomPolityRealmTransition Transition, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidateRealmTransition(Transition, out Failure) ||
				(Transition.Phase != KingdomPolityRealmTransitionPhase.Detached &&
				 Transition.Phase != KingdomPolityRealmTransitionPhase.Restored) ||
				!KingdomPolityRules.TryTransitionLedger(Transition,
					out KingdomPolityLedger source)) return false;
			if (!TrySetCurrentFaction(Transition, Hidden: false, out Failure)) return false;
			return TrySetImportedFaction(Transition, source, HiddenForReturn: false, out Failure);
		}

		internal static bool RealmExileTombstonesObserved(
			KingdomPolityRealmTransition Transition)
		{
			if (Transition == null) return false;
			Faction current = Factions.GetIfExists(Transition.OldCurrentFactionId);
			if (!CurrentMarked(current, Transition) || current.Visible ||
				current.GetIntProperty("PlayerKingdom") != 0 ||
				current.GetIntProperty(RealmTransitionTombstoneProperty) != 1) return false;
			if (string.IsNullOrEmpty(Transition.OldImportedProjectionId)) return true;
			KingdomPolityFactionProjectionView view = OldImportedView(Transition);
			Faction imported = Factions.GetIfExists(view.FactionId);
			return OwnedExactly(imported, Transition.OldRealmId, view) && !imported.Visible &&
				imported.GetIntProperty(TombstoneProperty) == 1 &&
				imported.GetStringProperty(RealmTransitionProperty, null) == Transition.TransitionId &&
				imported.GetStringProperty(RealmTransitionDigestProperty, null) ==
					ImportedTransitionDigest(Transition);
		}

		/// <summary>Read-only proof used by schema migration. A prepared transition may still
		/// expose its exact active prestate or its exact hidden poststate; later phases have one
		/// canonical presentation. Mixed and foreign marker states never authorize legacy reads.</summary>
		internal static bool OldCurrentFactionObserved(KingdomPolityRealmTransition T,
			Faction F, bool AllowDetachedActive, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidateRealmTransition(T, out Failure) || F == null ||
				F.Name != T.OldCurrentFactionId || F.Name != T.OldRealmId ||
				F.GetIntProperty("Village") != 1 ||
				F.GetIntProperty("TAFFoundingPending") != 0)
				return FailObservation("old current faction lacks founding provenance", out Failure);
			string marker = F.GetStringProperty(RealmTransitionProperty, null);
			string digest = F.GetStringProperty(RealmTransitionDigestProperty, null);
			if (marker == null && digest == null &&
				F.GetIntProperty(RealmTransitionTombstoneProperty) == 0)
				return T.Phase == KingdomPolityRealmTransitionPhase.Prepared && F.Visible &&
					F.GetIntProperty("PlayerKingdom") == 1 && F.WaterRitualLiquid == "water" ||
					FailObservation("unmarked old faction is not the prepared prestate", out Failure);
			bool marked = CurrentMarked(F, T);
			bool active = marked && F.Visible && F.GetIntProperty("PlayerKingdom") == 1 &&
				F.GetIntProperty(RealmTransitionTombstoneProperty) == 0 &&
				F.WaterRitualLiquid == "water";
			bool hidden = marked && !F.Visible && F.GetIntProperty("PlayerKingdom") == 0 &&
				F.GetIntProperty(RealmTransitionTombstoneProperty) == 1 &&
				F.WaterRitualLiquid == null;
			return ((T.Phase == KingdomPolityRealmTransitionPhase.Restored ||
				 T.Phase == KingdomPolityRealmTransitionPhase.Prepared) && active) ||
				(T.Phase == KingdomPolityRealmTransitionPhase.Detached &&
				 AllowDetachedActive && (active || hidden)) ||
				(T.Phase != KingdomPolityRealmTransitionPhase.Restored &&
				 !(T.Phase == KingdomPolityRealmTransitionPhase.Detached &&
				   AllowDetachedActive) && hidden) ||
				FailObservation("old current faction transition presentation differs", out Failure);
		}

		private static bool FailObservation(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}

		internal static bool TryReconcileRealmExileTombstones(
			KingdomPolityRealmTransition Transition, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidateRealmTransition(Transition, out Failure) ||
				!TrySetCurrentFaction(Transition, Hidden: true, out Failure)) return false;
			if (string.IsNullOrEmpty(Transition.OldImportedProjectionId)) return true;
			if (Transition.ReturnLedgerEnvelope != null &&
				KingdomPolityRules.TryTransitionLedger(Transition, out KingdomPolityLedger source))
				return TrySetImportedFaction(Transition, source, HiddenForReturn: true, out Failure);
			KingdomPolityFactionProjectionView view = OldImportedView(Transition);
			Faction faction = Factions.GetIfExists(view.FactionId);
			if (!OwnedExactly(faction, Transition.OldRealmId, view))
			{
				Failure = "committed old imported faction projection is missing"; return false;
			}
			string marker = faction.GetStringProperty(RealmTransitionProperty, null);
			if (!string.IsNullOrEmpty(marker) && marker != Transition.TransitionId)
			{
				Failure = "old imported faction carries foreign transition evidence"; return false;
			}
			faction.SetProperty(RealmTransitionProperty, Transition.TransitionId);
			faction.SetProperty(RealmTransitionDigestProperty,
				ImportedTransitionDigest(Transition));
			faction.Visible = false; faction.WaterRitualLiquid = null;
			faction.SetProperty(TombstoneProperty, 1);
			faction.SetProperty(RealmTransitionTombstoneProperty, 1);
			return RealmExileTombstonesObserved(Transition);
		}

		internal static bool TryReleaseRealmReturnMarkers(
			KingdomPolityRealmTransition Transition, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidateRealmTransition(Transition, out Failure) ||
				Transition.Phase != KingdomPolityRealmTransitionPhase.Restored ||
				!KingdomPolityRules.TryTransitionLedger(Transition,
					out KingdomPolityLedger source) ||
				!TrySetCurrentFaction(Transition, Hidden: false, out Failure) ||
				!TrySetImportedFaction(Transition, source, HiddenForReturn: false, out Failure))
				return false;
			Faction current = Factions.GetIfExists(Transition.OldCurrentFactionId);
			if (!TryReleaseMarker(current, Transition, CurrentTransitionDigest(Transition),
				out Failure)) return false;
			if (string.IsNullOrEmpty(Transition.OldImportedProjectionId)) return true;
			return TryReleaseMarker(Factions.GetIfExists(Transition.OldImportedFactionId),
				Transition, ImportedTransitionDigest(Transition), out Failure);
		}

		private static bool TrySetCurrentFaction(KingdomPolityRealmTransition T, bool Hidden,
			out string Failure)
		{
			Failure = null; Faction faction = Factions.GetIfExists(T.OldCurrentFactionId);
			if (faction == null || faction.Name != T.OldRealmId ||
				faction.GetIntProperty("Village") != 1 ||
				faction.GetIntProperty("TAFFoundingPending") != 0)
			{
				Failure = "old current faction lacks exact founding ownership"; return false;
			}
			string marker = faction.GetStringProperty(RealmTransitionProperty, null);
			if (string.IsNullOrEmpty(marker))
			{
				if (!faction.Visible || faction.GetIntProperty("PlayerKingdom") != 1)
				{
					Failure = "old current faction reached an unowned prestate"; return false;
				}
				faction.SetProperty(RealmTransitionProperty, T.TransitionId);
				faction.SetProperty(RealmTransitionDigestProperty, CurrentTransitionDigest(T));
			}
			else if (!CurrentMarked(faction, T))
			{
				Failure = "old current faction carries foreign transition evidence"; return false;
			}
			faction.Visible = !Hidden; faction.WaterRitualLiquid = Hidden ? null : "water";
			faction.SetProperty("PlayerKingdom", Hidden ? 0 : 1);
			faction.SetProperty(RealmTransitionTombstoneProperty, Hidden ? 1 : 0);
			return CurrentMarked(faction, T) && faction.Visible == !Hidden &&
				faction.GetIntProperty("PlayerKingdom") == (Hidden ? 0 : 1);
		}

		private static bool TrySetImportedFaction(KingdomPolityRealmTransition T,
			KingdomPolityLedger Source, bool HiddenForReturn, out string Failure)
		{
			Failure = null;
			if (string.IsNullOrEmpty(T.OldImportedProjectionId)) return true;
			KingdomPolityRecord polity = null;
			for (int i = 0; i < Source.Polities.Count; i++)
				if (Source.Polities[i].PolityId == T.OldImportedPolityId) polity = Source.Polities[i];
			KingdomPolityFactionProjectionView view = OldImportedView(T);
			Faction faction = Factions.GetIfExists(view.FactionId);
			if (polity != null && faction == null && !TryCreateOrRecover(T.OldRealmId, polity,
				view, out faction, out bool foreign, out Failure))
			{
				if (foreign) Failure = "old imported faction key carries foreign authority";
				return false;
			}
			if (polity == null || !OwnedExactly(faction, T.OldRealmId, view))
			{
				Failure = "old imported faction lacks exact projection ownership"; return false;
			}
			string marker = faction.GetStringProperty(RealmTransitionProperty, null);
			if (!string.IsNullOrEmpty(marker) && (marker != T.TransitionId ||
				faction.GetStringProperty(RealmTransitionDigestProperty, null) !=
				ImportedTransitionDigest(T)))
			{
				Failure = "old imported faction carries foreign transition evidence"; return false;
			}
			faction.SetProperty(RealmTransitionProperty, T.TransitionId);
			faction.SetProperty(RealmTransitionDigestProperty, ImportedTransitionDigest(T));
			bool hidden = HiddenForReturn || polity.Lifecycle != KingdomPolityLifecycle.Active;
			if (!ReapplyPresentation(faction, polity, hidden))
			{
				Failure = "old imported faction presentation cannot reconcile"; return false;
			}
			faction.SetProperty(RealmTransitionTombstoneProperty, hidden ? 1 : 0);
			return faction.Visible == !hidden;
		}

		private static bool CurrentMarked(Faction F, KingdomPolityRealmTransition T)
		{
			return F != null && F.GetStringProperty(RealmTransitionProperty, null) ==
				T.TransitionId && F.GetStringProperty(RealmTransitionDigestProperty, null) ==
				CurrentTransitionDigest(T);
		}

		private static bool TryReleaseMarker(Faction F, KingdomPolityRealmTransition T,
			string Digest, out string Failure)
		{
			Failure = null;
			if (F == null || F.GetStringProperty(RealmTransitionProperty, null) != T.TransitionId ||
				F.GetStringProperty(RealmTransitionDigestProperty, null) != Digest)
			{
				Failure = "returned faction lacks exact transition marker"; return false;
			}
			F.RemoveProperty(RealmTransitionProperty);
			F.RemoveProperty(RealmTransitionDigestProperty);
			F.RemoveProperty(RealmTransitionTombstoneProperty);
			return string.IsNullOrEmpty(F.GetStringProperty(RealmTransitionProperty, null)) &&
				string.IsNullOrEmpty(F.GetStringProperty(RealmTransitionDigestProperty, null));
		}

		private static KingdomPolityFactionProjectionView OldImportedView(
			KingdomPolityRealmTransition T)
		{
			return new KingdomPolityFactionProjectionView
			{
				PolityId = T.OldImportedPolityId, FactionId = T.OldImportedFactionId,
				ProjectionId = T.OldImportedProjectionId,
				AppliedDigest = T.OldImportedProjectionDigest,
				Phase = KingdomPolityProjectionPhase.Committed
			};
		}

		private static string CurrentTransitionDigest(KingdomPolityRealmTransition T)
		{
			return KingdomPolityRules.ActivationDigest("polity-current-faction-exile-v1",
				T.TransitionId, T.OldCurrentFactionId, T.OldCurrentProjectionDigest);
		}

		private static string ImportedTransitionDigest(KingdomPolityRealmTransition T)
		{
			return KingdomPolityRules.ActivationDigest("polity-imported-faction-ending-v1",
				T.TransitionId, T.OldImportedFactionId, T.OldImportedProjectionDigest);
		}
	}
}
