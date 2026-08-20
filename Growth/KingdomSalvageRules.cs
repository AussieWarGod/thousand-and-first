namespace ThousandAndFirst
{
	/// <summary>
	/// Pure eligibility and cost arithmetic for certifying a machine hauled home from a ruin.
	/// Engine-free by design: everything the settlement's inspection needs is passed in as
	/// plain values, so the refusal cases &mdash; the ones that keep something dangerous,
	/// broken, or simply unknown off the grid &mdash; are provable without a running game. The
	/// engine-coupled read of a real <c>GameObject</c> lives in <c>KingdomSalvage</c>, in the
	/// same folder.
	/// </summary>
	public static class KingdomSalvageRules
	{
		/// <summary>
		/// Why the settlement will or won't put a machine on its grid. Every refusal names its
		/// own cause; nothing here is a generic "no". Ordered the way <see cref="Assess"/>
		/// checks them: what protects the player comes before what the ledger can afford.
		/// </summary>
		public enum SalvageVerdict
		{
			Certified = 0,
			RefusedHazardous = 1,
			RefusedBroken = 2,
			RefusedRusted = 3,
			RefusedNotUnderstood = 4,
			RefusedCannotAfford = 5,
			RefusedNoHands = 6
		}

		/// <summary>Water spent even for the simplest machine: casks moved, seals tested, a
		/// day nobody was doing anything else.</summary>
		public const int SalvageBaseWaterCost = 15;

		/// <summary>Extra drams the inspection costs per point of the machine's Examiner
		/// Complexity.</summary>
		public const int SalvageWaterPerComplexity = 5;

		/// <summary>
		/// Settlers who must be free to test a machine before it joins the grid, even the
		/// simplest one. Mirrors the loyal core a settlement never falls below
		/// (<c>KingdomRules.LoyalCoreSettlers</c>): below that size nobody can be spared for
		/// anything but keeping the place alive, so nothing gets certified either.
		/// </summary>
		public const int SalvageBaseHandsRequired = 2;

		/// <summary>Points of Examiner Difficulty that add one more required hand.</summary>
		public const int SalvageDifficultyPerHand = 2;

		/// <summary>
		/// Drams the settlement's stores must hold before it will attempt certifying a machine
		/// of the given Examiner Complexity. Negative complexity &mdash; a hostile or malformed
		/// blueprint &mdash; is clamped to zero rather than paying the founder for the privilege.
		/// </summary>
		public static int ComputeWaterCost(int Complexity)
		{
			int complexity = (Complexity > 0) ? Complexity : 0;
			return SalvageBaseWaterCost + complexity * SalvageWaterPerComplexity;
		}

		/// <summary>
		/// Settlers who must be free of other duties for the given Examiner Difficulty.
		/// Negative difficulty is clamped to zero.
		/// </summary>
		public static int ComputeHandsRequired(int Difficulty)
		{
			int difficulty = (Difficulty > 0) ? Difficulty : 0;
			return SalvageBaseHandsRequired + difficulty / SalvageDifficultyPerHand;
		}

		/// <summary>
		/// The settlement's verdict on a machine. Checked in the order that protects the
		/// player: a hazard or a fault refuses outright, before the ledger is even read, so a
		/// bomb wired into a dry stores can never be waved through for lack of water. Only once
		/// the machine itself is sound and understood does affordability decide the rest.
		/// </summary>
		/// <param name="IsHazardous">True if the engine's own explosive check, or a fusion
		/// reactor part, flags the machine as dangerous to keep near people.</param>
		/// <param name="IsBroken">True if the machine carries the vanilla Broken effect.</param>
		/// <param name="IsRusted">True if the machine carries the vanilla Rusted effect.</param>
		/// <param name="IsUnderstood">True only if the founder has identified the machine and
		/// some tinker's schema for it exists at all &mdash; see
		/// <see cref="SalvageVerdict.RefusedNotUnderstood"/>.</param>
		/// <param name="Complexity">The machine's Examiner Complexity (0 if it has no Examiner).</param>
		/// <param name="Difficulty">The machine's Examiner Difficulty (0 if it has no Examiner).</param>
		/// <param name="StoredWater">Fresh water currently in the settlement's dedicated stores.</param>
		/// <param name="Population">The settlement's current population.</param>
		/// <param name="WaterCost">Set to what certifying this machine would cost, regardless
		/// of the verdict reached &mdash; a refusal still discloses the price.</param>
		/// <param name="HandsRequired">Set to how many settlers certifying this machine would
		/// need free, regardless of the verdict reached.</param>
		/// <returns>The verdict; <see cref="SalvageVerdict.Certified"/> only when every check
		/// passes.</returns>
		public static SalvageVerdict Assess(bool IsHazardous, bool IsBroken, bool IsRusted, bool IsUnderstood, int Complexity, int Difficulty, int StoredWater, int Population, out int WaterCost, out int HandsRequired)
		{
			WaterCost = ComputeWaterCost(Complexity);
			HandsRequired = ComputeHandsRequired(Difficulty);
			if (IsHazardous)
			{
				return SalvageVerdict.RefusedHazardous;
			}
			if (IsBroken)
			{
				return SalvageVerdict.RefusedBroken;
			}
			if (IsRusted)
			{
				return SalvageVerdict.RefusedRusted;
			}
			if (!IsUnderstood)
			{
				return SalvageVerdict.RefusedNotUnderstood;
			}
			if (StoredWater < WaterCost)
			{
				return SalvageVerdict.RefusedCannotAfford;
			}
			if (Population < HandsRequired)
			{
				return SalvageVerdict.RefusedNoHands;
			}
			return SalvageVerdict.Certified;
		}

		/// <summary>True for every verdict except <see cref="SalvageVerdict.Certified"/>.</summary>
		public static bool IsRefusal(SalvageVerdict Verdict)
		{
			return Verdict != SalvageVerdict.Certified;
		}

		/// <summary>
		/// True for a refusal the founder can fix by fetching more water or coming back once
		/// the settlement has hands to spare &mdash; as opposed to a refusal the machine itself
		/// has to earn its way out of first (mended, de-rusted, made safe, or identified).
		/// </summary>
		public static bool IsRetryable(SalvageVerdict Verdict)
		{
			return Verdict == SalvageVerdict.RefusedCannotAfford || Verdict == SalvageVerdict.RefusedNoHands;
		}
	}
}
