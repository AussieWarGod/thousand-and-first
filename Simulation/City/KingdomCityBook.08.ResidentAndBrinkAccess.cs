using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		/// <summary>Repairs the resident columns only if they are ragged. Square columns are the
		/// ordinary case and cost one length comparison per column to confirm.</summary>
		private void EnsureResidentColumnsSquare()
		{
			// A null column is an absent named field, which is ragged in the strongest sense; Rows
			// answers -1 for one so the comparison below can never be true.
			int count = Rows(ResidentIds);
			if (count >= 0
				&& Rows(ResidentNames) == count && Rows(ResidentOrigins) == count
				&& Rows(ResidentOriginCodes) == count && Rows(ResidentCreedCodes) == count
				&& Rows(ResidentArrivedTicks) == count && Rows(ResidentArrived) == count
				&& Rows(ResidentHomeWorkIds) == count
				&& Rows(ResidentJobWorkIds) == count && Rows(ResidentJobRoles) == count
				&& Rows(ResidentDayShapes) == count && Rows(ResidentStandings) == count
				&& Rows(ResidentCauses) == count && Rows(ResidentBoundZoneIds) == count
				&& Rows(ResidentRoofStanding) == count && Rows(ResidentRoofTicks) == count
				&& Rows(ResidentRoofWarnedTicks) == count && Rows(ResidentCreedStanding) == count
				&& Rows(ResidentCreedTicks) == count && Rows(ResidentCreedWarnedTicks) == count
				&& Rows(ResidentCreedToward) == count && Rows(ResidentCreedChannels) == count
				&& Rows(ResidentKeptCreeds) == count)
			{
				return;
			}
			Normalize();
		}

		private static int Rows<T>(List<T> column)
		{
			return (column == null) ? -1 : column.Count;
		}

		/// <summary>
		/// The cause a standing carries when the stored one did not fit it. <c>Resident</c> carries
		/// none; a <c>Dead</c> or <c>Abroad</c> row falls back to the honestly-unknown member of its
		/// own family rather than to a story nobody witnessed.
		/// </summary>
		private static KingdomStandingCause DefaultCauseFor(KingdomResidentStanding standing)
		{
			switch (standing)
			{
			case KingdomResidentStanding.Dead:
				return KingdomStandingCause.Unwitnessed;
			case KingdomResidentStanding.Abroad:
				return KingdomStandingCause.Astray;
			default:
				return KingdomStandingCause.None;
			}
		}

		/// <summary>
		/// The resident row for this id, or false. The lookup every reader that starts from a
		/// settler's body goes through &mdash; <c>KingdomBrink</c> above all, whose whole storage
		/// layer is now this index plus a column read.
		/// </summary>
		public bool TryResidentRow(int residentId, out int index)
		{
			index = -1;
			// Zero is not an identity, and a null column is a book nothing has ever been written
			// to: both are "no row here" rather than a reason to fault.
			if (residentId == 0 || ResidentIds == null)
			{
				return false;
			}
			for (int i = 0; i < ResidentIds.Count; i++)
			{
				if (ResidentIds[i] == residentId)
				{
					index = i;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// One settler's brink of one kind, straight off the columns.
		/// <para>
		/// Reads the columns rather than materialising the whole model, exactly as
		/// <c>KingdomCity.OtherZones</c> does and for the same reason: this is called once per
		/// settler per pass by three separate consumers, and a full <see cref="TryRead"/> per call
		/// would allocate a city to answer a question about one person.
		/// </para>
		/// <para>
		/// <see cref="Normalize"/> runs only when the resident columns are NOT square, which is a
		/// state only a save written by another build can produce &mdash; every load path and the
		/// one publisher leave them square. A repair on every read would be O(rows) over thirty
		/// columns, several hundred times a pass, to fix something that is already fixed.
		/// </para>
		/// </summary>
		/// <returns>False when this book holds no row for that id, which is the caller's signal
		/// that the settler belongs to some other city or to none.</returns>
		public bool TryReadBrink(int residentId, BrinkKind kind, out bool stands, out long reachedTick, out long warnedTick, out string toward, out int channel)
		{
			stands = false;
			reachedTick = 0L;
			warnedTick = 0L;
			toward = null;
			channel = 0;
			EnsureResidentColumnsSquare();
			int index;
			if (!TryResidentRow(residentId, out index) || (kind != BrinkKind.Roof && kind != BrinkKind.Creed))
			{
				return false;
			}
			bool creed = kind == BrinkKind.Creed;
			stands = (creed ? ResidentCreedStanding[index] : ResidentRoofStanding[index]) != 0;
			if (!stands)
			{
				return true;
			}
			reachedTick = creed ? ResidentCreedTicks[index] : ResidentRoofTicks[index];
			warnedTick = creed ? ResidentCreedWarnedTicks[index] : ResidentRoofWarnedTicks[index];
			if (creed)
			{
				toward = string.IsNullOrEmpty(ResidentCreedToward[index]) ? null : ResidentCreedToward[index];
				channel = ResidentCreedChannels[index];
			}
			return true;
		}

		/// <summary>
		/// Writes one settler's brink of one kind back into the columns.
		/// <para>
		/// A single-row write and not a republish, because that is what this actually is: the brink
		/// consumers change one person's window and nothing else, and rebuilding the whole book
		/// around each of those would make a fault in an unrelated row able to swallow a warning.
		/// A lifted brink clears its own fields, so a forgotten brink leaves nothing behind for a
		/// later read to half-believe.
		/// </para>
		/// </summary>
		public bool TryWriteBrink(int residentId, BrinkKind kind, bool stands, long reachedTick, long warnedTick, string toward, int channel)
		{
			EnsureResidentColumnsSquare();
			int index;
			if (!TryResidentRow(residentId, out index) || (kind != BrinkKind.Roof && kind != BrinkKind.Creed))
			{
				return false;
			}
			long reached = stands ? ((reachedTick > 0L) ? reachedTick : 0L) : 0L;
			long warned = stands ? ((warnedTick > 0L) ? warnedTick : 0L) : 0L;
			if (kind == BrinkKind.Creed)
			{
				ResidentCreedStanding[index] = stands ? 1 : 0;
				ResidentCreedTicks[index] = reached;
				ResidentCreedWarnedTicks[index] = warned;
				ResidentCreedToward[index] = (stands && !string.IsNullOrEmpty(toward)) ? toward : "";
				ResidentCreedChannels[index] = stands ? channel : 0;
				return true;
			}
			ResidentRoofStanding[index] = stands ? 1 : 0;
			ResidentRoofTicks[index] = reached;
			ResidentRoofWarnedTicks[index] = warned;
			return true;
		}
	}
}
