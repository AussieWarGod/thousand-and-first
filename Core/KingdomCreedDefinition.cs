namespace ThousandAndFirst
{
	/// <summary>Validated semantic definition for one engine faction key. This is resolved at
	/// runtime and never copied into saves, so old Creed values gain no fabricated state.</summary>
	public sealed class KingdomCreedDefinition
	{
		public string Name;
		public KingdomCreedKind Kind;
		public bool Theological;
	}
}
