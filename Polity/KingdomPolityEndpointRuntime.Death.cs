using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		internal sealed class DeathWitness
		{
			internal GameObject Body;
			internal Cell Cell;
			internal string RealmId;
			internal string CohortId;
			internal string ProjectionId;
			internal string ZoneId;
			internal string BodyId;
			internal int Ordinal;
			internal int X;
			internal int Y;
			internal long Tick;
			internal KingdomPolityDeathIntentRecord Intent;
		}

		internal static bool TryPrepareVisibleDeath(GameObject Body, string RealmId,
			string CohortId, GameObject Killer, out DeathWitness Witness, out string Failure)
		{
			Witness = null; Failure = null;
			try
			{
				if (!TryBindPhysicalDeath(Body, RealmId, CohortId, out KingdomSystem system,
					out Zone zone, out KingdomPolityLedger ledger, out KingdomPolityCohortPlan cohort,
					out KingdomPolityProjectionReceipt receipt, out int ordinal, out Failure) ||
					!TryBuildCustodyPlan(ledger, zone, RealmId, cohort, receipt, Body, ordinal,
						AllowRemovedGear: true, out FrozenCustodyPlan plan, out Failure))
					return false;
				long tick = Math.Max(0L, The.Game?.TimeTicks ?? 0L);
				bool visible = false, playerKiller = false;
				try { visible = Body.CurrentCell.IsVisible() && Body.IsVisible(); }
				catch (Exception ex) { KingdomLog.Log(
					"polity: death visibility lacked semantic proof (" + ex.GetType().Name + ")"); }
				if (visible && GameObject.Validate(Killer))
					try { playerKiller = Killer.IsPlayer(); }
					catch (Exception ex) { KingdomLog.Log(
						"polity: death attribution remained neutral (" + ex.GetType().Name + ")"); }
				if (!TryFreezeDeathIncident(ledger, cohort, ordinal, visible,
					out string incidentPlanId, out string incidentId,
					out string incidentDigest, out Failure)) return false;
				KingdomPolityDeathIntentRecord intent = new KingdomPolityDeathIntentRecord
				{
					Kind = KingdomPolityPhysicalCustodyRules.DeathRemovalKind,
					RealmId = RealmId, CohortId = CohortId,
					ProjectionId = receipt.ProjectionId, ZoneId = zone.ZoneID,
					ObjectId = Body.IDIfAssigned, Ordinal = ordinal, Purpose = cohort.Purpose,
					Representative = ordinal == 0, Tick = tick,
					Visibility = visible ? KingdomPolityDeathVisibility.PlayerVisible :
						KingdomPolityDeathVisibility.PhysicalOnly,
					Attribution = playerKiller
						? KingdomPolityDeathAttribution.PlayerWitnessed
						: KingdomPolityDeathAttribution.Unattributed,
					IncidentPlanId = incidentPlanId, IncidentId = incidentId,
					IncidentDigest = incidentDigest
				};
				DeathWitness candidate = new DeathWitness
				{
					Body = Body,
					Cell = Body.CurrentCell,
					RealmId = RealmId,
					CohortId = CohortId,
					ProjectionId = receipt.ProjectionId,
					ZoneId = zone.ZoneID,
					BodyId = Body.IDIfAssigned,
					Ordinal = ordinal,
					X = Body.CurrentCell.X,
					Y = Body.CurrentCell.Y, Tick = tick, Intent = intent
				};
				if (!TryReadDeathIntent(zone, RealmId, cohort, receipt, ordinal,
					out KingdomPolityDeathIntentState state,
					out KingdomPolityDeathIntentRecord prior, out Failure)) return false;
				if (state == KingdomPolityDeathIntentState.Outstanding && !SameIntent(prior, intent))
					return FailPhysical("death intent slot already owns a different exact event", out Failure);
				if (!TryWriteDeathIntent(zone, intent, out Failure) ||
					!TryWriteRemovalWitness(Body.CurrentCell,
						KingdomPolityPhysicalCustodyRules.DeathRemovalKind, RealmId,
						cohort.CohortId, receipt.ProjectionId, Body.IDIfAssigned, ordinal, out Failure) ||
					!TryReleaseFrozenCustody(ledger, RealmId, cohort, receipt, plan, out Failure))
					return false;
				if (!ReproveVisibleDeath(candidate, Body, ReleaseCustody: false, out Failure)) return false;
				Witness = candidate; return true;
			}
			catch (Exception ex)
			{
				Witness = null;
				Failure = "visible death preparation failed: " + ex.Message;
				return false;
			}
		}

		internal static bool TryReproveVisibleDeath(DeathWitness Witness, GameObject Body,
			out string Failure)
		{
			try { return ReproveVisibleDeath(Witness, Body, ReleaseCustody: true, out Failure); }
			catch (Exception ex)
			{
				Failure = "visible death reproof failed: " + ex.Message; return false;
			}
		}

		internal static bool TryCommitVisibleDeathWitness(DeathWitness Witness, GameObject Body,
			out string Failure)
		{
			Failure = null;
			try
			{
				if (!ReproveVisibleDeath(Witness, Body, ReleaseCustody: true, out Failure)) return false;
				if (!TryPrepareRemovalWitness(Witness.Cell,
					KingdomPolityPhysicalCustodyRules.DeathRemovalKind, Witness.RealmId,
					Witness.CohortId, Witness.ProjectionId, Witness.BodyId, Witness.Ordinal,
					out Failure)) return false;
				return TryWriteRemovalWitness(Witness.Cell,
					KingdomPolityPhysicalCustodyRules.DeathRemovalKind, Witness.RealmId,
					Witness.CohortId, Witness.ProjectionId, Witness.BodyId, Witness.Ordinal,
					out Failure);
			}
			catch (Exception ex)
			{
				Failure = "visible death witness failed: " + ex.Message; return false;
			}
		}

		private static bool ReproveVisibleDeath(DeathWitness Witness, GameObject Body,
			bool ReleaseCustody, out string Failure)
		{
			Failure = null;
			if (Witness == null || !ReferenceEquals(Witness.Body, Body) ||
				!ReferenceEquals(Witness.Cell, Body?.CurrentCell) || Body.CurrentCell.X != Witness.X ||
				Body.CurrentCell.Y != Witness.Y || (The.Game == null ? 0L : The.Game.TimeTicks) !=
					Witness.Tick)
				return FailPhysical("death body moved or escaped its exact callback", out Failure);
			if (!TryBindPhysicalDeath(Body, Witness.RealmId, Witness.CohortId,
				out KingdomSystem system, out Zone zone, out KingdomPolityLedger ledger,
				out KingdomPolityCohortPlan cohort, out KingdomPolityProjectionReceipt receipt,
				out int ordinal, out Failure) || !ReferenceEquals(zone, Witness.Cell.ParentZone) ||
				receipt.ProjectionId != Witness.ProjectionId || zone.ZoneID != Witness.ZoneId ||
				Body.IDIfAssigned != Witness.BodyId || ordinal != Witness.Ordinal ||
				!TryReadDeathIntent(zone, Witness.RealmId, cohort, receipt, ordinal,
					out KingdomPolityDeathIntentState state,
					out KingdomPolityDeathIntentRecord intent, out Failure) ||
				state != KingdomPolityDeathIntentState.Outstanding ||
				!SameIntent(intent, Witness.Intent) || !HasRemovalWitness(zone,
					KingdomPolityPhysicalCustodyRules.DeathRemovalKind, Witness.RealmId,
					Witness.CohortId, Witness.ProjectionId, Witness.BodyId, Witness.Ordinal))
				return FailPhysical(Failure ?? "death authority changed during callbacks", out Failure);
			if (!TryBuildCustodyPlan(ledger, zone, Witness.RealmId, cohort, receipt, Body,
				ordinal, AllowRemovedGear: true, out FrozenCustodyPlan plan, out Failure)) return false;
			if (ReleaseCustody && !TryReleaseFrozenCustody(ledger, Witness.RealmId, cohort,
				receipt, plan, out Failure)) return false;
			return true;
		}

		private static bool TryBindPhysicalDeath(GameObject Body, string RealmId, string CohortId,
			out KingdomSystem System, out Zone Zone, out KingdomPolityLedger Ledger,
			out KingdomPolityCohortPlan Cohort, out KingdomPolityProjectionReceipt Receipt,
			out int Ordinal, out string Failure)
		{
			System = The.Game?.GetSystem<KingdomSystem>(); Zone = null; Ledger = null;
			Cohort = null; Receipt = null; Ordinal = -1; Failure = null;
			if (!TryAdmit(System, CohortId, out Zone, out Ledger, out Cohort, out Failure)) return false;
			Receipt = KingdomPolityAuthority.Projection(Ledger, Cohort.ManifestationReceiptId);
			Ordinal = Body == null ? -1 : Body.GetIntProperty(MemberOrdinalProperty, -1);
			XRL.World.Parts.r_KingdomPolityCohortBody part = Body == null ? null :
				Body.GetPart<XRL.World.Parts.r_KingdomPolityCohortBody>();
			if (Receipt == null || Receipt.Phase != KingdomPolityProjectionPhase.Committed ||
				(Cohort.Phase != KingdomPolityCohortPhase.Materialized &&
				 Cohort.Phase != KingdomPolityCohortPhase.Concluded) ||
				!ExactReceipt(Cohort, Receipt, Zone, out Failure) ||
				!TryResolveFrozenSpec(Ledger, Cohort, Ordinal, out KingdomPolityNpcSpec spec,
					out string figureId, out Failure) ||
				!ExactPreparedBody(Body, Zone, RealmId, Cohort, Receipt, spec, figureId, Ordinal,
					out Failure) ||
				!KingdomPolityPhysicalCustodyRules.ExactPhysicalDeathBinding(Ledger.RealmId,
					Cohort.CohortId, Receipt.ProjectionId, Receipt.ZoneId,
					KingdomPolityCohortRules.PreparedObjectId(Cohort, Ordinal), Ordinal,
					part?.RealmId, part?.CohortId, Body?.GetStringProperty(ProjectionProperty),
					Body?.CurrentZone?.ZoneID, Body?.IDIfAssigned,
					Body?.GetIntProperty(MemberOrdinalProperty, -1) ?? -1,
					GameObject.Validate(Body), Body?.CurrentCell != null && Body.InInventory == null &&
						Body.Equipped == null, ReferenceEquals(Body?.CurrentZone, The.Player?.CurrentZone)))
				return FailPhysical(Failure ?? "death callback lacks exact physical authority",
					out Failure);
			return true;
		}

		internal static bool TryAuthorizePreparedCleanup(GameObject Body, string RealmId,
			string CohortId, string ProjectionId, string ObjectId, int TokenOrdinal, Cell TokenCell,
			byte TokenCohortPhase, byte TokenProjectionPhase, string TokenIntentKey,
			string TokenIntentValue)
		{
			try
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				KingdomPolityLedger ledger = system?.PolityLedger;
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger, CohortId);
				KingdomPolityProjectionReceipt receipt = cohort == null ? null :
					KingdomPolityAuthority.Projection(ledger, cohort.ManifestationReceiptId);
				Zone zone = Body?.CurrentZone; int ordinal = Body == null ? -1 :
					Body.GetIntProperty(MemberOrdinalProperty, -1);
				if (ledger == null || ledger.RealmId != RealmId || cohort == null || receipt == null ||
					zone == null || !ReferenceEquals(TokenCell, Body.CurrentCell) ||
					ProjectionId != receipt.ProjectionId || ObjectId != Body.IDIfAssigned ||
					TokenOrdinal != ordinal || TokenCohortPhase != (byte)cohort.Phase ||
					TokenProjectionPhase != (byte)receipt.Phase ||
					!((cohort.Phase == KingdomPolityCohortPhase.Planned && receipt.Phase ==
						KingdomPolityProjectionPhase.Prepared) || ((cohort.Phase ==
						KingdomPolityCohortPhase.Concluded || cohort.Phase ==
						KingdomPolityCohortPhase.Abandoned) && receipt.Phase ==
						KingdomPolityProjectionPhase.Committed)) ||
					!ExactReceipt(cohort, receipt, zone, out string _) || ordinal < 0 ||
					ordinal >= cohort.ResolvedMembers.Count || !TryResolveFrozenSpec(ledger, cohort,
					ordinal, out KingdomPolityNpcSpec spec, out string figureId, out string _) ||
					!ExactPreparedBody(Body, zone, RealmId, cohort, receipt, spec, figureId,
					ordinal, out string _)) return false;
				return TryProveExactCleanupIntent(zone, TokenCell, RealmId, CohortId,
					ProjectionId, ObjectId, TokenOrdinal, TokenCohortPhase,
					TokenProjectionPhase, TokenIntentKey, TokenIntentValue, out string _) ==
					KingdomPolityCleanupEvidenceProof.Exact;
			}
			catch { return false; }
		}
	}
}
