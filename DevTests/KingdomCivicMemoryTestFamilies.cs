#if TAF_TESTS
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// A stand-in for the eight wire families, so the envelope's own behaviour can be exercised
	/// without a game running.
	/// <para>
	/// The real readers cannot be used here. Two of the nine sections reach the engine through
	/// their own rules &mdash; <c>KingdomCuriosityRules</c> and <c>KingdomCivicLeadRules</c> both
	/// take a <c>KingdomExperienceLedger</c>, which pulls in <c>ThousandAndFirst.Simulation</c>
	/// and from there <c>XRL</c> &mdash; so a pure project cannot compile them. That is exactly
	/// why <see cref="KingdomCivicMemoryFamilyTable"/> takes its readers from outside: what the
	/// authority <i>does</i> with a verdict is testable here, and that the real codecs are the
	/// ones wired in is checked separately against the source.
	/// </para>
	/// <para>
	/// The convention is one marker byte, so a test can say what a family thinks of a payload
	/// without needing that family's wire format.
	/// </para>
	/// </summary>
	internal static class KingdomCivicMemoryTestFamilies
	{
		internal const byte Current = 0x01;
		internal const byte Future = 0x02;
		internal const byte Malformed = 0x03;

		/// <summary>A payload the fake families will call <paramref name="Verdict"/>.</summary>
		internal static byte[] Payload(byte Verdict, int Length)
		{
			byte[] bytes = new byte[Length < 1 ? 1 : Length];
			bytes[0] = Verdict;
			for (int i = 1; i < bytes.Length; i++) bytes[i] = (byte)(i & 0xFF);
			return bytes;
		}

		internal static byte[] Sound(int Length)
		{
			return Payload(Current, Length);
		}

		/// <summary>Every known id answered by the marker convention above.</summary>
		internal static KingdomCivicMemoryFamilyTable Table()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, Read);
			return table;
		}

		/// <summary>A table with one id deliberately left unanswered, to prove it fails closed.</summary>
		internal static KingdomCivicMemoryFamilyTable TableMissing(int Absent)
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				if (id != Absent) table.Add(id, Read);
			return table;
		}

		internal static KingdomCivicMemoryFamilyTable TableThrowing(int Throwing)
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == Throwing ? (KingdomCivicMemoryFamilyReader)Throw : Read);
			return table;
		}

		private static KingdomCivicMemoryNested Throw(byte[] Payload, out string Fault)
		{
			Fault = "";
			throw new System.InvalidOperationException("stand-in family inspection exploded");
		}

		private static KingdomCivicMemoryNested Read(byte[] Payload, out string Fault)
		{
			Fault = "";
			if (Payload == null || Payload.Length == 0)
			{
				Fault = "the stand-in family was handed nothing";
				return KingdomCivicMemoryNested.Malformed;
			}
			if (Payload[0] == Future) return KingdomCivicMemoryNested.Future;
			if (Payload[0] == Current) return KingdomCivicMemoryNested.Current;
			Fault = "the stand-in family refused a payload marked 0x"
				+ Payload[0].ToString("X2");
			return KingdomCivicMemoryNested.Malformed;
		}
	}
}
#endif
