namespace ThousandAndFirst
{
	/// <summary>
	/// One numbered payload inside the civic-memory envelope, carried as bytes and nothing else.
	/// <para>
	/// This authority never opens a section. The eight wire families each own a codec that knows
	/// how to read its own bytes, and each of those codecs has its own rules about what a
	/// malformed row means; a substrate that second-guessed them would end up holding two
	/// opinions about the same payload. So a section is stored, bounded, ordered and handed back
	/// exactly as it arrived &mdash; which is also the only way an id this build has never heard
	/// of can survive a round trip intact.
	/// </para>
	/// </summary>
	public sealed class KingdomCivicMemorySection
	{
		/// <summary>The stable numeric id. See <see cref="KingdomCivicMemoryLimits"/>.</summary>
		public readonly int Id;

		private readonly byte[] Bytes;

		public KingdomCivicMemorySection(int Id, byte[] Payload)
		{
			if (Payload == null) throw new System.ArgumentNullException("Payload");
			this.Id = Id;
			Bytes = (byte[])Payload.Clone();
		}

		/// <summary>The payload's length, without handing out the payload.</summary>
		public int Length => Bytes.Length;

		/// <summary>Whether this build knows what this id means, or is merely keeping it safe.</summary>
		public bool KnownToThisBuild => KingdomCivicMemoryLimits.Known(Id);

		/// <summary>
		/// A private copy of the bytes. Callers get their own array every time, because a caller
		/// holding a reference into authority state could edit the save without committing to it.
		/// </summary>
		public byte[] Payload()
		{
			return (byte[])Bytes.Clone();
		}
	}
}
