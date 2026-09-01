using System;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	internal sealed class KingdomHostedDepartureEnvelope
	{
		internal InteriorZone Zone;
		internal GameObject Shell;
		internal r_KingdomArcology Root;
		internal string LotKey;
		internal KingdomHostedLotReceipt Receipt;
		internal string Revision;
		internal string AnchorId;
	}

	/// <summary>Immediate attended-floor departure witness. It never opens another zone.</summary>
	public static partial class KingdomHostedArcology
	{
		internal static void OnDeactivated(KingdomSystem System, Zone Z)
		{
			InvalidateDeparture(System, Z);
		}

		internal static void OnSuspending(KingdomSystem System, Zone Z)
		{
			ObserveSuspension(System, Z);
		}

		private static void InvalidateDeparture(KingdomSystem System, Zone Z)
		{
			if (!TryDepartureZone(System, Z, out InteriorZone interior)) return;
			if (!TryFenceDeparture(System, interior,
				out KingdomHostedDepartureState ignoredFence, out string failure))
			{
				KingdomLog.Log("hosted departure fence refused: "
					+ (failure ?? "invalid departure authority")); return;
			}
			if (!TryRecoverDepartureEnvelope(System, interior,
				out KingdomHostedDepartureEnvelope envelope, out failure))
			{
				if (!string.IsNullOrEmpty(failure))
					KingdomLog.Log("hosted departure invalidation refused: " + failure);
				return;
			}
			bool corrupt = !TryLiveContext(Z, false,
				out KingdomHostedLiveContext ignored, out failure);
			PersistDepartureFault(System, envelope, corrupt
				? failure ?? "hosted departure context proof failed"
				: "awaiting final suspension observation", corrupt);
		}

		private static void ObserveSuspension(KingdomSystem System, Zone Z)
		{
			if (!TryDepartureZone(System, Z, out InteriorZone interior)) return;
			// Remote generation and cache bookkeeping can suspend a floor the founder never
			// attended. Only an exact attended anchor may open a fresh final-observation fence;
			// ordinary deactivation already revoked a previously attended floor first.
			if (!TryLiveContext(Z, false, out KingdomHostedLiveContext attended,
				out string initialFailure) || !attended.Anchor.Attended)
			{
				if (!string.IsNullOrEmpty(initialFailure))
					KingdomLog.Log("hosted final observation skipped: " + initialFailure);
				return;
			}
			if (!TryFenceDeparture(System, interior,
				out KingdomHostedDepartureState ignoredFence, out string failure))
			{
				KingdomLog.Log("hosted final fence refused: "
					+ (failure ?? "invalid departure authority")); return;
			}
			if (!TryRecoverDepartureEnvelope(System, interior,
				out KingdomHostedDepartureEnvelope envelope, out failure))
			{
				if (!string.IsNullOrEmpty(failure))
					KingdomLog.Log("hosted observation refused: " + failure);
				return;
			}

			if (!TryLiveContext(Z, false, out KingdomHostedLiveContext context, out failure))
			{
				GameObject anchorObject;
				r_KingdomArcologyZoneAnchor anchor;
				string ignored;
				if (TryExactAnchor(Z, envelope.Shell.IDIfAssigned, envelope.LotKey,
					out anchorObject, out anchor, out ignored))
				{
					if (!anchor.Attended) return;
					anchor.Attended = false;
				}
				PersistDepartureFault(System, envelope,
					failure ?? "final hosted context proof failed", true);
				return;
			}
			if (!context.Anchor.Attended) return;
			context.Anchor.Attended = false;
			KingdomHostedObservation observation = NewObservation(context, The.Game.TimeTicks);
			if (!TryLiveContext(Z, true, out context, out failure))
			{
				observation.Fault = Bound(failure ?? "final fixture proof failed");
				Quarantine(envelope.Root, observation.Fault);
				PersistFinalDeparture(System, envelope, observation); return;
			}

			observation = NewObservation(context, The.Game.TimeTicks);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z)
				?? KingdomSurvey.TakeCustodyOnly(Z);
			KingdomBenefitDesignation designation;
			KingdomDesignationIndex designations;
			KingdomBenefitIndex benefits = null;
			if (survey == null || !ReferenceEquals(survey.Ground, Z))
			{
				observation.Fault = "physical benefit scan has no exact custody survey";
				PersistFinalDeparture(System, envelope, observation); return;
			}
			using (KingdomSurvey.PassScope scope = survey.BindPass())
			{
				if (!TryBuildDesignation(context, out designation, out failure)
					|| !KingdomDesignationIndex.CompleteForSource(designation, Z, out failure)
					|| !FinalizeDesignationIdentity(designation)
					|| !KingdomDesignationIndex.TryCreate(
						new KingdomBenefitDesignation[] { designation }, Z.ZoneID,
						Z.Width, Z.Height, out designations, out failure)
					|| !KingdomBenefitIndex.TryBuild(Z, survey, designations,
						out benefits, out failure))
				{
					observation.Fault = Bound(failure ?? "physical benefit scan failed");
					PersistFinalDeparture(System, envelope, observation); return;
				}
			}
			KingdomBenefitReading reading = benefits.ReadingForRoot(
				context.AnchorObject.IDIfAssigned);
			if (reading?.Designation == null
				|| reading.Designation.ProviderId != "taf.hosted-arcology"
				|| reading.Designation.Revision != context.Revision
				|| reading.Designation.ZoneId != Z.ZoneID)
			{
				observation.Fault = "exact hosted designation was not present";
				PersistFinalDeparture(System, envelope, observation); return;
			}
			if (context.Receipt.LotKey == KingdomHostedArcologyTopology.WardLotKey)
			{
				observation.Roof = benefits.AmountForRoot(
					context.AnchorObject.IDIfAssigned, KingdomCatalogueRules.SupportRoof);
				observation.Luxury = benefits.AmountForRoot(
					context.AnchorObject.IDIfAssigned, "luxury");
			}
			else if (context.Receipt.LotKey == KingdomHostedArcologyTopology.TerraceLotKey)
				observation.Food = ObserveTerraceFood(context);
			PersistFinalDeparture(System, envelope, observation);
		}

		private static bool TryRecoverDepartureEnvelope(KingdomSystem System,
			InteriorZone Z, out KingdomHostedDepartureEnvelope Envelope, out string Failure)
		{
			Envelope = null; Failure = null;
			string lotKey = Z == null ? "" : KingdomHostedArcologyTopology.HostedLotAt(
				Z.X, Z.Y, Z.Z);
			GameObject shell = null;
			if (Z != null && !TryLoadedInteriorRoot(Z, out shell, out Failure)) return false;
			r_KingdomArcology root = shell?.GetPart<r_KingdomArcology>();
			if (Z == null || string.IsNullOrEmpty(lotKey) || !GameObject.Validate(shell)
				|| root == null || string.IsNullOrEmpty(shell.IDIfAssigned)
				|| Z.Instance != shell.IDIfAssigned
				|| !ReferenceEquals(The.Game?.GetSystem<KingdomSystem>(), System)
				|| !TryInteriorZoneIdentity(shell, lotKey, Z.ZoneID, out Failure))
				return DepartureFail(Failure ?? "hosted departure has no safe exact envelope",
					out Failure);
			Zone exterior = shell.CurrentZone;
			string settlement = exterior == null ? null
				: System.SettlementIdForOwnedZone(exterior.ZoneID);
			KingdomHostedArcologyAuthority authority;
			if (!System.Founded || string.IsNullOrEmpty(settlement)
				|| !TryReadAuthority(System, out authority, out Failure) || authority == null
				|| authority.Phase != KingdomHostedAuthorityPhase.Active
				|| authority.RealmId != System.RealmId
				|| authority.SettlementId != settlement
				|| authority.ZoneId != exterior.ZoneID
				|| authority.CarrierId != shell.IDIfAssigned)
				return DepartureFail(Failure ?? "hosted departure authority is not exact",
					out Failure);
			KingdomHostedLotReceipt receipt;
			KingdomHostedLotDefinition definition;
			if (!TryReceipt(root, lotKey, out receipt, out Failure) || receipt == null
				|| receipt.Phase != KingdomHostedLotPhase.Active
				|| receipt.RootId != shell.IDIfAssigned
				|| !KingdomHostedArcologyRules.TryHostedLot(lotKey, out definition)
				|| definition.ReadOnly || definition.InteriorCell != Z.Schema)
				return DepartureFail(Failure ?? "hosted departure receipt is not exact",
					out Failure);
			string revision = KingdomHostedArcologyRules.ReceiptRevision(receipt);
			if (string.IsNullOrEmpty(revision)
				|| !KingdomHostedArcologyTopology.TryHostedLotCoordinate(lotKey,
					out KingdomArcologyCoordinate at))
				return DepartureFail("hosted departure revision is invalid", out Failure);
			Envelope = new KingdomHostedDepartureEnvelope { Zone = Z, Shell = shell, Root = root,
				LotKey = lotKey, Receipt = receipt, Revision = revision,
				AnchorId = KingdomHostedArcologyRules.StableChildId(shell.IDIfAssigned,
					KingdomHostedArcologyTopology.StableRole(at.X, at.Y, at.Z, "anchor")) };
			return true;
		}

		private static bool TryDepartureZone(KingdomSystem System, Zone Z,
			out InteriorZone Interior)
		{
			Interior = Z as InteriorZone;
			return System != null && The.Game != null && Interior != null
				&& Interior.Schema == KingdomHostedArcologyTopology.Schema
				&& !string.IsNullOrEmpty(KingdomHostedArcologyTopology.HostedLotAt(
					Interior.X, Interior.Y, Interior.Z));
		}

		private static void PersistDepartureFault(KingdomSystem System,
			KingdomHostedDepartureEnvelope Envelope,
			string Failure, bool QuarantineRoot)
		{
			string fault = Bound(Failure);
			KingdomHostedObservation row = new KingdomHostedObservation {
				RootId = Envelope.Shell.IDIfAssigned, LotKey = Envelope.LotKey,
				ReceiptRevision = Envelope.Revision, InteriorZoneId = Envelope.Zone.ZoneID,
				AnchorId = Envelope.AnchorId, ObservedTick = Math.Max(0L, The.Game.TimeTicks),
				Fault = fault };
			if (QuarantineRoot) Quarantine(Envelope.Root, fault);
			PersistFinalDeparture(System, Envelope, row);
		}

		private static bool FinalizeDesignationIdentity(KingdomBenefitDesignation Designation)
		{
			if (Designation == null || string.IsNullOrEmpty(Designation.ProviderId)
				|| string.IsNullOrEmpty(Designation.Identity)) return false;
			Designation.Identity = "ext:" + Designation.ProviderId.ToLowerInvariant()
				+ ":" + Designation.Identity;
			return true;
		}

		private static bool DepartureFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
