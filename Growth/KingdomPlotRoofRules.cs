using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Roofs -----------------------------------------------------------------------

		/// <summary>How the mod says a roof state out loud.</summary>
		public static string RoofWord(RoofState Roof)
		{
			switch (Roof)
			{
				case RoofState.Open:
					return "open to the sky";
				case RoofState.Soft:
					return "under canvas";
				case RoofState.Carved:
					return "carved from the rock";
				default:
					return "walled";
			}
		}

		/// <summary>
		/// How much weather a roof state keeps off. Rock and raised wall shelter alike, so they
		/// share a rank rather than being ordered by the enum: nothing anywhere reads
		/// <see cref="RoofState"/> ordinally, and a comparison that did would quietly decide a
		/// carved chamber is better shelter than a house.
		/// </summary>
		public static int ShelterRank(RoofState Roof)
		{
			switch (Roof)
			{
				case RoofState.Open:
					return 0;
				case RoofState.Soft:
					return 1;
				default:
					return 2;
			}
		}

		/// <summary>The shelter a bed asks for. A settler sleeps under canvas and does not sleep
		/// in a field, which is the whole of the tent's argument for existing.</summary>
		public const int BedShelter = 1;

		/// <summary>Whether anyone would sleep under this roof.</summary>
		public static bool HoldsBeds(RoofState Roof)
		{
			return ShelterRank(Roof) >= BedShelter;
		}

		/// <summary>Whether weather reaches what stands under this roof. Wall and rock do not
		/// admit it; canvas does, because canvas rolls back.</summary>
		public static bool AdmitsSky(RoofState Roof)
		{
			return Roof == RoofState.Open || Roof == RoofState.Soft;
		}

		/// <summary>Whether the settlement raises an enclosure of its own here. Only
		/// <see cref="RoofState.Walled"/> does: canvas is the design's own object, rock is the
		/// hill's, and an open plot has none.</summary>
		public static bool RaisesWalls(RoofState Roof)
		{
			return Roof == RoofState.Walled;
		}

		/// <summary>Whether anything stands around this footprint at all, ours or the hill's.
		/// This is the roofed test, and <see cref="RoofFromEnclosure"/> is how a structure the
		/// founder built by hand answers it.</summary>
		public static bool Encloses(RoofState Roof)
		{
			return Roof == RoofState.Walled || Roof == RoofState.Carved;
		}

		/// <summary>
		/// The roof a design actually gets on the ground it is raised on. Underground, everything
		/// the settlement would otherwise have enclosed is carved instead: there is no weather to
		/// keep off, no wall worth raising, and the rock is already all four sides.
		/// <para>
		/// <b>An open plot is the exception, and it is not a special case.</b> Carving replaces
		/// the enclosure a design would have raised; it does not roof ground the design
		/// deliberately left unroofed. A field, a salt-pan, a market square or a reservoir taken
		/// underground is a field, a salt-pan, a market square or a reservoir cut into the rock
		/// &mdash; open ground with stone around it, not a sealed chamber. Forcing those to
		/// <see cref="RoofState.Carved"/> quietly made them shelter
		/// (<see cref="HoldsBeds"/> is true of carved and false of open), floored their whole rect
		/// and cut a door into ground that has no inside, and contradicted the measured half of
		/// the same rule &mdash; <see cref="RoofFromEnclosure"/> has always read unbounded ground
		/// underground as open. The two now agree, which is the invariant worth having: what the
		/// settlement declares and what the walls prove answer the same question the same way.
		/// </para>
		/// </summary>
		public static RoofState RoofOnGround(RoofState Declared, bool Underground)
		{
			if (!Underground || Declared == RoofState.Open)
			{
				return Declared;
			}
			return RoofState.Carved;
		}

		/// <summary>The roof state a tier that declares none reads as: an open plot is open and
		/// everything else is walled, which is exactly what every design written before footprints
		/// existed already got.</summary>
		public static RoofState DefaultRoof(bool Open)
		{
			return Open ? RoofState.Open : RoofState.Walled;
		}

		/// <summary>
		/// What a structure the founder raised themselves has over it, measured rather than
		/// declared. The adoption enclosure fill IS the roofed test
		/// (<see cref="KingdomAdoptRules.MeasureEnclosure"/>), and this is the only place its
		/// verdict is turned into a roof state, so the two can never drift apart.
		/// <para>
		/// A soft roof is never measured: canvas is not a wall and the fill runs straight past it,
		/// so a tent somebody pitched by hand honestly reads open. Soft is a thing a design
		/// declares about itself, never a thing walls prove.
		/// </para>
		/// </summary>
		public static RoofState RoofFromEnclosure(KingdomAdoptRules.EnclosureMeasurement Enclosure, bool Underground)
		{
			if (!Enclosure.Bounded)
			{
				return RoofState.Open;
			}
			return Underground ? RoofState.Carved : RoofState.Walled;
		}

		/// <summary>
		/// Whether a roof is enough for what a role needs: somewhere to sleep wants canvas at
		/// least, a work wants something around it, and a cask stands wherever it is put.
		/// Adoption and the catalogue ask the same question of the same table.
		/// </summary>
		public static bool RoofMeetsRole(KingdomAdoptRules.RoleKind Role, RoofState Roof)
		{
			switch (Role)
			{
				case KingdomAdoptRules.RoleKind.Housing:
					return HoldsBeds(Roof);
				case KingdomAdoptRules.RoleKind.Storage:
					return true;
				default:
					return Encloses(Roof);
			}
		}
	}
}
