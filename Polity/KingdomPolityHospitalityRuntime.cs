using System;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Loaded-endpoint executor for one optional food-and-water hospitality serving.</summary>
	public static partial class KingdomPolityHospitalityRuntime
	{
		internal const string OwnerProperty = "r_TAF_PolityHospitality_v1";
		internal const string DigestProperty = "r_TAF_PolityHospitalityDigest_v1";

		public static bool TryOffer(KingdomSystem System, string TermsPlanId, long Tick,
			out KingdomPolityHospitalityProof Proof, out string Failure)
		{
			Proof = null;
			Failure = null;
			if (System?.PolityLedger == null || Tick < 0L ||
				!KingdomPolityRules.Usable(System.PolityLedger))
				return Fail("polity hospitality authority is unavailable", out Failure);
			KingdomPolityIncidentRecord terms = KingdomPolityHospitalityRules.FindIncident(
				System.PolityLedger, TermsPlanId);
			if (terms == null || terms.Conclusion != null)
				return Fail("these terms cannot receive hospitality", out Failure);
			KingdomPolityHospitalityTransaction transaction = terms.Hospitality;
			if (transaction != null)
			{
				if (transaction.Phase == KingdomPolityHospitalityPhase.Debited)
				{
					Proof = transaction.Proof;
					return true;
				}
				if (transaction.Phase != KingdomPolityHospitalityPhase.Planned)
					return Fail(transaction.Fault ??
						"hospitality is no longer available for these terms", out Failure);
				return TryDrive(System, transaction, out Proof, out Failure);
			}
			if (!TryBuildRequest(System, terms, Tick,
				out KingdomPolityHospitalityPlanRequest request, out Failure)) return false;
			if (!KingdomPolityHospitalityRules.TryPlanDebit(System.PolityLedger,
				System.PolityLedger.Revision, TermsPlanId, request, out transaction,
				out KingdomPolityPublicationResult _, out Failure)) return false;
			return TryDrive(System, transaction, out Proof, out Failure);
		}

		public static void TryCleanupApplied(KingdomSystem System, string TermsPlanId)
		{
			try
			{
				KingdomPolityHospitalityTransaction transaction =
					KingdomPolityHospitalityRules.FindIncident(System?.PolityLedger,
						TermsPlanId)?.Hospitality;
				Zone zone = The.Player?.CurrentZone;
				if (transaction?.Phase != KingdomPolityHospitalityPhase.Applied ||
					zone == null || zone.ZoneID != transaction.ZoneId) return;
				for (int i = 0; i < transaction.Lines.Count; i++)
					if (TryFindExact(transaction.Lines[i].ObjectId, out GameObject item,
						out bool _) && Owned(item, transaction))
					{
						item.RemoveProperty(OwnerProperty);
						item.RemoveProperty(DigestProperty);
					}
			}
			catch (Exception ex)
			{
				KingdomLog.Log("polity: hospitality marker cleanup deferred (" +
					ex.GetType().Name + ")");
			}
		}

		internal static bool TryPrepareForEnvoyDeath(KingdomSystem System,
			string TermsPlanId, out string Failure)
		{
			Failure = null;
			KingdomPolityIncidentRecord terms = KingdomPolityHospitalityRules.FindIncident(
				System?.PolityLedger, TermsPlanId);
			if (terms == null) return Fail(
				"envoy death hospitality lost its terms owner", out Failure);
			KingdomPolityHospitalityTransaction transaction = terms.Hospitality;
			if (transaction == null || transaction.Phase == KingdomPolityHospitalityPhase.Debited ||
				transaction.Phase == KingdomPolityHospitalityPhase.Applied ||
				transaction.Phase == KingdomPolityHospitalityPhase.Abandoned ||
				transaction.Phase == KingdomPolityHospitalityPhase.Quarantined) return true;
			string driveFailure = null;
			for (int attempt = 0; attempt < 2; attempt++)
			{
				TryDrive(System, transaction, out KingdomPolityHospitalityProof _,
					out driveFailure);
				transaction = KingdomPolityHospitalityRules.FindIncident(System.PolityLedger,
					TermsPlanId)?.Hospitality;
				if (transaction?.Phase == KingdomPolityHospitalityPhase.Debited ||
					transaction?.Phase == KingdomPolityHospitalityPhase.Quarantined)
				{
					Failure = null; return true;
				}
				if (transaction?.Phase != KingdomPolityHospitalityPhase.Planned) break;
			}
			string quarantineFailure = null;
			if (transaction?.Phase == KingdomPolityHospitalityPhase.Planned &&
				KingdomPolityHospitalityRules.TryQuarantineDebit(System.PolityLedger,
					System.PolityLedger.Revision, TermsPlanId,
					"Envoy death interrupted exact hospitality reconciliation: " +
						(driveFailure ?? "unknown physical state"),
					out KingdomPolityPublicationResult _, out quarantineFailure))
			{
				Failure = null; return true;
			}
			return Fail(quarantineFailure ?? driveFailure ??
				"envoy death hospitality could not reach a terminal state", out Failure);
		}

		private static string Witness(KingdomPolityHospitalityTransaction T)
		{
			return KingdomPolityRules.ActivationId(
				"taf:fact:witnessed:hospitality:v1:", "polity-hospitality-witness-v1",
				T.TransactionId, T.PlannedTick.ToString(CultureInfo.InvariantCulture));
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
