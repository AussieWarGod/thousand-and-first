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
	/// <summary>
	/// The open jobs as the rules layer works on them: frozen, total, copy-on-write.
	/// <para>
	/// <b>Realm-scope, beside the binding registry rather than inside a city book</b>, and the
	/// reason is &sect;3.8's own: a carrier's legs can cross into the other city's ground or off the
	/// map, and a job a city carried would be lost on a seat swap exactly as a binding would.
	/// LIVING-CITY-ARCHITECTURE &sect;0.0(c) already prices the job rows <b>realm-wide</b> and
	/// &sect;3.8 caps them <b>per realm</b>, so this is where the budget already puts them.
	/// </para>
	/// <para>
	/// A closed job is evicted at once, so absence from this table is proof of closure &mdash; the
	/// same rule the binding registry keeps, for the same reason: there is no second list to fall
	/// out of step with the first.
	/// </para>
	/// </summary>
	internal sealed partial class KingdomJobTable
	{
		private readonly KingdomJobRow[] rows;

		private KingdomJobTable(KingdomJobRow[] rows)
		{
			this.rows = rows;
		}

		internal int Count
		{
			get { return rows.Length; }
		}

		internal static bool TryCreate(KingdomJobRow[] source, out KingdomJobTable table, out KingdomCityFault fault)
		{
			table = null;
			if (source == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (source.Length > KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomJobRow[] kept = new KingdomJobRow[source.Length];
			for (int i = 0; i < source.Length; i++)
			{
				if (source[i].JobId <= 0 || !ValidDeliveryEnvelope(source[i])
					|| (source[i].Kind == KingdomJobKind.Expedition
					&& (source[i].SubjectId <= 0
						|| !KingdomJobRules.IsExpeditionPhase(source[i].OriginCode)
						|| !KingdomJobRules.ValidExpeditionOutcomeForPhase(
							source[i].OriginCode, source[i].OutcomeCode))))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				for (int j = 0; j < i; j++)
				{
					if (kept[j].JobId == source[i].JobId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
					if (source[i].Kind == KingdomJobKind.Expedition
						&& kept[j].Kind == KingdomJobKind.Expedition
						&& kept[j].SubjectId == source[i].SubjectId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
				}
				kept[i] = source[i];
			}
			if (!ValidTrips(kept))
			{
				fault = KingdomCityFault.InvalidLegOrder;
				return false;
			}
			table = new KingdomJobTable(kept);
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
