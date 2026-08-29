using System;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>Owned crash-recoverable projection into Qud's irreversible faction registry.</summary>
	public static partial class KingdomPolityFactionRuntime
	{
		internal const string OwnerRealmProperty = "r_TAF_PolityOwnerRealm_v1";
		internal const string SourcePolityProperty = "r_TAF_PolitySource_v1";
		internal const string ProjectionProperty = "r_TAF_PolityProjection_v1";
		internal const string AppliedDigestProperty = "r_TAF_PolityDigest_v1";
		internal const string TombstoneProperty = "r_TAF_PolityTombstone_v1";

		public static bool TryReconcile(KingdomSystem System, long Tick, out string Failure)
		{
			Failure = null;
			if (System == null || System.PolityLedger == null || Tick < 0L ||
				!KingdomPolityRules.TryValidate(System.PolityLedger, out Failure)) return false;
			KingdomPolityRecord polity = Imported(System.PolityLedger);
			if (polity == null) return true;
			if (!KingdomPolityRules.TryGetImportedFactionProjection(System.PolityLedger,
				out KingdomPolityFactionProjectionView view, out string _))
			{
				if (polity.Lifecycle != KingdomPolityLifecycle.Latent ||
					!KingdomPolityRules.TryPrepareLegacyFaction(System.PolityLedger,
						System.PolityLedger.Revision, Tick,
						out KingdomPolityPublicationResult _, out Failure) ||
					!KingdomPolityRules.TryGetImportedFactionProjection(System.PolityLedger,
						out view, out Failure)) return false;
			}
			Faction faction = Factions.GetIfExists(view.FactionId);
			if (view.Phase == KingdomPolityProjectionPhase.Prepared)
			{
				if (faction == null)
				{
					if (!TryCreateOrRecover(System.RealmId, polity, view, out faction,
						out bool foreign, out Failure))
						return foreign ? Quarantine(System, Failure) : false;
				}
				else if (!OwnedExactly(faction, System.RealmId, view))
					return Quarantine(System, "prepared faction key belongs to foreign authority", out Failure);
				if (!KingdomPolityRules.TryCommitLegacyFaction(System.PolityLedger,
					System.PolityLedger.Revision, view.ProjectionId, Tick,
					out KingdomPolityPublicationResult _, out Failure)) return false;
				view.Phase = KingdomPolityProjectionPhase.Committed;
			}
			if (faction == null && view.Phase == KingdomPolityProjectionPhase.Committed)
			{
				if (!TryCreateOrRecover(System.RealmId, polity, view, out faction,
					out bool foreign, out Failure))
					return foreign ? Quarantine(System, Failure) : false;
			}
			if (!OwnedExactly(faction, System.RealmId, view))
				return Quarantine(System, "committed faction projection lost exact ownership", out Failure);
			if (!ReapplyPresentation(faction, polity, Hidden: false))
				return Quarantine(System, "owned faction presentation could not reconcile", out Failure);
			return ReconcileTombstone(System, polity, faction, Tick, out Failure);
		}

		public static bool ProjectionObserved(KingdomSystem System, KingdomPolityRecord Polity)
		{
			if (System == null || Polity == null ||
				!KingdomPolityRules.TryGetImportedFactionProjection(System.PolityLedger,
					out KingdomPolityFactionProjectionView view, out string _)) return false;
			Faction faction = Factions.GetIfExists(view.FactionId);
			return view.Phase == KingdomPolityProjectionPhase.Committed && faction != null &&
				OwnedExactly(faction, System.RealmId, view) &&
				(Polity.Lifecycle != KingdomPolityLifecycle.Dormant || !faction.Visible);
		}

		private static bool TryCreate(string RealmId, KingdomPolityRecord Polity,
			KingdomPolityFactionProjectionView View, out Faction Faction, out string Failure)
		{
			Faction = null; Failure = null;
			if (Factions.Exists(View.FactionId))
			{
				Failure = "faction key became occupied before projection"; return false;
			}
			Faction candidate = new Faction
			{
				Old = false, ExtradimensionalVersions = false, Visible = true, Name = View.FactionId,
				DisplayName = Polity.DisplayName,
				PositiveSound = "Sounds/Reputation/sfx_reputation_village_positive",
				NegativeSound = "Sounds/Reputation/sfx_reputation_village_negative",
				WaterRitualLiquid = "water"
			};
			try
			{
				Stamp(candidate, RealmId, View);
				VillageBase.SetVillageFactionEmblem(candidate, candidate.Name);
				Factions.AddNewFaction(candidate);
			}
			catch (Exception ex)
			{
				Faction recovered = Factions.GetIfExists(View.FactionId);
				if (!OwnedExactly(recovered, RealmId, View))
				{
					Failure = "faction registration failed without exact recovery: " + ex.Message;
					return false;
				}
			}
			Faction = Factions.GetIfExists(View.FactionId);
			if (!ReferenceEquals(Faction, candidate) || !OwnedExactly(Faction, RealmId, View))
			{
				Failure = "faction registry did not retain the exact owned projection"; return false;
			}
			return true;
		}

		private static bool TryCreateOrRecover(string RealmId, KingdomPolityRecord Polity,
			KingdomPolityFactionProjectionView View, out Faction Faction, out bool Foreign,
			out string Failure)
		{
			Foreign = false;
			if (TryCreate(RealmId, Polity, View, out Faction, out Failure)) return true;
			Faction = Factions.GetIfExists(View.FactionId);
			if (Faction == null) return false;
			if (OwnedExactly(Faction, RealmId, View)) return true;
			Foreign = true; Failure = "faction key carries foreign evidence after publication failure";
			return false;
		}

		private static void Stamp(Faction F, string RealmId, KingdomPolityFactionProjectionView V)
		{
			F.SetProperty(OwnerRealmProperty, RealmId); F.SetProperty(SourcePolityProperty, V.PolityId);
			F.SetProperty(ProjectionProperty, V.ProjectionId);
			F.SetProperty(AppliedDigestProperty, V.AppliedDigest); F.SetProperty(TombstoneProperty, 0);
		}

		private static bool OwnedExactly(Faction F, string RealmId,
			KingdomPolityFactionProjectionView V)
		{
			return F != null && F.Name == V.FactionId &&
				F.GetStringProperty(OwnerRealmProperty, null) == RealmId &&
				F.GetStringProperty(SourcePolityProperty, null) == V.PolityId &&
				F.GetStringProperty(ProjectionProperty, null) == V.ProjectionId &&
				F.GetStringProperty(AppliedDigestProperty, null) == V.AppliedDigest;
		}

		private static bool ReapplyPresentation(Faction F, KingdomPolityRecord P, bool Hidden)
		{
			if (F == null || P == null) return false;
			F.DisplayName = P.DisplayName; F.Visible = !Hidden; F.Old = false;
			F.ExtradimensionalVersions = false; F.WaterRitualLiquid = Hidden ? null : "water";
			F.SetProperty(TombstoneProperty, Hidden ? 1 : 0); return true;
		}

		private static KingdomPolityRecord Imported(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.ImportedLegacy) return L.Polities[i];
			return null;
		}

		private static bool Quarantine(KingdomSystem System, string Reason)
		{
			string ignored; return Quarantine(System, Reason, out ignored);
		}

		private static bool Quarantine(KingdomSystem System, string Reason, out string Failure)
		{
			Failure = Reason; KingdomPolityRules.Quarantine(System.PolityLedger, Reason); return false;
		}

		private static bool ReconcileTombstone(KingdomSystem System, KingdomPolityRecord Polity,
			Faction Faction, long Tick, out string Failure)
		{
			Failure = null; KingdomPolityProjectionReceipt tombstone = null;
			for (int i = 0; i < System.PolityLedger.Projections.Count; i++)
				if (System.PolityLedger.Projections[i].Kind ==
					KingdomPolityProjectionKind.FactionTombstone &&
					System.PolityLedger.Projections[i].SourceRef == Polity.PolityId)
					tombstone = System.PolityLedger.Projections[i];
			if (tombstone == null) return true;
			if (!ReapplyPresentation(Faction, Polity, Hidden: true)) return false;
			if (tombstone.Phase != KingdomPolityProjectionPhase.Prepared) return true;
			return KingdomPolityRules.TryCommitLegacyFactionTombstone(System.PolityLedger,
				System.PolityLedger.Revision, tombstone.ProjectionId, Tick,
				out KingdomPolityPublicationResult _, out Failure);
		}
	}
}
