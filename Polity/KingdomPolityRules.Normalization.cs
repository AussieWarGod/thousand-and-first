using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		public static void Normalize(KingdomPolityLedger Ledger)
		{
			if (Ledger == null) return;
			if (Ledger.SchemaState == KingdomPolitySchemaState.Unknown)
			{
				if (Ledger.OpaqueWireVersion > KingdomPolityCodec.CurrentWireVersion &&
					Ledger.OpaqueFuturePayload != null) return;
				Quarantine(Ledger, "Current polity wire falsely claims opaque future authority.");
				return;
			}
			if (Ledger.SchemaState == KingdomPolitySchemaState.Quarantined) return;
			if (Ledger.FormatVersion == LegacyFormatVersion ||
				Ledger.FormatVersion == OldestFormatVersion ||
				Ledger.FormatVersion == OlderFormatVersion ||
				Ledger.FormatVersion == PriorFormatVersion ||
				Ledger.FormatVersion == ImmediatePriorFormatVersion)
			{
				int sourceFormat = Ledger.FormatVersion;
				if (Ledger.MigratedFromVersion == 0) Ledger.MigratedFromVersion = sourceFormat;
				else if (Ledger.MigratedFromVersion < LegacyFormatVersion ||
					Ledger.MigratedFromVersion > sourceFormat)
				{
					Quarantine(Ledger, "Polity migration provenance is invalid.");
					return;
				}
				Ledger.FormatVersion = CurrentFormatVersion;
				if (sourceFormat == LegacyFormatVersion)
				{
					Ledger.Options = KingdomPolityCodec.DisabledDefaultOptions();
					Ledger.Options.ImportPolicyFrozen = Ledger.IdentityBound;
					Ledger.Projections = Ledger.Projections ??
						new System.Collections.Generic.List<KingdomPolityProjectionReceipt>();
					Ledger.Compactions = Ledger.Compactions ??
						new System.Collections.Generic.List<KingdomPolityCompactionReceipt>();
					Ledger.FoldedCompactionCount = 0L; Ledger.FoldedCompactionDigest = null;
				}
			}
			if (BlankAdditiveDefault(Ledger))
			{
				Ledger.Options = KingdomPolityCodec.DisabledDefaultOptions();
			}
			if (!TryValidate(Ledger, out string failure)) Quarantine(Ledger, failure);
		}

		public static void Quarantine(KingdomPolityLedger Ledger, string Failure)
		{
			if (Ledger == null) return;
			Ledger.SchemaState = KingdomPolitySchemaState.Quarantined;
			Ledger.SchemaFault = Text(Failure, true) ? Failure : "Polity authority is invalid.";
		}

		public static bool TryCreate(string RealmId, KingdomPolityImportPolicy ImportPolicy,
			out KingdomPolityLedger Ledger, out string Failure)
		{
			Ledger = null; Failure = null;
			if (!TypedId(RealmId, "taf:realm:") || !Defined((byte)ImportPolicy, 1))
				return Fail("polity identity or import policy is invalid", out Failure);
			KingdomPolityLedger candidate = new KingdomPolityLedger
			{
				RealmId = RealmId, IdentityBound = true, Revision = 1L,
				Options = KingdomPolityCodec.DisabledDefaultOptions()
			};
			candidate.Options.ImportPolicy = ImportPolicy;
			candidate.Options.ImportPolicyFrozen = true;
			if (!TryValidate(candidate, out Failure)) return false;
			Ledger = candidate; return true;
		}

		public static bool TryBindIdentity(KingdomPolityLedger Ledger, string RealmId,
			KingdomPolityImportPolicy ImportPolicy, out string Failure)
		{
			Failure = null;
			if (Ledger == null || !TypedId(RealmId, "taf:realm:") ||
				!Defined((byte)ImportPolicy, 1)) return Fail("polity bind input is invalid", out Failure);
			Normalize(Ledger);
			if (Ledger.SchemaState != KingdomPolitySchemaState.Compatible)
				return Fail("polity ledger is not compatible", out Failure);
			if (Ledger.IdentityBound)
				return string.Equals(Ledger.RealmId, RealmId, StringComparison.Ordinal) ||
					Fail("polity ledger is bound to another realm", out Failure);
			if (HasSemanticRows(Ledger)) return Fail("unbound polity ledger is not empty", out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			candidate.RealmId = RealmId; candidate.IdentityBound = true;
			candidate.Options.ImportPolicy = ImportPolicy;
			candidate.Options.ImportPolicyFrozen = true; candidate.Revision++;
			if (!TryValidate(candidate, out Failure)) return false;
			Ledger.CopyFrom(candidate); return true;
		}

		/// <summary>
		/// Moves a bound but still-empty additive ledger to a newly minted realm identity. Semantic
		/// rows and compaction evidence require the explicit exile/refounding transformation instead.
		/// </summary>
		public static bool TryRebindEmptyIdentity(KingdomPolityLedger Ledger, string RealmId,
			KingdomPolityImportPolicy ImportPolicy, out string Failure)
		{
			Failure = null;
			if (Ledger == null || !TypedId(RealmId, "taf:realm:") ||
				!Defined((byte)ImportPolicy, 1))
				return Fail("polity rebind input is invalid", out Failure);
			Normalize(Ledger);
			if (Ledger.SchemaState != KingdomPolitySchemaState.Compatible)
				return Fail("polity ledger is not compatible", out Failure);
			if (!Ledger.IdentityBound)
				return TryBindIdentity(Ledger, RealmId, ImportPolicy, out Failure);
			if (string.Equals(Ledger.RealmId, RealmId, StringComparison.Ordinal)) return true;
			if (HasSemanticRows(Ledger) || Ledger.Compactions.Count > 0 ||
				Ledger.FoldedCompactionCount > 0L)
				return Fail("non-empty polity ledger requires an explicit realm transition", out Failure);

			KingdomPolityLedger candidate = new KingdomPolityLedger
			{
				RealmId = RealmId, IdentityBound = true, Revision = Ledger.Revision + 1L,
				Options = KingdomPolityCodec.DisabledDefaultOptions()
			};
			candidate.Options.ImportPolicy = ImportPolicy;
			candidate.Options.ImportPolicyFrozen = true;
			if (!TryValidate(candidate, out Failure)) return false;
			Ledger.CopyFrom(candidate); return true;
		}

		public static KingdomPolityLedger Clone(KingdomPolityLedger Ledger)
		{
			if (!TryValidate(Ledger, out string failure))
				throw new InvalidOperationException("Cannot clone invalid polity authority: " + failure);
			return KingdomPolityCodec.DecodeEnvelopeRaw(KingdomPolityCodec.EncodeEnvelope(Ledger));
		}

		private static bool BlankAdditiveDefault(KingdomPolityLedger L)
		{
			return L.FormatVersion == CurrentFormatVersion && !L.IdentityBound &&
				string.IsNullOrEmpty(L.RealmId) && L.Revision == 0L && !HasSemanticRows(L) &&
				L.Options != null && L.Options.Presentation == KingdomPolityPresentationState.Unobserved &&
				L.Options.ObservedTick == 0L && L.Options.EnableEpoch == 0L &&
				L.Options.FutureCauseFloorTick == 0L;
		}
	}
}
