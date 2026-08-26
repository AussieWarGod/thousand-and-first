using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExtensions
	{
		/// <summary>
		/// Every extension-taught happening since the city last asked, recorded to the chronicle
		/// and pushed only while the pass's shared telling budget has a line to spare.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Reading">The frozen reading.</param>
		/// <param name="Label">The city's name, for the word surface.</param>
		/// <param name="Here">Whether the founder is standing in this city.</param>
		/// <param name="CursorWire">Bounded per-source last-ask receipts.</param>
		/// <param name="PublishCursor">Publishes one prepared receipt before its source runs.</param>
		/// <param name="LegacySinceTick">Retired city-wide receipt. Used only to seed an absent
		/// per-source wire after upgrade; never authorizes a current per-source window.</param>
		/// <param name="NowTick">The pass's own clock, and the ceiling a notice may be dated at.
		/// Passed in rather than read off the reading: the book's processed-through tick can lag
		/// the pass by the part of a day it has not integrated yet, and a source that honestly
		/// dated a notice "now" would have it silently dropped as the future.</param>
		/// <param name="Spare">Told lines still unspent on this pass.</param>
		/// <returns>Lines actually pushed. Recording is unbudgeted; only the push is.</returns>
		internal static int Happenings(KingdomSystem System, KingdomCityReading Reading, string Label,
			bool Here, string CursorWire, Action<string> PublishCursor, long LegacySinceTick,
			long NowTick, int Spare)
		{
			int pushed = 0;
			if (System == null || Reading == null || PublishCursor == null)
			{
				return 0;
			}
			List<Binding> sources = new List<Binding>();
			List<string> sourceKeys = new List<string>();
			foreach (Binding binding in Registry())
			{
				IKingdomHappeningSource source = binding.Extension as IKingdomHappeningSource;
				if (source == null)
				{
					continue;
				}
				if (sources.Count >= KingdomHappeningCursorRules.MaxSources)
				{
					Fault(binding.ModName, "happenings", "SourceCap");
					continue;
				}
				string sourceKey;
				if (!KingdomHappeningCursorRules.TrySourceKey(binding.ModName,
					binding.AssemblyName, binding.TypeName, out sourceKey))
				{
					Fault(binding.ModName, "happenings", "SourceIdentity");
					continue;
				}
				sources.Add(binding);
				sourceKeys.Add(sourceKey);
			}
			string cursor = CursorWire ?? "";
			if (cursor.Length == 0 && LegacySinceTick > 0L)
			{
				if (LegacySinceTick > NowTick || !KingdomHappeningCursorRules.TrySeedLegacy(
					sourceKeys, LegacySinceTick, out cursor))
				{
					Fault("The Thousand and First", "happening cursors", "LegacySeedRefused");
					return 0;
				}
				PublishCursor(cursor);
			}
			if (!KingdomHappeningCursorRules.TryRetain(cursor, sourceKeys, out cursor))
			{
				Fault("The Thousand and First", "happening cursors", "MalformedWire");
				return 0;
			}
			if (!string.Equals(cursor, CursorWire ?? "", StringComparison.Ordinal))
				PublishCursor(cursor);
			for (int i = 0; i < sources.Count; i++)
			{
				Binding binding = sources[i];
				IKingdomHappeningSource source = (IKingdomHappeningSource)binding.Extension;
				long sinceTick;
				string prepared;
				if (!KingdomHappeningCursorRules.TryAdvance(cursor, sourceKeys[i], NowTick,
					out sinceTick, out prepared))
				{
					Fault(binding.ModName, "happenings", "CursorRefused");
					continue;
				}
				// Advance before third-party code. A throw therefore loses this window on the same
				// documented terms as a timeout; it cannot replay already-recorded notices after load.
				PublishCursor(prepared);
				cursor = prepared;
				HappeningJob job = new HappeningJob(source, sinceTick,
					new KingdomExtensionDraws(System.SimulationSeed, Reading.SettlementId,
						binding.ModName), binding.ModName);
				KingdomComputeResult<KingdomNotice[]> result = KingdomCity.Seam.Submit(Reading, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "happenings", result.Status.ToString());
					continue;
				}
				pushed += Record(System, binding, result.Value, Label, Here, NowTick, sinceTick,
					Spare - pushed);
			}
			return pushed;
		}

	}
}
