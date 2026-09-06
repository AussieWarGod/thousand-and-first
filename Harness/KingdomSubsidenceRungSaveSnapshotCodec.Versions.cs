using System;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomSubsidenceRungSaveSnapshotCodec
	{
		internal const string Prefix = "taf-rung-save-v2:";
		internal const string LegacyPrefix = "taf-rung-save-v1:";
		internal const string StepWirePrefix = "ss5:";
		internal const string LegacyStepWirePrefix = "ss4:";
		internal const string RungWirePrefix = "sr2:";
		private const int Magic = 0x52535401;

		// Structural historical read/roundtrip only. Current native execution requires v2;
		// accepting a v1 envelope here does not prove that its old save can run with new mod bytes.
		internal static bool MatchesPrefix(string wire) { return EnvelopeVersion(wire) != 0; }
		internal static bool MatchesCurrentPrefix(string wire)
		{ return wire != null && wire.StartsWith(Prefix, StringComparison.Ordinal); }

		private static int EnvelopeVersion(string wire)
		{
			if (MatchesCurrentPrefix(wire)) return 2;
			return wire != null && wire.StartsWith(LegacyPrefix, StringComparison.Ordinal) ? 1 : 0;
		}
		private static int StepVersion(string wire)
		{
			if (wire == null) return 0;
			if (wire.StartsWith(StepWirePrefix, StringComparison.Ordinal)) return 2;
			return wire.StartsWith(LegacyStepWirePrefix, StringComparison.Ordinal) ? 1 : 0;
		}
		private static string VersionPrefix(int version) { return version == 1 ? LegacyPrefix : Prefix; }
	}
}
