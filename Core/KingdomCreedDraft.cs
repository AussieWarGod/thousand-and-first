namespace ThousandAndFirst
{
	/// <summary>Raw merge-by-name creed declaration. Null means omitted. Blank Theology clears
	/// an inherited order opt-in; Kind is required and can never be cleared.</summary>
	public sealed class KingdomCreedDraft
	{
		public string Name;
		public string Kind;
		public string Theology;

		public KingdomCreedDraft Copy()
		{
			return new KingdomCreedDraft { Name = Name, Kind = Kind, Theology = Theology };
		}
	}
}
