namespace ThousandAndFirst
{
	/// <summary>
	/// How the realm holds the founder, read off the founder's own reputation with the realm's
	/// faction. Ordered best-first, so a larger value is a worse standing and "the regard fell"
	/// is "the value rose" — every comparison in this file reads that way.
	/// </summary>
	public enum RealmRegard
	{
		Beloved = 0,
		Trusted = 1,
		Doubted = 2,
		Resented = 3,
		Repudiated = 4
	}
}
