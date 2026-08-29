using System;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently.
namespace XRL.World.Parts
{
	/// <summary>
	/// A commissioned building part-way up: the frame that stands on the ground between the
	/// order and the raising.
	/// <para>
	/// <b>It rises on labour, not on the calendar</b> (BUILDING-CATALOGUE-BRIEF.md Addendum 8
	/// clause 2, and the author's ruling that a scaffold nobody works on does not rise). The
	/// duration a design authors in <c>BuildTicks</c> is what a properly-crewed settlement takes
	/// &mdash; its bounded named gang of <see cref="KingdomRules.RaisingHandsWanted"/> hands &mdash; and it
	/// is banked into <see cref="RemainingTicks"/> when a receipt-backed frame becomes physical.
	/// Receiptless legacy frames retain the older first-look initialization below.
	/// After that, every stretch of elapsed time buys labour ticks at the pace recorded by this
	/// root's exact prior loaded gang witness (<see cref="KingdomRules.RaisingEffectiveness"/>), and a
	/// settlement with nobody free raises nothing at all, however long the founder is away.
	/// </para>
	/// <para>
	/// Idle time is SPENT, never banked: <see cref="LastWorkedTick"/> advances whether or not
	/// anyone stood here, exactly as an unstaffed yard's day budget does. A settlement that
	/// emptied out and refilled does not get the empty months back as a burst of building. And
	/// because a shortfall is a thing the founder can act on, it says so once and unsays itself
	/// the moment the crew is whole again (STANDARDS 7b).
	/// </para>
	/// </summary>
	[Serializable]
	public partial class r_KingdomScaffold : IPart
	{
		public string TargetBlueprint;

		public string TargetDisplayName;

		/// <summary>
		/// Initially the receipt's fully-crewed due tick. Receipt-backed projection separately
		/// freezes the full paid duration into <see cref="RemainingTicks"/> and anchors work at the
		/// physical projection tick, so a late callback cannot earn labour. Restamped when work
		/// runs out, to the tick it ACTUALLY ran out at &mdash; which is what the raising
		/// ceremony needs to know whether the founder was standing there for it
		/// (<c>KingdomCeremonyRules.IsAttended</c>). A frame that finished halfway through an
		/// absence is told in the homecoming, exactly as before; one that finishes under the
		/// founder's eye gathers the crew.
		/// </summary>
		public long CompleteTick;

		/// <summary>Labour ticks left to raise this. Receipt-backed projection freezes the exact
		/// paid duration here before AddObject; zero/zero remains only the receiptless legacy
		/// first-look sentinel.</summary>
		public long RemainingTicks;

		/// <summary>Tick labour was last charged against this frame. Receipt-backed work starts
		/// at its physical projection tick; 0 is the receiptless legacy sentinel.</summary>
		public long LastWorkedTick;

		/// <summary>Whether the founder has already been told this raising is short-handed, so
		/// the reason is given once per stall rather than every turn (STANDARDS 7b).</summary>
		public bool ShortfallSaid;

		public int StaffNeeded;

		public bool ThresholdManning;

		public const string RemovalProofProperty = "KingdomConstructionPredecessorRemoved";
		/// <summary>Named-object property holding exact retry identity for receiptless legacy
		/// scaffolds. This must not become a reflected part field: shipped saves serialize the
		/// public fields of this part positionally through <c>IComponent.Write</c>.</summary>
		public const string LegacySuccessorIdProperty = "KingdomConstructionLegacySuccessorId";
		public const string TellingProperty = "KingdomConstructionTold";
		public const string CompletionNameProperty = "KingdomConstructionCompletionName";
		public const string CompletionTickProperty = "KingdomConstructionCompletionTick";
		public const string CompletionPlanProperty = "KingdomConstructionCompletionPlan";
		public const string FinalPendingProperty = "KingdomConstructionFinalPending";

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			KingdomSystem master = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMaster.AutomaticWorkAllowed(master))
			{
				base.TurnTick(TimeTick, Amount);
				return;
			}
			if (LastWorkedTick <= master.MasterOptionTick)
			{
				LastWorkedTick = TimeTick;
				base.TurnTick(TimeTick, Amount);
				return;
			}
			// Receipt-bearing work advances only from KingdomConstruction.OnSettlementPass.
			// Receiptless scaffolds from old saves retain their legacy turn-tick path.
			if (TargetBlueprint != null && string.IsNullOrEmpty(
				ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty)))
			{
				if (AdvanceLabour(TimeTick)) CompleteLegacy();
			}
			base.TurnTick(TimeTick, Amount);
		}

	}
}
