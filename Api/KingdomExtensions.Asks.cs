using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExtensions
	{
		// ==================================================================================
		// The published lanes
		// ==================================================================================

		/// <summary>
		/// Every extension-taught ask, clamped and attributed.
		/// <para>
		/// Preconditions: <paramref name="Reading"/> is the frozen reading the board is being built
		/// from. Side effects: none beyond a log line per faulted source. Failure mode: a source
		/// that throws or runs past its lane's budget contributes nothing, is logged by mod name,
		/// and does not stop the rest.
		/// </para>
		/// </summary>
		internal static List<KingdomAsk> Asks(KingdomSystem System, KingdomCityReading Reading, List<string> Stalled)
		{
			List<KingdomAsk> asks = new List<KingdomAsk>();
			if (System == null || Reading == null)
			{
				return asks;
			}
			foreach (Binding binding in Registry())
			{
				IKingdomAskSource source = binding.Extension as IKingdomAskSource;
				if (source == null)
				{
					continue;
				}
				asks.AddRange(Run(System, Reading, source, binding.ModName, false, Stalled));
			}
			return asks;
		}

		/// <summary>
		/// One ask source across the seam, clamped and attributed. The city's own source goes
		/// through this call too, so a gap in the published contract is a gap in our own board
		/// first (&sect;6.6's reason for opening at W5 rather than W1).
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Reading">The frozen reading.</param>
		/// <param name="Source">The source. Null yields nothing.</param>
		/// <param name="Owner">Who to attribute a fault to, and whose slug prefixes the kinds.</param>
		/// <param name="Own">True for the city's own source: its kinds are already the board's own
		/// vocabulary, so they are not prefixed, and it answers to the board's cap rather than to
		/// the per-extension one.</param>
		/// <param name="Stalled">Collects the owner of a source that faulted, so the board can say
		/// so out loud. STANDARDS &sect;7b: a source that contributed nothing because it broke is
		/// applicable-but-blocked, and a log line is not somewhere the founder will see it.</param>
		internal static List<KingdomAsk> Run(KingdomSystem System, KingdomCityReading Reading, IKingdomAskSource Source, string Owner, bool Own = false, List<string> Stalled = null)
		{
			List<KingdomAsk> asks = new List<KingdomAsk>();
			if (System == null || Reading == null || Source == null)
			{
				return asks;
			}
			bool own = Own;
			AskJob job = new AskJob(Source, new KingdomExtensionDraws(
				System.SimulationSeed, Reading.SettlementId, Owner), Owner, own);
			KingdomComputeResult<KingdomAsk[]> result = KingdomCity.Seam.Submit(Reading, job);
			if (!result.Published)
			{
				Fault(Owner, "asks", result.Status.ToString());
				if (Stalled != null && !Stalled.Contains(Owner))
				{
					Stalled.Add(Owner);
				}
				return asks;
			}
			Keep(result.Value, own ? KingdomAskRules.MaxAsks : KingdomApiRules.MaxAsksPerSource, own ? null : Owner, Reading, asks);
			return asks;
		}

	}
}
