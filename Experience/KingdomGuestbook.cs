using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled shell for two co-opted ideas that share one file set because they share
	/// one shape: something outside the settlement is marked, and porters or a notable's own feet
	/// close the distance on a later attended pass, never a background clock.
	/// <para>
	/// <b>Guests at the gate</b> extends <see cref="KingdomLocus"/>'s already-shipped plain
	/// travellers with a rarer, structurally different arrival: a notable who carries one
	/// outward-pointing hook, can be lodged into a real bed, and — ignored — leaves the hook
	/// behind as a standing rumor rather than losing it. Runs as a sibling pass to
	/// <c>KingdomLocus</c> on the same <c>ZoneActivatedEvent</c>, under its own marker property
	/// and its own cadence, so the shipped plain-traveller path is never touched.
	/// </para>
	/// <para>
	/// <b>The carry-sign</b> marks a container or pile the founder owns anywhere in the world for
	/// porters to fetch. CarryBook freezes each whole GameObject and central logistics carries that
	/// same reference; the legacy aggregate haul below is decode/reconciliation only.
	/// </para>
	/// </summary>
	public static partial class KingdomGuestbook
	{
		public static bool GuestsEnabled => Options.GetOption("r_TAF_OptionGuestbook") != "No";

		public static bool CarrySignEnabled => Options.GetOption("r_TAF_OptionCarrySign") != "No";

		/// <summary>Marks a notable guest, as opposed to <c>KingdomLocus</c>'s plain
		/// <c>KingdomGuest</c> travellers. The two never collide: a plain traveller never carries
		/// this property, and a notable never carries <c>KingdomGuest</c>.</summary>
		public const string NotableGuestProperty = "KingdomNotableGuest";

		/// <summary>Open blueprint tag for the luxury-lane arrival. A third-party guest may opt
		/// into the same exact fine-house/shop contract without replacing this class.</summary>
		public const string LegendaryTraderTag = "r_TAF_LegendaryTrader";

		/// <summary>Durable resident marker after the visitor settles. The exact home is the
		/// ordinary <c>KingdomLodgingPlotId</c>, so save/reload and every lodging reader share one
		/// authority rather than a parallel guest-only reservation.</summary>
		public const string LegendaryTraderResidentProperty = "KingdomLegendaryTrader";

		internal const string HookKindProperty = "KingdomGuestHookKind";

		internal const string HookTextProperty = "KingdomGuestHookText";

		internal const string LodgeReceiptProperty = "r_TAF_NotableLodgeReceipt";

		private const string OriginProperty = "KingdomOrigin";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
			if (System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			if (KingdomGuestLifecycle.ObserveOption(System,
				KingdomLifecycleLane.NotableGuest, GuestsEnabled, timeTicks, out bool allowNew))
			{
				if (KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.NotableGuest) != null)
					KingdomGuestLifecycle.Drive(System, Z, KingdomLifecycleLane.NotableGuest);
				if (allowNew && KingdomGuestLifecycle.Open(System,
					KingdomLifecycleLane.NotableGuest) == null) RunNotableGuestPass(System, Z, Survey, timeTicks);
			}
			if (CarrySignEnabled)
			{
				KingdomCarryRuntime.Drive(System, Z, timeTicks);
				ResolveLegacyHaulIfDue(System, Z, Survey, timeTicks);
			}
		}

		// ==================================================================================
		// Guests at the gate
		// ==================================================================================

		/// <summary>
		/// Brings notables up the road on their own cadence, whether or not anybody is home, and
		/// tells the founder at awareness what became of the ones nobody met.
		/// <para>
		/// Addendum 8 clause 1 and 3, the same shape <c>KingdomLocus.RunGuestPass</c> keeps for
		/// ambient travellers: everyone whose patience ran out during the absence left a letter,
		/// and the letters are one dated entry between them rather than a queue of strangers in
		/// the square. At most one is still standing, and only when they arrived recently enough
		/// to still be waiting &mdash; which is guaranteed by
		/// <c>NotableGuestPatienceTicks</c> being shorter than
		/// <c>NotableGuestIntervalTicks</c>, not by a live object blocking the spawn.
		/// </para>
		/// </summary>
		private static void RunNotableGuestPass(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long TimeTicks)
		{
			GameObject guest = FindNotableGuest(Survey);
			if (guest != null)
			{
				if (KingdomGuestRules.ShouldDepartUnattended(TimeTicks, System.NotableGuestDepartTick))
				{
					DepartUnattended(System, guest);
				}
				return;
			}
			long effectiveDue = KingdomGuestLifecycle.EffectiveDue(System,
				KingdomLifecycleLane.NotableGuest, KingdomGuestRules.NotableGuestIntervalTicks);
			if (effectiveDue <= 0L || TimeTicks < effectiveDue) return;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(
				effectiveDue, TimeTicks, KingdomGuestRules.NotableGuestIntervalTicks,
				KingdomGuestRules.NotableGuestPatienceTicks);
			Cell standingCell = passages.StandingSince > 0L ? KingdomLocus.HeartArrivalCell(Z) : null;
			long before = System.NextNotableGuestTick > 0L ? System.NextNotableGuestTick : 0L;
			long after = passages.StandingSince > 0L && standingCell == null
				? passages.StandingSince : passages.NextDueTick;
			int daysAgo = passages.Departed > 0
				? KingdomRules.ElapsedDays(TimeTicks - passages.LastDepartedTick) : 0;
			string chronicle = passages.Departed > 0
				? KingdomGuestRules.PassedChronicleLine(passages.Departed, KingdomPresentation.Rich(System.SeatName), daysAgo)
				: null;
			string ledger = passages.Departed > 0
				? KingdomGuestRules.PassedLedgerNote(passages.Departed, daysAgo) : null;
			string guestbook = passages.Departed > 0
				? KingdomGuestRules.PassedGuestbookLine(passages.Departed, daysAgo) : null;
			if (!KingdomGuestLifecycle.PublishPassages(System, Z,
				KingdomLifecycleLane.NotableGuest, TimeTicks, before, after, passages.Departed,
				passages.LastDepartedTick, passages.StandingSince, chronicle, ledger, guestbook))
				return;
			if (passages.StandingSince <= 0L)
			{
				return;
			}
			// Spawned at the tick they actually walked up: their patience is already partly spent,
			// their hook is drawn on their own arrival ordinal, and they leave when they were
			// always going to leave.
			if (standingCell != null)
				SpawnNotableGuest(System, Z, standingCell, passages.StandingSince);
		}

		private static GameObject FindNotableGuest(KingdomSurvey Survey)
		{
			return Survey != null && Survey.NotableGuests.Count > 0
				? Survey.NotableGuests[0] : null;
		}

		/// <summary>Puts one notable on the ground at the tick they walked up. False when there
		/// was nowhere to stand them, which is the caller's signal to leave their arrival unspent
		/// rather than losing them.</summary>
		private static bool SpawnNotableGuest(KingdomSystem System, Zone Z, Cell cell,
			long ArrivalTick)
		{
			if (cell == null) return false;
			KingdomSemanticPersonPlan plan;
			string planFailure;
			if (!KingdomGuestLifecycle.TryPrepareSpawnPlan(System,
				KingdomLifecycleLane.NotableGuest, "r_KingdomNotableGuests",
				"r_KingdomNotableGuest", out plan, out planFailure))
			{
				KingdomLog.Log("notable guest waits: " + planFailure);
				return false;
			}
			KingdomGuestRules.HookKind kind;
			string hookText;
			// Drawn on the tick they arrived on, not the tick the founder walked in: the hook is
			// this guest's own fact, and keying it to the arrival ordinal means a reload asks the
			// same question and gets the same answer.
			if (!DrawHook(System, plan, out kind, out hookText)) return false;
			long depart = KingdomGuestRules.DepartTickFor(ArrivalTick);
			string shownName = KingdomPresentation.Rich(plan.Name);
			string shownHook = KingdomPresentation.Rich(hookText);
			string chronicle = KingdomGuestRules.ArrivalChronicleLine(shownName,
				KingdomPresentation.Rich(System.SeatName));
			string ledger = shownName + " is waiting at the rite ground with word of "
				+ shownHook + ".";
			string message = "{{C|" + shownName
				+ " has arrived at the rite ground as a notable guest.}}";
			string guestbook = shownName + ", waiting at the rite ground with word of "
				+ shownHook + " {{K|(standing)}}";
			return KingdomGuestLifecycle.PublishSpawn(System, Z,
				KingdomLifecycleLane.NotableGuest, cell, The.Game.TimeTicks, depart,
				plan.Blueprint, plan.Name, plan.Origin, (int)kind, 0, hookText, null, null,
				chronicle, ledger, message, guestbook, semanticPlan: plan);
		}

		private static bool DrawHook(KingdomSystem System, KingdomSemanticPersonPlan Plan,
			out KingdomGuestRules.HookKind Kind, out string HookText)
		{
			SemanticEventKey key;
			KernelFaultCode fault;
			ulong kindRoll;
			ulong flavorRoll;
			if (System != null && Plan != null && Plan.Sequence > 0L
				&& SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
					System.CurrentSettlementId, KingdomSemanticSelection.NotableGuestStream,
					KingdomSemanticSelection.HookEventKind, (ulong)Plan.Sequence,
					out key, out fault)
				&& CounterRandom.TryDrawBelow(System.SimulationSeed, key, 0u,
					(ulong)KingdomGuestRules.HookKindCount, out kindRoll, out fault)
				&& CounterRandom.TryDrawBelow(System.SimulationSeed, key, 1u, 1000uL,
					out flavorRoll, out fault))
			{
				Kind = KingdomGuestRules.PickHookKind(kindRoll);
				HookText = KingdomGuestRules.HookText(Kind, flavorRoll);
				return true;
			}
			// No immutable subject means no mutable fallback and therefore no published guest.
			Kind = KingdomGuestRules.PickHookKind(0UL);
			HookText = KingdomGuestRules.HookText(Kind, 0UL);
			return false;
		}

		private static void DepartUnattended(KingdomSystem System, GameObject Guest)
		{
			string name = PlainGuestName(Guest);
			string shownName = KingdomPresentation.Rich(name);
			KingdomGuestRules.HookKind kind = (KingdomGuestRules.HookKind)Guest.GetIntProperty(HookKindProperty);
			string hookText = Guest.GetStringProperty(HookTextProperty) ?? "";
			string shownHook = KingdomPresentation.Rich(hookText);
			string chronicle = KingdomGuestRules.DepartedChronicleLine(shownName,
				KingdomPresentation.Rich(System.SeatName)) + "; others said "
				+ KingdomGuestRules.DepartedOutsiderRumor(shownName, kind, shownHook);
			string ledger = KingdomGuestRules.DepartedLedgerNote(shownName,
				KingdomRules.ElapsedDays(The.Game.TimeTicks - System.NotableGuestDepartTick));
			string guestbook = KingdomGuestRules.GuestbookLine(shownName, kind, shownHook,
				Lodged: false);
			KingdomGuestLifecycle.PublishDeparture(System, Guest,
				KingdomLifecycleLane.NotableGuest, The.Game.TimeTicks,
				KingdomGuestRules.NextDueTick(The.Game.TimeTicks), greeted: false,
				chronicle, ledger, null, guestbook);
		}
	}
}
