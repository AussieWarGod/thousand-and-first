#if !TAF_TESTS
namespace ThousandAndFirst
{
	/// <summary>
	/// The tie between <see cref="KingdomCivicMemoryLimits"/>' mirrored caps and the frozen
	/// constants they were copied from.
	/// <para>
	/// A comment claiming a number was derived is worth nothing the day somebody edits the family
	/// it was derived from. This class reads the real constants and refuses if any of them has
	/// moved, which turns the derivation from a story about the past into a condition the running
	/// game checks before every save.
	/// </para>
	/// <para>
	/// It is compiled out of the pure test projects because naming these codecs reaches the game
	/// engine &mdash; see <see cref="KingdomCivicMemoryFamilyTable"/> for the chain. The
	/// source-contract test asserts instead that every mirror named in
	/// <see cref="KingdomCivicMemoryLimits"/> is still bound here, and the caps test independently
	/// recomputes each number from the frozen family's own source.
	/// </para>
	/// <para>
	/// Nothing here is repaired automatically. A moved maximum is a decision for whoever moved it,
	/// not something this authority may quietly absorb.
	/// </para>
	/// </summary>
	public static class KingdomCivicMemoryDerivation
	{
		/// <summary>
		/// Confirms every per-section cap still equals the frozen constant it mirrors.
		/// </summary>
		/// <param name="Failure">The first divergence found, or an empty string.</param>
		public static bool Verify(out string Failure)
		{
			if (!Same("civic artifacts", KingdomCivicMemoryLimits.MaxCivicArtifactsBytes,
				KingdomCivicArtifactsCodec.MaxEnvelopeBytes, out Failure)) return false;
			if (!Same("civic practice", KingdomCivicMemoryLimits.MaxCivicPracticeBytes,
				KingdomCivicPracticeCodec.MaxEnvelopeBytes, out Failure)) return false;
			if (!Same("body history", KingdomCivicMemoryLimits.MaxBodyHistoryBytes,
				KingdomBodyHistoryCodec.MaxEnvelopeBytes, out Failure)) return false;
			if (!Same("curiosity", KingdomCivicMemoryLimits.MaxCuriosityBytes,
				KingdomCuriosityLeadCodec.MaxCuriosityBookBytes, out Failure)) return false;
			if (!Same("civic leads", KingdomCivicMemoryLimits.MaxCivicLeadsBytes,
				KingdomCuriosityLeadCodec.MaxLeadBookBytes, out Failure)) return false;
			if (!Same("treaty", KingdomCivicMemoryLimits.MaxTreatyBytes,
				Treaty.KingdomTreatyCodec.MaxEnvelopeBytes, out Failure)) return false;
			if (!Same("communal rite", KingdomCivicMemoryLimits.MaxCommunalRiteBytes,
				KingdomCommunalRiteCodec.MaxEnvelopeBytes, out Failure)) return false;
			if (!Same("guest feast", KingdomCivicMemoryLimits.MaxGuestFeastBytes,
				KingdomGuestFeastCodec.MaxEnvelopeBytes, out Failure)) return false;
			if (!Same("village covenant", KingdomCivicMemoryLimits.MaxVillageCovenantBytes,
				KingdomVillageCovenantCodec.MaxEnvelopeBytes, out Failure)) return false;
			// The one cap that was chosen against this envelope rather than inherited from a
			// family frozen before it. Its ceiling is the widest cap an unknown section at that id
			// was already held to, so teaching this build what section 9 means can only narrow what
			// a payload there may be. A later revision that outgrows that headroom is a decision,
			// not something this authority may absorb.
			if (KingdomCivicMemoryLimits.MaxVillageCovenantBytes
				> KingdomCivicMemoryLimits.MaxTreatyBytes)
			{
				Failure = "the village-covenant archive now caps at "
					+ KingdomCivicMemoryLimits.MaxVillageCovenantBytes
					+ " bytes, above the " + KingdomCivicMemoryLimits.MaxTreatyBytes
					+ " an unknown section at that id was already held to; making section 9 known "
					+ "must never widen what a payload there may be";
				return false;
			}
			Failure = "";
			return true;
		}

		private static bool Same(string Family, int Mirror, int Frozen, out string Failure)
		{
			if (Mirror == Frozen)
			{
				Failure = "";
				return true;
			}
			Failure = "the " + Family + " wire family now caps at " + Frozen
				+ " bytes, but civic memory still reserves " + Mirror
				+ "; the envelope's section and cumulative caps must be re-derived";
			return false;
		}
	}
}
#endif
