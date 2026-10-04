using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		/// <summary>Diagnostic only, evaluated after GrowthEnvelopeWritable refused. Returns the
		/// first failing check in gate order. Never changes a verdict.</summary>
		internal static string GrowthEnvelopeRefusalReason(KingdomGrowthBook Book)
		{
			if (Book == null) return "absent";
			if (Book.FormatVersion != CurrentGrowthFormatVersion) return "format";
			if (TooLong(Book.Fault, MaxTextChars)) return "fault-text";
			if (Book.OpaquePayload != null)
				return KingdomLifecycleWireCodec.OpaqueGrowthEnvelopeWritable(Book)
					? "unknown" : "opaque-evidence";
			if (Book.OpaqueWireVersion != 0) return "opaque-wire-version";
			if (!GrowthCollectionsBounded(Book)) return "collections";
			if (Book.MigrationPending)
			{
				if (!StagedGrowthShape(Book)) return "staged-shape";
			}
			else if (Book.Quarantined)
			{
				if (!CanonicalQuarantinedGrowth(Book)) return "quarantine-shape";
			}
			else if (!GrowthRootShape(Book, ValidateOperations: true))
			{
				if (!Book.IdentityBound) return "identity-unbound";
				return !ValidRootId(Book.SettlementId) || !string.Equals(Book.IdentityProof,
					GrowthIdentityProof(Book.SettlementId), StringComparison.Ordinal)
					? "identity-proof" : "root-shape";
			}
			if (!KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(Book)) return "aggregate-cap";
			return "unknown";
		}
	}
}
