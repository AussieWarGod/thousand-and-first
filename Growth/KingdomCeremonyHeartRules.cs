namespace ThousandAndFirst
{
	/// <summary>
	/// What the chronicle and the founder are told when one rung of the heart closes. Engine-free,
	/// so every sentence is provable without a running game; the engine-coupled half &mdash;
	/// stamping the rung on the rite ground, sharing the water, filing both registers &mdash; is
	/// <see cref="KingdomCeremonyHeart"/> beside it.
	/// <para>
	/// The register is deliberate. A city-builder's town hall levels up and the small one vanishes;
	/// Qud's own great places are LAYERED &mdash; the Yd Freehold raised over hundreds of years,
	/// Ezra an "archaeological and cultural outgrowth" of the Tomb of the Eaters. So none of these
	/// lines says a building was finished. Each one says what the GROUND has become, and names what
	/// is still underfoot from the rung below.
	/// </para>
	/// </summary>
	public static class KingdomCeremonyHeartRules
	{
		/// <summary>
		/// The rung from which the heart's raising is an ACCOMPLISHMENT rather than a chronicle
		/// entry. The basin and the kerb are a settlement doing what settlements do; the moot yard
		/// is the first rung a stranger on the road would call a place.
		/// </summary>
		public const int AccomplishmentRung = 3;

		/// <summary>Whether a rung's closing is worth an accomplishment in the chronicle.</summary>
		public static bool IsAccomplishment(int Rung)
		{
			return Rung >= AccomplishmentRung && Rung <= KingdomPlotRules.HeartRungKeys.Length;
		}

		/// <summary>
		/// The chronicle's own line for one rung. Written in the voice the chronicle converts to
		/// anyway, and carrying no "was raised at" phrasing: there is one grammar for a building
		/// rising (<c>KingdomCeremony.OnBuildingRaised</c>) and this is an addition on top of it,
		/// never a fork of it.
		/// </summary>
		/// <param name="Rung">One-based rung. Anything off the ladder gets the plain line.</param>
		/// <param name="Realm">The settlement's name; empty falls back to a plain noun.</param>
		public static string ChronicleLine(int Rung, string Realm)
		{
			string name = string.IsNullOrEmpty(Realm) ? "the settlement" : Realm;
			switch (Rung)
			{
				case 1:
					return "the ground where the first water was poured at " + name
						+ " wore into a ring, and the basin was left standing in the middle of it, where anyone could drink";
				case 2:
					return "a kerb of dressed stone closed around the rite ground at " + name
						+ ", laid course by course and none of it over the basin, which stands on the stone now; there is a queue at dusk";
				case 3:
					return "beams went up over the waterstone at " + name
						+ " and the kerb became the floor of them; the charter is read out loud in there, and the awnings go up in the yard outside";
				case 4:
					return "a colonnade of shaped stone closed around the moot hall at " + name
						+ ", which stands inside it yet, beams and door and all; the cairns line the walk where they already stood, the kerb is underfoot, and the basin is in the middle of everything";
				default:
					return "the heart of " + name + " grew by one course";
			}
		}

		/// <summary>The line the founder reads standing there, or on the homecoming.</summary>
		public static string MessageLine(int Rung, string Realm)
		{
			string name = string.IsNullOrEmpty(Realm) ? "the settlement" : Realm;
			switch (Rung)
			{
				case 1:
					return "The rite ground of " + name + " is a ring of trodden earth now, with the basin standing in it.";
				case 2:
					return "The waterstone of " + name + " is closed. Nothing was taken up to lay it; the basin stands on the stone.";
				case 3:
					return "The moot yard of " + name + " stands over the waterstone. The kerb is its floor.";
				case 4:
					return "The great court of " + name + " is closed around the moot hall, and the hall is standing inside it. The basin is where it always was.";
				default:
					return "The heart of " + name + " has grown.";
			}
		}
	}
}
