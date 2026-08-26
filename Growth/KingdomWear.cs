using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name. This part is
// only ever added in code, but it lives here anyway alongside r_KingdomImprovement and
// r_KingdomScaffold: a part whose namespace depends on how it happened to be attached is a trap
// waiting for the first blueprint that names it.
namespace XRL.World.Parts
{
	/// <summary>
	/// One work's own wear record: how damaged it is, why, whether the founder has told the
	/// settlement to leave it be, and &mdash; while a mending is actually under way &mdash; how
	/// much labour is left to put into it.
	/// <para>
	/// Attached lazily, the instant a work first takes damage, and removed the instant a mending
	/// finishes: a sound work carries no part at all, which is the state every building in every
	/// existing save is in, and the state every building returns to once it is whole again. Absent
	/// means sound.
	/// </para>
	/// <para>
	/// What the wear COSTS to mend and how a worn work runs are <c>KingdomMaterialRules</c>' own
	/// (<c>MaxWearPercent</c>, <c>ConditionPercent</c>, <c>RepairCost</c>/<c>RepairBits</c>/
	/// <c>RepairEffort</c>). This part only ever holds the one work's own reading of them.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomWear : IPart
	{
		private const int SerializationMagic = 1415009618;
		private const int CurrentSerializationVersion = 1;

		/// <summary>How worn this work is, 0 to <see cref="KingdomMaterialRules.MaxWearPercent"/>.
		/// Never reaches 100: "a damaged work stands."</summary>
		public int Wear;

		/// <summary>Which of <see cref="KingdomWearRules.WearCause"/> last added to
		/// <see cref="Wear"/>, held as an int so the field's serialized type never depends on an
		/// enum's backing type.</summary>
		public int LastCause;

		/// <summary>The founder's standing "leave this one as it is". Persists on the object,
		/// shows in its description, and is only ever set or cleared from the Charter.</summary>
		public bool Held;

		/// <summary>Labour left to put into the mending under way on this work. Zero means no
		/// mending is under way, whether because none has been started or because
		/// <see cref="Held"/> or a shortage is holding it back.</summary>
		public int RepairEffortLeft;

		/// <summary>
		/// Tick this work's LEAK was last cashed, for a work that stores something (Addendum
		/// 10(b)). Zero means the leak has never been counted, and the first pass that looks
		/// PLANTS the stamp rather than counting from it &mdash; the lesson <c>LastFetchTick</c>
		/// learned, where an unplanted stamp read as the age of the world. Per-work state on the
		/// work's own part, so nothing on the settlement seat has to know that stores leak.
		/// </summary>
		public long LastLeakTick;

		/// <summary>Whether the founder has already been told this store is losing what it holds
		/// (STANDARDS 7b). Said once, and unsaid the moment a mending finishes &mdash; which is
		/// also the moment this whole part is removed, so it can never outlive the leak it
		/// records.</summary>
		public bool LeakAnnounced;

		/// <summary>
		/// The <c>KingdomWearRules.RepairVerdict</c> last announced to the founder for this
		/// work's mending, as an int. Zero means nothing has been announced &mdash; unambiguous
		/// because zero is <c>Ready</c>, which is never announced as a block. Announcing again is
		/// gated on the reason having actually CHANGED, so a settlement short of shaped stone for
		/// a season says so once and then stops.
		/// </summary>
		public int AnnouncedBlock;

		/// <summary>Fail-closed latch for malformed or physically ambiguous receipts.</summary>
		public bool LifecycleQuarantined;
		public string QuarantineReason;
		public bool QuarantineTold;
		public int QuarantineLedgerState;
		public int QuarantineMessageState;

		/// <summary>One exact damage mutation and its keyed telling.</summary>
		public string IncidentId;
		public int IncidentPhase;
		public int IncidentCause;
		public int IncidentBeforeWear;
		public int IncidentAfterWear;
		public string IncidentLine;
		public string LastCompletedIncidentId;
		public int IncidentMessageState;

		/// <summary>One exact storage-loss mutation and checkpoint.</summary>
		public bool LeakClockInitialized;
		public string LeakIncidentId;
		public int LeakPhase;
		public int LeakKind;
		public long LeakFromTick;
		public long LeakToTick;
		public int LeakBefore;
		public int LeakAfter;
		public int LeakWanted;
		public int LeakActualLost;
		public string LeakOwnerId;
		public string LeakZoneId;
		public int LeakCellX;
		public int LeakCellY;
		public int LeakCapacity;
		public string LeakLine;
		public string LeakItemIds;
		public string LeakItemOriginalCounts;
		public string LeakItemAllocations;
		public int LeakLedgerState;
		public int LeakMessageState;

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteObject(SerializationMagic);
			Writer.WriteObject(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(r_KingdomWear));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			object first = Reader.ReadObject();
			if (first is int && (int)first == SerializationMagic)
			{
				object version = Reader.ReadObject();
				if (!(version is int) || (int)version != CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst wear save version.");
				}
				Reader.ReadNamedFields(this, typeof(r_KingdomWear));
			}
			else
			{
				Wear = Convert.ToInt32(first);
				LastCause = Convert.ToInt32(Reader.ReadObject());
				Held = Convert.ToBoolean(Reader.ReadObject());
				RepairEffortLeft = Convert.ToInt32(Reader.ReadObject());
				LastLeakTick = Convert.ToInt64(Reader.ReadObject());
				LeakAnnounced = Convert.ToBoolean(Reader.ReadObject());
				AnnouncedBlock = Convert.ToInt32(Reader.ReadObject());
			}
			NormalizeSerializedFields();
		}

		private void NormalizeSerializedFields()
		{
			bool malformed = false;
			if (!SavedTextWithin(QuarantineReason, KingdomWearRules.MaxSavedTextChars)
				|| !SavedTextWithin(IncidentId, KingdomWearRules.MaxSavedTextChars)
				|| !SavedTextWithin(IncidentLine, KingdomWearRules.MaxSavedTextChars)
				|| !SavedTextWithin(LastCompletedIncidentId, KingdomWearRules.MaxSavedTextChars)
				|| !SavedTextWithin(LeakIncidentId, KingdomWearRules.MaxSavedTextChars)
				|| !SavedTextWithin(LeakOwnerId, KingdomWearRules.MaxObjectIdChars)
				|| !SavedTextWithin(LeakZoneId, KingdomWearRules.MaxObjectIdChars)
				|| !SavedTextWithin(LeakLine, KingdomWearRules.MaxSavedTextChars)
				|| !SavedTextWithin(LeakItemIds, KingdomWearRules.MaxRowsChars)
				|| !SavedTextWithin(LeakItemOriginalCounts, KingdomWearRules.MaxRowsChars)
				|| !SavedTextWithin(LeakItemAllocations, KingdomWearRules.MaxRowsChars))
			{
				malformed = true;
			}
			if (Wear < 0 || Wear > KingdomMaterialRules.MaxWearPercent)
			{
				Wear = (Wear < 0) ? 0 : KingdomMaterialRules.MaxWearPercent;
				malformed = true;
			}
			if (LastCause < (int)KingdomWearRules.WearCause.None
				|| LastCause > (int)KingdomWearRules.WearCause.TemperamentalTech)
			{
				LastCause = (int)KingdomWearRules.WearCause.None;
				malformed = true;
			}
			if (RepairEffortLeft < 0 || LastLeakTick < 0L)
			{
				RepairEffortLeft = (RepairEffortLeft < 0) ? 0 : RepairEffortLeft;
				LastLeakTick = (LastLeakTick < 0L) ? 0L : LastLeakTick;
				malformed = true;
			}
			if (IncidentPhase < 0 || IncidentPhase > (int)KingdomWearIncidentPhase.Quarantined
				|| LeakPhase < 0 || LeakPhase > (int)KingdomWearLeakPhase.Quarantined
				|| LeakFromTick < 0L || LeakToTick < 0L || LeakBefore < 0 || LeakAfter < 0
				|| LeakWanted < 0 || LeakActualLost < 0 || LeakCapacity < 0
				|| !ValidSink(QuarantineLedgerState) || !ValidSink(QuarantineMessageState)
				|| !ValidSink(IncidentMessageState) || !ValidSink(LeakLedgerState)
				|| !ValidSink(LeakMessageState))
			{
				malformed = true;
			}
			if (IncidentPhase != (int)KingdomWearIncidentPhase.None
				&& IncidentPhase != (int)KingdomWearIncidentPhase.Quarantined
				&& (string.IsNullOrEmpty(IncidentId)
					|| IncidentCause <= (int)KingdomWearRules.WearCause.None
					|| IncidentCause > (int)KingdomWearRules.WearCause.TemperamentalTech
					|| IncidentBeforeWear < 0 || IncidentAfterWear < IncidentBeforeWear
					|| IncidentAfterWear > KingdomMaterialRules.MaxWearPercent))
			{
				malformed = true;
			}
			if (LeakPhase != (int)KingdomWearLeakPhase.None
				&& LeakPhase != (int)KingdomWearLeakPhase.Quarantined
				&& (string.IsNullOrEmpty(LeakIncidentId)
					|| string.IsNullOrEmpty(LeakOwnerId)
					|| LeakKind < (int)KingdomWearRules.LeakKind.Water
					|| LeakKind > (int)KingdomWearRules.LeakKind.Food
					|| LeakToTick < LeakFromTick || LeakAfter > LeakBefore
					|| LeakCellX < 0 || LeakCellY < 0
					|| LeakCapacity <= 0 || LeakBefore > LeakCapacity))
			{
				malformed = true;
			}
			if (malformed)
			{
				LifecycleQuarantined = true;
				if (IncidentPhase != (int)KingdomWearIncidentPhase.None)
				{
					IncidentPhase = (int)KingdomWearIncidentPhase.Quarantined;
				}
				if (LeakPhase != (int)KingdomWearLeakPhase.None)
				{
					LeakPhase = (int)KingdomWearLeakPhase.Quarantined;
				}
				QuarantineReason = "A work's saved wear receipt is malformed; no damage, leak, or repair was guessed through it.";
			}
			if (LastLeakTick > 0L) LeakClockInitialized = true;
		}

		private static bool SavedTextWithin(string Text, int Maximum)
		{
			return Text == null || (Maximum >= 0 && Text.Length <= Maximum);
		}

		private static bool ValidSink(int Raw)
		{
			return Raw >= (int)KingdomWearSinkDisposition.None
				&& Raw <= (int)KingdomWearSinkDisposition.Lost;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID || ID == GetDisplayNameEvent.ID;
		}

