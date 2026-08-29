using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomHostedArcology
	{
		/// <summary>
		/// Reads one exact fixed-slot authority and, when active, its already loaded
		/// native root. No reconciliation, lookup, write or quarantine occurs here.
		/// </summary>
		internal static bool TryReadAuthorityForJointView(KingdomSystem System,
			GameObject ExactRoot, out KingdomHostedArcologyAuthority Authority,
			out string NativeReport, out bool Missing, out string Failure)
		{
			NativeReport = null;
			if (!TryReadAuthorityIdentityForJointView(System, out Authority,
				out Missing, out Failure) || Missing) return Missing;
			if (Authority.Phase == KingdomHostedAuthorityPhase.Quarantined)
			{
				NativeReport = Authority.Fault;
				return true;
			}
			if (Authority.Phase == KingdomHostedAuthorityPhase.Reserved)
			{
				NativeReport = "Hosted-shell ground is reserved; hosted lots are not active.";
				return true;
			}

			r_KingdomArcology root = ExactRoot?.GetPart<r_KingdomArcology>();
			if (!GameObject.Validate(ExactRoot) || ExactRoot.CurrentZone == null || root == null
				|| !string.Equals(ExactRoot.IDIfAssigned, Authority.CarrierId,
					StringComparison.Ordinal)
				|| !string.Equals(ExactRoot.CurrentZone.ZoneID, Authority.ZoneId,
					StringComparison.Ordinal))
				return FailJointRead("The exact loaded hosted-enclave root is unavailable.",
					out Failure);
			NativeReport = Status(root);
			if (string.IsNullOrWhiteSpace(NativeReport)
				|| NativeReport.StartsWith("{{r|Quarantined:", StringComparison.Ordinal))
				return FailJointRead("The hosted-lot native report is invalid.", out Failure);
			return true;
		}

		/// <summary>Returns only the copied fixed-slot owner; it never requires a physical root.</summary>
		internal static bool TryReadAuthorityIdentityForJointView(KingdomSystem System,
			out KingdomHostedArcologyAuthority Authority, out bool Missing, out string Failure)
		{
			Authority = null;
			Missing = false;
			Failure = null;
			if (System == null || !System.Founded)
				return FailJointRead("The current founded realm is unavailable.", out Failure);
			if (!TryReadAuthority(System, out KingdomHostedArcologyAuthority stored,
				out Failure)) return false;
			if (stored == null)
			{
				Missing = true;
				return true;
			}
			Authority = CopyAuthority(stored);
			return Authority.Valid() || FailJointRead(
				"The hosted-enclave authority is malformed.", out Failure);
		}

		private static KingdomHostedArcologyAuthority CopyAuthority(
			KingdomHostedArcologyAuthority Value)
		{
			return new KingdomHostedArcologyAuthority
			{
				Version = Value.Version,
				Phase = Value.Phase,
				RealmId = Value.RealmId,
				SettlementId = Value.SettlementId,
				ZoneId = Value.ZoneId,
				CarrierId = Value.CarrierId,
				ConstructionJobId = Value.ConstructionJobId,
				Fault = Value.Fault
			};
		}

		private static bool FailJointRead(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
