using System;
using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		public const int CurrentFormatVersion = 6;
		public const int ImmediatePriorFormatVersion = 5;
		public const int PriorFormatVersion = 4;
		public const int OlderFormatVersion = 3;
		public const int OldestFormatVersion = 2;
		public const int LegacyFormatVersion = 1;
		public const int MaxPolities = 4;
		public const int MaxRelations = 12;
		public const int MaxProfiles = 16;
		public const int MaxRoutes = 8;
		public const int MaxGrievances = 16;
		public const int MaxFronts = 4;
		public const int MaxActiveFronts = 1;
		public const int MaxCohorts = 16;
		public const int MaxNamedFigures = 16;
		public const int MaxIncidents = 8;
		public const int MaxProjections = 32;
		public const int MaxCompactions = 16;
		public const int MaxRefs = 16;
		public const int MaxPath = 32;
		public const int MaxCohortMembers = 7;
		public const int MaxObservedFacts = 64;
		public const int MaxDeltas = 16;
		public const int MaxTextBytes = 1024;
		public const int MaxValueBudget = 100000;
		public const int MaxLevel = 999;

		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool Usable(KingdomPolityLedger Ledger)
		{
			return TryValidate(Ledger, out string _);
		}

		public static bool TryValidate(KingdomPolityLedger Ledger, out string Failure)
		{
			Failure = null;
			if (Ledger == null) return Fail("ledger is null", out Failure);
			if (Ledger.SchemaState != KingdomPolitySchemaState.Compatible)
				return Fail("ledger schema is not compatible", out Failure);
			if (Ledger.FormatVersion != CurrentFormatVersion)
				return Fail("ledger format is not current", out Failure);
			if (Ledger.OpaqueWireVersion != 0 || Ledger.OpaqueFuturePayload != null)
				return Fail("compatible ledger carries opaque future bytes", out Failure);
			if (Ledger.Revision < 0L || Ledger.MigratedFromVersion < 0 ||
				Ledger.MigratedFromVersion > ImmediatePriorFormatVersion)
				return Fail("ledger version evidence is invalid", out Failure);
			if (!ValidOptions(Ledger.Options, Ledger.IdentityBound, out Failure)) return false;
			if (!Ledger.IdentityBound)
			{
				if (!string.IsNullOrEmpty(Ledger.RealmId) || HasSemanticRows(Ledger))
					return Fail("unbound ledger carries semantic authority", out Failure);
			}
			else if (!TypedId(Ledger.RealmId, "taf:realm:"))
				return Fail("bound ledger realm id is invalid", out Failure);
			if (!BoundedLists(Ledger, out Failure)) return false;
			if (!ValidateIdentityState(Ledger, out Failure)) return false;
			if (!ValidateTrafficState(Ledger, out Failure)) return false;
			if (!ValidateIncidentState(Ledger, out Failure)) return false;
			if (!ValidateGraph(Ledger, out Failure)) return false;
			return true;
		}

		private static bool BoundedLists(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			if (!Count(L.Polities, MaxPolities) || !Count(L.Relations, MaxRelations) ||
				!Count(L.Profiles, MaxProfiles) || !Count(L.Routes, MaxRoutes) ||
				!Count(L.Grievances, MaxGrievances) || !Count(L.Fronts, MaxFronts) ||
				!Count(L.Cohorts, MaxCohorts) || !Count(L.NamedFigures, MaxNamedFigures) ||
				!Count(L.Incidents, MaxIncidents) || !Count(L.Projections, MaxProjections) ||
				!Count(L.Compactions, MaxCompactions))
				return Fail("ledger collection is null or exceeds capacity", out Failure);
			if (L.FoldedCompactionCount < 0L ||
				(L.FoldedCompactionCount == 0L) != string.IsNullOrEmpty(L.FoldedCompactionDigest) ||
				(L.FoldedCompactionCount > 0L && !Digest(L.FoldedCompactionDigest)))
				return Fail("folded compaction evidence is invalid", out Failure);
			return true;
		}

		private static bool ValidOptions(KingdomPolityOptions O, bool Bound, out string Failure)
		{
			Failure = null;
			if (O == null || !Defined((byte)O.ImportPolicy, 1) ||
				!Defined((byte)O.Presentation, 2) || O.ObservedTick < 0L || O.EnableEpoch < 0L)
				return Fail("polity options are invalid", out Failure);
			if (Bound && !O.ImportPolicyFrozen)
				return Fail("bound realm has no frozen import policy", out Failure);
			if (O.Presentation == KingdomPolityPresentationState.Unobserved &&
				(O.ObservedTick != 0L || O.EnableEpoch != 0L ||
				 O.FutureCauseFloorTick != long.MaxValue))
				return Fail("unobserved presentation option is noncanonical", out Failure);
			if (O.Presentation == KingdomPolityPresentationState.Enabled &&
				(O.EnableEpoch < 1L || O.FutureCauseFloorTick < 0L ||
				 O.FutureCauseFloorTick > O.ObservedTick))
				return Fail("enabled presentation option is invalid", out Failure);
			if (O.Presentation == KingdomPolityPresentationState.Disabled &&
				O.FutureCauseFloorTick != long.MaxValue)
				return Fail("disabled presentation option can emit backlog", out Failure);
			return true;
		}

		private static bool HasSemanticRows(KingdomPolityLedger L)
		{
			return (L.Polities != null && L.Polities.Count > 0) ||
				(L.Relations != null && L.Relations.Count > 0) ||
				(L.Profiles != null && L.Profiles.Count > 0) ||
				(L.Routes != null && L.Routes.Count > 0) ||
				(L.Grievances != null && L.Grievances.Count > 0) ||
				(L.Fronts != null && L.Fronts.Count > 0) ||
				(L.Cohorts != null && L.Cohorts.Count > 0) ||
				(L.NamedFigures != null && L.NamedFigures.Count > 0) ||
				(L.Incidents != null && L.Incidents.Count > 0) ||
				(L.Projections != null && L.Projections.Count > 0);
		}

		internal static bool TypedId(string Value, string Prefix)
		{
			return KernelSemanticId.IsValid(Value) && Value.StartsWith(Prefix,
				StringComparison.Ordinal) && Value.Length > Prefix.Length;
		}

		internal static bool SemanticId(string Value) { return KernelSemanticId.IsValid(Value); }
		internal static bool Digest(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
			}
			return true;
		}

		internal static bool Text(string Value, bool Required)
		{
			if (Value == null) return !Required;
			if (Required && Value.Length == 0) return false;
			try
			{
				if (StrictUtf8.GetByteCount(Value) > MaxTextBytes) return false;
			}
			catch (EncoderFallbackException) { return false; }
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return false;
			return true;
		}

		internal static bool Count<T>(IList<T> Values, int Maximum)
		{
			return Values != null && Values.Count <= Maximum;
		}

		internal static bool Defined(byte Value, byte Maximum) { return Value <= Maximum; }
		internal static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
