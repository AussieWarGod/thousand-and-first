using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomJobTable
	{
		internal bool TryAt(int index, out KingdomJobRow row)
		{
			row = default(KingdomJobRow);
			if (index < 0 || index >= rows.Length)
			{
				return false;
			}
			row = rows[index];
			return true;
		}

		internal bool TryGet(int jobId, out KingdomJobRow row)
		{
			row = default(KingdomJobRow);
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].JobId == jobId)
				{
					row = rows[i];
					return true;
				}
			}
			return false;
		}

		internal bool Holds(int jobId)
		{
			KingdomJobRow row;
			return TryGet(jobId, out row);
		}

		/// <summary>Opens a job. Refuses a duplicate id and refuses past the cap; publishes nothing
		/// on either, so the caller's table stays byte-identical.</summary>
		internal bool TryOpen(KingdomJobRow row, out KingdomJobTable next, out KingdomCityFault fault)
		{
			next = null;
			if (row.JobId <= 0 || row.Kind == KingdomJobKind.None)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (Holds(row.JobId))
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			if (rows.Length >= KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomJobRow[] grown = new KingdomJobRow[rows.Length + 1];
			Array.Copy(rows, grown, rows.Length);
			grown[rows.Length] = row;
			return TryCreate(grown, out next, out fault);
		}

		/// <summary>Rewrites one job's row in place of the old one &mdash; a re-projection, a
		/// landed cargo, a status.</summary>
		internal bool TryReplace(KingdomJobRow row, out KingdomJobTable next, out KingdomCityFault fault)
		{
			next = null;
			if (!Holds(row.JobId))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			KingdomJobRow[] rewritten = new KingdomJobRow[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				rewritten[i] = (rows[i].JobId == row.JobId) ? row : rows[i];
			}
			return TryCreate(rewritten, out next, out fault);
		}

		/// <summary>Publishes one frozen trip transition atomically: every stop changes phase/route
		/// together or no row changes.</summary>
		internal bool TryRewrite(KingdomJobRow[] replacements, int count,
			out KingdomJobTable next, out KingdomCityFault fault)
		{
			next = null;
			if (replacements == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > replacements.Length || count > rows.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < count; i++)
			{
				if (!Holds(replacements[i].JobId))
				{
					fault = KingdomCityFault.UnknownBinding;
					return false;
				}
				for (int j = 0; j < i; j++)
					if (replacements[j].JobId == replacements[i].JobId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
			}
			KingdomJobRow[] rewritten = new KingdomJobRow[rows.Length];
			Array.Copy(rows, rewritten, rows.Length);
			for (int i = 0; i < rewritten.Length; i++)
				for (int j = 0; j < count; j++)
					if (rewritten[i].JobId == replacements[j].JobId)
						rewritten[i] = replacements[j];
			return TryCreate(rewritten, out next, out fault);
		}

		/// <summary>Evicts every row owned by one central trip in one table publication.</summary>
		internal bool TryCloseTrip(int tripId, out KingdomJobTable next,
			out KingdomJobRow[] closed, out KingdomCityFault fault)
		{
			next = null;
			closed = null;
			int count = 0;
			for (int i = 0; i < rows.Length; i++)
				if (rows[i].DeliveryTripId == tripId) count++;
			if (tripId <= 0 || count <= 0)
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			closed = new KingdomJobRow[count];
			KingdomJobRow[] kept = new KingdomJobRow[rows.Length - count];
			int c = 0;
			int k = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].DeliveryTripId == tripId) closed[c++] = rows[i];
				else kept[k++] = rows[i];
			}
			return TryCreate(kept, out next, out fault);
		}

		/// <summary>Evicts a job. There is no closed list: the eviction IS the closure.</summary>
		internal bool TryClose(int jobId, out KingdomJobTable next, out KingdomJobRow closed, out KingdomCityFault fault)
		{
			next = null;
			closed = default(KingdomJobRow);
			if (!TryGet(jobId, out closed))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			KingdomJobRow[] shrunk = new KingdomJobRow[rows.Length - 1];
			int at = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].JobId != jobId)
				{
					shrunk[at++] = rows[i];
				}
			}
			return TryCreate(shrunk, out next, out fault);
		}

		/// <summary>Every open job's id, oldest first. The order the pump renders them in, and
		/// stable so a save and reload resumes in exactly the same place.</summary>
		internal int[] OpenIds()
		{
			int[] ids = new int[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				ids[i] = rows[i].JobId;
			}
			return ids;
		}
	}
}