		/// <summary>
		/// Puts the stage of ruin into the work's own NAME (Addendum 10(c)): a settlement that
		/// fell reads as a field of ruins on the map and in every list it appears in, not as
		/// pristine buildings with quiet arithmetic against them.
		/// <para>
		/// The ladder is <c>KingdomMaterialRules.ConditionAdjective</c>, which is a function of
		/// the wear and of nothing else &mdash; so a mending walks the name back down exactly the
		/// stages the ruin walked it up, and the last of it goes when this part does. A given
		/// name survives all of it: this ADDS an adjective the engine composes, it does not
		/// replace anything, so "the ruined Cistern of Six Winters" is still hers.
		/// </para>
		/// </summary>
		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			string adjective = KingdomMaterialRules.ConditionAdjective(Wear);
			if (!string.IsNullOrEmpty(adjective))
			{
				E.AddAdjective(adjective);
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Puts the work's own condition on the work itself, so the founder can read it by
		/// looking at the thing rather than only in the Status report. What it LOOKS like first
		/// (Addendum 10(c)), then the arithmetic, then whatever the mending is doing about it.
		/// </summary>
		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			string look = KingdomMaterialRules.ConditionLook(Wear);
			if (!string.IsNullOrEmpty(look))
			{
				E.Postfix.Append("\n").Append(look);
			}
			E.Postfix.Append("\n{{rules|").Append(KingdomMaterialRules.ConditionWord(Wear))
				.Append(", running ").Append(KingdomMaterialRules.ConditionPercent(Wear)).Append(" parts in a hundred.")
				.Append(Held ? " Mending is held." : (RepairEffortLeft > 0 ? " Being mended." : "")).Append("}}");
			return base.HandleEvent(E);
		}
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Wear and repair (BUILDING-CATALOGUE-BRIEF.md Addendum 7: "maintenance/wear translation").
	/// Three causes damage a work &mdash; raiders who got past the wall
	/// (<see cref="OnRaidDamage"/>, called from <c>KingdomRaids.ExecuteRaid</c>), a streak of
	/// consecutive full-stretch attended passes, and certified salvage acting up on use &mdash;
	/// and a fourth, a lost rung, reaches a staffless work too (<c>KingdomSubsidence.Ruin</c>).
	/// Nothing else does. Absence never wears anything: every draw in
	/// <see cref="KingdomWearRules"/> is keyed to an event a real pass produced, never to elapsed
	/// time. What already-damaged works go on LOSING does run on world days, which is a
	/// consequence of the damage rather than a second cause of it.
	/// <para>
	/// A damaged work keeps working, at <c>KingdomMaterialRules.ConditionPercent(Wear)</c> of
	/// what it manages whole, and says so once (STANDARDS 7b) the moment it happens. That
	/// reduction reaches EVERY work, crewed or not (Addendum 10(b),
	/// <see cref="KingdomWearRules.WorkEffectiveness"/>), and on top of it damage has
	/// kind-appropriate consequences: a store loses what it holds (<see cref="Leak"/>), a power
	/// work makes less. Mending is a materials-and-hands job, auto-queued like an improvement but always
	/// visible (<c>r_KingdomWear.HandleEvent</c>) and holdable (<see cref="r_KingdomWear.Held"/>):
	/// one job at a time settlement-wide, the same "one gang, one job" law
	/// <c>KingdomMaterials.OnSettlementPass</c> already keeps for striking and clearing, costed
	/// and timed the same way a strike is &mdash; <c>KingdomMaterialRules.RepairCost</c>/
	/// <c>RepairBits</c> for what it costs, <c>RepairEffort</c> and
	/// <c>KingdomRules.ElapsedDays</c> for how long it takes. Nothing here spends water, and
	/// nothing here ever fails a work past <see cref="KingdomMaterialRules.MaxWearPercent"/>.
	/// </para>
	/// <para>
	/// <b>The clock.</b> <see cref="AdvanceRepair"/> is the reference for checkpoint ordering in
	/// this mod: it reads the gate, names the block once (STANDARDS 7b), and only then advances
	/// the stamp &mdash; so a mending nobody has hands for loses those days rather than banking
	/// them for a crew that was never there. <c>KingdomMaterials.WorkYard</c> keeps the same
	/// order for the same reason. The day count is the full elapsed, uncapped (Addendum 8
	/// clause 1): a crew mends through an absence exactly as it mends through a fortnight of
	/// visits, and what stops a season away from mending everything is that ordering &mdash;
	/// hands first, and one mending settlement-wide at a time. Idle hands put nothing back.
	/// </para>
	/// </summary>
	public static class KingdomWear
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.WearRepair)
			{
				return;
			}
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindSubject(Z, Job,
				out work);
			if (workState == KingdomPhysicalLookupState.Ambiguous)
			{
				MarkRepairRemovalLost(ref Job,
					"The repair receipt resolves to more than one exact physical subject.");
				return;
			}
			if (workState != KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(work) || work.CurrentZone != Z) return;
			if (!RepairSubjectExact(System, Z, work, Job))
			{
				MarkRepairRemovalLost(ref Job,
					"The repair receipt is no longer bound to its exact work, cell, zone, and owner.");
				return;
			}
			r_KingdomWear wear = work.GetPart<r_KingdomWear>();
			if (wear == null)
			{
				RecoverRemovedRepair(System, work, Job);
				return;
			}
			if (!string.IsNullOrEmpty(work.GetStringProperty(RepairRemovalAttemptProperty))
				|| !string.IsNullOrEmpty(work.GetStringProperty(RepairRemovalProofProperty)))
			{
				wear.LifecycleQuarantined = true;
				wear.QuarantineReason =
					"A repair part-removal callback was interrupted and will not be repeated.";
				MarkRepairRemovalLost(ref Job, wear.QuarantineReason);
				TellWearQuarantine(System, work, wear);
				return;
			}
			int paidWear;
			bool finishing;
			if (!TryRepairPayload(Job.Payload, out paidWear, out finishing)) return;
			if (finishing)
			{
				FinishRepairProjection(System, work, wear, Job, out _, out _);
				return;
			}
			if (wear.Wear <= 0)
			{
				FinishRepairProjection(System, work, wear, Job, out _, out _);
				return;
			}
			if (wear.RepairEffortLeft > 0)
			{
				KingdomConstructionJob working = Job;
				KingdomConstruction.FinishProjection(ref working, true, true);
				return;
			}
			ProjectRepair(System, work, wear, Job, out _, out _);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.WearRepair) return;
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindSubject(Z, Job,
				out work);
			if (workState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				MarkRepairRemovalLost(ref duplicate,
					"The repair receipt resolves to more than one exact physical subject.");
				return;
			}
			if (workState != KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(work) || work.CurrentZone != Z) return;
			KingdomConstructionJob inspected = Job;
			r_KingdomWear wear = work.GetPart<r_KingdomWear>();
			if (wear == null)
			{
				if (Job.Phase == KingdomConstructionPhase.Complete) return;
				RecoverRemovedRepair(System, work, inspected);
				return;
			}
			if (!RepairSubjectExact(System, Z, work, Job)
				|| !string.IsNullOrEmpty(work.GetStringProperty(RepairRemovalAttemptProperty))
				|| !string.IsNullOrEmpty(work.GetStringProperty(RepairRemovalProofProperty)))
			{
				wear.LifecycleQuarantined = true;
				wear.QuarantineReason =
					"The repair inspector found an uncertain part-removal callback or subject binding.";
				MarkRepairRemovalLost(ref inspected, wear.QuarantineReason);
				TellWearQuarantine(System, work, wear);
				return;
			}
			int paidWear;
			bool finishing;
			if (!TryRepairPayload(Job.Payload, out paidWear, out finishing)) return;
			if (!finishing && wear.RepairEffortLeft > 0)
			{
				if (Job.Phase != KingdomConstructionPhase.Working)
				{
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			if ((Job.Phase == KingdomConstructionPhase.ProjectionPending
					|| (finishing && Job.Phase == KingdomConstructionPhase.Working))
				&& (wear.Wear == paidWear || (finishing && wear.Wear == 0)))
			{
				KingdomConstruction.FinishProjection(ref inspected, false, false,
					finishing
						? "The receipt proves repair labour finished; its final condition is retryable."
						: "The damaged state survived before repair work was projected.");
			}
		}

		private static bool HasActiveRepair(GameObject Work, out KingdomConstructionJob Job)
		{
			Job = null;
			if (!KingdomConstruction.ReceiptBlocksCurrent(Work)) return false;
			string receipt = Work.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstruction.TryFind(receipt, out Job);
			return true;
		}

		private static bool RepairSubjectExact(KingdomSystem System, Zone Z,
			GameObject Work, KingdomConstructionJob Job)
		{
			return System != null && Z != null && Job != null
				&& Job.Route == KingdomConstructionRoute.WearRepair
				&& KingdomConstruction.Owns(System, Z, Job)
				&& GameObject.Validate(Work) && Work.ID == Job.SubjectId
				&& Work.CurrentZone == Z && Work.CurrentCell != null
				&& Work.CurrentCell.ParentZone == Z
				&& KingdomConstruction.HasReceipt(Work, Job);
		}

		private static KingdomConstructionSinkDisposition LoseOpenRepairSink(
			KingdomConstructionSinkDisposition State)
		{
			return State == KingdomConstructionSinkDisposition.Delivered
				|| State == KingdomConstructionSinkDisposition.Skipped
				|| State == KingdomConstructionSinkDisposition.Lost
					? State : KingdomConstructionSinkDisposition.Lost;
		}

		private static void MarkRepairRemovalLost(ref KingdomConstructionJob Job,
			string Failure)
		{
			if (Job == null) return;
			if (Job.Outbox != null)
			{
				KingdomConstructionOutbox lost = Job.Outbox.Copy();
				lost.ChronicleState = LoseOpenRepairSink(lost.ChronicleState);
				lost.LedgerState = LoseOpenRepairSink(lost.LedgerState);
				lost.MessageState = LoseOpenRepairSink(lost.MessageState);
				lost.DeedState = LoseOpenRepairSink(lost.DeedState);
				if (!KingdomConstruction.UpdateOutbox(ref Job, lost)) return;
			}
			KingdomConstruction.Quarantine(ref Job, Failure);
		}

		private static void RecoverRemovedRepair(KingdomSystem System, GameObject Work,
			KingdomConstructionJob Job)
		{
			KingdomConstructionJob recovered = Job;
			Zone zone = Work?.CurrentZone;
			bool proved = RepairSubjectExact(System, zone, Work, recovered)
				&& recovered.Outbox != null
				&& string.Equals(Work.GetStringProperty(RepairRemovalProofProperty),
					recovered.Id, StringComparison.Ordinal)
				&& string.IsNullOrEmpty(
					Work.GetStringProperty(RepairRemovalAttemptProperty));
			if (!proved)
			{
				MarkRepairRemovalLost(ref recovered,
					"The wear part is absent without a persisted exact post-callback removal proof.");
				return;
			}
			if (!KingdomConstruction.Complete(ref recovered)) return;
			Work.RemoveStringProperty(RepairRemovalProofProperty);
			KingdomCeremony.DispatchPending(System, ref recovered);
		}

		private sealed class RepairTargetFrame
		{
			internal GameObject Work;
			internal string Id;
			internal Zone Zone;
			internal Cell Cell;
			internal r_KingdomWear WearPart;
			internal int Wear;
			internal int LastCause;
			internal bool Held;
			internal int Effort;
			internal long LastLeakTick;
			internal bool LeakInitialized;
			internal bool LeakAnnounced;
			internal bool Quarantined;
			internal string Receipt;
		}

		private static bool TryCaptureRepairTarget(GameObject Work, r_KingdomWear Wear,
			out RepairTargetFrame Frame)
		{
			Frame = null;
			if (!GameObject.Validate(Work) || Work.CurrentZone == null || Work.CurrentCell == null
				|| Work.CurrentCell.ParentZone != Work.CurrentZone || Wear == null
				|| Wear.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)) return false;
			Frame = new RepairTargetFrame
			{
				Work = Work,
				Id = Work.ID,
				Zone = Work.CurrentZone,
				Cell = Work.CurrentCell,
				WearPart = Wear,
				Wear = Wear.Wear,
				LastCause = Wear.LastCause,
				Held = Wear.Held,
				Effort = Wear.RepairEffortLeft,
				LastLeakTick = Wear.LastLeakTick,
				LeakInitialized = Wear.LeakClockInitialized,
				LeakAnnounced = Wear.LeakAnnounced,
				Quarantined = Wear.LifecycleQuarantined,
				Receipt = Work.GetStringProperty(KingdomConstruction.ReceiptProperty)
			};
			return true;
		}

		private static bool RepairTargetExact(RepairTargetFrame Frame, string ExpectedReceipt)
		{
			return Frame != null && GameObject.Validate(Frame.Work) && Frame.Work.ID == Frame.Id
				&& Frame.Work.CurrentZone == Frame.Zone && Frame.Work.CurrentCell == Frame.Cell
				&& Frame.Cell != null && Frame.Cell.ParentZone == Frame.Zone
				&& Frame.WearPart != null && Frame.WearPart.ParentObject == Frame.Work
				&& ReferenceEquals(Frame.Work.GetPart<r_KingdomWear>(), Frame.WearPart)
				&& Frame.WearPart.Wear == Frame.Wear
				&& Frame.WearPart.LastCause == Frame.LastCause
				&& Frame.WearPart.Held == Frame.Held
				&& Frame.WearPart.RepairEffortLeft == Frame.Effort
				&& Frame.WearPart.LastLeakTick == Frame.LastLeakTick
				&& Frame.WearPart.LeakClockInitialized == Frame.LeakInitialized
				&& Frame.WearPart.LeakAnnounced == Frame.LeakAnnounced
				&& Frame.WearPart.LifecycleQuarantined == Frame.Quarantined
				&& string.Equals(Frame.Work.GetStringProperty(
					KingdomConstruction.ReceiptProperty), ExpectedReceipt,
					StringComparison.Ordinal);
		}

		private static string RepairPayload(int Wear, bool Finishing)
		{
			return "v1|" + Wear.ToString(global::System.Globalization.CultureInfo.InvariantCulture)
				+ "|" + (Finishing ? "1" : "0");
		}

		private static bool TryRepairPayload(string Payload, out int Wear, out bool Finishing)
		{
			return KingdomWearRules.TryRepairPayload(Payload, out Wear, out Finishing);
		}

		public static bool Enabled => Options.GetOption("r_TAF_OptionWear") != "No";

		/// <summary>Refuses a handover while a wear mutation has identity bound to this object.</summary>
		public static bool CanCarryStableState(GameObject Source, out string Failure)
		{
			Failure = null;
			r_KingdomWear wear = GameObject.Validate(Source)
				? Source.GetPart<r_KingdomWear>() : null;
			if (wear == null) return true;
			if (wear.LifecycleQuarantined
				|| (KingdomWearIncidentPhase)wear.IncidentPhase != KingdomWearIncidentPhase.None
				|| (KingdomWearLeakPhase)wear.LeakPhase != KingdomWearLeakPhase.None
				|| wear.RepairEffortLeft != 0)
			{
				Failure = "That work has a wear, leak, or repair receipt in hand; settle it before changing the plan.";
				return false;
			}
			return true;
		}

		/// <summary>Carries stable founder-visible wear state across an in-place handover.</summary>
		public static bool TryCarryStableState(GameObject Source, GameObject Target)
		{
			if (!GameObject.Validate(Source) || !GameObject.Validate(Target)
				|| !CanCarryStableState(Source, out _)) return false;
			r_KingdomWear before = Source.GetPart<r_KingdomWear>();
			if (before == null) return Target.GetPart<r_KingdomWear>() == null;
			r_KingdomWear after = Target.RequirePart<r_KingdomWear>();
			after.Wear = before.Wear;
			after.LastCause = before.LastCause;
			after.Held = before.Held;
			after.RepairEffortLeft = before.RepairEffortLeft;
			after.LastLeakTick = before.LastLeakTick;
			after.LeakAnnounced = before.LeakAnnounced;
			after.AnnouncedBlock = before.AnnouncedBlock;
			after.LastCompletedIncidentId = before.LastCompletedIncidentId;
			after.LeakClockInitialized = before.LeakClockInitialized;
			return SameStableState(Source, Target);
		}

		public static bool SameStableState(GameObject Source, GameObject Target)
		{
			r_KingdomWear before = GameObject.Validate(Source)
				? Source.GetPart<r_KingdomWear>() : null;
			r_KingdomWear after = GameObject.Validate(Target)
				? Target.GetPart<r_KingdomWear>() : null;
			if (before == null) return after == null;
			return after != null && before.Wear == after.Wear
				&& before.LastCause == after.LastCause && before.Held == after.Held
				&& before.RepairEffortLeft == after.RepairEffortLeft
				&& before.LastLeakTick == after.LastLeakTick
				&& before.LeakAnnounced == after.LeakAnnounced
				&& before.AnnouncedBlock == after.AnnouncedBlock
				&& before.LastCompletedIncidentId == after.LastCompletedIncidentId
				&& before.LeakClockInitialized == after.LeakClockInitialized;
		}

		/// <summary>Consecutive full-stretch attended passes a work carries right now. A plain
		/// property rather than a part field: every crewed work implicitly carries this at zero,
		/// the same way it implicitly carries <c>KingdomEffectiveness</c> at zero, and giving a
		/// sound work a whole part just to hold one counter would mean every crewed building in
		/// the game grows one.</summary>
		public const string HardRunStreakProperty = "KingdomHardRunStreak";
		public const string SemanticPassTickProperty = "KingdomWearPassTick";
		public const string SemanticPassCompletedTickProperty = "KingdomWearLastPassTick";
		public const string SemanticPassCompletedProperty = "KingdomWearLastPassSet";
		public const string SemanticPassPhaseProperty = "KingdomWearPassPhase";
		public const string SemanticPassOriginalStreakProperty = "KingdomWearPassOriginalStreak";
		public const string SemanticPassTargetStreakProperty = "KingdomWearPassTargetStreak";
		public const string SemanticPassHardRollProperty = "KingdomWearPassHardRoll";
		public const string SemanticPassTemperRollProperty = "KingdomWearPassTemperRoll";
		public const string LastRaidIncidentTickProperty = "KingdomWearLastRaidTick";

		/// <summary>Tick a mending under way last had labour charged against it. Read and written
		/// through <c>KingdomMaterials.ReadTick</c>/<c>WriteTick</c>, exactly as
		/// <c>KingdomMaterials.StrikeWorkedProperty</c> is: the same "day since this was last
		/// worked" accounting a strike already uses, so a founder cannot speed a mending by
		/// stepping in and out of the zone, and a long absence still resolves honestly.</summary>
		public const string RepairWorkedProperty = "KingdomRepairWorked";
		public const string DisabledAnchorProperty = "KingdomWearDisabledAnchor";
		public const string RepairRemovalAttemptProperty = "KingdomWearRemovalAttempt";
		public const string RepairRemovalProofProperty = "KingdomWearRemovalProof";

		/// <summary>
		/// The property <c>KingdomGrowth.AssignWork</c> stamps a work's crew-only effectiveness
		/// onto, 0-100. Read here to learn this pass's crew stretch, and never written: this file
		/// used to fold the work's own condition back into it, which made the property mean two
		/// different things at two different points in the same pass and quietly double-counted
		/// wear for anything that read it before the next staffing pass. It is now exactly one
		/// thing everywhere &mdash; what the CREW manages &mdash; and every consumer folds
		/// condition in for itself through <see cref="KingdomWearRules.WorkEffectiveness"/>.
		/// </summary>
		private const string EffectivenessProperty = "KingdomEffectiveness";

		/// <summary>The design's declared crew demand, as the staffing pass stamps it. Zero means
		/// the work asks for nobody, which after Addendum 10(b) no longer means it is immune to
		/// its own damage.</summary>
		private const string StaffNeededProperty = "KingdomStaffNeeded";

		/// <summary>The founder's mark on a vessel dedicated to the settlement's water. A store
		/// carrying it is a work whose CONTENTS can run out of a hole in it.</summary>
		private const string StoresProperty = "KingdomStores";

		/// <summary>The food side of <see cref="StoresProperty"/>: what marks a container the
		/// settlement keeps its food in, and therefore what can spoil.</summary>
		private const string LarderProperty = "KingdomLarder";

		/// <summary>
		/// One work's own wear, 0 when it carries no record at all. The single reader every
		/// consumer of <see cref="KingdomWearRules.WorkEffectiveness"/> goes through, so "absent
		/// means sound" is stated once rather than re-derived at four call sites.
		/// </summary>
		/// <param name="Work">Any object. Null and unvalidated read as sound.</param>
		public static int WearOf(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return 0;
			}
			r_KingdomWear wear = Work.GetPart<r_KingdomWear>();
			return (wear != null && wear.Wear > 0) ? wear.Wear : 0;
		}

		/// <summary>
		/// What one finished work is worth to the settlement this pass, crewed or not: the
		/// staffing pass's own stretch for a work that asks for crew, its bare condition for one
		/// that does not, and 100 for a sound work either way (Addendum 10(b)).
		/// </summary>
		/// <param name="Work">A finished work. Null reads as carrying nothing.</param>
		public static int EffectivenessOf(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return 0;
			}
			int crewAndCondition = KingdomWearRules.WorkEffectiveness(
				Work.GetIntProperty(StaffNeededProperty), Work.GetIntProperty(EffectivenessProperty), WearOf(Work));
			return KingdomCrews.ApplyAffinity(Work, crewAndCondition);
		}

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long now = The.Game.TimeTicks;
			if (!Enabled)
			{
				AnchorDisabledClocks(System, Z, Survey, now);
				return;
			}
			if (AnchorReenabledClocks(System, Z, Survey, now)) return;
			Resolve(System, Z, Survey);
		}

		private static void AnchorDisabledClocks(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long Now)
		{
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || work.CurrentZone != Z) continue;
				ResolveSafeReceipts(System, Survey, work);
				work.SetIntProperty(DisabledAnchorProperty, 1);
				r_KingdomWear wear = work.GetPart<r_KingdomWear>();
				if (wear != null)
				{
					wear.LastLeakTick = Now;
					wear.LeakClockInitialized = true;
				}
				if ((wear != null && wear.RepairEffortLeft > 0)
					|| KingdomConstruction.ReceiptBlocksCurrent(work))
				{
					KingdomMaterials.WriteTick(work, RepairWorkedProperty, Now);
				}
			}
		}

		private static bool AnchorReenabledClocks(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long Now)
		{
			bool anchored = false;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || work.CurrentZone != Z
					|| work.GetIntProperty(DisabledAnchorProperty) != 1) continue;
				anchored = true;
				ResolveSafeReceipts(System, Survey, work);
				work.SetIntProperty(DisabledAnchorProperty, 0);
				r_KingdomWear wear = work.GetPart<r_KingdomWear>();
				if (wear != null)
				{
					wear.LastLeakTick = Now;
					wear.LeakClockInitialized = true;
				}
				KingdomMaterials.WriteTick(work, RepairWorkedProperty, Now);
				KingdomMaterials.WriteTick(work, SemanticPassCompletedTickProperty, Now);
				work.SetIntProperty(SemanticPassCompletedProperty, 1);
				work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.None);
				KingdomMaterials.WriteTick(work, SemanticPassTickProperty, 0L);
			}
			return anchored;
		}

		private static void ResolveSafeReceipts(KingdomSystem System, KingdomSurvey Survey,
			GameObject Work)
		{
			r_KingdomWear wear = Work.GetPart<r_KingdomWear>();
			if (wear == null) return;
			KingdomWearIncidentPhase incident = (KingdomWearIncidentPhase)wear.IncidentPhase;
			if (incident > KingdomWearIncidentPhase.None
				&& incident < KingdomWearIncidentPhase.Complete)
			{
				ApplyDamageIncident(System, Work, (KingdomWearRules.WearCause)wear.IncidentCause,
					wear.IncidentId);
			}
			KingdomWearLeakPhase leak = (KingdomWearLeakPhase)wear.LeakPhase;
			if (leak == KingdomWearLeakPhase.MutationIntent
				|| (leak >= KingdomWearLeakPhase.Mutated && leak <= KingdomWearLeakPhase.Complete))
			{
				ContinueBoundLeak(System, Survey, Work, wear);
			}
		}

		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			long timeTicks = The.Game.TimeTicks;
			int hands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			List<GameObject> damaged = new List<GameObject>();
			GameObject workingRepair = null;
			// Everything the settlement finished, not only the works that ask for crew. Damage
			// reaches a staffless design (KingdomSubsidence.Ruin walks this same list), so mending
			// has to reach it back: a cistern the fall holed was previously damaged forever,
			// because nothing ever put it in front of the repair queue. Addendum 10(b) makes the
			// damage count against the level, and "mending restores function" is only true if the
			// mending can start.
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				// The two attended causes are causes of RUNNING, so they are only ever asked of a
				// work with a crew on it. A cistern is not run hard and a palisade does not act up.
				bool hasRepair = HasActiveRepair(work, out _);
				if (!hasRepair && work.GetIntProperty(StaffNeededProperty) > 0)
				{
					RollWear(System, settlementId, work, work.GetIntProperty(EffectivenessProperty), timeTicks);
				}
				r_KingdomWear wear = work.GetPart<r_KingdomWear>();
				if (wear != null && wear.LifecycleQuarantined)
				{
					TellWearQuarantine(System, work, wear);
					continue;
				}
				if (wear == null || wear.Wear <= 0)
				{
					continue;
				}
				// The kind-appropriate consequence, on top of the general effectiveness scale every
				// consumer now applies for itself (KingdomWearRules.WorkEffectiveness): a damaged
				// store loses what it is holding, on world time, until somebody mends it.
				Leak(System, Survey, work, wear, timeTicks);
				damaged.Add(work);
				if ((wear.RepairEffortLeft > 0 || hasRepair) && workingRepair == null)
				{
					workingRepair = work;
				}
			}
			System.DamagedWorks = damaged.Count;
			if (damaged.Count == 0)
			{
				return;
			}
			if (workingRepair != null)
			{
				r_KingdomWear workingWear = workingRepair.RequirePart<r_KingdomWear>();
				if (workingWear.RepairEffortLeft > 0)
				{
					AdvanceRepair(System, workingRepair, workingWear, hands, timeTicks);
				}
				AnnounceQueued(System, damaged, workingRepair);
				return;
			}
			GameObject readyWork = null;
			GameObject speaksFirst = null;
			KingdomWearRules.RepairVerdict speaksFirstVerdict = KingdomWearRules.RepairVerdict.Ready;
			for (int i = 0; i < damaged.Count; i++)
			{
				GameObject work = damaged[i];
				r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
				KingdomWearRules.RepairVerdict verdict = Assess(Z, work, wear, hands);
				if (verdict == KingdomWearRules.RepairVerdict.Ready && readyWork == null)
				{
					readyWork = work;
				}
				else if (KingdomWearRules.IsBlocked(verdict) && speaksFirst == null && wear.AnnouncedBlock != (int)verdict)
				{
					speaksFirst = work;
					speaksFirstVerdict = verdict;
				}
			}
			if (readyWork != null)
			{
				StartRepair(System, readyWork, readyWork.RequirePart<r_KingdomWear>(), timeTicks);
				return;
			}
			if (speaksFirst != null)
			{
				r_KingdomWear wear = speaksFirst.RequirePart<r_KingdomWear>();
				wear.AnnouncedBlock = (int)speaksFirstVerdict;
				string line = KingdomWearRules.ReasonLine(speaksFirstVerdict, DisplayName(speaksFirst));
				if (line != null)
				{
					System.Ledger.Note("{{r|" + line + "}}");
				}
			}
		}

		/// <summary>Every OTHER damaged work says once, if it has not already, that this pass's
		/// hands went to the one mending already under way &mdash; the same "one gang, one job"
		/// news a second condemned building gets from <c>KingdomMaterials.OnSettlementPass</c>.</summary>
		private static void AnnounceQueued(KingdomSystem System, List<GameObject> Damaged, GameObject Working)
		{
			for (int i = 0; i < Damaged.Count; i++)
			{
				GameObject work = Damaged[i];
				if (work == Working)
				{
					continue;
				}
				r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
				if (wear.Held || wear.AnnouncedBlock == (int)KingdomWearRules.RepairVerdict.OtherWorkUnderway)
				{
					continue;
				}
				wear.AnnouncedBlock = (int)KingdomWearRules.RepairVerdict.OtherWorkUnderway;
				string line = KingdomWearRules.ReasonLine(KingdomWearRules.RepairVerdict.OtherWorkUnderway, DisplayName(work));
				if (line != null)
				{
					System.Ledger.Note("{{K|" + line + "}}");
				}
			}
		}

		// ==================================================================================
		// The three causes.
		// ==================================================================================

		private static void RollWear(KingdomSystem System, string SettlementId, GameObject Work, int CrewStretch, long TimeTicks)
		{
			long last;
			long active;
			int completed = Work.GetIntProperty(SemanticPassCompletedProperty);
			if (!TryReadStrictTick(Work, SemanticPassCompletedTickProperty, out last)
				|| !TryReadStrictTick(Work, SemanticPassTickProperty, out active)
				|| (completed != 0 && completed != 1))
			{
				QuarantineWear(System, Work, "Its attended wear-pass clock is malformed.");
				return;
			}
			KingdomWearPassPhase phase = (KingdomWearPassPhase)Work.GetIntProperty(
				SemanticPassPhaseProperty);
			if (completed == 1 && active == last
				&& (phase == KingdomWearPassPhase.TemperDone
					|| phase == KingdomWearPassPhase.None))
			{
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.None);
				KingdomMaterials.WriteTick(Work, SemanticPassTickProperty, 0L);
				active = 0L;
				phase = KingdomWearPassPhase.None;
			}
			if (completed == 1 && last == TimeTicks)
			{
				return;
			}
			KingdomWearPassAction action = KingdomWearRules.PassAction(last, active, phase, TimeTicks);
			if (action == KingdomWearPassAction.AlreadyApplied) return;
			if (action == KingdomWearPassAction.Quarantine)
			{
				QuarantineWear(System, Work, "Its attended wear-pass receipt regressed or changed.");
				return;
			}
			if (action == KingdomWearPassAction.Start)
			{
				int original = Work.GetIntProperty(HardRunStreakProperty);
				if (original < 0)
				{
					QuarantineWear(System, Work, "Its hard-running streak is malformed.");
					return;
				}
				int target = (CrewStretch >= 100)
					? ((original == int.MaxValue) ? int.MaxValue : original + 1) : 0;
				KingdomMaterials.WriteTick(Work, SemanticPassTickProperty, TimeTicks);
				Work.SetIntProperty(SemanticPassOriginalStreakProperty, original);
				Work.SetIntProperty(SemanticPassTargetStreakProperty, target);
				Work.SetIntProperty(SemanticPassHardRollProperty,
					CrewStretch >= 100 && KingdomWearRules.RollHardRun(SettlementId, Work.ID, target) ? 1 : 0);
				Work.SetIntProperty(SemanticPassTemperRollProperty,
					CrewStretch > 0 && Work.GetIntProperty(KingdomSalvage.CertifiedProperty) == 1
					&& KingdomWearRules.RollTemperamental(SettlementId, Work.ID, TimeTicks) ? 1 : 0);
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.Bound);
				phase = KingdomWearPassPhase.Bound;
			}
			int beforeStreak = Work.GetIntProperty(SemanticPassOriginalStreakProperty);
			int targetStreak = Work.GetIntProperty(SemanticPassTargetStreakProperty);
			int hardRoll = Work.GetIntProperty(SemanticPassHardRollProperty);
			int temperRoll = Work.GetIntProperty(SemanticPassTemperRollProperty);
			if (beforeStreak < 0 || targetStreak < 0
				|| (hardRoll != 0 && hardRoll != 1) || (temperRoll != 0 && temperRoll != 1))
			{
				QuarantineWear(System, Work, "Its bound hard-running streak is malformed.");
				return;
			}
			if (phase == KingdomWearPassPhase.Bound)
			{
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.StreakIntent);
				phase = KingdomWearPassPhase.StreakIntent;
			}
			if (phase == KingdomWearPassPhase.StreakIntent)
			{
				int current = Work.GetIntProperty(HardRunStreakProperty);
				if (current == beforeStreak) Work.SetIntProperty(HardRunStreakProperty, targetStreak);
				else if (current != targetStreak)
				{
					QuarantineWear(System, Work, "Its hard-running streak changed inside a bound pass.");
					return;
				}
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.StreakDone);
				phase = KingdomWearPassPhase.StreakDone;
			}
			if (phase == KingdomWearPassPhase.StreakDone)
			{
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.HardIncident);
				phase = KingdomWearPassPhase.HardIncident;
			}
			if (phase == KingdomWearPassPhase.HardIncident)
			{
				if (Work.GetIntProperty(SemanticPassHardRollProperty) == 1
					&& !ApplyDamageIncident(System, Work, KingdomWearRules.WearCause.HardRunning,
						WearEventId(Work, "hard", TimeTicks))) return;
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.HardDone);
				phase = KingdomWearPassPhase.HardDone;
			}
			if (phase == KingdomWearPassPhase.HardDone)
			{
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.TemperIncident);
				phase = KingdomWearPassPhase.TemperIncident;
			}
			if (phase == KingdomWearPassPhase.TemperIncident)
			{
				if (Work.GetIntProperty(SemanticPassTemperRollProperty) == 1
					&& !ApplyDamageIncident(System, Work, KingdomWearRules.WearCause.TemperamentalTech,
						WearEventId(Work, "temper", TimeTicks))) return;
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.TemperDone);
			}
			KingdomMaterials.WriteTick(Work, SemanticPassCompletedTickProperty, TimeTicks);
			Work.SetIntProperty(SemanticPassCompletedProperty, 1);
			Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.None);
			KingdomMaterials.WriteTick(Work, SemanticPassTickProperty, 0L);
		}

		/// <summary>
		/// Raiders who got past the wall may leave one or two works worse for it. Called from
		/// <c>KingdomRaids.ExecuteRaid</c> once for a raid that actually put raiders on the
		/// ground; does nothing for one the wall turned back outright, because nothing got past
		/// it to damage anything.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the raid landed in.</param>
		/// <param name="Survey">This pass's survey, so the candidate list is exactly the works
		/// already known to be crewed here. A fresh survey is taken when null.</param>
		/// <param name="RaidersThrough">Raiders who made it past the wall this raid.</param>
		/// <param name="RaidTick">The raid's own due tick, so a reload asks each candidate work
		/// this exact question exactly once.</param>
		public static void OnRaidDamage(KingdomSystem System, Zone Z, KingdomSurvey Survey, int RaidersThrough, long RaidTick)
		{
			if (!Enabled || System == null || Z == null || RaidersThrough <= 0 || RaidTick < 0L)
			{
				return;
			}
			KingdomSurvey survey = Survey ?? KingdomSurvey.Take(Z, System);
			if (survey.Works.Count == 0)
			{
				return;
			}
			int want = KingdomWearRules.WorksToDamage(RaidersThrough);
			if (want <= 0)
			{
				return;
			}
			string settlementId = KingdomChronicle.SettlementId(System);
			int hit = 0;
			for (int i = 0; i < survey.Works.Count && hit < want; i++)
			{
				GameObject work = survey.Works[i];
				if (!GameObject.Validate(work) || !KingdomWearRules.RollRaidDamage(settlementId, work.ID, RaidTick))
				{
					continue;
				}
				long lastRaid;
				if (!TryReadStrictTick(work, LastRaidIncidentTickProperty, out lastRaid)
					|| RaidTick < lastRaid)
				{
					QuarantineWear(System, work, "Its raid-damage receipt regressed or is malformed.");
					hit++;
					continue;
				}
				if (RaidTick > lastRaid || work.GetIntProperty("KingdomWearRaidTickSet") != 1)
				{
					if (!ApplyDamageIncident(System, work, KingdomWearRules.WearCause.Raid,
						WearEventId(work, "raid", RaidTick))) return;
					KingdomMaterials.WriteTick(work, LastRaidIncidentTickProperty, RaidTick);
					work.SetIntProperty("KingdomWearRaidTickSet", 1);
				}
				hit++;
			}
		}

		private static bool ApplyDamageIncident(KingdomSystem System, GameObject Work,
			KingdomWearRules.WearCause Cause, string IncidentId)
		{
			r_KingdomWear wear = Work.RequirePart<r_KingdomWear>();
			if ((KingdomWearIncidentPhase)wear.IncidentPhase == KingdomWearIncidentPhase.None
				&& HasActiveRepair(Work, out _)) return true;
			if (wear.LifecycleQuarantined)
			{
				TellWearQuarantine(System, Work, wear);
				return false;
			}
			KingdomWearIncidentPhase phase = (KingdomWearIncidentPhase)wear.IncidentPhase;
			if (phase == KingdomWearIncidentPhase.None)
			{
				if (string.Equals(wear.LastCompletedIncidentId, IncidentId,
					StringComparison.Ordinal)) return true;
				wear.IncidentId = IncidentId;
				wear.IncidentCause = (int)Cause;
				wear.IncidentBeforeWear = wear.Wear;
				wear.IncidentAfterWear = KingdomMaterialRules.AddWear(wear.Wear,
					KingdomWearRules.IncrementFor(Cause));
				wear.IncidentLine = KingdomWearRules.DamagedLine(DisplayName(Work), Cause,
					wear.IncidentAfterWear);
				wear.IncidentMessageState = (int)KingdomWearSinkDisposition.None;
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;
				phase = KingdomWearIncidentPhase.Bound;
			}
			else if (!string.Equals(wear.IncidentId, IncidentId, StringComparison.Ordinal)
				|| wear.IncidentCause != (int)Cause)
			{
				QuarantineWear(System, Work, "Two damage incidents claim the same work.");
				return false;
			}
			if (phase == KingdomWearIncidentPhase.Bound)
			{
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.MutationIntent;
				phase = KingdomWearIncidentPhase.MutationIntent;
			}
			if (phase == KingdomWearIncidentPhase.MutationIntent)
			{
				KingdomWearMutationAction action = KingdomWearRules.DamageMutationAction(phase,
					wear.IncidentBeforeWear, wear.Wear, wear.IncidentAfterWear);
				if (action == KingdomWearMutationAction.Apply)
				{
					wear.Wear = wear.IncidentAfterWear;
					wear.LastCause = (int)Cause;
				}
				else if (action == KingdomWearMutationAction.Confirm)
				{
					wear.LastCause = (int)Cause;
				}
				else
				{
					QuarantineWear(System, Work, "A damage incident no longer matches its exact before/after state.");
					return false;
				}
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Mutated;
				phase = KingdomWearIncidentPhase.Mutated;
			}
			if (wear.IncidentBeforeWear == wear.IncidentAfterWear)
			{
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Complete;
				phase = KingdomWearIncidentPhase.Complete;
			}
			if (phase == KingdomWearIncidentPhase.Mutated)
			{
				if (!KingdomChronicle.RecordOnce(System, wear.IncidentId + ":chronicle",
					wear.IncidentLine)) return false;
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.ChronicleDone;
				phase = KingdomWearIncidentPhase.ChronicleDone;
			}
			if (phase == KingdomWearIncidentPhase.ChronicleDone)
			{
				if (wear.IncidentMessageState == (int)KingdomWearSinkDisposition.None)
					wear.IncidentMessageState = (int)KingdomWearSinkDisposition.Pending;
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.MessageIntent;
				DeliverWearMessage(ref wear.IncidentMessageState,
					"{{r|" + wear.IncidentLine + "}}");
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.MessageDone;
				phase = KingdomWearIncidentPhase.MessageDone;
			}
			else if (phase == KingdomWearIncidentPhase.MessageIntent)
			{
				if (wear.IncidentMessageState == (int)KingdomWearSinkDisposition.None)
					wear.IncidentMessageState = (int)KingdomWearSinkDisposition.Attempting;
				wear.IncidentMessageState = (int)KingdomWearRules.RecoverUninspectable(
					(KingdomWearSinkDisposition)wear.IncidentMessageState);
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.MessageDone;
				phase = KingdomWearIncidentPhase.MessageDone;
			}
			if (phase == KingdomWearIncidentPhase.MessageDone)
			{
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Complete;
				phase = KingdomWearIncidentPhase.Complete;
				KingdomLog.Log("wear: damaged " + Work.Blueprint + " cause=" + Cause
					+ " wear=" + wear.Wear + " incident=" + IncidentId);
			}
			if (phase != KingdomWearIncidentPhase.Complete) return false;
			wear.LastCompletedIncidentId = IncidentId;
			wear.IncidentPhase = (int)KingdomWearIncidentPhase.None;
			wear.IncidentId = null;
			wear.IncidentLine = null;
			return true;
		}

		// ==================================================================================
		// The kind-appropriate consequence (Addendum 10(b)): a damaged STORE loses what it holds.
		//
		// The clock is the P1 substrate and nothing else: KingdomRules.ElapsedDays over a stamp
		// that lives on the work's own part, planted on the first pass that looks at it and never
		// counted from zero. Days that produced no loss are BANKED rather than spent, so a small
		// store whose daily share rounds to nothing still empties honestly over a season, and a
		// founder cannot stop a leak by stepping in and out of the zone. Loss, not transfer: this
		// is water going into the ground, not the manifest's pour-on-ground surplus.
		// ==================================================================================

		private static void Leak(KingdomSystem System, KingdomSurvey Survey, GameObject Work, r_KingdomWear Wear, long TimeTicks)
		{
			if (Wear.LifecycleQuarantined)
			{
				TellWearQuarantine(System, Work, Wear);
				return;
			}
			if ((KingdomWearLeakPhase)Wear.LeakPhase != KingdomWearLeakPhase.None)
			{
				ContinueBoundLeak(System, Survey, Work, Wear);
				return;
			}
			if (Work.GetIntProperty(StoresProperty) == 1)
			{
				LiquidVolume vessel = Work.GetPart<LiquidVolume>();
				if (vessel != null && vessel.MaxVolume > 0)
				{
					LeakWater(System, Survey, Work, Wear, vessel, TimeTicks);
				}
				return;
			}
			// The third kind, and the one Addendum 10(b) deferred until food was a flow: a
			// holed granary lets the damp in and the harvest goes over. Same clock, same
			// day-banking, same announce-once, and the same loss-not-transfer reading - this
			// food rots where it stands and is not a pile somebody can walk up to.
			if (Work.GetIntProperty(LarderProperty) == 1)
			{
				if (Work.Inventory != null)
				{
					SpoilFood(System, Survey, Work, Wear, TimeTicks);
				}
				return;
			}
			if (Work.GetPart<r_KingdomPowerStore>() != null)
			{
				Capacitor bed = Work.GetPart<Capacitor>();
				if (bed != null && bed.MaxCharge > 0)
				{
					LeakCharge(System, Work, Wear, bed, TimeTicks);
				}
			}
		}

		private static void SpoilFood(KingdomSystem System, KingdomSurvey Survey, GameObject Work, r_KingdomWear Wear, long TimeTicks)
		{
			int days;
			long checkpoint;
			if (!TryLeakWindow(System, Work, Wear, TimeTicks, out days, out checkpoint)) return;
			int held = KingdomSurvey.HeldIn(Work);
			int wanted = KingdomWearRules.Leaked(KingdomSurvey.CapacityOf(Work), held, Wear.Wear, days);
			if (wanted <= 0)
			{
				if (held <= 0) Wear.LastLeakTick = checkpoint;
				return;
			}
			string ids;
			string originals;
			string allocations;
			if (!TryFoodPlan(Work, wanted, out ids, out originals, out allocations))
			{
				QuarantineWear(System, Work, "Its spoilage could not bind exact food identities.");
				return;
			}
			BindLeak(Work, Wear, KingdomWearRules.LeakKind.Food, Wear.LastLeakTick,
				checkpoint, held, held - wanted, wanted, KingdomSurvey.CapacityOf(Work),
				ids, originals, allocations);
			ContinueBoundLeak(System, Survey, Work, Wear);
		}

		private static void LeakWater(KingdomSystem System, KingdomSurvey Survey, GameObject Work, r_KingdomWear Wear,
			LiquidVolume Vessel, long TimeTicks)
		{
			int days;
			long checkpoint;
			if (!TryLeakWindow(System, Work, Wear, TimeTicks, out days, out checkpoint)) return;
			int wanted = KingdomWearRules.Leaked(Vessel.MaxVolume, Vessel.Volume, Wear.Wear, days);
			if (wanted <= 0)
			{
				if (Vessel.Volume <= 0) Wear.LastLeakTick = checkpoint;
				return;
			}
			BindLeak(Work, Wear, KingdomWearRules.LeakKind.Water, Wear.LastLeakTick,
				checkpoint, Vessel.Volume, Vessel.Volume - wanted, wanted, Vessel.MaxVolume,
				null, null, null);
			ContinueBoundLeak(System, Survey, Work, Wear);
		}

		private static void LeakCharge(KingdomSystem System, GameObject Work, r_KingdomWear Wear, Capacitor Bed, long TimeTicks)
		{
			int days;
			long checkpoint;
			if (!TryLeakWindow(System, Work, Wear, TimeTicks, out days, out checkpoint)) return;
			int wanted = KingdomWearRules.Leaked(Bed.MaxCharge, Bed.Charge, Wear.Wear, days);
			if (wanted <= 0)
			{
				if (Bed.Charge <= 0) Wear.LastLeakTick = checkpoint;
				return;
			}
			BindLeak(Work, Wear, KingdomWearRules.LeakKind.Charge, Wear.LastLeakTick,
				checkpoint, Bed.Charge, Bed.Charge - wanted, wanted, Bed.MaxCharge,
				null, null, null);
			ContinueBoundLeak(System, null, Work, Wear);
		}

		private static bool TryLeakWindow(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear, long TimeTicks, out int Days, out long Checkpoint)
		{
			Days = 0;
			Checkpoint = Wear.LastLeakTick;
			int elapsed = (TimeTicks >= Wear.LastLeakTick && Wear.LastLeakTick >= 0L)
				? KingdomRules.ElapsedDays(TimeTicks - Wear.LastLeakTick) : 0;
			KingdomWearClockAction action = KingdomWearRules.LeakClockAction(
				Wear.LeakClockInitialized, Wear.LastLeakTick, TimeTicks, elapsed);
			if (action == KingdomWearClockAction.Quarantine)
			{
				QuarantineWear(System, Work, TimeTicks < Wear.LastLeakTick
					? "Its storage-loss clock regressed." : "Its storage-loss clock is malformed.");
				return false;
			}
			if (action == KingdomWearClockAction.Plant)
			{
				Wear.LastLeakTick = TimeTicks;
				Wear.LeakClockInitialized = true;
				Checkpoint = TimeTicks;
				return false;
			}
			Days = elapsed;
			if (action == KingdomWearClockAction.Wait) return false;
			Checkpoint = KingdomRules.AdvanceCheckpoint(Wear.LastLeakTick, TimeTicks);
			return Checkpoint >= Wear.LastLeakTick;
		}

		private static void BindLeak(GameObject Work, r_KingdomWear Wear,
			KingdomWearRules.LeakKind Kind, long FromTick, long ToTick,
			int Before, int After, int Wanted, int Capacity, string ItemIds,
			string ItemOriginals, string ItemAllocations)
		{
			Wear.LeakIncidentId = WearEventId(Work, "leak-" + (int)Kind, ToTick);
			Wear.LeakKind = (int)Kind;
			Wear.LeakFromTick = FromTick;
			Wear.LeakToTick = ToTick;
			Wear.LeakBefore = Before;
			Wear.LeakAfter = After;
			Wear.LeakWanted = Wanted;
			Wear.LeakActualLost = 0;
			Wear.LeakOwnerId = Work.ID;
			Wear.LeakZoneId = Work.CurrentZone?.ZoneID;
			Wear.LeakCellX = (Work.CurrentCell == null) ? -1 : Work.CurrentCell.X;
			Wear.LeakCellY = (Work.CurrentCell == null) ? -1 : Work.CurrentCell.Y;
			Wear.LeakCapacity = Capacity;
			Wear.LeakLine = KingdomWearRules.LeakBegunLine(DisplayName(Work), Kind);
			Wear.LeakItemIds = ItemIds;
			Wear.LeakItemOriginalCounts = ItemOriginals;
			Wear.LeakItemAllocations = ItemAllocations;
			Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.None;
			Wear.LeakMessageState = (int)KingdomWearSinkDisposition.None;
			Wear.LeakPhase = (int)KingdomWearLeakPhase.Bound;
		}

		private sealed class LeakWorkFrame
		{
			internal GameObject Work;
			internal string WorkId;
			internal Zone Zone;
			internal Cell Cell;
			internal r_KingdomWear WearPart;
			internal int Wear;
			internal int LastCause;
			internal bool Held;
			internal int RepairEffort;
			internal long LastLeakTick;
			internal bool LeakClockInitialized;
			internal bool LeakAnnounced;
			internal string IncidentId;
			internal int LeakKind;
			internal long FromTick;
			internal long ToTick;
			internal int Before;
			internal int After;
			internal int Wanted;
			internal int Capacity;
			internal string OwnerId;
			internal string ZoneId;
			internal string ItemIds;
			internal string ItemOriginals;
			internal string ItemAllocations;
			internal LiquidVolume Vessel;
			internal Capacitor Bed;
			internal Inventory Inventory;
			internal r_KingdomPowerStore PowerStore;
			internal int StoresMark;
			internal int LarderMark;
		}

		private static bool TryCaptureLeakWork(GameObject Work, r_KingdomWear Wear,
			out LeakWorkFrame Frame)
		{
			Frame = null;
			if (!GameObject.Validate(Work) || Wear == null || Work.CurrentZone == null
				|| Work.CurrentCell == null || Work.CurrentCell.ParentZone != Work.CurrentZone
				|| Wear.ParentObject != Work || !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				|| Work.ID != Wear.LeakOwnerId || Work.CurrentZone.ZoneID != Wear.LeakZoneId
				|| Work.CurrentCell.X != Wear.LeakCellX || Work.CurrentCell.Y != Wear.LeakCellY) return false;
			Frame = new LeakWorkFrame
			{
				Work = Work,
				WorkId = Work.ID,
				Zone = Work.CurrentZone,
				Cell = Work.CurrentCell,
				WearPart = Wear,
				Wear = Wear.Wear,
				LastCause = Wear.LastCause,
				Held = Wear.Held,
				RepairEffort = Wear.RepairEffortLeft,
				LastLeakTick = Wear.LastLeakTick,
				LeakClockInitialized = Wear.LeakClockInitialized,
				LeakAnnounced = Wear.LeakAnnounced,
				IncidentId = Wear.LeakIncidentId,
				LeakKind = Wear.LeakKind,
				FromTick = Wear.LeakFromTick,
				ToTick = Wear.LeakToTick,
				Before = Wear.LeakBefore,
				After = Wear.LeakAfter,
				Wanted = Wear.LeakWanted,
				Capacity = Wear.LeakCapacity,
				OwnerId = Wear.LeakOwnerId,
				ZoneId = Wear.LeakZoneId,
				ItemIds = Wear.LeakItemIds,
				ItemOriginals = Wear.LeakItemOriginalCounts,
				ItemAllocations = Wear.LeakItemAllocations,
				Vessel = Work.GetPart<LiquidVolume>(),
				Bed = Work.GetPart<Capacitor>(),
				Inventory = Work.Inventory,
				PowerStore = Work.GetPart<r_KingdomPowerStore>(),
				StoresMark = Work.GetIntProperty(StoresProperty),
				LarderMark = Work.GetIntProperty(LarderProperty)
			};
			return true;
		}

		private static bool LeakWorkExact(LeakWorkFrame Frame,
			KingdomWearLeakPhase ExpectedPhase)
		{
			if (Frame == null || !GameObject.Validate(Frame.Work) || Frame.Work.ID != Frame.WorkId
				|| Frame.Work.CurrentZone != Frame.Zone || Frame.Work.CurrentCell != Frame.Cell
				|| Frame.Cell == null || Frame.Cell.ParentZone != Frame.Zone
				|| Frame.WearPart == null || Frame.WearPart.ParentObject != Frame.Work
				|| !ReferenceEquals(Frame.Work.GetPart<r_KingdomWear>(), Frame.WearPart)
				|| Frame.WearPart.Wear != Frame.Wear || Frame.WearPart.LastCause != Frame.LastCause
				|| Frame.WearPart.Held != Frame.Held
				|| Frame.WearPart.RepairEffortLeft != Frame.RepairEffort
				|| Frame.WearPart.LastLeakTick != Frame.LastLeakTick
				|| Frame.WearPart.LeakClockInitialized != Frame.LeakClockInitialized
				|| Frame.WearPart.LeakAnnounced != Frame.LeakAnnounced
				|| Frame.WearPart.LeakIncidentId != Frame.IncidentId
				|| Frame.WearPart.LeakKind != Frame.LeakKind
				|| Frame.WearPart.LeakFromTick != Frame.FromTick
				|| Frame.WearPart.LeakToTick != Frame.ToTick
				|| Frame.WearPart.LeakBefore != Frame.Before
				|| Frame.WearPart.LeakAfter != Frame.After
				|| Frame.WearPart.LeakWanted != Frame.Wanted
				|| Frame.WearPart.LeakCapacity != Frame.Capacity
				|| Frame.WearPart.LeakOwnerId != Frame.OwnerId
				|| Frame.WearPart.LeakZoneId != Frame.ZoneId
				|| Frame.WearPart.LeakItemIds != Frame.ItemIds
				|| Frame.WearPart.LeakItemOriginalCounts != Frame.ItemOriginals
				|| Frame.WearPart.LeakItemAllocations != Frame.ItemAllocations
				|| (KingdomWearLeakPhase)Frame.WearPart.LeakPhase != ExpectedPhase
				|| !ReferenceEquals(Frame.Work.GetPart<LiquidVolume>(), Frame.Vessel)
				|| !ReferenceEquals(Frame.Work.GetPart<Capacitor>(), Frame.Bed)
				|| !ReferenceEquals(Frame.Work.Inventory, Frame.Inventory)
				|| !ReferenceEquals(Frame.Work.GetPart<r_KingdomPowerStore>(), Frame.PowerStore)
				|| Frame.Work.GetIntProperty(StoresProperty) != Frame.StoresMark
				|| Frame.Work.GetIntProperty(LarderProperty) != Frame.LarderMark) return false;
			return true;
		}

		private static void ContinueBoundLeak(KingdomSystem System, KingdomSurvey Survey,
			GameObject Work, r_KingdomWear Wear)
		{
			KingdomWearLeakPhase phase = (KingdomWearLeakPhase)Wear.LeakPhase;
			if (phase == KingdomWearLeakPhase.Quarantined)
			{
				TellWearQuarantine(System, Work, Wear);
				return;
			}
			if (phase == KingdomWearLeakPhase.None) return;
			if (phase == KingdomWearLeakPhase.MutationIntent)
			{
				QuarantineLeak(System, Work, Wear, 0,
					"A storage-loss callback was interrupted; its mutation was not inspected, credited, or repeated.");
				return;
			}
			if (phase >= KingdomWearLeakPhase.Mutated)
			{
				ContinueLeakOutputs(System, Work, Wear);
				return;
			}
			if (!GameObject.Validate(Work) || Wear == null || Wear.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				|| !string.Equals(Wear.LeakOwnerId, Work.ID, StringComparison.Ordinal)
				|| !string.Equals(Wear.LeakZoneId, Work.CurrentZone?.ZoneID,
					StringComparison.Ordinal) || Work.CurrentCell == null
				|| Work.CurrentCell.X != Wear.LeakCellX || Work.CurrentCell.Y != Wear.LeakCellY)
			{
				QuarantineLeak(System, Work, Wear, 0,
					"Its bound storage work changed identity or zone.");
				return;
			}
			int current;
			bool exactFood;
			int foodProved;
			LiquidVolume boundVessel = null;
			Capacitor boundBed = null;
			if ((KingdomWearRules.LeakKind)Wear.LeakKind == KingdomWearRules.LeakKind.Water)
			{
				boundVessel = Work.GetPart<LiquidVolume>();
				if (boundVessel == null || boundVessel.ParentObject != Work
					|| Work.GetIntProperty(StoresProperty) != 1
					|| boundVessel.MaxVolume != Wear.LeakCapacity
					|| !(boundVessel.Volume == 0 || boundVessel.IsFreshWater()))
				{
					QuarantineLeak(System, Work, Wear, 0,
						"Its bound water vessel changed identity or contents.");
					return;
				}
				current = boundVessel.Volume;
				exactFood = true;
				foodProved = (current == Wear.LeakAfter) ? Wear.LeakWanted : 0;
			}
			else if ((KingdomWearRules.LeakKind)Wear.LeakKind == KingdomWearRules.LeakKind.Charge)
			{
				boundBed = Work.GetPart<Capacitor>();
				if (boundBed == null || boundBed.ParentObject != Work
					|| boundBed.MaxCharge != Wear.LeakCapacity
					|| Work.GetPart<r_KingdomPowerStore>() == null)
				{
					QuarantineLeak(System, Work, Wear, 0,
						"Its bound charge bed changed identity.");
					return;
				}
				current = boundBed.Charge;
				exactFood = true;
				foodProved = (current == Wear.LeakAfter) ? Wear.LeakWanted : 0;
			}
			else
			{
				if (Work.GetIntProperty(LarderProperty) != 1
					|| KingdomSurvey.CapacityOf(Work) != Wear.LeakCapacity)
				{
					QuarantineLeak(System, Work, Wear, 0,
						"Its bound larder dedication or capacity changed.");
					return;
				}
				if (!ObserveFoodPlan(Work, Wear, out current, out exactFood, out foodProved))
				{
					QuarantineLeak(System, Work, Wear, 0,
						"Its bound food identities no longer have one exact location and count.");
					return;
				}
			}
			KingdomWearMutationAction action = KingdomWearRules.LeakMutationAction(phase,
				Wear.LeakBefore, current, Wear.LeakAfter);
			if (!exactFood)
			{
				QuarantineLeak(System, Work, Wear, 0,
					"Only part of a storage-loss incident can be proved.");
				return;
			}
			if (action == KingdomWearMutationAction.Apply)
			{
				LeakWorkFrame frame;
				if (!TryCaptureLeakWork(Work, Wear, out frame))
				{
					QuarantineLeak(System, Work, Wear, 0,
						"The storage-loss live frame could not capture its exact work, wear part, cell, zone, and storage parts.");
					return;
				}
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MutationIntent;
				if ((KingdomWearRules.LeakKind)Wear.LeakKind == KingdomWearRules.LeakKind.Water)
				{
					if (Survey == null || !Survey.Stores.Contains(boundVessel))
					{
						QuarantineLeak(System, Work, Wear, 0, "Its water survey is absent.");
						return;
					}
					int removed;
					bool exact = Survey.TryLeakFromExact(boundVessel, Wear.LeakWanted, out removed);
					if (!exact || removed != frame.Wanted
						|| !LeakWorkExact(frame, KingdomWearLeakPhase.MutationIntent)
						|| boundVessel.Volume != frame.After)
					{
						QuarantineLeak(System, Work, Wear, 0,
							"The water-loss callback changed an exact work, wear, owner, vessel, dictionary, survey-list, cell, zone, counter, capacity, or delta witness.");
						return;
					}
				}
				else if ((KingdomWearRules.LeakKind)Wear.LeakKind == KingdomWearRules.LeakKind.Charge)
				{
					boundBed.UseCharge(Wear.LeakWanted);
					bool stillExact = LeakWorkExact(frame, KingdomWearLeakPhase.MutationIntent)
						&& ReferenceEquals(Work.GetPart<Capacitor>(), boundBed)
						&& boundBed.ParentObject == Work
						&& boundBed.MaxCharge == Wear.LeakCapacity
						&& boundBed.Charge == Wear.LeakAfter;
					if (!stillExact)
					{
						QuarantineLeak(System, Work, Wear, 0,
							"The charge-loss mutation did not leave its exact bound bed and delta.");
						return;
					}
				}
				else
				{
					string ids;
					string originals;
					string allocations;
					if (Survey == null || !TryFoodPlan(Work, Wear.LeakWanted, out ids,
							out originals, out allocations)
						|| ids != Wear.LeakItemIds || originals != Wear.LeakItemOriginalCounts
						|| allocations != Wear.LeakItemAllocations)
					{
						QuarantineLeak(System, Work, Wear, 0,
							"Its bound food plan changed before spoilage began.");
						return;
					}
					int spoiled;
					bool complete = Survey.TrySpoilFromExact(Work, Wear.LeakWanted, out spoiled);
					if (!LeakWorkExact(frame, KingdomWearLeakPhase.MutationIntent))
					{
						QuarantineLeak(System, Work, Wear, 0,
							"A spoilage callback changed the bound work, wear part, storage parts, cell, zone, or receipt.");
						return;
					}
					if (!complete || spoiled != frame.Wanted
						|| KingdomSurvey.HeldIn(Work) != frame.After)
					{
						QuarantineLeak(System, Work, Wear, spoiled,
							"A spoilage callback was vetoed or changed an exact work, wear, Inventory/list, item, owner, count, cell, zone, survey-counter, or full-topology witness.");
						return;
					}
				}
				Wear.LeakActualLost = Wear.LeakWanted;
				Wear.LastLeakTick = Wear.LeakToTick;
				Wear.LeakClockInitialized = true;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.Mutated;
				ContinueLeakOutputs(System, Work, Wear);
				return;
			}
			QuarantineLeak(System, Work, Wear, 0,
				"A bound storage-loss receipt changed before its one live callback frame began.");
		}

		private static void ContinueLeakOutputs(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear)
		{
			if (Wear == null || !GameObject.Validate(Work) || Wear.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				|| !string.Equals(Wear.LeakOwnerId, Work.ID, StringComparison.Ordinal)
				|| Work.CurrentZone == null || Work.CurrentCell == null
				|| Work.CurrentCell.ParentZone != Work.CurrentZone
				|| !string.Equals(Wear.LeakZoneId, Work.CurrentZone.ZoneID,
					StringComparison.Ordinal)
				|| Work.CurrentCell.X != Wear.LeakCellX || Work.CurrentCell.Y != Wear.LeakCellY)
			{
				if (Wear != null)
				{
					Wear.LifecycleQuarantined = true;
					Wear.QuarantineReason =
						"A completed storage loss is no longer bound to its exact work, wear part, cell, and zone.";
					Wear.LeakPhase = (int)KingdomWearLeakPhase.Quarantined;
				}
				return;
			}
			KingdomWearLeakPhase phase = (KingdomWearLeakPhase)Wear.LeakPhase;
			if (Wear.LeakAnnounced && phase == KingdomWearLeakPhase.Mutated)
			{
				Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Skipped;
				Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Skipped;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.Complete;
				phase = KingdomWearLeakPhase.Complete;
			}
			if (phase == KingdomWearLeakPhase.Mutated)
			{
				if (!KingdomChronicle.RecordOnce(System, Wear.LeakIncidentId + ":chronicle",
					Wear.LeakLine)) return;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.ChronicleDone;
				phase = KingdomWearLeakPhase.ChronicleDone;
			}
			if (phase == KingdomWearLeakPhase.ChronicleDone)
			{
				if (Wear.LeakLedgerState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Pending;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerIntent;
				DeliverWearLedger(System, ref Wear.LeakLedgerState,
					"{{r|" + XRL.Language.Grammar.InitCap(Wear.LeakLine) + "}}");
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerDone;
				phase = KingdomWearLeakPhase.LedgerDone;
			}
			else if (phase == KingdomWearLeakPhase.LedgerIntent)
			{
				if (Wear.LeakLedgerState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Attempting;
				Wear.LeakLedgerState = (int)KingdomWearRules.RecoverUninspectable(
					(KingdomWearSinkDisposition)Wear.LeakLedgerState);
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerDone;
				phase = KingdomWearLeakPhase.LedgerDone;
			}
			if (phase == KingdomWearLeakPhase.LedgerDone)
			{
				if (Wear.LeakMessageState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Pending;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageIntent;
				DeliverWearMessage(ref Wear.LeakMessageState,
					"{{r|" + XRL.Language.Grammar.InitCap(Wear.LeakLine) + "}}");
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageDone;
				phase = KingdomWearLeakPhase.MessageDone;
			}
			else if (phase == KingdomWearLeakPhase.MessageIntent)
			{
				if (Wear.LeakMessageState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Attempting;
				Wear.LeakMessageState = (int)KingdomWearRules.RecoverUninspectable(
					(KingdomWearSinkDisposition)Wear.LeakMessageState);
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageDone;
				phase = KingdomWearLeakPhase.MessageDone;
			}
			if (phase == KingdomWearLeakPhase.MessageDone)
			{
				Wear.LeakAnnounced = true;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.Complete;
				phase = KingdomWearLeakPhase.Complete;
				KingdomLog.Log("wear: leak " + Work.Blueprint + " kind=" + Wear.LeakKind
					+ " lost=" + Wear.LeakActualLost + " incident=" + Wear.LeakIncidentId);
			}
			if (phase == KingdomWearLeakPhase.Complete) ClearLeakReceipt(Wear);
		}

		private static bool TryFoodPlan(GameObject Work, int Wanted, out string Ids,
			out string Originals, out string Allocations)
		{
			Ids = Originals = Allocations = null;
			if (!GameObject.Validate(Work) || Work.Inventory == null || Wanted <= 0) return false;
			List<string> ids = new List<string>();
			List<int> originals = new List<int>();
			List<int> allocations = new List<int>();
			List<GameObject> seen = new List<GameObject>();
			int remaining = Wanted;
			for (int i = 0; i < Work.Inventory.Objects.Count && remaining > 0; i++)
			{
				GameObject food = Work.Inventory.Objects[i];
				bool duplicate = false;
				for (int j = 0; j < seen.Count; j++)
				{
					if (ReferenceEquals(seen[j], food)) duplicate = true;
				}
				if (duplicate || !GameObject.Validate(food) || food.InInventory != Work
					|| (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient")))
				{
					continue;
				}
				if (string.IsNullOrEmpty(food.ID) || food.ID.IndexOf('|') >= 0 || food.Count <= 0)
				{
					return false;
				}
				int take = (food.Count < remaining) ? food.Count : remaining;
				if (food.ID.Length > KingdomWearRules.MaxObjectIdChars
					|| ids.Count >= KingdomWearRules.MaxRows) return false;
				seen.Add(food);
				ids.Add(food.ID);
				originals.Add(food.Count);
				allocations.Add(take);
				remaining -= take;
			}
			if (remaining != 0 || ids.Count == 0) return false;
			Ids = string.Join("|", ids.ToArray());
			Originals = JoinWearInts(originals);
			Allocations = JoinWearInts(allocations);
			return Ids.Length <= KingdomWearRules.MaxRowsChars
				&& Originals.Length <= KingdomWearRules.MaxRowsChars
				&& Allocations.Length <= KingdomWearRules.MaxRowsChars;
		}

		private static bool ObserveFoodPlan(GameObject Work, r_KingdomWear Wear,
			out int Current, out bool Exact, out int Proved)
		{
			Current = 0;
			Exact = false;
			Proved = 0;
			string[] ids;
			if (!GameObject.Validate(Work) || Work.Inventory == null
				|| !KingdomWearRules.TryObjectIdRows(Wear.LeakItemIds, out ids)) return false;
			int[] originals;
			int[] allocations;
			if (!TryWearInts(Wear.LeakItemOriginalCounts, out originals)
				|| !TryWearInts(Wear.LeakItemAllocations, out allocations)
				|| ids.Length == 0 || ids.Length != originals.Length
				|| ids.Length != allocations.Length) return false;
			bool allOriginal = true;
			bool allAfter = true;
			for (int i = 0; i < ids.Length; i++)
			{
				if (string.IsNullOrEmpty(ids[i]) || originals[i] <= 0 || allocations[i] <= 0
					|| allocations[i] > originals[i]) return false;
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(ids[j], ids[i], StringComparison.Ordinal)) return false;
				}
				GameObject food = GameObject.FindByID(ids[i]);
				int rowCurrent;
				if (!GameObject.Validate(food))
				{
					rowCurrent = 0;
				}
				else
				{
					if (food.InInventory != Work || !Work.Inventory.Objects.Contains(food)
						|| (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
						|| food.Count < 0) return false;
					rowCurrent = food.Count;
				}
				int intended = originals[i] - allocations[i];
				if (rowCurrent != originals[i]) allOriginal = false;
				if (rowCurrent != intended) allAfter = false;
				if (rowCurrent < intended || rowCurrent > originals[i]) return false;
				Proved += originals[i] - rowCurrent;
			}
			Current = KingdomSurvey.HeldIn(Work);
			Exact = (allOriginal && Current == Wear.LeakBefore)
				|| (allAfter && Current == Wear.LeakAfter && Proved == Wear.LeakWanted);
			return true;
		}

		private static string JoinWearInts(List<int> Values)
		{
			string[] rows = new string[Values.Count];
			for (int i = 0; i < Values.Count; i++)
			{
				rows[i] = Values[i].ToString(global::System.Globalization.CultureInfo.InvariantCulture);
			}
			return string.Join("|", rows);
		}

		private static bool TryWearInts(string Text, out int[] Values)
		{
			return KingdomWearRules.TryCanonicalIntRows(Text, out Values);
		}

		private static bool TryReadStrictTick(GameObject Work, string Property, out long Tick)
		{
			Tick = 0L;
			if (!GameObject.Validate(Work) || string.IsNullOrEmpty(Property)) return false;
			string text = Work.GetStringProperty(Property);
			if (string.IsNullOrEmpty(text)) return true;
			if (text.Length > 20) return false;
			return long.TryParse(text, global::System.Globalization.NumberStyles.None,
				global::System.Globalization.CultureInfo.InvariantCulture, out Tick)
				&& Tick >= 0L && Tick.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture) == text;
		}

		private static string WearEventId(GameObject Work, string Kind, long Tick)
		{
			return KingdomWearRules.WorkStream(Work?.ID) + ":event:" + Kind + ":"
				+ Tick.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
		}

		private static void QuarantineLeak(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear, int Proved, string Reason)
		{
			if (Wear == null) return;
			Wear.LeakActualLost = (Proved > Wear.LeakWanted) ? Wear.LeakWanted
				: ((Proved > 0) ? Proved : 0);
			if (Wear.LeakActualLost > 0 && Wear.LeakToTick >= Wear.LastLeakTick)
			{
				Wear.LastLeakTick = Wear.LeakToTick;
				Wear.LeakClockInitialized = true;
			}
			Wear.LeakPhase = (int)KingdomWearLeakPhase.Quarantined;
			Wear.LifecycleQuarantined = true;
			Wear.QuarantineReason = string.IsNullOrEmpty(Reason)
				? "Its storage-loss receipt is physically ambiguous." : Reason;
			if (GameObject.Validate(Work) && Wear.ParentObject == Work
				&& ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear))
			{
				Work.SetIntProperty(SemanticPassPhaseProperty,
					(int)KingdomWearPassPhase.Quarantined);
				TellWearQuarantine(System, Work, Wear);
			}
		}

		private static void ClearLeakReceipt(r_KingdomWear Wear)
		{
			Wear.LeakIncidentId = null;
			Wear.LeakPhase = (int)KingdomWearLeakPhase.None;
			Wear.LeakKind = 0;
			Wear.LeakFromTick = 0L;
			Wear.LeakToTick = 0L;
			Wear.LeakBefore = 0;
			Wear.LeakAfter = 0;
			Wear.LeakWanted = 0;
			Wear.LeakActualLost = 0;
			Wear.LeakOwnerId = null;
			Wear.LeakZoneId = null;
			Wear.LeakCellX = 0;
			Wear.LeakCellY = 0;
			Wear.LeakCapacity = 0;
			Wear.LeakLine = null;
			Wear.LeakItemIds = null;
			Wear.LeakItemOriginalCounts = null;
			Wear.LeakItemAllocations = null;
		}

		private static void QuarantineWear(KingdomSystem System, GameObject Work, string Reason)
		{
			if (!GameObject.Validate(Work)) return;
			r_KingdomWear wear = Work.GetPart<r_KingdomWear>();
			if (wear == null || wear.ParentObject != Work) return;
			wear.LifecycleQuarantined = true;
			wear.QuarantineReason = string.IsNullOrEmpty(Reason)
				? "Its wear receipt is physically ambiguous." : Reason;
			if ((KingdomWearIncidentPhase)wear.IncidentPhase != KingdomWearIncidentPhase.None)
			{
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Quarantined;
			}
			if ((KingdomWearLeakPhase)wear.LeakPhase != KingdomWearLeakPhase.None)
			{
				wear.LeakPhase = (int)KingdomWearLeakPhase.Quarantined;
			}
			Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.Quarantined);
			TellWearQuarantine(System, Work, wear);
		}

		private static void TellWearQuarantine(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear)
		{
			if (Wear == null) return;
			if (Wear.QuarantineTold)
			{
				if (Wear.QuarantineLedgerState == (int)KingdomWearSinkDisposition.None)
					Wear.QuarantineLedgerState = (int)KingdomWearSinkDisposition.Skipped;
				if (Wear.QuarantineMessageState == (int)KingdomWearSinkDisposition.None)
					Wear.QuarantineMessageState = (int)KingdomWearSinkDisposition.Skipped;
				return;
			}
			string name = GameObject.Validate(Work) ? DisplayName(Work) : "A damaged work";
			string line = name + " has an uncertain wear receipt and is quarantined; "
				+ (Wear.QuarantineReason ?? "no physical mutation will be guessed through it.");
			string eventId = WearEventId(Work, "quarantine", 0L);
			if (!KingdomChronicle.RecordOnce(System, eventId, line)) return;
			DeliverWearLedger(System, ref Wear.QuarantineLedgerState,
				"{{r|" + XRL.Language.Grammar.InitCap(line) + "}}");
			DeliverWearMessage(ref Wear.QuarantineMessageState,
				"{{r|" + XRL.Language.Grammar.InitCap(line) + "}}");
			Wear.QuarantineTold = KingdomWearRules.SinkSettled(
				(KingdomWearSinkDisposition)Wear.QuarantineLedgerState)
				&& KingdomWearRules.SinkSettled(
					(KingdomWearSinkDisposition)Wear.QuarantineMessageState);
		}

		private static bool DeliverWearMessage(ref int RawState, string Line)
		{
			KingdomWearSinkDisposition state = KingdomWearRules.RecoverUninspectable(
				(KingdomWearSinkDisposition)RawState);
			RawState = (int)state;
			if (KingdomWearRules.SinkSettled(state)) return true;
			if (string.IsNullOrEmpty(Line))
			{
				RawState = (int)KingdomWearSinkDisposition.Skipped;
				return true;
			}
			RawState = (int)KingdomWearSinkDisposition.Attempting;
			MessageQueue.AddPlayerMessage(Line);
			RawState = (int)KingdomWearSinkDisposition.Delivered;
			return true;
		}

		private static bool DeliverWearLedger(KingdomSystem System, ref int RawState,
			string Line)
		{
			KingdomWearSinkDisposition state = KingdomWearRules.RecoverUninspectable(
				(KingdomWearSinkDisposition)RawState);
			RawState = (int)state;
			if (KingdomWearRules.SinkSettled(state)) return true;
			if (System == null || string.IsNullOrEmpty(Line))
			{
				RawState = (int)KingdomWearSinkDisposition.Skipped;
				return true;
			}
			RawState = (int)KingdomWearSinkDisposition.Attempting;
			System.Ledger.Note(Line);
			RawState = (int)KingdomWearSinkDisposition.Delivered;
			return true;
		}

		// ==================================================================================
		// Repair: costed and timed exactly as a strike is (KingdomMaterials.WorkStrike), one job
		// settlement-wide at a time.
		// ==================================================================================

		private static KingdomWearRules.RepairVerdict Assess(Zone Z, GameObject Work, r_KingdomWear WearPart, int FreeHands)
		{
			if (WearPart.Held)
			{
				return KingdomWearRules.RepairVerdict.Held;
			}
			bool covered = Covers(Z, Work, WearPart.Wear);
			return KingdomWearRules.AssessRepair(WearPart.Held, FreeHands, covered);
		}

		private static bool Covers(Zone Z, GameObject Work, int Wear)
		{
			BuildTallies(Work, Wear, out KingdomMaterialTally cost, out KingdomBitTally bitCost);
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			return KingdomMaterialRules.Covers(stock.Tally, cost) && KingdomMaterialRules.CoversBits(stock.Bits, bitCost);
		}

		private static void BuildTallies(GameObject Work, int Wear, out KingdomMaterialTally Cost, out KingdomBitTally BitCost)
		{
			string designKey = KingdomUpgrade.DesignKeyOf(Work);
			KingdomMaterialTally buildCost = string.IsNullOrEmpty(designKey) ? null : KingdomMaterials.CostFor(designKey);
			KingdomBitTally buildBits = string.IsNullOrEmpty(designKey) ? null : KingdomMaterials.BitCostFor(designKey);
			Cost = KingdomMaterialRules.RepairCost(buildCost, Wear);
			BitCost = KingdomMaterialRules.RepairBits(buildBits, Wear);
		}

		private static void StartRepair(KingdomSystem System, GameObject Work, r_KingdomWear WearPart, long TimeTicks)
		{
			RepairTargetFrame targetFrame;
			if (!TryCaptureRepairTarget(Work, WearPart, out targetFrame)) return;
			BuildTallies(Work, WearPart.Wear, out KingdomMaterialTally cost, out KingdomBitTally bitCost);
			Zone zone = Work.CurrentZone;
			if (zone == null || HasActiveRepair(Work, out _)
				|| KingdomConstruction.HasActiveSubject(System, zone,
					KingdomConstructionRoute.WearRepair, Work))
			{
				return;
			}
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(cost, bitCost, null);
			KingdomSurvey survey = KingdomSurvey.Take(zone, System);
			KingdomWaterDebit water = survey.ReserveExactWater(0);
			KingdomMaterialDebit materials = cost.IsEmpty()
				? KingdomMaterials.ReserveBits(zone, bitCost)
				: KingdomMaterials.ReserveComposite(zone, claim);
			string target = KingdomUpgrade.DesignKeyOf(Work);
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, zone,
				KingdomConstructionRoute.WearRepair, Work.CurrentCell, Work, target,
				RepairPayload(WearPart.Wear, false),
				0, claim, TimeTicks, TimeTicks);
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (!RepairTargetExact(targetFrame, targetFrame.Receipt))
			{
				if (job != null && !KingdomConstructionRules.IsTerminal(job.Phase))
				{
					KingdomConstruction.Quarantine(ref job,
						"A funding callback changed the exact repair work, wear part, cell, zone, or state.");
				}
				KingdomLog.Log("wear: repair funding target became uncertain");
				return;
			}
			if (funding == KingdomConstructionStartResult.Refused)
			{
				KingdomLog.Log("wear: repair refused cleanly " + (fundingFailure ?? Work.Blueprint));
				return;
			}
			KingdomConstruction.Bind(Work, job);
			if (!RepairTargetExact(targetFrame, job.Id))
			{
				KingdomConstruction.Quarantine(ref job,
					"The funded repair receipt did not bind to its exact original work and wear part.");
				return;
			}
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				System.Ledger.Note("{{r|The mending receipt remains outstanding. The damaged work stays queued without another charge.}}");
				return;
			}
			if (!ProjectRepair(System, Work, WearPart, job, out job, out string projectionFailure))
			{
				System.Ledger.Note("{{r|The paid mending could not yet be put in hand. Its receipt remains queued.}}");
				KingdomLog.Log("construction: repair projection waits: " + projectionFailure);
				return;
			}
			string name = DisplayName(Work);
			System.Ledger.Note("{{K|" + KingdomWearRules.RepairBegunLine(name) + "}}");
			KingdomLog.Log("wear: repair begun " + Work.Blueprint + " wear=" + WearPart.Wear + " effort=" + WearPart.RepairEffortLeft);
		}

		private static bool ProjectRepair(KingdomSystem System, GameObject Work,
			r_KingdomWear WearPart,
			KingdomConstructionJob Job, out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			if (!GameObject.Validate(Work) || Work.CurrentCell == null || WearPart == null
				|| WearPart.Wear <= 0)
			{
				Failure = "The paid damaged work is absent.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			if (WearPart.RepairEffortLeft > 0 && KingdomConstruction.HasReceipt(Work, Job))
			{
				KingdomConstruction.FinishProjection(ref Updated, true, true);
				return true;
			}
			int requestedWear;
			bool finishing;
			KingdomMaterialDebitCost requested;
			if (!TryRepairPayload(Job.Payload, out requestedWear, out finishing)
				|| WearPart.Wear != requestedWear
				|| !KingdomMaterialDebitCost.TryParseClaim(Job.Claims.MaterialRequested, out requested))
			{
				Failure = "The paid repair target no longer matches its durable receipt.";
				return false;
			}
			if (finishing)
			{
				return FinishRepairProjection(System, Work, WearPart, Job,
					out Updated, out Failure);
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			int effort = KingdomMaterialRules.RepairEffort(
				requested.Materials.Total() + requested.Bits.Total(), requestedWear);
			if (effort <= 0)
			{
				Failure = "The repair receipt resolved to no measurable work.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			KingdomConstruction.Bind(Work, Updated);
			WearPart.RepairEffortLeft = effort;
			KingdomMaterials.WriteTick(Work, RepairWorkedProperty, The.Game.TimeTicks);
			WearPart.AnnouncedBlock = 0;
			if (!KingdomConstruction.HasReceipt(Work, Updated)
				|| WearPart.RepairEffortLeft != effort)
			{
				Failure = "The repair work could not be verified on its damaged work.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			KingdomConstruction.FinishProjection(ref Updated, true, true);
			return true;
		}

		private static void AdvanceRepair(KingdomSystem System, GameObject Work, r_KingdomWear WearPart, int Hands, long TimeTicks)
		{
			long worked = KingdomMaterials.ReadTick(Work, RepairWorkedProperty);
			if (worked <= 0)
			{
				KingdomMaterials.WriteTick(Work, RepairWorkedProperty, TimeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(TimeTicks - worked);
			if (days <= 0)
			{
				return;
			}
			if (Hands <= 0)
			{
				if (WearPart.AnnouncedBlock != (int)KingdomWearRules.RepairVerdict.NoHands)
				{
					WearPart.AnnouncedBlock = (int)KingdomWearRules.RepairVerdict.NoHands;
					string blockLine = KingdomWearRules.ReasonLine(KingdomWearRules.RepairVerdict.NoHands, DisplayName(Work));
					if (blockLine != null)
					{
						System.Ledger.Note("{{r|" + blockLine + "}}");
					}
				}
				KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
				return;
			}
			WearPart.AnnouncedBlock = 0;
			KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
			int left = WearPart.RepairEffortLeft - KingdomMaterialRules.EffortWorked(Hands, days);
			if (left > 0)
			{
				WearPart.RepairEffortLeft = left;
				return;
			}
			string receipt = Work.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob job;
			if (!string.IsNullOrEmpty(receipt))
			{
				if (!KingdomConstruction.TryFind(receipt, out job)
					|| !KingdomConstruction.Owns(System, Work.CurrentZone, job)
					|| job.Route != KingdomConstructionRoute.WearRepair
					|| KingdomConstructionRules.IsTerminal(job.Phase)) return;
				int paidWear;
				bool finishing;
				if (!TryRepairPayload(job.Payload, out paidWear, out finishing)) return;
				if (!finishing)
				{
					if (!KingdomConstruction.UpdatePayload(ref job,
						RepairPayload(paidWear, true))) return;
				}
				if (!FinishRepairProjection(System, Work, WearPart, job, out job, out _))
				{
					return;
				}
			}
			else
			{
				// A legacy save has no keyed construction row on which to freeze an outbox or
				// publish a one-shot callback intent. Do not guess its destructive continuation.
				QuarantineWear(System, Work,
					"A legacy repair reached part removal without a durable keyed receipt.");
				return;
			}
		}

		private static bool FinishRepairProjection(KingdomSystem System, GameObject Work,
			r_KingdomWear WearPart,
			KingdomConstructionJob Job, out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			Zone zone = Work?.CurrentZone;
			if (!RepairSubjectExact(System, zone, Work, Job))
			{
				Failure = "The paid repair target could not be verified.";
				return false;
			}
			if (WearPart == null)
			{
				Failure = "The wear part is absent without a live exact removal proof.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			if (WearPart.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), WearPart)
				|| !string.IsNullOrEmpty(Work.GetStringProperty(RepairRemovalAttemptProperty))
				|| !string.IsNullOrEmpty(Work.GetStringProperty(RepairRemovalProofProperty)))
			{
				Failure = "A repair removal intent already exists or the exact wear part was replaced.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			int requestedWear;
			bool finishing;
			if (!TryRepairPayload(Job.Payload, out requestedWear, out finishing) || !finishing
				|| (WearPart.Wear != requestedWear && WearPart.Wear != 0))
			{
				Failure = "The damaged state no longer matches its repair receipt.";
				return false;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			RepairTargetFrame frame;
			if (!TryCaptureRepairTarget(Work, WearPart, out frame)
				|| !RepairTargetExact(frame, Updated.Id)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				Failure = "The exact repair target changed before its completion outbox was frozen.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			string name = DisplayName(Work);
			string leakStopped = WearPart.LeakAnnounced
				? KingdomWearRules.LeakStoppedLine(name, LeakKindOf(Work)) : null;
			if (!KingdomCeremony.PrepareWearRepaired(System, name, leakStopped, ref Updated)
				|| !RepairTargetExact(frame, Updated.Id)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				Failure = "The repair completion outbox or exact target could not be frozen.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			Work.SetStringProperty(RepairRemovalAttemptProperty, Updated.Id);
			if (!string.Equals(Work.GetStringProperty(RepairRemovalAttemptProperty),
					Updated.Id, StringComparison.Ordinal)
				|| !RepairTargetExact(frame, Updated.Id)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				Failure = "The one-shot repair removal intent could not be proved before its callback.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			WearPart.RepairEffortLeft = 0;
			WearPart.Wear = 0;
			WearPart.LastCause = (int)KingdomWearRules.WearCause.None;
			bool callbackReturned = true;
			try
			{
				// The only wear-owned PartRemoved callback. The durable attempt latch above means
				// no recovery path can enter this call a second time.
				Work.RemovePart(WearPart);
			}
			catch (Exception)
			{
				callbackReturned = false;
			}
			bool exactRemoval = callbackReturned && GameObject.Validate(Work)
				&& Work.ID == frame.Id && Work.CurrentZone == frame.Zone
				&& Work.CurrentCell == frame.Cell && frame.Cell.ParentZone == frame.Zone
				&& string.Equals(Work.GetStringProperty(KingdomConstruction.ReceiptProperty),
					Updated.Id, StringComparison.Ordinal)
				&& string.Equals(Work.GetStringProperty(RepairRemovalAttemptProperty),
					Updated.Id, StringComparison.Ordinal)
				&& WearPart.ParentObject == null && Work.GetPart<r_KingdomWear>() == null
				&& KingdomConstruction.IsCurrent(Updated);
			if (!exactRemoval)
			{
				Failure = "The PartRemoved callback changed or obscured the exact repaired work, part, cell, zone, receipt, or job.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			Work.SetStringProperty(RepairRemovalProofProperty, Updated.Id);
			Work.RemoveStringProperty(RepairRemovalAttemptProperty);
			if (!string.Equals(Work.GetStringProperty(RepairRemovalProofProperty),
					Updated.Id, StringComparison.Ordinal)
				|| !string.IsNullOrEmpty(
					Work.GetStringProperty(RepairRemovalAttemptProperty))
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				Failure = "The exact post-callback repair proof could not be persisted.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.Complete(ref Updated)) return false;
			Work.RemoveStringProperty(RepairRemovalProofProperty);
			bool dispatched = KingdomCeremony.DispatchPending(System, ref Updated);
			KingdomLog.Log("wear: repair complete " + Work.Blueprint);
			return dispatched;
		}

		private static string DisplayName(GameObject Work)
		{
			return KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
		}

		/// <summary>Which kind of contents this work stores, for the sentence a leak is told in.
		/// Water is the default because the vessel is the ordinary case; a work that stores
		/// nothing never reaches either line.</summary>
		private static KingdomWearRules.LeakKind LeakKindOf(GameObject Work)
		{
			if (Work.GetIntProperty(StoresProperty) == 1)
			{
				return KingdomWearRules.LeakKind.Water;
			}
			if (Work.GetIntProperty(LarderProperty) == 1)
			{
				return KingdomWearRules.LeakKind.Food;
			}
			return (Work.GetPart<r_KingdomPowerStore>() != null)
				? KingdomWearRules.LeakKind.Charge
				: KingdomWearRules.LeakKind.Water;
		}
	}
}
