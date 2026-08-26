using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Where the city's receipts land. LIVING-CITY-ARCHITECTURE &sect;6.5: one greppable line per
	/// event, in the shape the log-watcher already reads, behind the dev-log option.
	/// <para>
	/// The ring underneath it is <c>KingdomComputeJournalRing</c>'s, so the session worsts are kept
	/// whether or not the option is on: a tester who turns the log on halfway through a session
	/// still has the worst reckon of the session to read.
	/// </para>
	/// </summary>
	public sealed class KingdomCityJournal : IKingdomComputeJournal
	{
		private readonly KingdomComputeJournalRing ring = new KingdomComputeJournalRing();

		void IKingdomComputeJournal.Record(KingdomPerfReceipt receipt)
		{
			((IKingdomComputeJournal)ring).Record(receipt);
			KingdomLog.Log(KingdomBudgetRules.FormatReceiptBody(receipt));
		}

		internal bool TryWorst(KingdomBudgetLane lane, out KingdomPerfReceipt receipt)
		{
			return ring.TryWorst(lane, out receipt);
		}
	}

	/// <summary>
	/// The city book at the engine's edge: check-in, check-out, and the reify that makes a
	/// deficit real.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;1.1: <i>the city is a book, and a zone is a page of it that
	/// happens to be open.</i> While a zone is attended the ground is authoritative and the model
	/// is a mirror; while it is suspended the model is authoritative. The handoff is here and
	/// nowhere else — every consumer that used to read a <c>r_TAF_Supports_*</c> or
	/// <c>r_TAF_Larders_*</c> game-state key now reads a zone row through this class.
	/// </para>
	/// <para>
	/// Engine-coupled by design, and paired with <c>KingdomCityRules</c> exactly as
	/// <c>KingdomSubsidence</c> is paired with <c>KingdomSubsidenceRules</c>: nothing here decides
	/// anything, it only reads the ground, asks the rules, and applies the answer.
	/// </para>
	/// </summary>
	public static partial class KingdomCity
	{
		/// <summary>
		/// Dedication order, stamped on a vessel or larder the first pass that counts it as the
		/// city's.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 needs dedication order to be a STORED FACT rather
		/// than a ranking recomputed from contents, and the several places a container becomes the
		/// city's — the Charter, a commission, a scaffold, an adoption — have no single moment to
		/// stamp from. The earliest moment the city can know about a container is the first pass
		/// that sees it dedicated, so that is when the ordinal is minted, and it never moves
		/// afterwards. The founder's newest dedication is the reserve that outlives everything
		/// else, which is what the ordering is for.
		/// </para>
		/// </summary>
		public const string DedicationOrderProperty = "KingdomDedicationOrder";

		private static readonly KingdomCityJournal Journal = new KingdomCityJournal();

		private static readonly KingdomExecutor Executor = new KingdomExecutor(new KingdomStopwatchClock(), Journal);

		/// <summary>
		/// The one computation seam the city has, shared with the heartbeat.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;2.5: the choke point exists so that no wave after W0 can
		/// grow a second computation path, and &sect;3.6 spends that promise immediately &mdash; the
		/// micro-reckon goes through the same executor as the homecoming reckon, so a slice and a
		/// pass are one advancement split rather than two implementations of a clock.
		/// </para>
		/// </summary>
		internal static KingdomExecutor Seam
		{
			get { return Executor; }
		}

		/// <summary>Records a receipt for work the executor did not run &mdash; the per-turn reify
		/// spend and the prefetch thaw, both of which touch the ground and therefore cannot cross
		/// the seam's engine-free boundary. Same journal, same log line, same session worsts
		/// (&sect;6.5).</summary>
		internal static void Record(KingdomPerfReceipt receipt)
		{
			((IKingdomComputeJournal)Journal).Record(receipt);
		}

	}
}
