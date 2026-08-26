namespace ThousandAndFirst.Api
{
	/// <summary>
	/// One person's identity, frozen for an <see cref="IKingdomIdentitySource"/> call. Culture and
	/// species are Qud's own open string vocabularies; creed and genotype may be empty when the
	/// creature carries neither. Nothing here exposes the creature, city, clock, or mutable state.
	/// </summary>
	public readonly struct KingdomIdentityReading
	{
		/// <summary>What this people knows, from <c>GameObject.GetCulture()</c>.</summary>
		public readonly string Culture;

		/// <summary>What this body is, from <c>GameObject.GetSpecies()</c>.</summary>
		public readonly string Species;

		/// <summary>The resident's present kingdom creed, or empty.</summary>
		public readonly string Creed;

		/// <summary>The creature's vanilla genotype tag/property, or empty.</summary>
		public readonly string Genotype;

		/// <summary>
		/// Builds a bounded frozen identity. Null becomes empty; surrounding whitespace and control
		/// characters are removed; each value is capped by <see cref="KingdomApiRules.MaxIdentityNameLength"/>.
		/// This constructor never throws.
		/// </summary>
		/// <param name="Culture">The exact open culture name, or null.</param>
		/// <param name="Species">The exact open species name, or null.</param>
		/// <param name="Creed">The resident's present creed key, or null.</param>
		/// <param name="Genotype">The vanilla genotype name, or null.</param>
		public KingdomIdentityReading(string Culture, string Species, string Creed, string Genotype)
		{
			this.Culture = KingdomApiRules.IdentityName(Culture);
			this.Species = KingdomApiRules.IdentityName(Species);
			this.Creed = KingdomApiRules.IdentityName(Creed);
			this.Genotype = KingdomApiRules.IdentityName(Genotype);
		}
	}

	/// <summary>
	/// One identity paired with the existing work-kind lane. Internal execution uses this immutable
	/// request so extension affinity calls cross the same validated compute seam as every other
	/// behaviour contract.
	/// </summary>
	internal readonly struct KingdomIdentityWorkReading
	{
		internal readonly KingdomIdentityReading Identity;

		internal readonly string WorkKind;

		internal KingdomIdentityWorkReading(KingdomIdentityReading Identity, string WorkKind)
		{
			this.Identity = Identity;
			this.WorkKind = KingdomApiRules.IdentityWorkKind(WorkKind);
		}
	}
}
