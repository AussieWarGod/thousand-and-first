namespace ThousandAndFirst
{
	public sealed partial class KingdomCivicMemoryAuthority
	{
		/// <summary>
		/// Gives a family only a disposable inspection copy and proves it returned that copy intact.
		/// A reader is allowed to parse bytes, not normalize them in place and then vouch for a
		/// different payload than the authority actually holds.
		/// </summary>
		private KingdomCivicMemoryNested InspectStable(int Id, byte[] Payload, out string Fault)
		{
			byte[] inspection = Payload == null ? null : (byte[])Payload.Clone();
			KingdomCivicMemoryNested verdict = Families.Inspect(Id, inspection, out Fault);
			if (SameInspection(Payload, inspection)) return verdict;
			Fault = "civic-memory family for section " + Id
				+ " changed the inspection copy it was given";
			Latch.Trip(Fault);
			return KingdomCivicMemoryNested.Malformed;
		}

		private static bool SameInspection(byte[] Left, byte[] Right)
		{
			if (Left == null || Right == null) return Left == Right;
			if (Left.Length != Right.Length) return false;
			int difference = 0;
			for (int i = 0; i < Left.Length; i++) difference |= Left[i] ^ Right[i];
			return difference == 0;
		}
	}
}
