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
			KingdomWear.RetireFoodLeakReceipt(ParentObject, this);
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
