using System;
using System.Collections.Generic;

using ThousandAndFirst;

// XRL.World.Parts, for the reason r_KingdomPlot, r_KingdomSeed and r_KingdomMirrorGate all state:
// GamePartBlueprint resolves a part named in XML as exactly "XRL.World.Parts.<Name>" and tries no
// other name. Only the parts move; everything they do lives in ThousandAndFirst.KingdomLab below.
namespace XRL.World.Parts
{
	/// <summary>
	/// The butcher's slab. Rung 0, and not the lab: the work that turns what the founder drags home
	/// into parts.
	/// <para>
	/// It invents no butchery. Vanilla's <c>Butcherable</c> and <c>Corpse</c> already do the whole
	/// job, gated on the founder's own <c>CookingAndGathering_Butchery</c> skill, and Addendum 11(c)
	/// says inherit rather than reinvent. What the slab adds is the one thing vanilla has no opinion
	/// about: reading what the creature was BEARING before the knife, and stamping it onto what
	/// comes off, so that a part can still be a part a season later.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomButcherSlab : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Dress", "dress a carcass on the slab", "r_DressCarcass", null, 'd', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_DressCarcass" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("butcher slab", delegate
				{
					KingdomLab.Dress(E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// The vat-house. Rung 1, and the preservation chain lives here.
	/// <para>
	/// <b>Nothing rots, here or anywhere.</b> Vanilla has no rot at all &mdash;
	/// <c>PreservableItem</c> is two fields and no behaviour
	/// (<c>D/XRL/World/Parts/PreservableItem.cs:8,10</c>) &mdash; and a decay timer would be a rate
	/// running on time alone, which Addendum 8 clause 2 forbids outright. What gates the chain is
	/// LABOUR: a staffed work, real hands, real world-days. An empty vat-house keeps what it holds
	/// forever and preserves nothing new.
	/// </para>
	/// <para>
	/// The point of it is not the gate. A preserved part is a permanent, storable, tradeable item
	/// the day it exists, so the vat-house is worth building for a founder who never raises the hall
	/// at all &mdash; a bonus for engaging, never a penalty for abstaining.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomVatHouse : IPart
	{
		/// <summary>Tick this vat last settled its labour to. Zero until the first day boundary
		/// plants it, so a vat never works for the day it was raised &mdash; the same discipline
		/// <c>r_KingdomMirrorGate.LastDrawTick</c> keeps, and for the same reason.
		/// <para>
		/// This stays the part's only serialized field. Pending work lives on the physical input's
		/// ordinary property dictionaries inside the vat's ordinary inventory, so both halves ride
		/// the engine's existing object serialization and this part's positional save layout
		/// never changes.
		/// </para>
		/// </summary>
		public long LastWorkedTick;

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
			if (KingdomLab.HasPending(this))
			{
				KingdomSystem.Guard("vat-house work", delegate
				{
					KingdomLab.Advance(this, TimeTick);
				});
			}
			base.TurnTick(TimeTick, Amount);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Keep", "put a part up to keep", "r_KeepPart", null, 'k', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_KeepPart" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("vat house", delegate
				{
					KingdomLab.Keep(this, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// The grafting hall. Rung 2, and the lab proper: Class I and Class II.
	/// <para>
	/// <b>The verb is on the building and there is no charter hotkey.</b> The Charter's letters are
	/// full at thirty-six and a new entry there would be a chapter rather than a line, so the slate
	/// opens where the work is done &mdash; which is also where the founder is standing when they
	/// want it, and is the same call the mirror-gate's own dedication made.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomGraftingHall : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Slate", "read the hall's slate", "r_OpenLabSlate", null, 'l', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenLabSlate" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("lab slate", delegate
				{
					using (KingdomGovernanceScope.Begin(E.Actor))
					{
						KingdomLab.OpenSlate(ParentObject, E.Actor);
					}
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// The chimeric theatre. Rung 3, Class III, the four named procedures &mdash; and the city's one
	/// purpose (Addendum 22 A1, Design B).
	/// <para>
	/// It carries the same slate as the hall, because it IS the hall with its ceiling raised, and a
	/// second screen for the same act would be a second screen for the same act.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomChimericTheatre : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Slate", "read the theatre's slate", "r_OpenLabSlate", null, 'l', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenLabSlate" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("lab slate", delegate
				{
					using (KingdomGovernanceScope.Begin(E.Actor))
					{
						KingdomLab.OpenSlate(ParentObject, E.Actor);
					}
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>One persisted, paid procedure job owned by its physical hall.</summary>
	[Serializable]
	public class r_KingdomLabJob : IPart
	{
		public string JobId = "";
		public string BuildingId = "";
		public string ProcedureKey = "";
		public string PatientId = "";
		public string GameId = "";
		public string RealmId = "";
		public long RealmFoundedTick;
		public int BodyPartId;
		public string BearerId = "";
		public string Stamp = "";
		public string City = "";
		public int ContractVersion;
		public string FrozenName = "";
		public string FrozenGrants = "";
		public int FrozenSource = -1;
		public int FrozenAttach = -1;
		public string FrozenManager = "";
		public string FrozenDetail = "";
		public string FrozenMagnitude = "";
		public string FrozenCreeds = "";
		public int FrozenClass = -1;
		public int FrozenStaffDays;
		public string FrozenFingerprint = "";
		public int Phase;
		public int RemainingTicks;
		public long LastWorkedTick;
		public int WaterOwed;
		public int WaterPaid;
		public int WaterLost;
		public bool WaterMeasurementExact = true;
		public bool WaterQuarantined;
		public int KeptOwed;
		public int KeptPaid;
		public int KeptLost;
		public bool KeptMeasurementExact = true;
		public bool KeptQuarantined;
		public string BitClaim = "";
		public string BitOutstanding = "";
		public List<string> StandingFactions = new List<string>();
		public List<int> StandingDeltas = new List<int>();
		public List<int> StandingBefore = new List<int>();
		public List<int> StandingTargets = new List<int>();
		public List<int> StandingPhases = new List<int>();
		public bool GovernanceCommitted;
		public bool IntentPublished;
		public bool EffectCommitted;
		public bool OwnershipPublished;
		public int EffectBodyPartId;
		public int EffectPartOrdinal = -1;
		public bool MarkerCleanupPending;
		public bool MarkerCleaned;
		public bool RegistryFinalized;
		public bool SchemaQuarantined;
		public bool StandingApplied;
		public int StandingAppliedCount;
		public bool Announced;
		public bool Chronicled;
		public bool Spoken;
		public bool ReadyAnnounced;
		public int ReadyMessagePhase;
		public int TerminalMessagePhase;
		public string ReadyMessageText = "";
		public string TerminalMessageText = "";
		public string ReadyMessageEventId = "";
		public string ChronicleEventId = "";
		public string PetitionEventId = "";
		public string AnnounceEventId = "";
		public long PetitionAttemptTick = -1L;
		public string PetitionFaction = "";
		public string Fault = "";

		internal KingdomLabJobPhase State
		{
			get { return (KingdomLabJobPhase)Phase; }
			set { Phase = (int)value; }
		}

		public void Normalize()
		{
			JobId = JobId ?? "";
			BuildingId = BuildingId ?? "";
			ProcedureKey = ProcedureKey ?? "";
			PatientId = PatientId ?? "";
			GameId = GameId ?? "";
			RealmId = RealmId ?? "";
			BearerId = BearerId ?? "";
			Stamp = Stamp ?? "";
			City = City ?? "";
			FrozenName = FrozenName ?? "";
			FrozenGrants = FrozenGrants ?? "";
			FrozenManager = FrozenManager ?? "";
			FrozenDetail = FrozenDetail ?? "";
			FrozenMagnitude = FrozenMagnitude ?? "";
			FrozenCreeds = FrozenCreeds ?? "";
			FrozenFingerprint = FrozenFingerprint ?? "";
			BitClaim = BitClaim ?? "";
			BitOutstanding = BitOutstanding ?? "";
			ChronicleEventId = ChronicleEventId ?? "";
			PetitionEventId = PetitionEventId ?? "";
			AnnounceEventId = AnnounceEventId ?? "";
			ReadyMessageEventId = ReadyMessageEventId ?? "";
			ReadyMessageText = ReadyMessageText ?? "";
			TerminalMessageText = TerminalMessageText ?? "";
			PetitionFaction = PetitionFaction ?? "";
			Fault = Fault ?? "";
			bool malformed = WaterOwed < 0 || WaterPaid < 0 || WaterPaid > WaterOwed
				|| WaterLost < 0 || KeptOwed < 0 || KeptPaid < 0 || KeptPaid > KeptOwed
				|| KeptLost < 0 || RemainingTicks < 0 || StandingAppliedCount < 0
				|| PetitionAttemptTick < -1L
				|| !Enum.IsDefined(typeof(KingdomLabJobPhase), (KingdomLabJobPhase)Phase)
				|| !KingdomLabRules.ValidEffectContract(ContractVersion, ProcedureKey,
					FrozenGrants, FrozenSource, FrozenAttach, FrozenManager, FrozenFingerprint,
					FrozenDetail)
				|| string.IsNullOrEmpty(JobId) || string.IsNullOrEmpty(BuildingId)
				|| string.IsNullOrEmpty(PatientId) || string.IsNullOrEmpty(GameId)
				|| string.IsNullOrEmpty(RealmId) || RealmFoundedTick < 0L
				|| JobId.Length > 128 || BuildingId.Length > 128 || PatientId.Length > 128
				|| GameId.Length > 256 || RealmId.Length > 256 || ProcedureKey.Length > 128
				|| FrozenName.Length > 512 || FrozenDetail.Length > 512
				|| FrozenMagnitude.Length > 512 || FrozenCreeds.Length > 4096
				|| !Enum.IsDefined(typeof(LabClass), (LabClass)FrozenClass) || FrozenStaffDays < 0
				|| Stamp.Length > 32768 || City.Length > 512
				|| BitClaim.Length > 4096 || BitOutstanding.Length > 4096
				|| ReadyMessageText.Length > 2048 || TerminalMessageText.Length > 2048
				|| !Enum.IsDefined(typeof(KingdomLabMessagePhase),
					(KingdomLabMessagePhase)ReadyMessagePhase)
				|| !Enum.IsDefined(typeof(KingdomLabMessagePhase),
					(KingdomLabMessagePhase)TerminalMessagePhase);
			if (ReadyAnnounced && ReadyMessagePhase == (int)KingdomLabMessagePhase.Pending)
				ReadyMessagePhase = (int)KingdomLabMessagePhase.Lost;
			if (Announced && TerminalMessagePhase == (int)KingdomLabMessagePhase.Pending)
				TerminalMessagePhase = (int)KingdomLabMessagePhase.Lost;
			ReadyMessagePhase = (int)KingdomLabRules.ResumeMessage(
				(KingdomLabMessagePhase)ReadyMessagePhase);
			TerminalMessagePhase = (int)KingdomLabRules.ResumeMessage(
				(KingdomLabMessagePhase)TerminalMessagePhase);
			WaterOwed = Math.Max(0, WaterOwed);
			WaterPaid = Math.Min(WaterOwed, Math.Max(0, WaterPaid));
			WaterLost = Math.Max(0, WaterLost);
			KeptOwed = Math.Max(0, KeptOwed);
			KeptPaid = Math.Min(KeptOwed, Math.Max(0, KeptPaid));
			KeptLost = Math.Max(0, KeptLost);
			RemainingTicks = Math.Max(0, RemainingTicks);
			if (!WaterMeasurementExact)
			{
				WaterQuarantined = true;
			}
			if (!KeptMeasurementExact)
			{
				KeptQuarantined = true;
			}
			StandingFactions = StandingFactions ?? new List<string>();
			StandingDeltas = StandingDeltas ?? new List<int>();
			StandingBefore = StandingBefore ?? new List<int>();
			StandingTargets = StandingTargets ?? new List<int>();
			StandingPhases = StandingPhases ?? new List<int>();
			if (StandingFactions.Count > KingdomLabRules.MaxStandingRows
				|| StandingDeltas.Count > KingdomLabRules.MaxStandingRows
				|| StandingBefore.Count > KingdomLabRules.MaxStandingRows
				|| StandingTargets.Count > KingdomLabRules.MaxStandingRows
				|| StandingPhases.Count > KingdomLabRules.MaxStandingRows)
			{
				malformed = true;
				Trim(StandingFactions, KingdomLabRules.MaxStandingRows);
				Trim(StandingDeltas, KingdomLabRules.MaxStandingRows);
				Trim(StandingBefore, KingdomLabRules.MaxStandingRows);
				Trim(StandingTargets, KingdomLabRules.MaxStandingRows);
				Trim(StandingPhases, KingdomLabRules.MaxStandingRows);
			}
			if (StandingDeltas.Count != StandingFactions.Count
				|| (StandingBefore.Count != 0 && StandingBefore.Count != StandingFactions.Count)
				|| StandingTargets.Count != StandingFactions.Count
				|| (StandingPhases.Count != 0 && StandingPhases.Count != StandingFactions.Count))
			{
				malformed = true;
			}
			while (StandingDeltas.Count < StandingFactions.Count)
			{
				StandingDeltas.Add(0);
			}
			while (StandingTargets.Count < StandingFactions.Count)
			{
				StandingTargets.Add(int.MinValue);
			}
			while (StandingBefore.Count < StandingFactions.Count)
			{
				StandingBefore.Add(int.MinValue);
			}
			while (StandingPhases.Count < StandingFactions.Count)
			{
				// A legacy row with a persisted target lacks a CAS before-value and is not
				// authorized to overwrite current standing.
				StandingPhases.Add(StandingTargets[StandingPhases.Count] == int.MinValue
					? (int)KingdomLabStandingPhase.Pending
					: (int)KingdomLabStandingPhase.Quarantined);
			}
			Trim(StandingDeltas, StandingFactions.Count);
			Trim(StandingBefore, StandingFactions.Count);
			Trim(StandingTargets, StandingFactions.Count);
			Trim(StandingPhases, StandingFactions.Count);
			for (int i = 0; i < StandingPhases.Count; i++)
			{
				if (!Enum.IsDefined(typeof(KingdomLabStandingPhase),
					(KingdomLabStandingPhase)StandingPhases[i])) malformed = true;
			}
			if (StandingAppliedCount > StandingFactions.Count)
			{
				malformed = true;
				StandingAppliedCount = StandingFactions.Count;
			}
			bool fundedPhase = State == KingdomLabJobPhase.Working
				|| State == KingdomLabJobPhase.Ready || State == KingdomLabJobPhase.Applying
				|| State == KingdomLabJobPhase.ApplicationRecovery || State == KingdomLabJobPhase.Complete;
			if (fundedPhase && (WaterPaid != WaterOwed || WaterQuarantined
				|| !string.IsNullOrEmpty(BitOutstanding) || KeptPaid != KeptOwed
				|| KeptQuarantined))
			{
				malformed = true;
			}
			if (State == KingdomLabJobPhase.Complete
				&& (!EffectCommitted || !OwnershipPublished || !MarkerCleaned
					|| !RegistryFinalized))
			{
				malformed = true;
			}
			if (malformed)
			{
				SchemaQuarantined = true;
				State = KingdomLabJobPhase.ApplicationRecovery;
				Fault = "The application receipt is malformed or predates exact effect contracts. It is quarantined; no payment, body mutation, cleanup, or telling will replay.";
			}
		}

		private static void Trim<T>(List<T> Values, int Count)
		{
			if (Values != null && Values.Count > Count)
			{
				Values.RemoveRange(Count, Values.Count - Count);
			}
		}

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
		{
			r_KingdomLabJob copy = (r_KingdomLabJob)base.DeepCopy(Parent, MapInv);
			copy.StandingFactions = new List<string>(StandingFactions ?? new List<string>());
			copy.StandingDeltas = new List<int>(StandingDeltas ?? new List<int>());
			copy.StandingBefore = new List<int>(StandingBefore ?? new List<int>());
			copy.StandingTargets = new List<int>(StandingTargets ?? new List<int>());
			copy.StandingPhases = new List<int>(StandingPhases ?? new List<int>());
			return copy;
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			SchemaQuarantined = true;
			State = KingdomLabJobPhase.ApplicationRecovery;
			Fault = "Copied commission receipt has no canonical mutation authority.";
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomLabJob));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabJob));
			Normalize();
		}
	}

	/// <summary>One patient-owned, persisted graft-removal receipt.</summary>
	[Serializable]
	public class r_KingdomLabRemovalJob : IPart
	{
		public string RemovalId = "";
		public string ProcedureKey = "";
		public string OriginalJobId = "";
		public string PatientId = "";
		public string GameId = "";
		public string RealmId = "";
		public long RealmFoundedTick;
		public int BodyPartId;
		public string BearerId = "";
		public string City = "";
		public int ContractVersion;
		public string FrozenName = "";
		public string FrozenGrants = "";
		public int FrozenSource = -1;
		public int FrozenAttach = -1;
		public string FrozenManager = "";
		public string FrozenDetail = "";
		public string FrozenFingerprint = "";
		public string EffectNonce = "";
		public int PartOrdinal = -1;
		public int WaterOwed;
		public int WaterPaid;
		public int WaterLost;
		public bool WaterMeasurementExact = true;
		public bool WaterQuarantined;
		public int Phase;
		public bool GovernanceCommitted;
		public bool EffectRemoved;
		public bool OwnershipCleaned;
		public bool RecordCleaned;
		public bool Chronicled;
		public bool Announced;
		public int TerminalMessagePhase;
		public string TerminalMessageText = "";
		public bool ReceiptPresented;
		public bool SchemaQuarantined;
		public string ChronicleEventId = "";
		public string AnnounceEventId = "";
		public string Fault = "";

		internal KingdomLabRemovalPhase State
		{
			get { return (KingdomLabRemovalPhase)Phase; }
			set { Phase = (int)value; }
		}

		public void Normalize()
		{
			RemovalId = RemovalId ?? "";
			ProcedureKey = ProcedureKey ?? "";
			OriginalJobId = OriginalJobId ?? "";
			PatientId = PatientId ?? "";
			GameId = GameId ?? "";
			RealmId = RealmId ?? "";
			BearerId = BearerId ?? "";
			City = City ?? "";
			FrozenName = FrozenName ?? "";
			FrozenGrants = FrozenGrants ?? "";
			FrozenManager = FrozenManager ?? "";
			FrozenDetail = FrozenDetail ?? "";
			FrozenFingerprint = FrozenFingerprint ?? "";
			EffectNonce = EffectNonce ?? "";
			ChronicleEventId = ChronicleEventId ?? "";
			AnnounceEventId = AnnounceEventId ?? "";
			TerminalMessageText = TerminalMessageText ?? "";
			Fault = Fault ?? "";
			bool malformed = WaterOwed < 0 || WaterPaid < 0 || WaterPaid > WaterOwed
				|| WaterLost < 0 || string.IsNullOrEmpty(RemovalId)
				|| string.IsNullOrEmpty(OriginalJobId) || string.IsNullOrEmpty(PatientId)
				|| string.IsNullOrEmpty(GameId) || string.IsNullOrEmpty(RealmId)
				|| RealmFoundedTick < 0L || RemovalId.Length > 128 || OriginalJobId.Length > 128
				|| PatientId.Length > 128 || GameId.Length > 256 || RealmId.Length > 256
				|| !KingdomLabRules.ValidEffectContract(ContractVersion, ProcedureKey,
					FrozenGrants, FrozenSource, FrozenAttach, FrozenManager, FrozenFingerprint,
					FrozenDetail) || EffectNonce.Length != 32
				|| TerminalMessageText.Length > 2048
				|| !Enum.IsDefined(typeof(KingdomLabMessagePhase),
					(KingdomLabMessagePhase)TerminalMessagePhase);
			if (Announced && TerminalMessagePhase == (int)KingdomLabMessagePhase.Pending)
				TerminalMessagePhase = (int)KingdomLabMessagePhase.Lost;
			TerminalMessagePhase = (int)KingdomLabRules.ResumeMessage(
				(KingdomLabMessagePhase)TerminalMessagePhase);
			WaterOwed = Math.Max(0, WaterOwed);
			WaterPaid = Math.Min(WaterOwed, Math.Max(0, WaterPaid));
			WaterLost = Math.Max(0, WaterLost);
			if (!WaterMeasurementExact)
			{
				WaterQuarantined = true;
			}
			if (!Enum.IsDefined(typeof(KingdomLabRemovalPhase), (KingdomLabRemovalPhase)Phase))
			{
				malformed = true;
			}
			if (malformed)
			{
				SchemaQuarantined = true;
				State = KingdomLabRemovalPhase.Quarantined;
				Fault = "The removal receipt is malformed or predates exact effect contracts. It is quarantined rather than replayed.";
			}
		}

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			EffectNonce = Guid.NewGuid().ToString("N");
			SchemaQuarantined = true;
			State = KingdomLabRemovalPhase.Quarantined;
			Fault = "Copied removal receipt has a fresh nonce and no body authority.";
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomLabRemovalJob));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabRemovalJob));
			Normalize();
		}
	}
}

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of the lab as a place: dressing a carcass, keeping what comes off,
	/// and the slate the founder reads at the hall.
	/// <para>
	/// <b>One screen, two levels, both <c>Popup.PickOption</c>, and no new screen class.</b> That is
	/// simultaneously the vanilla golem quest's shape, Playable Golem's shape and the precedent's
	/// control-menu shape &mdash; which is not a coincidence; it is Qud's house idiom, and this
	/// system has no reason to be the exception.
	/// </para>
	/// <para>
	/// Two things are inherited on purpose and both were expensive to learn elsewhere. Effects are
	/// shown BEFORE commitment, prefixed <c>{{rules|--}}</c>, because the one documented complaint
	/// about the vanilla picker is that players cannot tell what a choice will do; and the
	/// three-way consent prompt's third answer writes to a permanent exclusion list, because a
	/// founder who says never should be believed.
	/// </para>
	/// <para>
	/// Every decision that does not need a real object &mdash; what a body will take, where a part
	/// has to sit, what a thing costs, every sentence a refusal is told with &mdash; is delegated to
	/// the engine-free <see cref="KingdomProcedureRules"/> and <see cref="KingdomLabRules"/>.
	/// </para>
	/// </summary>
	internal static class KingdomLab
	{
		/// <summary>The property a preserved part carries to mark it as the vat-house's own work,
		/// so a founder's own jerky is never mistaken for a graftable organ (the protection law: we
		/// count only what we made and marked).</summary>
		internal const string KeptProperty = "r_TAF_LabKept";

		private const string VatPendingProperty = "r_TAF_VatPending";
		private const string VatRemainingProperty = "r_TAF_VatRemaining";
		private const string VatResultProperty = "r_TAF_VatResult";
		private const string VatYieldProperty = "r_TAF_VatYield";
		private const string VatJobProperty = "r_TAF_VatJob";
		private const string VatReadyProperty = "r_TAF_VatReady";
		private const string VatOutputJobProperty = "r_TAF_VatOutputJob";
		private const string VatOutputIdProperty = "r_TAF_VatOutputId";
		private const string VatOutputFingerprintProperty = "r_TAF_VatOutputFingerprint";
		private const string VatOutputPhaseProperty = "r_TAF_VatOutputPhase";
		private const string VatRawPhaseProperty = "r_TAF_VatRawPhase";
		private const string VatRawIdProperty = "r_TAF_VatRawId";
		private const string VatRawBlueprintProperty = "r_TAF_VatRawBlueprint";
		private const string VatRawCountProperty = "r_TAF_VatRawCount";
		private const string VatRawFingerprintProperty = "r_TAF_VatRawFingerprint";
		private const string VatOwnerIdProperty = "r_TAF_VatOwnerId";
		private const string VatBlockedProperty = "r_TAF_VatBlocked";
		private const string LabRegistryState = "r_TAF_LabJobRegistry_v1";
		private const string LabReplayState = "r_TAF_LabReplayProof_v1";

		private sealed class KeptSpendPreparation
		{
			public readonly List<GameObject> Sources;
			public readonly List<string> Stamps;
			public readonly LabProcedure Procedure;
			public readonly KingdomKeptSpendPlan Plan;

			public KeptSpendPreparation(List<GameObject> Sources, List<string> Stamps,
				LabProcedure Procedure, KingdomKeptSpendPlan Plan)
			{
				this.Sources = Sources;
				this.Stamps = Stamps;
				this.Procedure = Procedure;
				this.Plan = Plan;
			}
		}

		private static string RealmIdentity(KingdomSystem System)
		{
			return System?.CurrentRealmId;
		}

		private static KingdomLabMessagePhase PublishMessage(ref int StoredPhase,
			ref string FrozenText, string EventId, string Text, bool ShouldPublish = true)
		{
			KingdomLabMessagePhase phase = KingdomLabRules.ResumeMessage(
				(KingdomLabMessagePhase)StoredPhase);
			StoredPhase = (int)phase;
			if (KingdomLabRules.MessageSettled(phase)) return phase;
			if (phase != KingdomLabMessagePhase.Pending || string.IsNullOrEmpty(EventId))
			{
				StoredPhase = (int)KingdomLabMessagePhase.Lost;
				return KingdomLabMessagePhase.Lost;
			}
			FrozenText = Text ?? "";
			if (!ShouldPublish || string.IsNullOrEmpty(FrozenText))
			{
				StoredPhase = (int)KingdomLabMessagePhase.Skipped;
				return KingdomLabMessagePhase.Skipped;
			}
			StoredPhase = (int)KingdomLabMessagePhase.Intent;
			try
			{
				MessageQueue.AddPlayerMessage(FrozenText);
				StoredPhase = (int)KingdomLabMessagePhase.Delivered;
				return KingdomLabMessagePhase.Delivered;
			}
			catch (Exception ex)
			{
				StoredPhase = (int)KingdomLabMessagePhase.Lost;
				KingdomLog.Log("lab: message intent " + EventId
					+ " returned unknown/lost (" + ex.Message + ")");
				return KingdomLabMessagePhase.Lost;
			}
		}

		private static KingdomLabRegistryEntry RegistryEntry(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			return new KingdomLabRegistryEntry
			{
				JobId = Job?.JobId ?? "",
				BuildingId = Job?.BuildingId ?? "",
				PatientId = Job?.PatientId ?? "",
				GameId = Job?.GameId ?? "",
				RealmId = Job?.RealmId ?? "",
				RealmFoundedTick = Job?.RealmFoundedTick ?? -1L,
				ContractVersion = Job?.ContractVersion ?? 0,
				ProcedureKey = Job?.ProcedureKey ?? "",
				Grants = Job?.FrozenGrants ?? "",
				Source = Job?.FrozenSource ?? -1,
				Attach = Job?.FrozenAttach ?? -1,
				Manager = Job?.FrozenManager ?? "",
				Detail = Job?.FrozenDetail ?? "",
				Fingerprint = Job?.FrozenFingerprint ?? "",
				Status = Status,
				UpdatedTick = Math.Max(0L, The.Game?.TimeTicks ?? 0L)
			};
		}

		private static bool WriteCanonical(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			if (The.Game == null || Job == null) return false;
			bool replayMalformed;
			if (Status == KingdomLabRegistryStatus.Active
				&& KingdomLabRules.ReplayContains(
					The.Game.GetStringGameState(LabReplayState, ""), "apply:" + Job.JobId,
					out replayMalformed)) return false;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			KingdomLabRegistryEntry expected = RegistryEntry(Job, Status);
			if (quarantined || !KingdomLabRules.UpsertRegistry(rows, expected)) return false;
			string written = KingdomLabRules.FormatRegistry(rows);
			The.Game.SetStringGameState(LabRegistryState, written);
			if (!string.Equals(The.Game.GetStringGameState(LabRegistryState, ""), written,
				StringComparison.Ordinal)) return false;
			rows = KingdomLabRules.ParseRegistry(written, out quarantined);
			int at = KingdomLabRules.IndexOfRegistry(rows, Job.JobId);
			return !quarantined && at >= 0 && rows[at].Status == Status
				&& KingdomLabRules.RegistryAuthority(rows[at], expected,
					RequireActive: Status == KingdomLabRegistryStatus.Active);
		}

		private static bool CanonicalAuthority(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			if (The.Game == null || Job == null) return false;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			int at = KingdomLabRules.IndexOfRegistry(rows, Job.JobId);
			return !quarantined && at >= 0 && rows[at].Status == Status
				&& KingdomLabRules.RegistryAuthority(rows[at], RegistryEntry(Job, Status),
					RequireActive: Status == KingdomLabRegistryStatus.Active);
		}

		private static bool RecordReplayProof(string StableId)
		{
			if (The.Game == null || string.IsNullOrEmpty(StableId)) return false;
			string written;
			if (!KingdomLabRules.AddReplayProof(
				The.Game.GetStringGameState(LabReplayState, ""), StableId, out written)) return false;
			The.Game.SetStringGameState(LabReplayState, written);
			bool malformed;
			return string.Equals(The.Game.GetStringGameState(LabReplayState, ""), written,
				StringComparison.Ordinal)
				&& KingdomLabRules.ReplayContains(written, StableId, out malformed) && !malformed;
		}

		private static bool PurgeCanonical(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			if (The.Game == null || Job == null || Status == KingdomLabRegistryStatus.Active
				|| !RecordReplayProof("apply:" + Job.JobId)) return false;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			int at = KingdomLabRules.IndexOfRegistry(rows, Job.JobId);
			if (quarantined || at < 0 || rows[at].Status != Status
				|| !KingdomLabRules.RegistryAuthority(rows[at], RegistryEntry(Job, Status),
					RequireActive: false)
				|| !KingdomLabRules.RemoveRegistry(rows, Job.JobId, Status)) return false;
			string written = KingdomLabRules.FormatRegistry(rows);
			The.Game.SetStringGameState(LabRegistryState, written);
			rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			return !quarantined && KingdomLabRules.IndexOfRegistry(rows, Job.JobId) < 0;
		}

		private static bool PurgeApplicationReceipt(GameObject Building,
			r_KingdomLabJob Job, KingdomLabRegistryStatus Status)
		{
			if (Building == null || Job == null || !ReferenceEquals(Job.ParentObject, Building)
				|| !RecordReplayProof("apply:" + Job.JobId)) return false;
			try { Building.RemovePart(Job); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: exact terminal job cleanup threw (" + ex.Message + ")");
			}
			if (ReferenceEquals(Job.ParentObject, Building)
				|| KingdomProcedures.ReferencePartOrdinal(Building, Job) >= 0) return false;
			return PurgeCanonical(Job, Status);
		}

		private static bool CurrentAuthority(GameObject Building, GameObject Actor,
			KingdomSystem System, r_KingdomLabJob Job, KingdomLabRegistryStatus Status)
		{
			return Building != null && Actor != null && System != null && Job != null && The.Game != null
				&& string.Equals(Actor.ID, Job.PatientId, StringComparison.Ordinal)
				&& string.Equals(Building.ID, Job.BuildingId, StringComparison.Ordinal)
				&& string.Equals(The.Game.GameID, Job.GameId, StringComparison.Ordinal)
				&& string.Equals(RealmIdentity(System), Job.RealmId, StringComparison.Ordinal)
				&& System.FoundedTick == Job.RealmFoundedTick
				&& (Status != KingdomLabRegistryStatus.Active
					|| !KingdomLabRules.ReplayContains(
						The.Game.GetStringGameState(LabReplayState, ""),
						"apply:" + Job.JobId, out _))
				&& CanonicalAuthority(Job, Status);
		}

		private static bool HandleActivePatientRegistry(GameObject Actor, KingdomSystem System)
		{
			if (Actor == null || System == null || The.Game == null) return false;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			if (quarantined)
			{
				Popup.Show("The canonical lab-job registry is malformed. New commissions are blocked; existing physical receipts are untouched.");
				return true;
			}
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomLabRegistryEntry row = rows[i];
				if (row.Status != KingdomLabRegistryStatus.Active
					|| !string.Equals(row.PatientId, Actor.ID, StringComparison.Ordinal)
					|| !string.Equals(row.GameId, The.Game.GameID, StringComparison.Ordinal)
					|| !string.Equals(row.RealmId, RealmIdentity(System), StringComparison.Ordinal)
					|| row.RealmFoundedTick != System.FoundedTick) continue;
				GameObject owner = GameObject.FindByID(row.BuildingId);
				r_KingdomLabJob physical = owner?.GetPart<r_KingdomLabJob>();
				if (GameObject.Validate(owner) && physical != null
					&& string.Equals(physical.JobId, row.JobId, StringComparison.Ordinal))
				{
					Popup.Show("An active commission for this patient belongs to hall {{W|"
						+ row.BuildingId + "}}. Recover or cancel it there; this hall cannot inherit it.");
					return true;
				}
				int choice = Popup.PickOption(Title: "orphaned lab receipt",
					Intro: "The canonical receipt still binds job {{W|" + row.JobId
						+ "}} to missing or unloaded hall {{W|" + row.BuildingId
						+ "}}. No successor hall may assume its payment or body authority.",
					Options: new string[] { "Leave the receipt preserved.",
						"Abandon this receipt; paid costs are not returned." }, AllowEscape: true);
				if (choice != 1) return true;
				string markerKey = PendingProperty(row.ProcedureKey);
				string marker = Actor.GetStringProperty(markerKey);
				if (!string.IsNullOrEmpty(marker)
					&& !string.Equals(marker, row.JobId, StringComparison.Ordinal))
				{
					row.Status = KingdomLabRegistryStatus.Quarantined;
					row.UpdatedTick = Math.Max(0L, The.Game.TimeTicks);
					KingdomLabRules.UpsertRegistry(rows, row);
					The.Game.SetStringGameState(LabRegistryState,
						KingdomLabRules.FormatRegistry(rows));
					Popup.Show("The patient marker belongs to another job. This stale receipt was quarantined and cleared nothing.");
					return true;
				}
				row.Status = KingdomLabRegistryStatus.Abandoned;
				row.UpdatedTick = Math.Max(0L, The.Game.TimeTicks);
				if (!KingdomLabRules.UpsertRegistry(rows, row)) return true;
				string written = KingdomLabRules.FormatRegistry(rows);
				The.Game.SetStringGameState(LabRegistryState, written);
				if (!string.Equals(The.Game.GetStringGameState(LabRegistryState, ""), written,
					StringComparison.Ordinal)) return true;
				if (string.Equals(marker, row.JobId, StringComparison.Ordinal))
					Actor.RemoveStringProperty(markerKey);
				Popup.Show("The orphaned receipt was abandoned. No body effect was applied and no paid cost was returned.");
				return true;
			}
			return false;
		}

		private static LabProcedure FrozenProcedure(r_KingdomLabJob Job)
		{
			if (Job == null || !KingdomLabRules.ValidEffectContract(Job.ContractVersion,
				Job.ProcedureKey, Job.FrozenGrants, Job.FrozenSource, Job.FrozenAttach,
				Job.FrozenManager, Job.FrozenFingerprint, Job.FrozenDetail)) return null;
			return new LabProcedure
			{
				Key = Job.ProcedureKey,
				DisplayName = Job.FrozenName,
				Grants = Job.FrozenGrants,
				Source = (LabSource)Job.FrozenSource,
				Attach = (LabAttach)Job.FrozenAttach,
				Magnitude = Job.FrozenMagnitude,
				Creeds = Job.FrozenCreeds,
				Class = (LabClass)Job.FrozenClass,
				Preserved = Job.KeptOwed,
				StaffDays = Job.FrozenStaffDays
			};
		}

		private static bool ValidApplicationTarget(GameObject Actor, r_KingdomLabJob Job,
			LabProcedure Procedure)
		{
			if (Actor == null || Job == null || Procedure == null
				|| !string.Equals(Actor.ID, Job.PatientId, StringComparison.Ordinal)
				|| !string.Equals(Actor.GetStringProperty(PendingProperty(Job.ProcedureKey)),
					Job.JobId, StringComparison.Ordinal)) return false;
			XRL.World.Anatomy.BodyPart slot = KingdomProcedures.ExactBodyPart(Actor, Job.BodyPartId);
			if (slot == null || slot.Abstract || !KingdomProcedures.BodyOwnsPart(Actor, slot))
				return false;
			GameObject bearer = (Procedure.Attach == LabAttach.Weapon)
				? slot.DefaultBehavior : Actor;
			return GameObject.Validate(bearer)
				&& string.Equals(bearer.ID, Job.BearerId, StringComparison.Ordinal)
				&& (Procedure.Attach != LabAttach.Weapon
					|| ReferenceEquals(slot.DefaultBehavior, bearer))
				&& !KingdomProcedures.HasProcedureClass(Actor, Procedure);
		}

		private static void EnsureJobGovernance(r_KingdomLabJob Job)
		{
			if (Job == null || Job.GovernanceCommitted) return;
			bool durable = Job.WaterPaid > 0 || Job.WaterLost > 0 || Job.WaterQuarantined
				|| Job.KeptPaid > 0 || Job.KeptLost > 0 || Job.KeptQuarantined
				|| !string.Equals(Job.BitOutstanding, Job.BitClaim, StringComparison.Ordinal)
				|| Job.EffectCommitted || (int)Job.State >= (int)KingdomLabJobPhase.Working;
			if (durable && KingdomGovernanceScope.Commit("commission lab procedure"))
				Job.GovernanceCommitted = true;
		}

		private static bool CleanupApplicationMarker(GameObject Actor, r_KingdomLabJob Job)
		{
			if (Actor == null || Job == null
				|| !string.Equals(Actor.ID, Job.PatientId, StringComparison.Ordinal)) return false;
			Job.MarkerCleanupPending = true;
			string key = PendingProperty(Job.ProcedureKey);
			string marker = Actor.GetStringProperty(key);
			if (!string.IsNullOrEmpty(marker)
				&& !string.Equals(marker, Job.JobId, StringComparison.Ordinal))
			{
				Job.SchemaQuarantined = true;
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The patient marker belongs to a different job. This receipt cleared nothing.";
				return false;
			}
			try
			{
				if (string.Equals(marker, Job.JobId, StringComparison.Ordinal))
					Actor.RemoveStringProperty(key);
			}
			catch (Exception ex)
			{
				Job.Fault = "Patient marker cleanup threw and will retry: " + ex.Message;
				return false;
			}
			if (string.Equals(Actor.GetStringProperty(key), Job.JobId, StringComparison.Ordinal))
				return false;
			Job.MarkerCleaned = true;
			return true;
		}

		private static bool FinalizeApplicationProjection(GameObject Actor, r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			if (!CleanupApplicationMarker(Actor, Job)) return false;
			if (!WriteCanonical(Job, Status))
			{
				Job.Fault = "The canonical job registry did not accept terminal cleanup. The hall projection remains for retry.";
				return false;
			}
			Job.RegistryFinalized = true;
			return true;
		}

		private static KingdomLabOwnershipSnapshot RemovalSnapshot(r_KingdomLabRemovalJob Job)
		{
			return new KingdomLabOwnershipSnapshot(Job.ProcedureKey, Job.OriginalJobId,
				Job.PatientId, Job.BodyPartId, Job.BearerId, Job.FrozenGrants,
				Job.FrozenSource, Job.FrozenAttach, Job.FrozenManager, Job.FrozenDetail,
				Job.FrozenFingerprint, Job.PartOrdinal, Job.EffectNonce);
		}

		private static LabProcedure FrozenRemovalProcedure(r_KingdomLabRemovalJob Job)
		{
			if (Job == null || !KingdomLabRules.ValidEffectContract(Job.ContractVersion,
				Job.ProcedureKey, Job.FrozenGrants, Job.FrozenSource, Job.FrozenAttach,
				Job.FrozenManager, Job.FrozenFingerprint, Job.FrozenDetail)) return null;
			return new LabProcedure { Key = Job.ProcedureKey, DisplayName = Job.FrozenName,
				Grants = Job.FrozenGrants, Source = (LabSource)Job.FrozenSource,
				Attach = (LabAttach)Job.FrozenAttach };
		}

		private static bool CurrentRemovalAuthority(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job)
		{
			return Actor != null && System != null && Job != null && The.Game != null
				&& string.Equals(Actor.ID, Job.PatientId, StringComparison.Ordinal)
				&& string.Equals(The.Game.GameID, Job.GameId, StringComparison.Ordinal)
				&& string.Equals(RealmIdentity(System), Job.RealmId, StringComparison.Ordinal)
				&& System.FoundedTick == Job.RealmFoundedTick && !Job.SchemaQuarantined;
		}

		// ==================================================================================
		// Rung 0 — the slab
		// ==================================================================================

		/// <summary>
		/// Reads a carcass, then lets vanilla butcher it.
		/// <para>
		/// The order is the whole point and the precedent wrote the lesson down: the butchering
		/// destroys the source, so <i>nothing useful can be read from the target afterward</i>. The
		/// stamp is taken first, off the whole creature, and travels on whatever comes off.
		/// </para>
		/// </summary>
		internal static void Dress(GameObject Actor)
		{
			if (!KingdomProcedures.Enabled)
			{
				return;
			}
			List<GameObject> carcasses = new List<GameObject>();
			List<string> names = new List<string>();
			foreach (GameObject item in Actor.GetInventoryAndEquipment())
			{
				if (item != null && item.HasPart("Butcherable"))
				{
					carcasses.Add(item);
					names.Add(item.DisplayName);
				}
			}
			if (carcasses.Count == 0)
			{
				// 7b's applicable-but-blocked case: the slab works and there is nothing on it, and
				// nothing else in the game would ever say so.
				Popup.Show("There is nothing on you the slab could open. Bring a carcass home whole.");
				return;
			}
			int picked = Popup.PickOption(Title: "Dress a carcass", Options: names, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject carcass = carcasses[picked];
			string stamp = KingdomProcedures.Stamp(carcass);
			string source = carcass.DisplayNameOnly;
			// Carried on the carcass, so that whatever vanilla's own butchery makes of it inherits
			// the reading through the ordinary property path rather than through a hook of ours.
			carcass.SetStringProperty(KingdomProcedures.StampProperty, stamp);
			carcass.SetStringProperty(KingdomProcedures.SourceProperty, source);
			MessageQueue.AddPlayerMessage(string.IsNullOrEmpty(stamp)
				? ("{{K|There was nothing about " + source + " worth writing down. It is still meat.}}")
				: ("{{G|What " + source + " was carrying is written down. Butcher it, and take what comes off to the vats.}}"));
		}

		// ==================================================================================
		// Rung 1 — the vats
		// ==================================================================================

		/// <summary>
		/// Puts one raw part up to keep.
		/// <para>
		/// The yield is vanilla's own and nothing else: <c>PreservableItem.Number</c> times the
		/// stack (<c>D/XRL/World/Parts/Campfire.cs:543-557</c>). Inventing a multiplier here would
		/// be inventing a second economy on top of one that already works, and the vat-house would
		/// stop being a rendering of the game and start being a machine of ours.
		/// </para>
		/// </summary>
		internal static void Keep(r_KingdomVatHouse Vat, GameObject Actor)
		{
			if (Vat == null || Vat.ParentObject == null || Actor == null)
			{
				return;
			}
			Advance(Vat, The.Game?.TimeTicks ?? Vat.LastWorkedTick);
			GameObject pending = Pending(Vat);
			if (pending != null)
			{
				ManagePending(Vat, Actor, pending);
				return;
			}
			List<GameObject> ready = VatContents(Vat, VatReadyProperty);
			if (ready.Count > 0)
			{
				Collect(Vat, Actor, ready);
				return;
			}
			// The option gates only a new keeping. Existing physical receipts must remain
			// recoverable and collectable after the player turns new lab work off.
			if (!KingdomProcedures.Enabled)
			{
				return;
			}
			List<GameObject> raw = new List<GameObject>();
			List<string> names = new List<string>();
			foreach (GameObject item in Actor.GetInventoryAndEquipment())
			{
				if (item == null || item.GetIntProperty(KeptProperty) == 1
					|| item.GetIntProperty(VatPendingProperty) == 1)
				{
					continue;
				}
				string stamp = item.GetStringProperty(KingdomProcedures.StampProperty);
				if (!string.IsNullOrEmpty(stamp) || item.HasPart("DismemberedProperties"))
				{
					raw.Add(item);
					names.Add(item.DisplayName);
				}
			}
			if (raw.Count == 0)
			{
				Popup.Show("The vats have nothing to work on. Dress a carcass at the slab first — what the vats keep is what the slab took a reading of.");
				return;
			}
			int picked = Popup.PickOption(Title: "Put a part up to keep", Options: names, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject part = raw[picked];
			XRL.World.Parts.PreservableItem preservable = part.GetPart<XRL.World.Parts.PreservableItem>();
			int yield = KingdomProcedureRules.PreservedYield((preservable == null) ? 1 : preservable.Number, part.Count);
			if (yield <= 0)
			{
				Popup.Show("Nothing would come out of the vats for that. It is not the kind of thing that keeps.");
				return;
			}
			if (Popup.ShowYesNoCancel("The vats will keep " + part.DisplayName + " — {{C|" + yield + "}} "
				+ ((yield == 1) ? "part" : "parts") + ", after {{C|" + KingdomProcedureRules.PreserveDays
				+ "}} day of the vat crew's work.\n\nWhat is kept is permanent. It can be stored, traded, or spent at the hall.") != DialogResult.Yes)
			{
				return;
			}
			string stamp2 = part.GetStringProperty(KingdomProcedures.StampProperty);
			string source = part.GetStringProperty(KingdomProcedures.SourceProperty);
			string blueprint = (preservable == null || string.IsNullOrEmpty(preservable.Result)) ? part.Blueprint : preservable.Result;
			if (string.IsNullOrEmpty(blueprint))
			{
				MessageQueue.AddPlayerMessage("{{r|The vats could not make anything of it.}}");
				return;
			}
			string job = string.IsNullOrEmpty(part.ID) ? Guid.NewGuid().ToString("N") : part.ID;
			part.SetIntProperty(VatPendingProperty, 1);
			part.SetIntProperty(VatRemainingProperty,
				KingdomProcedureRules.StaffDayTicks(KingdomProcedureRules.PreserveDays));
			part.SetStringProperty(VatResultProperty, blueprint);
			part.SetIntProperty(VatYieldProperty, yield);
			part.SetStringProperty(VatJobProperty, job);
			part.SetStringProperty(KingdomProcedures.StampProperty, stamp2);
			part.SetStringProperty(KingdomProcedures.SourceProperty, source);
			part.SetIntProperty(VatOutputPhaseProperty, (int)KingdomVatOutputPhase.None);
			part.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.Present);
			part.SetStringProperty(VatRawIdProperty, part.ID ?? "");
			part.SetStringProperty(VatRawBlueprintProperty, part.Blueprint ?? "");
			part.SetIntProperty(VatRawCountProperty, part.Count);
			part.SetStringProperty(VatRawFingerprintProperty,
				KingdomLabRules.VatRawFingerprint(job, part.ID, part.Blueprint, part.Count,
					stamp2, source));
			part.SetStringProperty(VatOwnerIdProperty, Vat.ParentObject.ID ?? "");
			Inventory inventory = Vat.ParentObject.RequirePart<Inventory>();
			inventory.AddObjectToInventory(part, Actor, Silent: true, NoStack: true);
			if (!VatRawReceiptMatches(part, Vat.ParentObject))
			{
				Actor.RequirePart<Inventory>().AddObjectToInventory(part, Actor, Silent: true, NoStack: true);
				ClearPending(part);
				Popup.Show((part.Physics != null && part.Physics.InInventory == Actor)
					? "The vats could not take hold of that part. It is back in your hands; nothing was spent."
					: "The vats could not take hold of that part. Check the ground and your inventory; the raw part was not consumed.");
				return;
			}
			Vat.LastWorkedTick = The.Game?.TimeTicks ?? 0L;
			MessageQueue.AddPlayerMessage("{{G|" + KingdomLabRules.StakedLine("keeping " + source,
				KingdomProcedureRules.PreserveDays) + "}}");
		}

		internal static bool HasPending(r_KingdomVatHouse Vat)
		{
			List<GameObject> contents = Vat?.ParentObject?.Inventory?.Objects;
			for (int i = 0; contents != null && i < contents.Count; i++)
			{
				if (contents[i] != null && contents[i].GetIntProperty(VatPendingProperty) == 1)
				{
					return true;
				}
			}
			return false;
		}

		internal static void Advance(r_KingdomVatHouse Vat, long TimeTick)
		{
			if (Vat == null || Vat.ParentObject == null)
			{
				return;
			}
			RecoverVatReceipts(Vat);
			GameObject input = Pending(Vat);
			if (input == null) return;
			KingdomVatOutputPhase outputPhase = (KingdomVatOutputPhase)
				input.GetIntProperty(VatOutputPhaseProperty);
			KingdomVatRawPhase rawPhaseAtStart = (KingdomVatRawPhase)
				input.GetIntProperty(VatRawPhaseProperty);
			bool frozenOutput = !string.IsNullOrEmpty(
				input.GetStringProperty(VatOutputIdProperty));
			if (!Enum.IsDefined(typeof(KingdomVatOutputPhase), outputPhase)
				|| !Enum.IsDefined(typeof(KingdomVatRawPhase), rawPhaseAtStart)
				|| rawPhaseAtStart != KingdomVatRawPhase.Present
				|| outputPhase == KingdomVatOutputPhase.Quarantined
				|| (outputPhase == KingdomVatOutputPhase.None && frozenOutput)
				|| (outputPhase != KingdomVatOutputPhase.None && !frozenOutput)
				|| !VatRawReceiptMatches(input, Vat.ParentObject))
			{
				QuarantineVatReceipt(input, frozenOutput
					? GameObject.FindByID(input.GetStringProperty(VatOutputIdProperty)) : null);
				return;
			}
			int staffNeeded = Vat.ParentObject.GetIntProperty(KingdomAdopt.StaffNeededProperty);
			int crew = (staffNeeded > 0) ? Vat.ParentObject.GetIntProperty("KingdomEffectiveness") : 100;
			int wear = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Vat.ParentObject));
			KingdomVatAccrual accrual = KingdomLabRules.AccrueVat(Vat.LastWorkedTick, TimeTick,
				input.GetIntProperty(VatRemainingProperty), crew, wear, Settled: false,
				Cancelled: false, IdentityAffinity: KingdomCrews.AffinityOf(Vat.ParentObject));
			Vat.LastWorkedTick = accrual.NextTick;
			input.SetIntProperty(VatRemainingProperty, accrual.RemainingTicks);
			if (crew <= 0 || wear <= 0)
			{
				if (input.GetIntProperty(VatBlockedProperty) == 0)
				{
					input.SetIntProperty(VatBlockedProperty, 1);
					MessageQueue.AddPlayerMessage((crew <= 0)
						? "{{r|The vat-house stands idle. No crew is working the vats; assign hands or free them from other works.}}"
						: "{{r|The vat-house cannot work in its present condition. Mend it, and the crew will take the keeping up again.}}");
				}
				return;
			}
			if (!accrual.Complete)
			{
				input.RemoveIntProperty(VatBlockedProperty);
				return;
			}
			string job = input.GetStringProperty(VatJobProperty);
			GameObject output = OutputFor(Vat, input);
			KingdomVatSettlement settlement = KingdomLabRules.VatSettlement(InputPresent: true,
				OutputPresent: output != null, WorkComplete: true, CancelRequested: false);
			if (settlement == KingdomVatSettlement.CreateOutput)
			{
				if (!string.IsNullOrEmpty(input.GetStringProperty(VatOutputIdProperty)))
				{
					input.SetIntProperty(VatBlockedProperty, 1);
					return;
				}
				output = CreateVatOutput(Vat, input, job);
				if (output == null)
				{
					if (input.GetIntProperty(VatBlockedProperty) == 0)
					{
						input.SetIntProperty(VatBlockedProperty, 1);
						MessageQueue.AddPlayerMessage("{{r|The vats finished their work but could not jar the result. The raw part remains untouched; inspect the vat-house and try again.}}");
					}
					return;
				}
				settlement = KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: true,
					WorkComplete: true, CancelRequested: false);
			}
			if (settlement != KingdomVatSettlement.ConsumeInput)
			{
				return;
			}
			if (!VatRawReceiptMatches(input, Vat.ParentObject)
				|| !VatOutputMatches(output, input, job, VatFingerprint(input, job),
				Vat.ParentObject) || output.GetIntProperty(VatOutputPhaseProperty)
					!= (int)KingdomVatOutputPhase.Added)
			{
				QuarantineVatReceipt(input, output);
				return;
			}
			// Freeze the destructive intent on both objects. If execution stops after this
			// point, recovery observes the exact raw identity once and never calls Obliterate again.
			input.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.DestroyIntent);
			output.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.DestroyIntent);
			try { input.Obliterate(); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: vat raw destruction intent threw (" + ex.Message + ")");
			}
			bool outputExact = VatOutputReceiptMatches(output, Vat.ParentObject);
			KingdomVatRawPhase rawPhase = KingdomLabRules.ResumeVatRaw(
				KingdomVatRawPhase.DestroyIntent, GameObject.Validate(input), outputExact);
			output.SetIntProperty(VatRawPhaseProperty, (int)rawPhase);
			if (GameObject.Validate(input)) input.SetIntProperty(VatRawPhaseProperty, (int)rawPhase);
			if (rawPhase != KingdomVatRawPhase.Destroyed)
			{
				QuarantineVatReceipt(GameObject.Validate(input) ? input : null, output);
				if (GameObject.Validate(input) && input.GetIntProperty(VatBlockedProperty) == 0)
				{
					input.SetIntProperty(VatBlockedProperty, 1);
					MessageQueue.AddPlayerMessage("{{r|The vats have sealed the result but cannot release the raw part. Both remain in the vat-house; collect nothing until the obstruction is cleared.}}");
				}
				return;
			}
			output.SetIntProperty(VatReadyProperty, 1);
			Vat.LastWorkedTick = 0L;
			MessageQueue.AddPlayerMessage("{{G|The vat-house has finished its keeping. The sealed parts wait there for collection.}}");
		}

		private static GameObject CreateVatOutput(r_KingdomVatHouse Vat, GameObject Input, string Job)
		{
			string blueprint = Input.GetStringProperty(VatResultProperty);
			int yield = Input.GetIntProperty(VatYieldProperty);
			if (string.IsNullOrEmpty(blueprint) || yield <= 0)
			{
				return null;
			}
			GameObject kept = GameObject.Create(blueprint);
			if (kept == null || string.IsNullOrEmpty(kept.ID))
			{
				return null;
			}
			string fingerprint = VatFingerprint(Input, Job);
			kept.Count = yield;
			kept.SetIntProperty(KeptProperty, 1);
			kept.SetStringProperty(VatOutputJobProperty, Job);
			kept.SetStringProperty(VatOutputFingerprintProperty, fingerprint);
			kept.SetStringProperty(KingdomProcedures.StampProperty,
				Input.GetStringProperty(KingdomProcedures.StampProperty));
			kept.SetStringProperty(KingdomProcedures.SourceProperty,
				Input.GetStringProperty(KingdomProcedures.SourceProperty));
			// Freeze identity before the first transfer callback. From here on, retry may
			// resolve/re-home this object only; it may never create a replacement.
			Input.SetStringProperty(VatOutputIdProperty, kept.ID);
			kept.SetStringProperty(VatOutputIdProperty, kept.ID);
			Input.SetStringProperty(VatOutputFingerprintProperty, fingerprint);
			Input.SetIntProperty(VatOutputPhaseProperty, (int)KingdomVatOutputPhase.AddIntent);
			kept.SetIntProperty(VatOutputPhaseProperty, (int)KingdomVatOutputPhase.AddIntent);
			kept.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.Present);
			kept.SetStringProperty(VatRawIdProperty, Input.GetStringProperty(VatRawIdProperty));
			kept.SetStringProperty(VatRawBlueprintProperty,
				Input.GetStringProperty(VatRawBlueprintProperty));
			kept.SetIntProperty(VatRawCountProperty, Input.GetIntProperty(VatRawCountProperty));
			kept.SetStringProperty(VatRawFingerprintProperty,
				Input.GetStringProperty(VatRawFingerprintProperty));
			kept.SetStringProperty(VatOwnerIdProperty, Vat.ParentObject.ID ?? "");
			if (!string.Equals(Input.GetStringProperty(VatOutputIdProperty), kept.ID,
				StringComparison.Ordinal)) return null;
			try
			{
				Vat.ParentObject.RequirePart<Inventory>().AddObject(kept, null,
					Silent: true, NoStack: true);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: vat output add intent threw (" + ex.Message + ")");
			}
			KingdomVatOutputPhase phase = KingdomLabRules.ResumeVatOutput(
				KingdomVatOutputPhase.AddIntent,
				VatOutputMatches(kept, Input, Job, fingerprint, Vat.ParentObject));
			Input.SetIntProperty(VatOutputPhaseProperty, (int)phase);
			kept.SetIntProperty(VatOutputPhaseProperty, (int)phase);
			if (phase != KingdomVatOutputPhase.Added)
			{
				QuarantineVatReceipt(Input, kept);
				return null;
			}
			return kept;
		}

		private static void ManagePending(r_KingdomVatHouse Vat, GameObject Actor, GameObject Input)
		{
			int remaining = Input.GetIntProperty(VatRemainingProperty);
			GameObject output = OutputFor(Vat, Input);
			if (output != null)
			{
				Popup.Show("The keeping is finished and its sealed result is already in the vat-house, but the raw part has not released. Nothing can be collected or cancelled until the obstruction is cleared.");
				return;
			}
			if (!string.IsNullOrEmpty(Input.GetStringProperty(VatOutputIdProperty)))
			{
				Popup.Show("The vat has frozen an exact output identity, but that same object is missing or no longer matches its receipt. The raw input and receipt are quarantined; cancellation cannot create or return a duplicate.");
				return;
			}
			int crew = Vat.ParentObject.GetIntProperty("KingdomEffectiveness");
			int wear = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Vat.ParentObject));
			int whole = KingdomProcedureRules.StaffDayTicks(KingdomProcedureRules.PreserveDays);
			long earned = (long)whole - remaining;
			int done = (whole > 0) ? (int)(earned * 100L / whole) : 100;
			if (done < 0)
			{
				done = 0;
			}
			else if (done > 100)
			{
				done = 100;
			}
			string state;
			if (crew <= 0)
			{
				state = "{{r|Nobody is working the vats. No idle time has counted as work.}}";
			}
			else if (wear <= 0)
			{
				state = "{{r|The vat-house needs mending before the crew can continue.}}";
			}
			else
			{
				state = "The keeping is {{C|" + done + "%}} done, and the crew is working.";
			}
			int picked = Popup.PickOption(Title: "The vat-house",
				Intro: Input.DisplayName + " is still in the vats. " + state,
				Options: new string[2] { "Leave it in the crew's hands.", "Cancel the keeping and take the raw part back." },
				AllowEscape: true);
			if (picked != 1 || Popup.ShowYesNo("Cancel this keeping? The raw part returns unchanged, and the work already spent is lost.") != DialogResult.Yes)
			{
				return;
			}
			if (KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: false,
				WorkComplete: remaining <= 0, CancelRequested: true) != KingdomVatSettlement.ReturnInput)
			{
				return;
			}
			Actor.RequirePart<Inventory>().AddObjectToInventory(Input, Actor, Silent: true, NoStack: true);
			if (Input.Physics == null || Input.Physics.InInventory != Actor)
			{
				Vat.ParentObject.RequirePart<Inventory>().AddObjectToInventory(Input, Actor, Silent: true, NoStack: true);
				Popup.Show("The vat-house could not hand the part back. The raw part was not consumed; inspect the vats before trying again.");
				return;
			}
			ClearPending(Input);
			Vat.LastWorkedTick = 0L;
			MessageQueue.AddPlayerMessage("{{K|The keeping was cancelled. The raw part is back in your hands.}}");
		}

		private static void Collect(r_KingdomVatHouse Vat, GameObject Actor, List<GameObject> Ready)
		{
			int taken = 0;
			Inventory inventory = Actor.RequirePart<Inventory>();
			for (int i = 0; i < Ready.Count; i++)
			{
				GameObject output = Ready[i];
				if (output.GetIntProperty(VatOutputPhaseProperty)
						!= (int)KingdomVatOutputPhase.Added
					|| output.GetIntProperty(VatRawPhaseProperty)
						!= (int)KingdomVatRawPhase.Destroyed
					|| !VatOutputReceiptMatches(output, Vat.ParentObject))
				{
					QuarantineVatReceipt(null, output);
					continue;
				}
				inventory.AddObjectToInventory(output, Actor, Silent: true, NoStack: true);
				if (output.Physics != null && output.Physics.InInventory == Actor)
				{
					output.RemoveIntProperty(VatReadyProperty);
					output.RemoveStringProperty(VatOutputJobProperty);
					output.RemoveStringProperty(VatOwnerIdProperty);
					taken += output.Count;
				}
				else
				{
					Vat.ParentObject.RequirePart<Inventory>().AddObjectToInventory(output, Actor,
						Silent: true, NoStack: true);
				}
			}
			if (taken > 0)
			{
				MessageQueue.AddPlayerMessage("{{G|You collect " + taken + " kept "
					+ ((taken == 1) ? "part" : "parts") + " from the vat-house.}}");
			}
			else
			{
				Popup.Show("The sealed parts could not be handed over. They remain in the vat-house.");
			}
		}

		private static GameObject Pending(r_KingdomVatHouse Vat)
		{
			List<GameObject> contents = Vat?.ParentObject?.Inventory?.Objects;
			for (int i = 0; contents != null && i < contents.Count; i++)
			{
				if (contents[i] != null && contents[i].GetIntProperty(VatPendingProperty) == 1)
				{
					return contents[i];
				}
			}
			return null;
		}

		private static GameObject OutputFor(r_KingdomVatHouse Vat, GameObject Input)
		{
			if (Input == null) return null;
			string job = Input.GetStringProperty(VatJobProperty);
			string expected = VatFingerprint(Input, job);
			string frozenId = Input.GetStringProperty(VatOutputIdProperty);
			if (!string.IsNullOrEmpty(frozenId))
			{
				GameObject exact = GameObject.FindByID(frozenId);
				bool matches = VatOutputMatches(exact, Input, job, expected, Vat?.ParentObject);
				KingdomVatOutputDecision decision = KingdomLabRules.VatOutputIdentity(
					FrozenId: true, Resolved: GameObject.Validate(exact),
					FingerprintMatches: matches);
				if (decision != KingdomVatOutputDecision.UseExact)
				{
					QuarantineVatReceipt(Input, exact);
					return null;
				}
				KingdomVatOutputPhase phase = (KingdomVatOutputPhase)
					Input.GetIntProperty(VatOutputPhaseProperty);
				if (!Enum.IsDefined(typeof(KingdomVatOutputPhase), phase)
					|| phase == KingdomVatOutputPhase.Quarantined)
				{
					QuarantineVatReceipt(Input, exact);
					return null;
				}
				if (phase == KingdomVatOutputPhase.AddIntent)
				{
					phase = KingdomLabRules.ResumeVatOutput(phase, matches);
					Input.SetIntProperty(VatOutputPhaseProperty, (int)phase);
					exact.SetIntProperty(VatOutputPhaseProperty, (int)phase);
				}
				if (phase != KingdomVatOutputPhase.Added) return null;
				return exact;
			}
			// A pre-receipt output may be inspected but is never adopted by job/class/ordinal.
			// Only a new job with no output intent reaches CreateVatOutput.
			return null;
		}

		private static void QuarantineVatReceipt(GameObject Input, GameObject Output)
		{
			if (GameObject.Validate(Input))
			{
				Input.SetIntProperty(VatOutputPhaseProperty,
					(int)KingdomVatOutputPhase.Quarantined);
				Input.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.Quarantined);
				Input.SetIntProperty(VatBlockedProperty, 1);
			}
			if (GameObject.Validate(Output))
			{
				Output.SetIntProperty(VatOutputPhaseProperty,
					(int)KingdomVatOutputPhase.Quarantined);
				Output.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.Quarantined);
				Output.RemoveIntProperty(VatReadyProperty);
				Output.SetIntProperty(VatBlockedProperty, 1);
			}
		}

		private static string VatFingerprint(GameObject Input, string Job)
		{
			return KingdomLabRules.VatOutputFingerprint(Job,
				Input?.GetStringProperty(VatResultProperty), Input?.GetIntProperty(VatYieldProperty) ?? 0,
				Input?.GetStringProperty(KingdomProcedures.StampProperty),
				Input?.GetStringProperty(KingdomProcedures.SourceProperty));
		}

		private static bool VatOutputMatches(GameObject Output, GameObject Input, string Job,
			string Fingerprint, GameObject VatOwner)
		{
			return GameObject.Validate(Output) && Output.GetIntProperty(KeptProperty) == 1
				&& GameObject.Validate(Input) && GameObject.Validate(VatOwner)
				&& Output.Physics != null && ReferenceEquals(Output.Physics.InInventory, VatOwner)
				&& string.Equals(Output.GetStringProperty(VatOwnerIdProperty), VatOwner.ID,
					StringComparison.Ordinal)
				&& !string.IsNullOrEmpty(Output.ID)
				&& string.Equals(Output.GetStringProperty(VatOutputIdProperty), Output.ID,
					StringComparison.Ordinal)
				&& string.Equals(Input.GetStringProperty(VatOutputIdProperty), Output.ID,
					StringComparison.Ordinal)
				&& string.Equals(Output.GetStringProperty(VatOutputJobProperty), Job,
					StringComparison.Ordinal)
				&& string.Equals(Output.GetStringProperty(VatOutputFingerprintProperty),
					Fingerprint, StringComparison.Ordinal)
				&& string.Equals(Input.GetStringProperty(VatOutputFingerprintProperty),
					Fingerprint, StringComparison.Ordinal)
				&& Output.Count == Input.GetIntProperty(VatYieldProperty)
				&& string.Equals(Output.Blueprint, Input.GetStringProperty(VatResultProperty),
					StringComparison.Ordinal);
		}

		private static bool VatOutputReceiptMatches(GameObject Output, GameObject VatOwner)
		{
			if (!GameObject.Validate(Output) || !GameObject.Validate(VatOwner)
				|| Output.Physics == null || !ReferenceEquals(Output.Physics.InInventory, VatOwner)
				|| Output.GetIntProperty(KeptProperty) != 1
				|| Output.GetIntProperty(VatOutputPhaseProperty)
					!= (int)KingdomVatOutputPhase.Added
				|| string.IsNullOrEmpty(Output.ID)
				|| !string.Equals(Output.GetStringProperty(VatOutputIdProperty), Output.ID,
					StringComparison.Ordinal)
				|| !string.Equals(Output.GetStringProperty(VatOwnerIdProperty), VatOwner.ID,
					StringComparison.Ordinal)) return false;
			string job = Output.GetStringProperty(VatOutputJobProperty);
			string fingerprint = KingdomLabRules.VatOutputFingerprint(job, Output.Blueprint,
				Output.Count, Output.GetStringProperty(KingdomProcedures.StampProperty),
				Output.GetStringProperty(KingdomProcedures.SourceProperty));
			return !string.IsNullOrEmpty(job)
				&& string.Equals(Output.GetStringProperty(VatOutputFingerprintProperty),
					fingerprint, StringComparison.Ordinal)
				&& string.Equals(Output.GetStringProperty(VatRawFingerprintProperty),
					KingdomLabRules.VatRawFingerprint(job,
						Output.GetStringProperty(VatRawIdProperty),
						Output.GetStringProperty(VatRawBlueprintProperty),
						Output.GetIntProperty(VatRawCountProperty),
						Output.GetStringProperty(KingdomProcedures.StampProperty),
						Output.GetStringProperty(KingdomProcedures.SourceProperty)),
					StringComparison.Ordinal);
		}

		private static bool VatRawReceiptMatches(GameObject Raw, GameObject VatOwner)
		{
			if (!GameObject.Validate(Raw) || !GameObject.Validate(VatOwner)
				|| Raw.Physics == null || !ReferenceEquals(Raw.Physics.InInventory, VatOwner)
				|| string.IsNullOrEmpty(Raw.ID)
				|| !string.Equals(Raw.GetStringProperty(VatRawIdProperty), Raw.ID,
					StringComparison.Ordinal)
				|| !string.Equals(Raw.GetStringProperty(VatOwnerIdProperty), VatOwner.ID,
					StringComparison.Ordinal)
				|| !string.Equals(Raw.GetStringProperty(VatRawBlueprintProperty), Raw.Blueprint,
					StringComparison.Ordinal)
				|| Raw.GetIntProperty(VatRawCountProperty) != Raw.Count) return false;
			string job = Raw.GetStringProperty(VatJobProperty);
			return !string.IsNullOrEmpty(job)
				&& string.Equals(Raw.GetStringProperty(VatRawFingerprintProperty),
					KingdomLabRules.VatRawFingerprint(job, Raw.ID, Raw.Blueprint, Raw.Count,
						Raw.GetStringProperty(KingdomProcedures.StampProperty),
						Raw.GetStringProperty(KingdomProcedures.SourceProperty)),
					StringComparison.Ordinal);
		}

		private static void RecoverVatReceipts(r_KingdomVatHouse Vat)
		{
			List<GameObject> contents = Vat?.ParentObject?.Inventory?.Objects;
			for (int i = 0; contents != null && i < contents.Count; i++)
			{
				GameObject output = contents[i];
				if (output == null || string.IsNullOrEmpty(
					output.GetStringProperty(VatOutputJobProperty))) continue;
				KingdomVatRawPhase phase = (KingdomVatRawPhase)
					output.GetIntProperty(VatRawPhaseProperty);
				if (phase != KingdomVatRawPhase.DestroyIntent) continue;
				GameObject raw = GameObject.FindByID(output.GetStringProperty(VatRawIdProperty));
				phase = KingdomLabRules.ResumeVatRaw(phase, GameObject.Validate(raw),
					VatOutputReceiptMatches(output, Vat.ParentObject));
				output.SetIntProperty(VatRawPhaseProperty, (int)phase);
				if (phase == KingdomVatRawPhase.Destroyed)
				{
					output.SetIntProperty(VatReadyProperty, 1);
				}
				else
				{
					QuarantineVatReceipt(raw, output);
				}
			}
		}

		private static List<GameObject> VatContents(r_KingdomVatHouse Vat, string Marker)
		{
			List<GameObject> result = new List<GameObject>();
			List<GameObject> contents = Vat?.ParentObject?.Inventory?.Objects;
			for (int i = 0; contents != null && i < contents.Count; i++)
			{
				if (contents[i] != null && contents[i].GetIntProperty(Marker) == 1)
				{
					result.Add(contents[i]);
				}
			}
			return result;
		}

		private static void ClearPending(GameObject Input)
		{
			Input.RemoveIntProperty(VatPendingProperty);
			Input.RemoveIntProperty(VatRemainingProperty);
			Input.RemoveStringProperty(VatResultProperty);
			Input.RemoveIntProperty(VatYieldProperty);
			Input.RemoveStringProperty(VatJobProperty);
			Input.RemoveStringProperty(VatOutputIdProperty);
			Input.RemoveStringProperty(VatOutputFingerprintProperty);
			Input.RemoveIntProperty(VatOutputPhaseProperty);
			Input.RemoveIntProperty(VatRawPhaseProperty);
			Input.RemoveStringProperty(VatRawIdProperty);
			Input.RemoveStringProperty(VatRawBlueprintProperty);
			Input.RemoveIntProperty(VatRawCountProperty);
			Input.RemoveStringProperty(VatRawFingerprintProperty);
			Input.RemoveStringProperty(VatOwnerIdProperty);
			Input.RemoveIntProperty(VatBlockedProperty);
		}

		// ==================================================================================
		// Rungs 2 and 3 — the slate
		// ==================================================================================

		/// <summary>
		/// Level one of the slate: the founder's own body, place by place, and what is on each.
		/// <para>
		/// Straight from the golem mound's own option list &mdash; the marks, the sentinel rows, the
		/// escape &mdash; because that screen is the canon for exactly this act and re-inventing it
		/// would cost the player their familiarity for nothing.
		/// </para>
		/// </summary>
		internal static void OpenSlate(GameObject Building, GameObject Actor)
		{
			if (Actor == null || Building == null) return;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (system == null || !system.Founded)
			{
				return;
			}
			r_KingdomLabRemovalJob removal = ActiveRemovalJob(Actor);
			if (removal != null)
			{
				if (!string.Equals(removal.PatientId, Actor.ID, StringComparison.Ordinal))
				{
					removal.State = KingdomLabRemovalPhase.Quarantined;
					removal.Fault = "This removal receipt belongs to another patient. It offers no action here.";
					Popup.Show(removal.Fault);
					return;
				}
				ManageRemoval(Actor, system, removal);
				return;
			}
			r_KingdomLabJob existing = Building?.GetPart<r_KingdomLabJob>();
			if (existing != null)
			{
				if (!string.Equals(existing.PatientId, Actor.ID, StringComparison.Ordinal))
				{
					existing.State = KingdomLabJobPhase.ApplicationRecovery;
					existing.Fault = "This hall's commission belongs to another patient. No payment, cancellation, application, or cleanup is offered.";
					Popup.Show(existing.Fault);
					return;
				}
				ManageJob(Building, Actor, system, existing);
				return;
			}
			if (HandleActivePatientRegistry(Actor, system)) return;
			// Turning the feature off refuses only a new commission. Existing application,
			// removal, registry and terminal outbox cleanup above must always keep running.
			if (!KingdomProcedures.Enabled) return;
			string city = system.SeatName;
			int rung = RungAt(Building);
			List<GameObject> kept = KeptParts(Actor);
			// Read once per slate rather than once per row: the roster is the same for every place
			// on the founder's body, and it is one city's rolls, not a per-procedure question.
			List<string> roster = KingdomZoning.Roster(system);
			r_KingdomLabRecord record = KingdomProcedures.Record(Actor);
			record.Normalize();
			List<string> names = new List<string>();
			List<LabSlot> anatomy = KingdomProcedures.Census(Actor, names);
			while (true)
			{
				List<string> options = new List<string>();
				List<int> slotIndex = new List<int>();
				List<string> directRemoval = new List<string>();
				List<XRL.World.Anatomy.BodyPart> live = Actor.Body?.GetParts();
				for (int receipt = 0; receipt < record.Keys.Count; receipt++)
				{
					int bodyId = record.BodyPartIds[receipt];
					XRL.World.Anatomy.BodyPart detached = KingdomProcedures.ExactBodyPart(Actor, bodyId);
					if (bodyId <= 0 || detached == null || ContainsBodyReference(live, detached)) continue;
					string label = (receipt < record.DisplayNames.Count
						&& !string.IsNullOrEmpty(record.DisplayNames[receipt]))
						? record.DisplayNames[receipt] : record.Keys[receipt];
					options.Add("{{M|detached graft receipt}} #" + bodyId + "  " + label);
					slotIndex.Add(-1);
					directRemoval.Add(record.Keys[receipt]);
				}
				for (int i = 0; i < anatomy.Count; i++)
				{
					List<LabProcedure> offers = Candidates(anatomy, i, rung, kept, record, roster);
					XRL.World.Anatomy.BodyPart exactPart = SelectedPart(Actor, i);
					string grafted = record.GraftedAt(exactPart?.ID ?? 0, anatomy[i].Type);
					if (offers.Count == 0 && grafted == null)
					{
						// A place the hall could never do anything with is not a row. A slate that
						// listed all 157 body-part types would be a slate nobody reads.
						continue;
					}
					LabProcedure held;
					options.Add(KingdomLabRules.SlotRow(names[i],
						(grafted != null && KingdomProcedures.TryGet(grafted, out held)) ? held.Named : null,
						offers.Count > 0));
					slotIndex.Add(i);
					directRemoval.Add(null);
				}
				string gap = KingdomLabRules.LadderGapLine(rung >= KingdomProcedureRules.RungSlab,
					rung >= KingdomProcedureRules.RungVat, rung >= KingdomProcedureRules.RungHall,
					rung >= KingdomProcedureRules.RungTheatre);
				if (options.Count == 0)
				{
					Popup.Show(gap ?? ("The hall has nothing it could do to you today, and it says so rather than "
						+ "showing you an empty list. Bring something to the vats, or raise the hall higher."));
					return;
				}
				int picked = Popup.PickOption(
					Title: KingdomLabRules.SlateTitle(KingdomPresentation.Rich(city)),
					Intro: KingdomLabRules.SlateIntro(
						KingdomPresentation.Rich(SavantAt(system)), null, TotalKept(kept))
						+ ((gap == null) ? "" : ("\n" + gap)),
					Options: options, AllowEscape: true, RespectOptionNewlines: true);
				if (picked < 0)
				{
					return;
				}
				int at = slotIndex[picked];
				if (!string.IsNullOrEmpty(directRemoval[picked]))
				{
					OfferRemoval(Actor, system, directRemoval[picked], city);
					names = new List<string>();
					anatomy = KingdomProcedures.Census(Actor, names);
					kept = KeptParts(Actor);
					if (KingdomGovernanceScope.HasCommitted) return;
					continue;
				}
				XRL.World.Anatomy.BodyPart exactStandingPart = SelectedPart(Actor, at);
				string standing = record.GraftedAt(exactStandingPart?.ID ?? 0, anatomy[at].Type);
				if (standing != null)
				{
					OfferRemoval(Actor, system, standing, city);
				}
				else
				{
					OfferProcedure(Building, Actor, system, anatomy, at, names[at], rung, kept, record, roster, city);
				}
				// The body may have changed under us, so it is read again rather than patched.
				names = new List<string>();
				anatomy = KingdomProcedures.Census(Actor, names);
					kept = KeptParts(Actor);
					if (KingdomGovernanceScope.HasCommitted)
					{
						return;
					}
				}
		}

		private static r_KingdomLabRemovalJob ActiveRemovalJob(GameObject Actor)
		{
			IList<IPart> parts = Actor?.PartsList;
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				r_KingdomLabRemovalJob job = parts[i] as r_KingdomLabRemovalJob;
				if (job == null) continue;
				job.Normalize();
				if (job.State != KingdomLabRemovalPhase.Complete
					&& job.State != KingdomLabRemovalPhase.Cancelled) return job;
				if (!string.Equals(job.PatientId, Actor.ID, StringComparison.Ordinal)
					|| job.SchemaQuarantined) return job;
				KingdomLabOwnedTarget ignored;
				KingdomLabOwnedTargetState observed = KingdomProcedures.ClassifyOwned(Actor,
					RemovalSnapshot(job), out ignored);
				if (observed == KingdomLabOwnedTargetState.Absent) continue;
				if (job.State == KingdomLabRemovalPhase.Complete
					&& observed == KingdomLabOwnedTargetState.Present)
				{
					job.State = KingdomLabRemovalPhase.RemovalRecovery;
					job.Fault = "The exact removed effect is present again. Its paid receipt was reopened and will charge no more water.";
				}
				else
				{
					job.State = KingdomLabRemovalPhase.Quarantined;
					job.Fault = observed == KingdomLabOwnedTargetState.Present
						? "An effect returned after a clean cancelled receipt. No procedure debt or removal authority was inferred."
						: "Terminal physical state is uncertain. The archived receipt was reopened only to quarantine it.";
				}
				return job;
			}
			return null;
		}

		private static int RemovalReceiptCount(GameObject Actor)
		{
			int count = 0;
			IList<IPart> parts = Actor?.PartsList;
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				if (parts[i] is r_KingdomLabRemovalJob) count++;
			}
			return count;
		}

		private static int CountParts<T>(GameObject Object) where T : IPart
		{
			int count = 0;
			IList<IPart> parts = Object?.PartsList;
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				if (parts[i] is T) count++;
			}
			return count;
		}

		/// <summary>
		/// Level two: what the hall could do at one place, each with its effects and its whole price
		/// stated before anything is committed.
		/// </summary>
		private static void OfferProcedure(GameObject Building, GameObject Actor, KingdomSystem System,
			List<LabSlot> Anatomy, int At,
			string SlotName, int Rung, List<GameObject> Kept, r_KingdomLabRecord Record, List<string> Roster, string City)
		{
			List<LabProcedure> offers = Candidates(Anatomy, At, Rung, Kept, Record, Roster);
			if (offers.Count == 0)
			{
				Popup.ShowFail(KingdomLabRules.NothingMeetsRequirement(SlotName));
				return;
			}
			List<string> rows = new List<string>();
			for (int i = 0; i < offers.Count; i++)
			{
				rows.Add(KingdomLabRules.CandidateRow(offers[i], CountFor(Kept, offers[i])));
			}
			int picked = Popup.PickOption(Title: "Choose a procedure for " + SlotName,
				Options: rows, AllowEscape: true, RespectOptionNewlines: true);
			if (picked < 0)
			{
				return;
			}
			LabProcedure procedure = offers[picked];
			int consent = Popup.PickOption(
				Title: procedure.Named,
				Intro: KingdomLabRules.PriceLine(procedure) + "\n" + KingdomLabRules.ReversibilityLine(),
				Options: KingdomLabRules.ConsentOptions, AllowEscape: true);
			if (consent == 2)
			{
				Record.Exclude(procedure.Key);
				MessageQueue.AddPlayerMessage("{{K|The hall will not offer that again.}}");
				return;
			}
			if (consent != 0)
			{
				return;
			}
			Commission(Building, Actor, System, procedure, Anatomy, At, Kept, City);
		}

		/// <summary>
		/// Takes the drams, spends the kept parts, pays the standing, and performs the work.
		/// <para>
		/// <b>The whole verdict is asked AGAIN here</b>, and not because the slate might be wrong:
		/// because the founder may have walked away, come back a season later, and had the answer
		/// change under them. A commit that trusts the screen that opened it is a commit that will
		/// one day take a founder's water for a thing it cannot do.
		/// </para>
		/// </summary>
		private static void Commission(GameObject Building, GameObject Actor, KingdomSystem System, LabProcedure Procedure,
			List<LabSlot> Anatomy, int At, List<GameObject> Kept, string City)
		{
			if (Building == null || Building.GetPart<r_KingdomLabJob>() != null
				|| ActiveRemovalJob(Actor) != null)
			{
				Popup.Show("This hall already owns a commission. Inspect its slate first.");
				return;
			}
			string realmId = RealmIdentity(System);
			if (!KingdomIdentityRules.IsRealmId(realmId))
			{
				Popup.Show("The realm's immutable identity cannot be proved. No commission or charge was started.");
				return;
			}
			List<int> categories = KingdomProcedures.Categories(Procedure);
			LabVerdict verdict = KingdomProcedureRules.JudgeSlot(Procedure, Anatomy[At], categories);
			if (verdict != LabVerdict.Allowed)
			{
				Popup.Show(KingdomProcedureRules.RefusalLine(verdict, Procedure));
				return;
			}
			GameObject source = FirstSourceFor(Kept, Procedure);
			if (source == null || CountFor(Kept, Procedure) < Procedure.Preserved)
			{
				Popup.Show(KingdomProcedureRules.RefusalLine(LabVerdict.RefusedUnkept, Procedure));
				return;
			}
			string stamp = source.GetStringProperty(KingdomProcedures.StampProperty);
			KeptSpendPreparation keptSpend;
			KingdomKeptSpendPhase keptPhase = PrepareKeptSpend(Kept, Procedure, out keptSpend);
			if (keptPhase != KingdomKeptSpendPhase.ApplyCounts)
			{
				Popup.Show(keptPhase == KingdomKeptSpendPhase.RefusedClean
					? "The kept parts would not agree to be spent. Nothing else was changed."
					: "A kept stack changed while the hall asked whether every source could release. No water was spent and no graft was made; inspect your kept parts before trying again.");
				return;
			}
			if (KingdomProcedures.HasProcedureClass(Actor, Procedure))
			{
				Popup.Show("That procedure already exists somewhere on you. The hall will not commission a second live instance. Nothing was spent.");
				return;
			}
			XRL.World.Anatomy.BodyPart selected = SelectedPart(Actor, At);
			GameObject bearer = (Procedure.Attach == LabAttach.Weapon) ? selected?.DefaultBehavior : Actor;
			if (selected == null || !GameObject.Validate(bearer))
			{
				Popup.Show("The selected body part changed before the commission could be recorded. Nothing was spent.");
				return;
			}
			string pendingProperty = PendingProperty(Procedure.Key);
			if (!string.IsNullOrEmpty(Actor.GetStringProperty(pendingProperty)))
			{
				Popup.Show("A live commission for that procedure already follows you. Recover it before commissioning another.");
				return;
			}
			KingdomSurvey survey = (Actor.CurrentZone == null) ? null : KingdomSurvey.Take(Actor.CurrentZone, System);
			KingdomWaterDebit debit;
			if (survey == null || !survey.TryReserveExactWater(Procedure.Cost, out debit))
			{
				Popup.Show("The stores at " + KingdomLabRules.Named(
					KingdomPresentation.Rich(City)) + " cannot spare {{C|" + Procedure.Cost
					+ "}} drams. Fill them, and the hall will take the work on.");
				return;
			}
			KingdomBitTally bitCost;
			string bitError;
			if (!KingdomMaterialRules.TryParseBitCost(Procedure.Bits, out bitCost, out bitError))
			{
				Popup.Show("The procedure's bit price is invalid (" + bitError + "). Nothing was spent.");
				return;
			}
			KingdomMaterialDebit bitDebit = bitCost.IsEmpty() ? null
				: KingdomMaterials.ReserveBits(Actor.CurrentZone, bitCost);
			if (!bitCost.IsEmpty() && (bitDebit == null
				|| bitDebit.Reservation.Outcome != KingdomMaterialDebitOutcome.Reserved))
			{
				Popup.Show("The settlement's dedicated stockpiles cannot cover that exact bit price. Nothing was spent.");
				return;
			}
			string jobId = Guid.NewGuid().ToString("N");
			string manager = KingdomProcedures.ManagerFor(Procedure.Key);
			string detail = KingdomProcedures.ExecutionDetail(Procedure, stamp);
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, Procedure.Key, Procedure.Grants,
				(int)Procedure.Source, (int)Procedure.Attach, manager, detail);
			r_KingdomLabJob job = new r_KingdomLabJob
			{
				JobId = jobId,
				BuildingId = Building.ID,
				ProcedureKey = Procedure.Key,
				PatientId = Actor.ID,
				GameId = The.Game?.GameID ?? "",
				RealmId = realmId,
				RealmFoundedTick = System.FoundedTick,
				BodyPartId = selected.ID,
				BearerId = bearer.ID,
				Stamp = stamp,
				City = City ?? "",
				ContractVersion = KingdomLabRules.EffectContractVersion,
				FrozenName = Procedure.Named,
				FrozenGrants = Procedure.Grants,
				FrozenSource = (int)Procedure.Source,
				FrozenAttach = (int)Procedure.Attach,
				FrozenManager = manager,
				FrozenDetail = detail,
				FrozenMagnitude = Procedure.Magnitude ?? "",
				FrozenCreeds = Procedure.Creeds ?? "",
				FrozenClass = (int)Procedure.Class,
				FrozenStaffDays = Procedure.StaffDays,
				FrozenFingerprint = fingerprint,
				Phase = (int)KingdomLabJobPhase.Funding,
				RemainingTicks = KingdomProcedureRules.StaffDayTicks(Procedure.StaffDays),
				LastWorkedTick = The.Game?.TimeTicks ?? 0L,
				WaterOwed = Procedure.Cost,
				KeptOwed = Procedure.Preserved,
				BitClaim = bitCost.IsEmpty() ? "" : bitDebit.Reservation.Requested.ToClaimString(),
				BitOutstanding = bitCost.IsEmpty() ? "" : bitDebit.Reservation.Requested.ToClaimString()
			};
			List<KeyValuePair<string, int>> standing = KingdomLabRules.StandingCost(Procedure.Creeds,
				KingdomLabRules.StandingPerCreed);
			for (int i = 0; i < standing.Count; i++)
			{
				job.StandingFactions.Add(standing[i].Key);
				job.StandingDeltas.Add(standing[i].Value);
				job.StandingBefore.Add(int.MinValue);
				job.StandingTargets.Add(int.MinValue);
				job.StandingPhases.Add((int)KingdomLabStandingPhase.Pending);
			}
			job.ChronicleEventId = "lab:apply:" + jobId + ":chronicle";
			job.PetitionEventId = "lab:apply:" + jobId + ":petition";
			job.AnnounceEventId = "lab:apply:" + jobId + ":message";
			job.ReadyMessageEventId = "lab:apply:" + jobId + ":ready-message";
			job.Normalize();
			if (job.SchemaQuarantined || !WriteCanonical(job, KingdomLabRegistryStatus.Active))
			{
				debit.Rollback();
				bitDebit?.Cancel();
				Popup.Show("The canonical commission receipt could not be persisted. Nothing was spent.");
				return;
			}
			try
			{
				Building.AddPart(job);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: job publication threw (" + ex.Message + ")");
			}
			if (KingdomProcedures.ReferencePartOrdinal(Building, job) < 0
				|| !ReferenceEquals(job.ParentObject, Building)
				|| CountParts<r_KingdomLabJob>(Building) != 1)
			{
				WriteCanonical(job, KingdomLabRegistryStatus.Quarantined);
				debit.Rollback();
				bitDebit?.Cancel();
				try
				{
					if (KingdomProcedures.ReferencePartOrdinal(Building, job) >= 0)
						Building.RemovePart(job);
				}
				catch { }
				Popup.Show("The hall could not prove one exact physical commission projection. Its canonical intent was quarantined; nothing was spent.");
				return;
			}
			try
			{
				Actor.SetStringProperty(pendingProperty, jobId);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: patient pending marker threw (" + ex.Message + ")");
			}
			if (!string.Equals(Actor.GetStringProperty(pendingProperty), jobId, StringComparison.Ordinal))
			{
				job.State = KingdomLabJobPhase.FundingRecovery;
				job.Fault = "The patient-side commission identity could not be persisted. No payment was attempted.";
				bitDebit?.Cancel();
				Popup.Show(job.Fault);
				return;
			}
			job.IntentPublished = true;
			LabProcedure frozen = FrozenProcedure(job);
			if (!ValidApplicationTarget(Actor, job, frozen))
			{
				debit.Rollback();
				bitDebit?.Cancel();
				job.State = KingdomLabJobPhase.FundingRecovery;
				job.Fault = "The exact patient slot or bearer changed before water commit. Nothing was charged.";
				return;
			}
			debit.Commit();
			if (!ValidApplicationTarget(Actor, job, frozen))
			{
				debit.Rollback();
				MergeWaterReceipt(job, debit);
				bitDebit?.Cancel();
				job.State = job.WaterQuarantined ? KingdomLabJobPhase.ApplicationRecovery
					: KingdomLabJobPhase.FundingRecovery;
				job.Fault = job.WaterQuarantined
					? "The target changed during water callbacks and exact compensation could not be proved. The receipt is quarantined."
					: "The target changed during water callbacks. The exact debit was compensated; retry charges only the outstanding price.";
				EnsureJobGovernance(job);
				return;
			}
			bool waterExact = MergeWaterReceipt(job, debit);
			bool bitsExact = bitCost.IsEmpty();
			if (!waterExact)
			{
				bitDebit?.Cancel();
			}
			else if (bitDebit != null)
			{
				if (!ValidApplicationTarget(Actor, job, frozen))
				{
					debit.Rollback();
					MergeWaterReceipt(job, debit);
					bitDebit.Cancel();
					job.State = job.WaterQuarantined ? KingdomLabJobPhase.ApplicationRecovery
						: KingdomLabJobPhase.FundingRecovery;
					job.Fault = "The exact target changed before bit commit. Water compensation was measured; no bits or body effect were touched.";
					EnsureJobGovernance(job);
					return;
				}
				KingdomMaterialDebitResult bitResult = bitDebit.Commit();
				bitsExact = bitResult.Exact;
				if (bitResult.Outcome == KingdomMaterialDebitOutcome.RecoverablePartial
					&& bitDebit.CanCompensate)
				{
					KingdomMaterialDebitResult compensation = bitDebit.Compensate();
					if (compensation.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
					{
						bitResult = compensation;
					}
				}
				job.BitOutstanding = bitsExact ? "" : ((bitResult.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
					? bitDebit.Reservation.Requested.ToClaimString()
					: bitResult.Outstanding.ToClaimString());
				if (!bitsExact)
				{
					job.Fault = bitResult.Failure ?? "The exact bit debit was interrupted.";
				}
			}
			if (waterExact && bitsExact && !ValidApplicationTarget(Actor, job, frozen))
			{
				bool bitsRestored = bitDebit == null;
				if (bitDebit != null && bitDebit.CanCompensate)
				{
					KingdomMaterialDebitResult compensation = bitDebit.Compensate();
					bitsRestored = compensation.Outcome == KingdomMaterialDebitOutcome.CompensatedExact;
					if (bitsRestored) job.BitOutstanding = job.BitClaim;
				}
				bool waterRestored = debit.Rollback();
				MergeWaterReceipt(job, debit);
				job.State = (bitsRestored && waterRestored && !job.WaterQuarantined)
					? KingdomLabJobPhase.FundingRecovery : KingdomLabJobPhase.ApplicationRecovery;
				job.Fault = (bitsRestored && waterRestored && !job.WaterQuarantined)
					? "The exact target changed during funding callbacks. Water and bits were compensated; no kept part or body effect was touched."
					: "The exact target changed during funding callbacks and complete compensation could not be proved. The receipt is quarantined.";
				EnsureJobGovernance(job);
				return;
			}
			keptPhase = (waterExact && bitsExact) ? SpendKeptExact(keptSpend)
				: KingdomKeptSpendPhase.RefusedClean;
			int keptMeasured = (waterExact && bitsExact) ? KeptSpent(keptSpend) : 0;
			job.KeptPaid = keptMeasured;
			job.KeptLost = keptMeasured;
			if (keptPhase == KingdomKeptSpendPhase.Partial)
			{
				job.KeptMeasurementExact = false;
				job.KeptQuarantined = true;
			}
			job.State = job.KeptQuarantined ? KingdomLabJobPhase.ApplicationRecovery
				: KingdomLabRules.FundingPhase(waterExact, bitsExact, keptPhase);
			EnsureJobGovernance(job);
			if (job.State == KingdomLabJobPhase.Working)
			{
				MessageQueue.AddPlayerMessage("{{G|" + KingdomLabRules.StakedLine(Procedure.Named,
					Procedure.StaffDays) + "}}");
				return;
			}
			Popup.Show("The paid commission is persisted, but its exact funding was interrupted. No graft was made. Read this hall's slate to inspect and retry the outstanding receipt.");
		}

		internal static void OnSemanticStep(KingdomSystem System, Zone Zone, KingdomSurvey Survey,
			long BoundaryTick)
		{
			List<GameObject> objects = (Survey != null && ReferenceEquals(Survey.Ground, Zone))
				? Survey.LabJobs : null;
			for (int i = 0; objects != null && i < objects.Count; i++)
			{
				GameObject building = objects[i];
				r_KingdomLabJob job = building?.GetPart<r_KingdomLabJob>();
				if (job == null || job.State != KingdomLabJobPhase.Working)
				{
					continue;
				}
				job.Normalize();
				GameObject patient = GameObject.FindByID(job.PatientId);
				if (job.SchemaQuarantined || !GameObject.Validate(patient)
					|| !CurrentAuthority(building, patient, System, job,
						KingdomLabRegistryStatus.Active)) continue;
				int need = building.GetIntProperty(KingdomAdopt.StaffNeededProperty);
				int crew = (need <= 0) ? 100 : ((building.GetIntProperty("KingdomStaffed") == 1)
					? building.GetIntProperty("KingdomEffectiveness") : 0);
				int wear = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(building));
				KingdomLabJobAccrual accrual = KingdomLabRules.AccrueJob(job.LastWorkedTick,
					BoundaryTick, job.RemainingTicks, crew, wear, job.State,
					KingdomCrews.AffinityOf(building));
				job.LastWorkedTick = accrual.NextTick;
				job.RemainingTicks = accrual.RemainingTicks;
				job.State = accrual.Phase;
				if (job.State == KingdomLabJobPhase.Ready
					&& !KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)job.ReadyMessagePhase))
				{
					KingdomLabMessagePhase phase = PublishMessage(ref job.ReadyMessagePhase,
						ref job.ReadyMessageText, job.ReadyMessageEventId,
						"{{G|The staffed work on " + job.FrozenName + " is ready at "
							+ KingdomLabRules.Named(KingdomPresentation.Rich(job.City))
							+ ". Return to the hall to complete the procedure.}}",
						ShouldPublish: !string.IsNullOrEmpty(job.FrozenName));
					job.ReadyAnnounced = phase == KingdomLabMessagePhase.Delivered;
				}
			}
		}

		private static void ManageJob(GameObject Building, GameObject Actor, KingdomSystem System,
			r_KingdomLabJob Job)
		{
			Job.Normalize();
			if (Job.SchemaQuarantined || !string.Equals(Job.PatientId, Actor?.ID,
				StringComparison.Ordinal))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = string.IsNullOrEmpty(Job.Fault)
					? "This job cannot prove its patient or immutable schema. It offers no action."
					: Job.Fault;
				Popup.Show(KingdomLabRules.JobProgressLine(Job.ProcedureKey, Job.State,
					Job.RemainingTicks, 0, false, false) + "\n" + Job.Fault);
				return;
			}
			KingdomLabRegistryStatus expected = Job.RegistryFinalized
				? (Job.State == KingdomLabJobPhase.Cancelled
					? KingdomLabRegistryStatus.Cancelled : KingdomLabRegistryStatus.Complete)
				: KingdomLabRegistryStatus.Active;
			if (!CurrentAuthority(Building, Actor, System, Job, expected))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The canonical patient/building/realm receipt is missing or disagrees. This hall cannot pay, cancel, apply, clean, or inherit the job.";
				Popup.Show(Job.Fault);
				return;
			}
			LabProcedure procedure = FrozenProcedure(Job);
			if (procedure == null)
			{
				Job.SchemaQuarantined = true;
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The frozen effect contract is invalid. The job is quarantined.";
				Popup.Show(Job.Fault);
				return;
			}
			bool staffed = Building.GetIntProperty(KingdomAdopt.StaffNeededProperty) <= 0
				|| Building.GetIntProperty("KingdomStaffed") == 1;
			bool wornOut = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Building)) <= 0;
			string receipt = "\n\npaid receipt: water " + Job.WaterPaid + "/" + Job.WaterOwed
				+ ((Job.WaterLost > Job.WaterPaid) ? (" (" + Job.WaterLost + " physically lost)") : "")
				+ (Job.WaterQuarantined ? " {{r|[measurement quarantined; automatic retry forbidden]}}" : "")
				+ ", kept " + Job.KeptPaid + "/" + Job.KeptOwed
				+ ", bits " + (string.IsNullOrEmpty(Job.BitOutstanding) ? "exact" : "outstanding")
				+ ", standing " + Job.StandingAppliedCount + "/" + Job.StandingFactions.Count
				+ " projected. Paid costs are not returned after commissioning.";
			string intro = KingdomLabRules.JobProgressLine(procedure.Named, Job.State,
				Job.RemainingTicks, procedure.StaffDays, staffed, wornOut) + receipt
				+ (string.IsNullOrEmpty(Job.Fault) ? "" : ("\n\n{{r|" + Job.Fault + "}}"));
			if (Job.State == KingdomLabJobPhase.Funding || Job.State == KingdomLabJobPhase.FundingRecovery)
			{
				string[] fundingOptions = Job.WaterQuarantined
					? new string[] { "Leave the quarantined receipt preserved.",
						"Cancel it; any measured payment is not returned." }
					: new string[] { "Retry the outstanding exact payment.", "Leave it preserved.",
						"Cancel it; any measured payment is not returned." };
				int picked = Popup.PickOption(Title: "recover commission funding", Intro: intro,
					Options: fundingOptions, AllowEscape: true);
				if (!Job.WaterQuarantined && picked == 0)
				{
					RecoverFunding(Building, Actor, System, Job, procedure);
				}
				else if (picked == (Job.WaterQuarantined ? 1 : 2)
					&& Popup.ShowYesNo("Cancel this persisted commission? Any water, bits, or kept parts already measured on its receipt are not returned.") == DialogResult.Yes)
				{
					Job.State = KingdomLabJobPhase.Cancelled;
					FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Cancelled);
				}
				return;
			}
			if (Job.State == KingdomLabJobPhase.Ready || Job.State == KingdomLabJobPhase.Applying
				|| Job.State == KingdomLabJobPhase.ApplicationRecovery)
			{
				int picked = Popup.PickOption(Title: "complete procedure", Intro: intro,
					Options: new string[] { "Finish or recover the terminal procedure.", "Leave it ready." }, AllowEscape: true);
				if (picked == 0)
				{
					ApplyJob(Building, Actor, System, Job, procedure);
				}
				return;
			}
			if (Job.State == KingdomLabJobPhase.Complete || Job.State == KingdomLabJobPhase.Cancelled)
			{
				if (Job.State == KingdomLabJobPhase.Complete)
				{
					FinishJobTellings(Actor, System, Job, procedure);
				}
				Popup.Show(intro);
				if (Job.State == KingdomLabJobPhase.Cancelled && !Job.RegistryFinalized)
					FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Cancelled);
				if (Job.State == KingdomLabJobPhase.Cancelled
					&& !KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)Job.TerminalMessagePhase))
				{
					KingdomLabMessagePhase phase = PublishMessage(ref Job.TerminalMessagePhase,
						ref Job.TerminalMessageText, Job.AnnounceEventId,
						"{{K|The commission was cancelled. Its paid price was not returned.}}");
					Job.Announced = phase == KingdomLabMessagePhase.Delivered;
				}
				if (Job.RegistryFinalized && Job.MarkerCleaned
					&& KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)Job.TerminalMessagePhase)
					&& (Job.State == KingdomLabJobPhase.Cancelled
						|| (Job.Chronicled && Job.Spoken)))
				{
					PurgeApplicationReceipt(Building, Job,
						Job.State == KingdomLabJobPhase.Cancelled
							? KingdomLabRegistryStatus.Cancelled
							: KingdomLabRegistryStatus.Complete);
				}
				return;
			}
			int choice = Popup.PickOption(Title: "commission in progress", Intro: intro,
				Options: new string[] { "Leave the crew to it.", "Cancel it; paid costs are not returned." }, AllowEscape: true);
			if (choice == 1 && Popup.ShowYesNo("Cancel this paid commission? Its water, bits, kept parts, and completed work are not returned.") == DialogResult.Yes)
			{
				Job.State = KingdomLabJobPhase.Cancelled;
				if (FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Cancelled)
					&& !KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)Job.TerminalMessagePhase))
				{
					KingdomLabMessagePhase phase = PublishMessage(ref Job.TerminalMessagePhase,
						ref Job.TerminalMessageText, Job.AnnounceEventId,
						"{{K|The commission was cancelled. Its paid price was not returned.}}");
					Job.Announced = phase == KingdomLabMessagePhase.Delivered;
				}
				if (Job.RegistryFinalized && Job.MarkerCleaned
					&& KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)Job.TerminalMessagePhase))
				{
					PurgeApplicationReceipt(Building, Job,
						KingdomLabRegistryStatus.Cancelled);
				}
			}
		}

		private static void RecoverFunding(GameObject Building, GameObject Actor, KingdomSystem System,
			r_KingdomLabJob Job, LabProcedure Procedure)
		{
			if (!CurrentAuthority(Building, Actor, System, Job, KingdomLabRegistryStatus.Active)
				|| !string.Equals(Job.PatientId, Actor?.ID, StringComparison.Ordinal)) return;
			bool waterExact = !Job.WaterQuarantined && Job.WaterPaid == Job.WaterOwed;
			if (!waterExact && !Job.WaterQuarantined)
			{
				KingdomSurvey survey = (Actor.CurrentZone == null) ? null : KingdomSurvey.Take(Actor.CurrentZone, System);
				KingdomWaterDebit debit;
				if (survey != null && survey.TryReserveExactWater(Job.WaterOwed - Job.WaterPaid, out debit))
				{
					if (!ValidApplicationTarget(Actor, Job, Procedure))
					{
						debit.Rollback();
						Job.Fault = "The frozen patient slot or bearer changed before retry payment. Nothing was charged.";
						return;
					}
					debit.Commit();
					if (!ValidApplicationTarget(Actor, Job, Procedure))
					{
						debit.Rollback();
						MergeWaterReceipt(Job, debit);
						Job.State = Job.WaterQuarantined ? KingdomLabJobPhase.ApplicationRecovery
							: KingdomLabJobPhase.FundingRecovery;
						Job.Fault = "The target changed during water retry callbacks; exact compensation was measured before any bit, kept, or body mutation.";
						EnsureJobGovernance(Job);
						return;
					}
					waterExact = MergeWaterReceipt(Job, debit);
				}
			}
			bool bitsExact = string.IsNullOrEmpty(Job.BitOutstanding);
			if (waterExact && !bitsExact)
			{
				if (!ValidApplicationTarget(Actor, Job, Procedure))
				{
					Job.Fault = "The frozen target changed before outstanding bit payment. Nothing further was charged.";
					return;
				}
				KingdomMaterialDebitCost cost;
				KingdomMaterialDebit debit = KingdomMaterialDebitCost.TryParseClaim(Job.BitOutstanding, out cost)
					? KingdomMaterials.ReserveComposite(Actor.CurrentZone, cost) : null;
				KingdomMaterialDebitResult result = (debit != null
					&& debit.Reservation.Outcome == KingdomMaterialDebitOutcome.Reserved)
					? debit.Commit() : null;
				bitsExact = result != null && result.Exact;
				if (result != null)
				{
					if (result.Outcome == KingdomMaterialDebitOutcome.RecoverablePartial
						&& debit.CanCompensate)
					{
						KingdomMaterialDebitResult compensation = debit.Compensate();
						if (compensation.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
						{
							result = compensation;
							bitsExact = false;
						}
					}
					Job.BitOutstanding = bitsExact ? "" : ((result.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
						? result.Requested.ToClaimString() : result.Outstanding.ToClaimString());
					Job.Fault = result.Failure ?? "";
				}
			}
			if (waterExact && bitsExact && !ValidApplicationTarget(Actor, Job, Procedure))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The frozen target changed during funding callbacks. Paid receipts are preserved; no body effect was made.";
				EnsureJobGovernance(Job);
				return;
			}
			int keptOwed = Job.KeptOwed - Job.KeptPaid;
			KingdomKeptSpendPhase keptPhase = (keptOwed <= 0) ? KingdomKeptSpendPhase.SpentExact
				: KingdomKeptSpendPhase.RefusedClean;
			if (waterExact && bitsExact && keptOwed > 0)
			{
				if (!ValidApplicationTarget(Actor, Job, Procedure))
				{
					Job.Fault = "The frozen target changed before outstanding kept parts. Nothing further was consumed.";
					return;
				}
				KeptSpendPreparation preparation;
				keptPhase = PrepareKeptSpend(KeptParts(Actor), Procedure, out preparation, keptOwed);
				if (keptPhase == KingdomKeptSpendPhase.ApplyCounts)
				{
					keptPhase = SpendKeptExact(preparation);
					int measured = KeptSpent(preparation);
					Job.KeptPaid = Math.Min(Job.KeptOwed, Job.KeptPaid + measured);
					Job.KeptLost += measured;
					if (keptPhase == KingdomKeptSpendPhase.Partial)
					{
						Job.KeptMeasurementExact = false;
						Job.KeptQuarantined = true;
					}
				}
			}
			Job.State = Job.KeptQuarantined ? KingdomLabJobPhase.ApplicationRecovery
				: KingdomLabRules.FundingPhase(waterExact, bitsExact, keptPhase);
			EnsureJobGovernance(Job);
			if (Job.State == KingdomLabJobPhase.Working)
			{
				Job.Fault = "";
				Job.LastWorkedTick = The.Game?.TimeTicks ?? Job.LastWorkedTick;
				MessageQueue.AddPlayerMessage("{{G|The exact receipt is settled. The staffed work can begin.}}");
			}
			else
			{
				Popup.Show("The commission remains in funding recovery. No graft was made; every measured payment remains on its persisted receipt.");
			}
		}

		private static bool MergeWaterReceipt(r_KingdomLabJob Job, KingdomWaterDebit Debit)
		{
			if (Job == null || Debit == null)
			{
				return false;
			}
			KingdomLabWaterClaim claim = KingdomLabRules.MergeWaterClaim(Job.WaterOwed,
				Job.WaterPaid, Job.WaterLost, Job.WaterQuarantined,
				Debit.Spent, Debit.Lost, Debit.MeasurementExact);
			Job.WaterMeasurementExact = Job.WaterMeasurementExact && Debit.MeasurementExact;
			Job.WaterPaid = claim.Paid;
			Job.WaterLost = claim.Lost;
			Job.WaterQuarantined = claim.Quarantined;
			if (!claim.Settled && !string.IsNullOrEmpty(Debit.Failure))
			{
				Job.Fault = Debit.Failure;
			}
			if (claim.Quarantined)
			{
				Job.Fault = "The water receipt lost exact vessel identity or composition. Automatic retry is quarantined so the hall cannot charge an uncertain balance twice."
					+ (string.IsNullOrEmpty(Debit.Failure) ? "" : (" " + Debit.Failure));
			}
			return claim.Settled;
		}

		private static void ApplyJob(GameObject Building, GameObject Actor, KingdomSystem System,
			r_KingdomLabJob Job, LabProcedure Procedure)
		{
			if (!CurrentAuthority(Building, Actor, System, Job, KingdomLabRegistryStatus.Active)
				|| !string.Equals(Actor?.ID, Job.PatientId, StringComparison.Ordinal)
				|| Job.SchemaQuarantined || Procedure == null)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The canonical paid job does not authorize this patient, hall, realm, or frozen contract.";
				Popup.Show(Job.Fault);
				return;
			}
			KingdomLabOwnershipSnapshot snapshot;
			KingdomLabOwnedTargetState observed = SnapshotJobEffect(Actor, Procedure, Job,
				out snapshot);
			if (observed == KingdomLabOwnedTargetState.Uncertain && !Job.EffectCommitted)
			{
				if (!KingdomProcedures.HasProcedureClass(Actor, Procedure))
					observed = KingdomLabOwnedTargetState.Absent;
			}
			if (observed == KingdomLabOwnedTargetState.Absent && Job.EffectCommitted)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The once-committed exact effect is absent. Recovery will not create a second instance or adopt a replacement.";
				return;
			}
			if (observed == KingdomLabOwnedTargetState.Uncertain)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The exact effect receipt is uncertain or a foreign same-class effect exists. The hall will neither duplicate nor adopt it.";
				Popup.Show(Job.Fault + " The paid job remains ready for recovery.");
				return;
			}
			Job.State = KingdomLabJobPhase.Applying;
			if (observed == KingdomLabOwnedTargetState.Absent)
			{
				if (!ValidApplicationTarget(Actor, Job, Procedure))
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "The frozen patient slot/bearer changed or a foreign effect appeared before body mutation.";
					return;
				}
				Job.IntentPublished = true;
				KingdomLabGrantAttempt attempt = KingdomProcedures.GrantAtExact(Actor, Procedure,
					Job.BodyPartId, Job.BearerId, Job.Stamp, Job.JobId, Job.FrozenManager,
					Job.FrozenDetail, Job.FrozenFingerprint);
				if (attempt.State == KingdomLabOwnedTargetState.Present)
				{
					Job.EffectBodyPartId = attempt.BodyPartId;
					Job.EffectPartOrdinal = attempt.PartOrdinal;
				}
				observed = SnapshotJobEffect(Actor, Procedure, Job, out snapshot);
				if (observed != KingdomLabOwnedTargetState.Present)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = string.IsNullOrEmpty(attempt.Failure)
						? "The exact body mutation did not publish a recoverable owned effect."
						: attempt.Failure;
					return;
				}
			}
			Job.EffectCommitted = true;
			Job.EffectBodyPartId = snapshot.BodyPartId;
			Job.EffectPartOrdinal = snapshot.PartOrdinal;
			if (!RepairProcedureOwnership(Actor, Procedure, Job, snapshot))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The exact effect is present, but its owner marker/patient receipt needs repair. It was not announced as complete.";
				Popup.Show(Job.Fault);
				return;
			}
			Job.OwnershipPublished = true;
			while (Job.StandingAppliedCount < Job.StandingFactions.Count
				&& Job.StandingAppliedCount < Job.StandingDeltas.Count)
			{
				int at = Job.StandingAppliedCount;
				KingdomLabStandingPhase standingPhase = (KingdomLabStandingPhase)
					Job.StandingPhases[at];
				if (standingPhase == KingdomLabStandingPhase.Pending)
				{
					Job.StandingBefore[at] = System.GetStanding(Job.StandingFactions[at]);
					Job.StandingTargets[at] = KingdomLabRules.StandingAfter(
						Job.StandingBefore[at], Job.StandingDeltas[at]);
					Job.StandingPhases[at] = (int)KingdomLabStandingPhase.Bound;
					standingPhase = KingdomLabStandingPhase.Bound;
				}
				int currentStanding = System.GetStanding(Job.StandingFactions[at]);
				standingPhase = KingdomLabRules.ObserveStanding(standingPhase,
					currentStanding, Job.StandingBefore[at], Job.StandingTargets[at]);
				Job.StandingPhases[at] = (int)standingPhase;
				if (standingPhase == KingdomLabStandingPhase.Applied)
				{
					Job.StandingAppliedCount++;
					continue;
				}
				if (standingPhase != KingdomLabStandingPhase.Bound)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "Standing changed outside the exact before/delta/after receipt. The hall will not overwrite the interleaving value.";
					return;
				}
				if (SnapshotJobEffect(Actor, Procedure, Job, out snapshot)
					!= KingdomLabOwnedTargetState.Present)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "The exact effect changed before a standing callback. No further standing was applied.";
					return;
				}
				try
				{
					Job.StandingPhases[at] = (int)KingdomLabStandingPhase.Intent;
					System.SetStanding(Job.StandingFactions[at], Job.StandingTargets[at]);
				}
				catch (Exception ex)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "Standing callback threw after intent. Recovery will observe the exact after-value once and never write again: " + ex.Message;
					return;
				}
				standingPhase = KingdomLabRules.ObserveStanding(KingdomLabStandingPhase.Intent,
					System.GetStanding(Job.StandingFactions[at]), Job.StandingBefore[at],
					Job.StandingTargets[at]);
				Job.StandingPhases[at] = (int)standingPhase;
				if (standingPhase != KingdomLabStandingPhase.Applied)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "Standing callback did not leave the exact after-value. The interleaving value is preserved and the receipt is quarantined.";
					return;
				}
				if (SnapshotJobEffect(Actor, Procedure, Job, out snapshot)
					!= KingdomLabOwnedTargetState.Present)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "The exact effect changed during a standing callback. Recovery is quarantined from touching replacements.";
					return;
				}
				Job.StandingAppliedCount++;
			}
			Job.StandingApplied = Job.StandingAppliedCount >= Job.StandingFactions.Count;
			if (!Job.StandingApplied)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The effect is present, but its standing receipt is incomplete. Retry to finish bookkeeping.";
				return;
			}
			if (SnapshotJobEffect(Actor, Procedure, Job, out snapshot)
				!= KingdomLabOwnedTargetState.Present)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The exact effect changed before terminal cleanup.";
				return;
			}
			if (!FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Complete))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				return;
			}
			Job.State = KingdomLabJobPhase.Complete;
			Job.Fault = "";
			FinishJobTellings(Actor, System, Job, Procedure);
		}

		private static void FinishJobTellings(GameObject Actor, KingdomSystem System,
			r_KingdomLabJob Job, LabProcedure Procedure)
		{
			if (!ExactJobEffectPresent(Actor, Procedure, Job))
			{
				QuarantineApplicationTelling(Job,
					"The exact effect changed before terminal publication. No further telling was attempted.");
				return;
			}
			if (!Job.Chronicled)
			{
				try
				{
					Job.Chronicled = KingdomChronicle.RecordOnce(System, Job.ChronicleEventId,
						KingdomLabRules.DoneTelling(Job.FrozenName,
							KingdomPresentation.Rich(Job.City)));
				}
				catch (Exception ex)
				{
					KingdomLog.Log("lab: chronicle intent " + Job.ChronicleEventId
						+ " threw after publication (" + ex.Message + ")");
				}
			}
			if (!ExactJobEffectPresent(Actor, Procedure, Job))
			{
				QuarantineApplicationTelling(Job,
					"The exact effect changed during chronicle publication. Petition and message publication stopped.");
				return;
			}
			if (!Job.Spoken)
			{
				try { Job.Spoken = Speak(System, Actor, Procedure, Job); }
				catch (Exception ex)
				{
					Job.Fault = "The keyed petition outbox stopped: " + ex.Message;
					return;
				}
			}
			if (!ExactJobEffectPresent(Actor, Procedure, Job))
			{
				QuarantineApplicationTelling(Job,
					"The exact effect changed during petition publication. Completion messaging stopped.");
				return;
			}
			if (!KingdomLabRules.MessageSettled(
				(KingdomLabMessagePhase)Job.TerminalMessagePhase))
			{
				KingdomLabMessagePhase phase = PublishMessage(ref Job.TerminalMessagePhase,
					ref Job.TerminalMessageText, Job.AnnounceEventId,
					KingdomLabRules.DoneLine(Job.FrozenName,
						KingdomPresentation.Rich(Job.City)));
				Job.Announced = phase == KingdomLabMessagePhase.Delivered;
			}
			if (!ExactJobEffectPresent(Actor, Procedure, Job))
			{
				QuarantineApplicationTelling(Job,
					"The exact effect changed during completion presentation. The hall receipt is quarantined and no replacement will be touched.");
				return;
			}
			if (Job.State == KingdomLabJobPhase.Complete && Job.Chronicled && Job.Spoken
				&& KingdomLabRules.MessageSettled(
					(KingdomLabMessagePhase)Job.TerminalMessagePhase))
			{
				PurgeApplicationReceipt(Job.ParentObject, Job,
					KingdomLabRegistryStatus.Complete);
			}
		}

		private static bool ExactJobEffectPresent(GameObject Actor, LabProcedure Procedure,
			r_KingdomLabJob Job)
		{
			KingdomLabOwnershipSnapshot snapshot;
			return Actor != null && Procedure != null && Job != null
				&& string.Equals(Actor.ID, Job.PatientId, StringComparison.Ordinal)
				&& SnapshotJobEffect(Actor, Procedure, Job, out snapshot)
					== KingdomLabOwnedTargetState.Present;
		}

		private static void QuarantineApplicationTelling(r_KingdomLabJob Job, string Fault)
		{
			if (Job == null) return;
			bool recorded = false;
			try { recorded = WriteCanonical(Job, KingdomLabRegistryStatus.Quarantined); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: canonical telling quarantine threw (" + ex.Message + ")");
			}
			Job.RegistryFinalized = recorded;
			Job.SchemaQuarantined = true;
			Job.State = KingdomLabJobPhase.ApplicationRecovery;
			Job.Fault = Fault ?? "The exact effect changed during terminal publication.";
		}

		private static string PendingProperty(string Key)
		{
			return "r_TAF_LabPending::" + (Key ?? "").Trim().ToLowerInvariant();
		}

		private static XRL.World.Anatomy.BodyPart SelectedPart(GameObject Actor, int CensusIndex)
		{
			List<XRL.World.Anatomy.BodyPart> parts = Actor?.Body?.GetParts();
			int seen = 0;
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				if (parts[i] != null && !parts[i].Abstract && seen++ == CensusIndex)
				{
					return parts[i];
				}
			}
			return null;
		}

		private static bool ContainsBodyReference(IList<XRL.World.Anatomy.BodyPart> Parts,
			XRL.World.Anatomy.BodyPart Candidate)
		{
			for (int i = 0; Parts != null && i < Parts.Count; i++)
				if (ReferenceEquals(Parts[i], Candidate)) return true;
			return false;
		}

		private static int KeptSpent(KeptSpendPreparation Preparation)
		{
			int spent = 0;
			for (int i = 0; Preparation != null && i < Preparation.Plan.Steps.Count; i++)
			{
				KingdomKeptSpendStep step = Preparation.Plan.Steps[i];
				GameObject source = Preparation.Sources[step.Source];
				if (!GameObject.Validate(source))
				{
					spent += step.Taken;
				}
				else if (source.Count < step.Original)
				{
					spent += Math.Min(step.Taken, step.Original - Math.Max(0, source.Count));
				}
			}
			return spent;
		}

		private static KingdomLabOwnedTargetState SnapshotJobEffect(GameObject Actor,
			LabProcedure Procedure, r_KingdomLabJob Job,
			out KingdomLabOwnershipSnapshot Snapshot)
		{
			Snapshot = default(KingdomLabOwnershipSnapshot);
			KingdomLabOwnershipSnapshot found;
			KingdomLabOwnedTargetState state = KingdomProcedures.SnapshotTracked(Actor,
				Procedure, Job.JobId, Job.BearerId, out found);
			if (!string.Equals(found.ProcedureKey, Job.ProcedureKey, StringComparison.OrdinalIgnoreCase)
				|| !string.Equals(found.PatientId, Job.PatientId, StringComparison.Ordinal)
				|| !string.Equals(found.Grants, Job.FrozenGrants, StringComparison.Ordinal)
				|| found.Source != Job.FrozenSource || found.Attach != Job.FrozenAttach
				|| !string.Equals(found.Manager, Job.FrozenManager, StringComparison.Ordinal)
				|| !string.Equals(found.Detail, Job.FrozenDetail, StringComparison.Ordinal)
				|| !string.Equals(found.Fingerprint, Job.FrozenFingerprint, StringComparison.Ordinal)
				|| (Job.EffectBodyPartId > 0 && found.BodyPartId != Job.EffectBodyPartId)
				|| (Job.EffectCommitted && found.PartOrdinal != Job.EffectPartOrdinal))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			Snapshot = found;
			return state;
		}

		private static bool RepairProcedureOwnership(GameObject Actor, LabProcedure Procedure,
			r_KingdomLabJob Job, KingdomLabOwnershipSnapshot Snapshot)
		{
			KingdomLabOwnershipSnapshot observed;
			if (SnapshotJobEffect(Actor, Procedure, Job, out observed)
				!= KingdomLabOwnedTargetState.Present
				|| !string.Equals(observed.Fingerprint, Snapshot.Fingerprint,
					StringComparison.Ordinal))
			{
				return false;
			}
			GameObject bearer = Actor;
			if (Snapshot.Source == (int)LabSource.Part
				&& Snapshot.Attach == (int)LabAttach.Weapon)
			{
				bearer = KingdomProcedures.ExactBodyPart(Actor,
					Snapshot.BodyPartId)?.DefaultBehavior;
			}
			if (!GameObject.Validate(bearer)
				|| !string.Equals(bearer.ID, Snapshot.BearerId, StringComparison.Ordinal)) return false;
			string marker = bearer.GetStringProperty(
				KingdomProcedures.OwnerProperty(Snapshot.ProcedureKey));
			if (!string.IsNullOrEmpty(marker)
				&& !string.Equals(marker, Snapshot.JobId, StringComparison.Ordinal)) return false;
			try
			{
				bearer.SetStringProperty(KingdomProcedures.OwnerProperty(Snapshot.ProcedureKey),
					Snapshot.JobId);
			}
			catch { return false; }
			r_KingdomLabRecord record = KingdomProcedures.Record(Actor);
			record.Normalize();
			for (int i = 0; i < record.Keys.Count; i++)
			{
				if (string.Equals(record.Keys[i], Snapshot.ProcedureKey,
					StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(record.JobIds[i], Snapshot.JobId,
						StringComparison.Ordinal)) return false;
			}
			XRL.World.Anatomy.BodyPart part = KingdomProcedures.ExactBodyPart(Actor,
				Snapshot.BodyPartId);
			try
			{
				record.Note(Snapshot.ProcedureKey,
					(Snapshot.Source == (int)LabSource.Mutation) ? "" : (part?.Type ?? ""),
					Snapshot.Attach == (int)LabAttach.Weapon, Snapshot.BodyPartId,
					Snapshot.BearerId, Snapshot.JobId, Job.FrozenName, Snapshot.Grants,
					Snapshot.Source, Snapshot.Attach, Snapshot.Manager, Snapshot.Detail,
					Snapshot.Fingerprint, Snapshot.PartOrdinal, Snapshot.EffectNonce);
			}
			catch { return false; }
			int at = record.IndexOf(Snapshot.ProcedureKey);
			KingdomLabOwnershipSnapshot receipt;
			return record.ContractAt(at, out receipt, Actor.ID)
				&& string.Equals(receipt.JobId, Snapshot.JobId, StringComparison.Ordinal)
				&& string.Equals(receipt.Fingerprint, Snapshot.Fingerprint,
					StringComparison.Ordinal)
				&& string.Equals(receipt.EffectNonce, Snapshot.EffectNonce,
					StringComparison.Ordinal);
		}

		/// <summary>
		/// The first of &sect;3.6's three authored happenings: the hall is spoken against.
		/// <para>
		/// It rides the petitions surface that already ships and builds nothing parallel &mdash; a
		/// named person, waiting to speak, about a thing they actually mind. There is no correct
		/// answer to it, which is the point: friction is placement constraints and named people, and
		/// never a meter (Addendum 4's pillar guard, DIVERSITY &sect;3.6's own closing rule).
		/// </para>
		/// </summary>
		private static bool Speak(KingdomSystem System, GameObject Actor, LabProcedure Procedure,
			r_KingdomLabJob Job)
		{
			r_KingdomLabRecord record = KingdomProcedures.Record(Actor);
			if (record.SpokenAgainst || Procedure.Class == LabClass.Rider)
			{
				return true;
			}
			List<KeyValuePair<string, int>> offended = KingdomLabRules.StandingCost(Procedure.Creeds, 1);
			int holding = 0;
			string creed = null;
			for (int i = 0; i < offended.Count; i++)
			{
				int here = CreedCount(System, offended[i].Key);
				if (here > holding)
				{
					holding = here;
					creed = offended[i].Key;
				}
			}
			if (!KingdomLabRules.SpeaksAgainstHall(holding, System.Population, record.SpokenAgainst))
			{
				return true;
			}
			// Through the shipped petitions machinery, which builds nothing parallel: a named person,
			// waiting at the Charter, about a thing they actually mind (DIVERSITY §3.6's mesh
			// condition). The latch is set only when a petition was really raised, so a founder who
			// happened to be carrying another petition still gets this one the next time.
			string petitionFaction = Job.PetitionAttemptTick >= 0L
				? Job.PetitionFaction : (creed ?? "");
			if (ExactLabPetition(System, Job, petitionFaction))
			{
				record.SpokenAgainst = true;
				return true;
			}
			if (Job.PetitionAttemptTick < 0L)
			{
				Job.PetitionAttemptTick = Math.Max(0L, The.Game?.TimeTicks ?? 0L);
				Job.PetitionFaction = petitionFaction;
			}
			bool raised = false;
			try
			{
				raised = KingdomPetitions.RaiseOnce(System,
					KingdomRules.PetitionKind.Flesh, Job.PetitionFaction,
					Job.PetitionEventId);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: petition intent " + Job.PetitionEventId
					+ " threw (" + ex.Message + ")");
			}
			if (!raised && !ExactLabPetition(System, Job, Job.PetitionFaction)) return false;
			if (!ExactLabPetition(System, Job, Job.PetitionFaction)) return false;
			record.SpokenAgainst = true;
			KingdomLog.Log("lab: hall spoken against (" + creed + " x" + holding
				+ ", event " + Job.PetitionEventId + ")");
			return true;
		}

		private static bool ExactLabPetition(KingdomSystem System, r_KingdomLabJob Job,
			string Faction)
		{
			return System != null && Job != null
				&& string.Equals(System.PetitionEventId, Job.PetitionEventId,
					StringComparison.Ordinal)
				&& System.PetitionKind == KingdomRules.PetitionKind.Flesh
				&& string.Equals(System.PetitionFaction, Faction, StringComparison.Ordinal)
				&& KingdomPetitionRules.IsActive(System.PetitionState);
		}

		private static int CreedCount(KingdomSystem System, string Creed)
		{
			int count;
			return (System.CreedCounts != null && Creed != null && System.CreedCounts.TryGetValue(Creed, out count)) ? count : 0;
		}

		/// <summary>Offers to take a graft off. Costs less than the graft, returns nothing, and says
		/// so before the founder agrees.</summary>
		private static void OfferRemoval(GameObject Actor, KingdomSystem System, string Key, string City)
		{
			if (Actor == null || System == null || The.Game == null) return;
			string realmId = RealmIdentity(System);
			if (!KingdomIdentityRules.IsRealmId(realmId))
			{
				Popup.Show("The realm's immutable identity cannot be proved. No removal or charge was started.");
				return;
			}
			KingdomLabOwnershipSnapshot snapshot;
			KingdomLabOwnedTargetState target = KingdomProcedures.SnapshotOwned(Actor, Key,
				out snapshot);
			if (target == KingdomLabOwnedTargetState.Absent)
			{
				LabProcedure stale = new LabProcedure { Key = snapshot.ProcedureKey,
					Grants = snapshot.Grants, Source = (LabSource)snapshot.Source,
					Attach = (LabAttach)snapshot.Attach };
				KingdomProcedures.CleanupOwned(Actor, stale, snapshot);
				Popup.Show("The exact graft is absent. Its stale receipt was cleaned; no water was reserved or spent.");
				return;
			}
			if (target != KingdomLabOwnedTargetState.Present)
			{
				Popup.Show("The hall cannot prove which exact current or detached effect this record owns. Nothing was reserved, charged, or touched.");
				return;
			}
			LabProcedure procedure;
			if (!KingdomProcedures.TryGet(Key, out procedure))
			{
				Popup.Show("The immutable graft is known, but no current catalogue row can quote a removal price. The receipt is left untouched and nothing was charged.");
				return;
			}
			if (!KingdomProcedures.CatalogMatchesExecutionDetail(procedure, snapshot.Detail))
			{
				Popup.Show("The catalogue execution shape changed since this graft was made. It cannot redirect or price the frozen removal receipt.");
				return;
			}
			string currentDetail = snapshot.Detail;
			string currentManager = KingdomProcedures.ManagerFor(procedure.Key);
			string currentFingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, procedure.Key, procedure.Grants,
				(int)procedure.Source, (int)procedure.Attach, currentManager, currentDetail);
			if (!string.Equals(currentFingerprint, snapshot.Fingerprint,
				StringComparison.Ordinal))
			{
				Popup.Show("The catalogue row changed since this graft was made. It may describe the receipt, but cannot redirect or price its removal. Nothing was charged.");
				return;
			}
			if (ActiveRemovalJob(Actor) != null)
			{
				Popup.Show("A live removal receipt already follows you. Recover it before asking for another procedure.");
				return;
			}
			int priorReceiptCount = RemovalReceiptCount(Actor);
			if (priorReceiptCount >= KingdomLabRules.MaxEffectRows)
			{
				Popup.Show("The bounded patient removal archive is full. No new receipt, charge, or body action was started.");
				return;
			}
			int price = procedure.Cost / 4;
			if (Popup.ShowYesNoCancel("Have " + procedure.Named + " taken off?\n\n{{rules|--}} It costs {{C|"
				+ price + "}} drams and returns nothing. What was kept for it is spent and stays spent."
				+ (procedure.IsNamed ? "\n{{r|--}} It was a once-ever procedure. Taking it off does not give you the once back."
					: "")) != DialogResult.Yes)
			{
				return;
			}
			KingdomSurvey survey = (Actor.CurrentZone == null) ? null : KingdomSurvey.Take(Actor.CurrentZone, System);
			KingdomWaterDebit debit = null;
			if (price > 0 && (survey == null || !survey.TryReserveExactWater(price, out debit)))
			{
				Popup.Show("The stores cannot reserve exactly {{C|" + price + "}} drams. Nothing was taken off and no water was spent.");
				return;
			}
			r_KingdomLabRemovalJob job = new r_KingdomLabRemovalJob
			{
				RemovalId = Guid.NewGuid().ToString("N"),
				ProcedureKey = procedure.Key,
				OriginalJobId = snapshot.JobId,
				PatientId = Actor.ID,
				GameId = The.Game.GameID,
				RealmId = realmId,
				RealmFoundedTick = System.FoundedTick,
				BodyPartId = snapshot.BodyPartId,
				BearerId = snapshot.BearerId,
				City = City ?? "",
				ContractVersion = KingdomLabRules.EffectContractVersion,
				FrozenName = procedure.Named,
				FrozenGrants = snapshot.Grants,
				FrozenSource = snapshot.Source,
				FrozenAttach = snapshot.Attach,
				FrozenManager = snapshot.Manager,
				FrozenDetail = snapshot.Detail,
				FrozenFingerprint = snapshot.Fingerprint,
				EffectNonce = snapshot.EffectNonce,
				PartOrdinal = snapshot.PartOrdinal,
				WaterOwed = price,
				WaterPaid = 0,
				Phase = (int)KingdomLabRemovalPhase.Funding
			};
			job.ChronicleEventId = "lab:remove:" + job.RemovalId + ":chronicle";
			job.AnnounceEventId = "lab:remove:" + job.RemovalId + ":message";
			job.Normalize();
			if (job.SchemaQuarantined)
			{
				debit?.Rollback();
				Popup.Show(job.Fault);
				return;
			}
			try
			{
				Actor.AddPart(job);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: removal receipt publication threw (" + ex.Message + ")");
			}
			if (KingdomProcedures.ReferencePartOrdinal(Actor, job) < 0
				|| !ReferenceEquals(job.ParentObject, Actor)
				|| RemovalReceiptCount(Actor) != priorReceiptCount + 1)
			{
				debit?.Rollback();
				job.State = KingdomLabRemovalPhase.Cancelled;
				try
				{
					if (KingdomProcedures.ReferencePartOrdinal(Actor, job) >= 0)
						Actor.RemovePart(job);
				}
				catch { }
				Popup.Show("The patient-side removal receipt was absent or duplicated during publication. Nothing was spent or removed.");
				return;
			}
			KingdomLabOwnedTarget ignored;
			target = KingdomProcedures.ClassifyOwned(Actor, snapshot, out ignored);
			if (target != KingdomLabOwnedTargetState.Present)
			{
				debit?.Rollback();
				if (target == KingdomLabOwnedTargetState.Absent)
				{
					ArchiveCleanAbsentRemoval(Actor, job, procedure, snapshot);
					Popup.Show("The exact graft became absent while its receipt was attached. Nothing was charged and no governance action was committed.");
				}
				else
				{
					job.State = KingdomLabRemovalPhase.Quarantined;
					job.Fault = "The exact target became uncertain before payment. The patient receipt is quarantined and no water or effect was touched.";
					Popup.Show(job.Fault);
				}
				return;
			}
			if (price <= 0)
			{
				job.State = KingdomLabRemovalPhase.Paid;
			}
			else
			{
				debit.Commit();
				target = KingdomProcedures.ClassifyOwned(Actor, snapshot, out ignored);
				if (target != KingdomLabOwnedTargetState.Present)
				{
					bool compensated = debit.Rollback();
					MergeRemovalWater(job, debit);
					if (target == KingdomLabOwnedTargetState.Absent && compensated
						&& job.WaterPaid == 0 && job.WaterLost == 0 && !job.WaterQuarantined)
					{
						ArchiveCleanAbsentRemoval(Actor, job, procedure, snapshot);
						Popup.Show("The exact graft became absent during water callbacks. The debit was compensated exactly; no removal success or governance action was claimed.");
					}
					else
					{
						job.State = KingdomLabRemovalPhase.Quarantined;
						job.Fault = "The exact target changed during water callbacks. Compensation was measured; the receipt is quarantined and no replacement was touched.";
						EnsureRemovalGovernance(job);
						Popup.Show(job.Fault);
					}
					return;
				}
				MergeRemovalWater(job, debit);
				job.State = KingdomLabRules.RemovalFundingPhase(job.WaterOwed,
					job.WaterPaid, job.WaterQuarantined);
			}
			if (job.State == KingdomLabRemovalPhase.FundingRecovery
				&& job.WaterPaid == 0 && job.WaterLost == 0 && !job.WaterQuarantined)
			{
				DiscardCleanRemovalReceipt(Actor, job);
				Popup.Show("The exact water debit refused cleanly. Nothing was spent or removed, and the action remains free.");
				return;
			}
			EnsureRemovalGovernance(job);
			if (job.State == KingdomLabRemovalPhase.Paid)
			{
				AttemptRemoval(Actor, System, job, procedure);
			}
			else
			{
				Popup.Show(job.WaterQuarantined
					? "The persisted water receipt is uncertain and has been quarantined. No effect was touched."
					: "Part of the exact water price was measured. The persisted receipt will retry only its outstanding balance; no effect was touched.");
			}
		}

		private static void ManageRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job)
		{
			Job.Normalize();
			LabProcedure procedure = FrozenRemovalProcedure(Job);
			if (!CurrentRemovalAuthority(Actor, System, Job) || procedure == null)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = string.IsNullOrEmpty(Job.Fault)
					? "The removal receipt cannot prove its exact patient, realm lineage, or frozen contract. It offers no action."
					: Job.Fault;
				EnsureRemovalGovernance(Job);
				Popup.Show(Job.Fault);
				return;
			}
			EnsureRemovalGovernance(Job);
			string receipt = RemovalReceipt(Job, procedure);
			KingdomLabOwnershipSnapshot snapshot = RemovalSnapshot(Job);
			KingdomLabOwnedTarget ignored;
			KingdomLabOwnedTargetState physical = KingdomProcedures.ClassifyOwned(Actor,
				snapshot, out ignored);
			if (physical == KingdomLabOwnedTargetState.Uncertain)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The frozen effect identity or patient-slot bearer is uncertain. No payment, cleanup, or body callback will run.";
				Popup.Show(RemovalReceipt(Job, procedure));
				return;
			}
			if (physical == KingdomLabOwnedTargetState.Absent)
			{
				bool durable = Job.WaterPaid > 0 || Job.WaterLost > 0 || Job.WaterQuarantined
					|| Job.EffectRemoved;
				if (!durable)
				{
					ArchiveCleanAbsentRemoval(Actor, Job, procedure, snapshot);
					Popup.Show("The exact graft is absent. The unspent receipt was cleaned without governance or success tellings.");
					return;
				}
				CompleteRemoval(Actor, System, Job, procedure, snapshot);
				Popup.Show(RemovalReceipt(Job, procedure));
				Job.ReceiptPresented = true;
				return;
			}
			if (Job.State == KingdomLabRemovalPhase.Funding
				|| Job.State == KingdomLabRemovalPhase.FundingRecovery)
			{
				bool clean = Job.WaterPaid == 0 && Job.WaterLost == 0
					&& Job.WaterMeasurementExact && !Job.WaterQuarantined;
				string[] options = clean
					? new string[] { "Retry only the outstanding exact water.",
						"Discard this clean, unspent receipt; keep the action free.",
						"Leave the receipt preserved." }
					: new string[] { "Retry only the outstanding exact water.",
						"Leave the receipt preserved." };
				int choice = Popup.PickOption(Title: "recover removal payment", Intro: receipt,
					Options: options, AllowEscape: true);
				if (choice == 0)
				{
					RecoverRemovalFunding(Actor, System, Job, procedure);
				}
				else if (clean && choice == 1)
				{
					DiscardCleanRemovalReceipt(Actor, Job);
				}
				return;
			}
			if (Job.State == KingdomLabRemovalPhase.Paid
				|| Job.State == KingdomLabRemovalPhase.Removing
				|| Job.State == KingdomLabRemovalPhase.RemovalRecovery
				|| Job.State == KingdomLabRemovalPhase.Removed
				|| Job.State == KingdomLabRemovalPhase.Complete)
			{
				int choice = Popup.PickOption(Title: "recover exact removal", Intro: receipt,
					Options: new string[] { "Retry the exact tracked effect; charge no more water.",
						"Leave the receipt preserved." }, AllowEscape: true);
				if (choice == 0)
				{
					AttemptRemoval(Actor, System, Job, procedure);
				}
				return;
			}
			Popup.Show(RemovalReceipt(Job, procedure));
		}

		private static void RecoverRemovalFunding(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure)
		{
			if (Job.WaterQuarantined || !CurrentRemovalAuthority(Actor, System, Job))
			{
				return;
			}
			KingdomLabOwnershipSnapshot snapshot = RemovalSnapshot(Job);
			KingdomLabOwnedTarget ignored;
			KingdomLabOwnedTargetState before = KingdomProcedures.ClassifyOwned(Actor,
				snapshot, out ignored);
			if (before == KingdomLabOwnedTargetState.Absent)
			{
				if (Job.WaterPaid > 0 || Job.WaterLost > 0)
					CompleteRemoval(Actor, System, Job, Procedure, snapshot);
				else if (KingdomProcedures.CleanupOwned(Actor, Procedure, snapshot))
					ArchiveCleanAbsentRemoval(Actor, Job, Procedure, snapshot);
				return;
			}
			if (before != KingdomLabOwnedTargetState.Present)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact target is uncertain before outstanding water. Nothing further was charged.";
				return;
			}
			int outstanding = Math.Max(0, Job.WaterOwed - Job.WaterPaid);
			if (outstanding > 0)
			{
				KingdomSurvey survey = (Actor.CurrentZone == null) ? null
					: KingdomSurvey.Take(Actor.CurrentZone, System);
				KingdomWaterDebit debit;
				if (survey == null || !survey.TryReserveExactWater(outstanding, out debit))
				{
					Popup.Show("The stores cannot reserve the exact outstanding {{C|" + outstanding
						+ "}} drams. The receipt was unchanged.");
					return;
				}
				KingdomLabOwnedTargetState preCommit = KingdomProcedures.ClassifyOwned(Actor,
					snapshot, out ignored);
				if (preCommit != KingdomLabOwnedTargetState.Present)
				{
					debit.Rollback();
					if (preCommit == KingdomLabOwnedTargetState.Absent
						&& Job.WaterPaid == 0 && Job.WaterLost == 0)
					{
						ArchiveCleanAbsentRemoval(Actor, Job, Procedure, snapshot);
					}
					else
					{
						Job.State = KingdomLabRemovalPhase.Quarantined;
						Job.Fault = "The exact target changed after water reservation but before commit. Nothing was charged or touched.";
					}
					return;
				}
				debit.Commit();
				KingdomLabOwnedTargetState afterCommit = KingdomProcedures.ClassifyOwned(Actor,
					snapshot, out ignored);
				if (afterCommit != KingdomLabOwnedTargetState.Present)
				{
					bool compensated = debit.Rollback();
					MergeRemovalWater(Job, debit);
					if (afterCommit == KingdomLabOwnedTargetState.Absent && compensated
						&& Job.WaterPaid == 0 && Job.WaterLost == 0 && !Job.WaterQuarantined)
					{
						ArchiveCleanAbsentRemoval(Actor, Job, Procedure, snapshot);
						return;
					}
					Job.State = KingdomLabRemovalPhase.Quarantined;
					Job.Fault = "The exact target changed during retry water callbacks. Compensation was measured; no replacement was touched.";
					EnsureRemovalGovernance(Job);
					return;
				}
				MergeRemovalWater(Job, debit);
			}
			Job.State = KingdomLabRules.RemovalFundingPhase(Job.WaterOwed,
				Job.WaterPaid, Job.WaterQuarantined);
			EnsureRemovalGovernance(Job);
			if (Job.State == KingdomLabRemovalPhase.Paid)
			{
				AttemptRemoval(Actor, System, Job, Procedure);
			}
			else
			{
				Popup.Show(RemovalReceipt(Job, Procedure));
			}
		}

		private static void MergeRemovalWater(r_KingdomLabRemovalJob Job,
			KingdomWaterDebit Debit)
		{
			if (Job == null || Debit == null)
			{
				return;
			}
			KingdomLabWaterClaim claim = KingdomLabRules.MergeWaterClaim(Job.WaterOwed,
				Job.WaterPaid, Job.WaterLost, Job.WaterQuarantined,
				Debit.Spent, Debit.Lost, Debit.MeasurementExact);
			Job.WaterMeasurementExact = Job.WaterMeasurementExact && Debit.MeasurementExact;
			Job.WaterPaid = claim.Paid;
			Job.WaterLost = claim.Lost;
			Job.WaterQuarantined = claim.Quarantined;
			if (claim.Quarantined)
			{
				Job.Fault = "Water vessel identity or composition became uncertain. Automatic retry is quarantined so the apparent balance cannot be charged twice."
					+ (string.IsNullOrEmpty(Debit.Failure) ? "" : (" " + Debit.Failure));
			}
			else if (!claim.Settled && !string.IsNullOrEmpty(Debit.Failure))
			{
				Job.Fault = Debit.Failure;
			}
		}

		private static void AttemptRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure)
		{
			if (!CurrentRemovalAuthority(Actor, System, Job)) return;
			if (KingdomLabRules.RemovalFundingPhase(Job.WaterOwed, Job.WaterPaid,
				Job.WaterQuarantined) != KingdomLabRemovalPhase.Paid)
			{
				Job.State = Job.WaterQuarantined ? KingdomLabRemovalPhase.Quarantined
					: KingdomLabRemovalPhase.FundingRecovery;
				return;
			}
			KingdomLabOwnershipSnapshot snapshot = RemovalSnapshot(Job);
			KingdomLabOwnedTarget ignored;
			KingdomLabOwnedTargetState before = KingdomProcedures.ClassifyOwned(Actor,
				snapshot, out ignored);
			if (before == KingdomLabOwnedTargetState.Absent)
			{
				CompleteRemoval(Actor, System, Job, Procedure, snapshot);
				return;
			}
			if (before != KingdomLabOwnedTargetState.Present)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact tracked effect cannot be distinguished from a foreign same-class replacement. Nothing was touched.";
				return;
			}
			Job.State = KingdomLabRemovalPhase.Removing;
			KingdomLabOwnedTargetState after;
			try
			{
				after = KingdomProcedures.RemoveExact(Actor, Procedure, snapshot);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: exact removal threw (" + ex.Message + ")");
					after = KingdomProcedures.ClassifyOwned(Actor, snapshot, out ignored);
				Job.Fault = "The exact engine removal threw: " + ex.Message;
			}
			Job.State = KingdomLabRules.RemovalObservation(after, RemovingStarted: true);
			if (after == KingdomLabOwnedTargetState.Absent)
			{
				CompleteRemoval(Actor, System, Job, Procedure, snapshot);
			}
			else if (after == KingdomLabOwnedTargetState.Present)
			{
				Job.Fault = "The exact owned effect remains present. Retry charges no more water.";
			}
			else
			{
				Job.Fault = "Removal returned an uncertain identity. The receipt is quarantined; no class scan will be attempted.";
			}
		}

		private static void CompleteRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			Job.EffectRemoved = true;
			Job.State = KingdomLabRemovalPhase.Removed;
			EnsureRemovalGovernance(Job);
			FinishRemoval(Actor, System, Job, Procedure, Snapshot);
		}

		private static void FinishRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure)
		{
			KingdomLabOwnershipSnapshot snapshot = RemovalSnapshot(Job);
			FinishRemoval(Actor, System, Job, Procedure, snapshot);
		}

		private static void FinishRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			KingdomLabOwnedTarget ignored;
			KingdomLabOwnedTargetState observed = KingdomProcedures.ClassifyOwned(Actor,
				Snapshot, out ignored);
			if (observed == KingdomLabOwnedTargetState.Present)
			{
				Job.State = KingdomLabRemovalPhase.RemovalRecovery;
				Job.Fault = "The exact effect is present again. Terminal cleanup and tellings were stopped; retry removes only that identity.";
				return;
			}
			if (observed != KingdomLabOwnedTargetState.Absent)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "Terminal observation is uncertain. No marker, record, chronicle, or message was changed.";
				return;
			}
			try { Actor.WantToReequip(); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: post-removal reequip callback threw (" + ex.Message + ")");
			}
			observed = KingdomProcedures.ClassifyOwned(Actor, Snapshot, out ignored);
			if (observed != KingdomLabOwnedTargetState.Absent)
			{
				Job.State = observed == KingdomLabOwnedTargetState.Present
					? KingdomLabRemovalPhase.RemovalRecovery : KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact target changed during terminal callbacks. Tellings were stopped.";
				return;
			}
			if (!Job.Chronicled)
			{
				try
				{
					Job.Chronicled = KingdomChronicle.RecordOnce(System,
						Job.ChronicleEventId, KingdomLabRules.RemovedTelling(
							Job.FrozenName, KingdomPresentation.Rich(Job.City)));
				}
				catch (Exception ex)
				{
					Job.Fault = "The keyed chronicle outbox stopped: " + ex.Message;
					return;
				}
				if (!Job.Chronicled) return;
			}
			observed = KingdomProcedures.ClassifyOwned(Actor, Snapshot, out ignored);
			if (observed != KingdomLabOwnedTargetState.Absent)
			{
				Job.State = observed == KingdomLabOwnedTargetState.Present
					? KingdomLabRemovalPhase.RemovalRecovery : KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact target changed during chronicle publication. The completion message was stopped.";
				return;
			}
			if (!KingdomLabRules.MessageSettled(
				(KingdomLabMessagePhase)Job.TerminalMessagePhase))
			{
				KingdomLabMessagePhase phase = PublishMessage(ref Job.TerminalMessagePhase,
					ref Job.TerminalMessageText, Job.AnnounceEventId,
					"{{K|It is off. Nothing was given back for it.}}");
				Job.Announced = phase == KingdomLabMessagePhase.Delivered;
			}
			observed = KingdomProcedures.ClassifyOwned(Actor, Snapshot, out ignored);
			if (observed != KingdomLabOwnedTargetState.Absent)
			{
				Job.State = observed == KingdomLabOwnedTargetState.Present
					? KingdomLabRemovalPhase.RemovalRecovery : KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact target changed during completion presentation. Completion was revoked.";
				return;
			}
			// Ownership proof stays live through every external callback above. This is
			// essential for modifier-only mutation removal: the exact unlisted runtime part
			// proves our contribution absent until all tellings have settled.
			if (!Job.OwnershipCleaned)
			{
				if (!KingdomProcedures.CleanupOwned(Actor, Procedure, Snapshot))
				{
					Job.State = KingdomLabRemovalPhase.RemovalRecovery;
					Job.Fault = "Exact ownership-marker cleanup did not complete. It will retry without touching a replacement.";
					return;
				}
				Job.OwnershipCleaned = true;
				Job.RecordCleaned = true;
			}
			if (!Job.Chronicled || !KingdomLabRules.MessageSettled(
				(KingdomLabMessagePhase)Job.TerminalMessagePhase)) return;
			if (!RecordReplayProof("remove:" + Job.RemovalId))
			{
				Job.State = KingdomLabRemovalPhase.RemovalRecovery;
				Job.Fault = "The bounded replay proof could not be persisted; the exact tombstone and removal receipt remain.";
				return;
			}
			if (!KingdomProcedures.PurgeOwnedTombstone(Actor, Snapshot))
			{
				Job.State = KingdomLabRemovalPhase.RemovalRecovery;
				Job.Fault = "The exact effect tombstone could not be purged after all tellings settled.";
				return;
			}
			Job.Fault = "";
			Job.State = KingdomLabRemovalPhase.Complete;
			try { Actor.RemovePart(Job); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: terminal removal receipt cleanup threw (" + ex.Message + ")");
			}
		}

		private static bool ArchiveCleanAbsentRemoval(GameObject Actor,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			if (!KingdomProcedures.CleanupOwned(Actor, Procedure, Snapshot))
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The unspent receipt could not prove exact marker cleanup. It remains quarantined and offers no charge or body callback.";
				return false;
			}
			Job.OwnershipCleaned = true;
			Job.RecordCleaned = true;
			Job.State = KingdomLabRemovalPhase.Cancelled;
			Job.Fault = "The exact effect was already absent. This clean receipt was archived without charge, governance, or success tellings.";
			return true;
		}

		private static void DiscardCleanRemovalReceipt(GameObject Actor,
			r_KingdomLabRemovalJob Job)
		{
			if (Actor == null || Job == null || Job.WaterPaid != 0 || Job.WaterLost != 0
				|| Job.WaterQuarantined || !Job.WaterMeasurementExact
				|| !string.Equals(Job.PatientId, Actor.ID, StringComparison.Ordinal)) return;
			Job.State = KingdomLabRemovalPhase.Cancelled;
			Job.Fault = "Clean unspent receipt discarded; no procedure debt was inherited.";
			try { Actor.RemovePart(Job); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: clean removal-receipt discard threw after durable cancellation ("
					+ ex.Message + ")");
			}
		}

		private static void EnsureRemovalGovernance(r_KingdomLabRemovalJob Job)
		{
			if (Job == null || Job.GovernanceCommitted)
			{
				return;
			}
			bool durable = Job.WaterPaid > 0 || Job.WaterLost > 0 || Job.WaterQuarantined
				|| Job.EffectRemoved;
			if (durable && KingdomGovernanceScope.Commit("remove lab procedure"))
			{
				Job.GovernanceCommitted = true;
			}
		}

		private static string RemovalReceipt(r_KingdomLabRemovalJob Job,
			LabProcedure Procedure)
		{
			return KingdomLabRules.Named(Procedure.Named) + " removal: water {{C|"
				+ Job.WaterPaid + "/" + Job.WaterOwed + "}} measured"
				+ ((Job.WaterLost > Job.WaterPaid) ? (" (" + Job.WaterLost
					+ " physically lost)") : "")
				+ (Job.WaterQuarantined ? " {{r|[quarantined]}}" : "")
				+ "; phase {{W|" + Job.State + "}}."
				+ (string.IsNullOrEmpty(Job.Fault) ? "" : ("\n\n{{r|" + Job.Fault + "}}"));
		}

		// ==================================================================================
		// Reading the world
		// ==================================================================================

		/// <summary>
		/// Which records this hall could perform at this place, for this founder, today.
		/// <para>
		/// The visibility law is enforced HERE and by the accessor rather than by discipline: a
		/// named procedure the founder has not found is dropped before any row, count or refusal is
		/// computed, so "cannot have it" and "have never heard of it" are the same absence of a row.
		/// </para>
		/// </summary>
		private static List<LabProcedure> Candidates(List<LabSlot> Anatomy, int At, int Rung,
			List<GameObject> Kept, r_KingdomLabRecord Record, List<string> Roster)
		{
			List<LabProcedure> offers = new List<LabProcedure>();
			List<LabProcedure> all = KingdomProcedures.All;
			for (int i = 0; i < all.Count; i++)
			{
				LabProcedure procedure = all[i];
				if (!KingdomProcedures.Discovered(procedure) || Record.Refuses(procedure.Key))
				{
					continue;
				}
				if (procedure.IsNamed && Record.AlreadyHad(procedure.Key))
				{
					continue;
				}
				// The record's own Knowledge gate, read through the SHIPPED roster grammar and
				// nothing of ours: a procedure gates on a research node, a rite, a taught disk or a
				// certified machine with one attribute, exactly as a building does, and a third
				// party's procedure gates on a third party's research with no code at all.
				if (!KingdomProcedureRules.KnowledgeMet(Roster, procedure.Knowledge))
				{
					continue;
				}
				if (Rung < procedure.MinRung || CountFor(Kept, procedure) < procedure.Preserved)
				{
					continue;
				}
				if (KingdomProcedureRules.JudgeSlot(procedure, Anatomy[At], KingdomProcedures.Categories(procedure)) == LabVerdict.Allowed)
				{
					offers.Add(procedure);
				}
			}
			return offers;
		}

		private static List<GameObject> KeptParts(GameObject Actor)
		{
			List<GameObject> kept = new List<GameObject>();
			foreach (GameObject item in Actor.GetInventoryAndEquipment())
			{
				if (item != null && item.GetIntProperty(KeptProperty) == 1)
				{
					kept.Add(item);
				}
			}
			return kept;
		}

		private static int TotalKept(List<GameObject> Kept)
		{
			int total = 0;
			for (int i = 0; i < Kept.Count; i++)
			{
				total += Kept[i].Count;
			}
			return total;
		}

		/// <summary>How many kept parts would answer this record: stamped with the class it grants,
		/// and inside its band if it names one.</summary>
		private static int CountFor(List<GameObject> Kept, LabProcedure Procedure)
		{
			int total = 0;
			for (int i = 0; i < Kept.Count; i++)
			{
				string stamp = Kept[i].GetStringProperty(KingdomProcedures.StampProperty);
				if (KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					&& KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					total += Kept[i].Count;
				}
			}
			return total;
		}

		private static GameObject FirstSourceFor(List<GameObject> Kept, LabProcedure Procedure)
		{
			for (int i = 0; i < Kept.Count; i++)
			{
				string stamp = Kept[i].GetStringProperty(KingdomProcedures.StampProperty);
				if (KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					&& KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					return Kept[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Builds and preflights the physical receipt before water or body changes. Qud 2.0.211.51's
		/// <c>Stacker.BeforeDestroyObjectEvent</c> decrements and vetoes a non-obliterating destroy
		/// whenever Count is above one. The same check with <c>Obliterate=true</c> bypasses only that
		/// decrement, not any other veto, so every terminal source is asked before any is lost.
		/// </summary>
		private static KingdomKeptSpendPhase PrepareKeptSpend(List<GameObject> Kept, LabProcedure Procedure,
			out KeptSpendPreparation Preparation, int Owed = -1)
		{
			Preparation = null;
			List<GameObject> sources = new List<GameObject>();
			List<string> stamps = new List<string>();
			List<int> counts = new List<int>();
			for (int i = 0; Kept != null && i < Kept.Count; i++)
			{
				GameObject item = Kept[i];
				if (!GameObject.Validate(item) || item.Count <= 0 || sources.Contains(item))
				{
					continue;
				}
				string stamp = item.GetStringProperty(KingdomProcedures.StampProperty);
				if (!KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					|| !KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					continue;
				}
				sources.Add(item);
				stamps.Add(stamp);
				counts.Add(item.Count);
			}
			KingdomKeptSpendPlan plan;
			int owed = (Owed >= 0) ? Owed : Procedure.Preserved;
			if (!KingdomLabRules.TryPlanKeptSpend(counts, owed, out plan))
			{
				return KingdomKeptSpendPhase.RefusedClean;
			}
			Preparation = new KeptSpendPreparation(sources, stamps, Procedure, plan);
			return PreflightKeptSpend(Preparation);
		}

		private static KingdomKeptSpendPhase PreflightKeptSpend(KeptSpendPreparation Preparation)
		{
			KingdomKeptSpendPlan plan = Preparation.Plan;
			// Destroy() itself dispatches BeforeDestroyObjectEvent. Calling Check here dispatched
			// the destructive callback twice for every terminal source and let the first callback
			// mutate topology before the durable spend began. Preflight is therefore observation
			// only; every consumed unit gets exactly the one callback owned by Destroy below.
			if (!SourcesAtOriginal(Preparation))
			{
				return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: false,
					CountsApplied: false, Finalized: 0, OperationRefused: true,
					CountsRestored: false);
			}
			return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: true,
				CountsApplied: false, Finalized: 0, OperationRefused: false, CountsRestored: true);
		}

		/// <summary>
		/// Spends every unit through Qud's ordinary <c>Destroy</c> path. Stacker owns each decrement;
		/// the last unit owns the real object lifecycle. No direct count write may bypass a callback.
		/// If a later final unit refuses, reversible stack decrements are restored only while no whole
		/// source has vanished; after that, the caller receives Partial and keeps the receipt.
		/// </summary>
		private static KingdomKeptSpendPhase SpendKeptExact(KeptSpendPreparation Preparation)
		{
			if (Preparation == null || !SourcesAtOriginal(Preparation))
			{
				return KingdomKeptSpendPhase.Partial;
			}
			KingdomKeptSpendPlan plan = Preparation.Plan;
			List<int> changed = new List<int>();
			int finalized = 0;
			for (int i = 0; i < plan.Steps.Count; i++)
			{
				KingdomKeptSpendStep step = plan.Steps[i];
				GameObject item = Preparation.Sources[step.Source];
				if (!changed.Contains(step.Source))
				{
					changed.Add(step.Source);
				}
				for (int unit = 0; unit < step.Taken; unit++)
				{
					int expected = step.Original - unit;
					if (!GameObject.Validate(item) || item.Count != expected)
					{
						bool restored = finalized == 0
							&& RestoreChangedCounts(Preparation, changed);
						return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: true,
							CountsApplied: finalized > 0, Finalized: finalized,
							OperationRefused: true, CountsRestored: restored);
					}
					try
					{
						item.Destroy(null, Silent: true);
					}
					catch (Exception ex)
					{
						KingdomLog.Log("lab: kept unit release threw (" + ex.Message + ")");
					}
					bool last = expected == 1;
					bool measured = last ? !GameObject.Validate(item)
						: (GameObject.Validate(item) && item.Count == expected - 1);
					if (!measured)
					{
						bool restored = finalized == 0
							&& RestoreChangedCounts(Preparation, changed);
						return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: true,
							CountsApplied: finalized > 0, Finalized: finalized,
							OperationRefused: true, CountsRestored: restored);
					}
				}
				if (step.NeedsFinalization)
				{
					finalized++;
				}
			}
			bool exact = SourcesAtPlannedResult(Preparation);
			return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: true,
				CountsApplied: true, Finalized: finalized, OperationRefused: !exact,
				CountsRestored: false);
		}

		private static bool SourcesAtOriginal(KeptSpendPreparation Preparation)
		{
			for (int i = 0; i < Preparation.Plan.Steps.Count; i++)
			{
				KingdomKeptSpendStep step = Preparation.Plan.Steps[i];
				GameObject item = Preparation.Sources[step.Source];
				string stamp = GameObject.Validate(item)
					? item.GetStringProperty(KingdomProcedures.StampProperty)
					: null;
				if (!GameObject.Validate(item) || item.Count != step.Original
					|| !string.Equals(stamp, Preparation.Stamps[step.Source], StringComparison.Ordinal)
					|| !KingdomProcedureRules.StampCarries(stamp, Preparation.Procedure.Grants)
					|| !KingdomProcedureRules.MagnitudeAdmits(Preparation.Procedure, stamp))
				{
					return false;
				}
			}
			return true;
		}

		private static bool SourcesAtPlannedResult(KeptSpendPreparation Preparation)
		{
			for (int i = 0; i < Preparation.Plan.Steps.Count; i++)
			{
				KingdomKeptSpendStep step = Preparation.Plan.Steps[i];
				GameObject item = Preparation.Sources[step.Source];
				string stamp = GameObject.Validate(item)
					? item.GetStringProperty(KingdomProcedures.StampProperty)
					: null;
				if (step.NeedsFinalization ? GameObject.Validate(item)
					: (!GameObject.Validate(item) || item.Count != step.Remaining
						|| !string.Equals(stamp, Preparation.Stamps[step.Source], StringComparison.Ordinal)
						|| !KingdomProcedureRules.StampCarries(stamp, Preparation.Procedure.Grants)
						|| !KingdomProcedureRules.MagnitudeAdmits(Preparation.Procedure, stamp)))
				{
					return false;
				}
			}
			return true;
		}

		private static bool RestoreChangedCounts(KeptSpendPreparation Preparation, List<int> Changed)
		{
			for (int i = Changed.Count - 1; i >= 0; i--)
			{
				int source = Changed[i];
				KingdomKeptSpendStep step = StepForSource(Preparation.Plan, source);
				GameObject item = Preparation.Sources[source];
				try
				{
					if (GameObject.Validate(item) && item.Count != step.Original)
					{
						item.Count = step.Original;
						item.FlushTransientCache();
						item.FlushContextWeightCaches();
						item.InInventory?.Inventory?.FlushWeightCache();
					}
				}
				catch (Exception ex)
				{
					KingdomLog.Log("lab: kept count rollback threw (" + ex.Message + ")");
				}
			}
			return SourcesAtOriginal(Preparation);
		}

		private static KingdomKeptSpendStep StepForSource(KingdomKeptSpendPlan Plan, int Source)
		{
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				if (Plan.Steps[i].Source == Source)
				{
					return Plan.Steps[i];
				}
			}
			return new KingdomKeptSpendStep(Source, 0, 0);
		}

		/// <summary>Counts the actual engine effect, independent of the lab record. Callers compare
		/// before and after so a callback fault after mutation cannot turn a completed graft free.</summary>
		private static int ProcedurePresence(GameObject Actor, LabProcedure Procedure)
		{
			try
			{
				if (Actor == null || Procedure == null)
				{
					return -1;
				}
				if (Procedure.Source == LabSource.Mutation)
				{
					XRL.World.Parts.Mutations mutations = Actor.GetPart<XRL.World.Parts.Mutations>();
					return KingdomLabRules.MutationPresence(
						mutations != null && mutations.HasMutation(Procedure.Grants),
						Actor.GetPart(Procedure.Grants) is XRL.World.Parts.Mutation.BaseMutation);
				}
				List<XRL.World.Anatomy.BodyPart> parts = Actor.Body?.GetParts();
				if (Procedure.Source == LabSource.Limb)
				{
					int limbs = 0;
					string manager = KingdomProcedures.ManagerFor(Procedure.Key);
					for (int i = 0; parts != null && i < parts.Count; i++)
					{
						if (parts[i] != null && string.Equals(parts[i].Manager, manager,
							StringComparison.OrdinalIgnoreCase))
						{
							limbs++;
						}
					}
					return limbs;
				}
				int held = (Actor.GetPart(Procedure.Grants) == null) ? 0 : 1;
				List<GameObject> seen = new List<GameObject>();
				for (int i = 0; parts != null && i < parts.Count; i++)
				{
					GameObject bearer = parts[i]?.DefaultBehavior;
					if (!GameObject.Validate(bearer) || seen.Contains(bearer))
					{
						continue;
					}
					seen.Add(bearer);
					if (bearer.GetPart(Procedure.Grants) != null)
					{
						held++;
					}
				}
				return held;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: procedure presence read threw (" + ex.Message + ")");
				return -1;
			}
		}

		/// <summary>
		/// The rung this building performs at. Read off the building's own part rather than off a
		/// survey, because the founder is standing in front of it and the thing they are standing in
		/// front of is the authority on what it can do.
		/// </summary>
		private static int RungAt(GameObject Building)
		{
			if (Building == null)
			{
				return -1;
			}
			if (Building.HasPart("r_KingdomChimericTheatre"))
			{
				return KingdomProcedureRules.RungTheatre;
			}
			if (Building.HasPart("r_KingdomGraftingHall"))
			{
				return KingdomProcedureRules.RungHall;
			}
			return Building.HasPart("r_KingdomVatHouse") ? KingdomProcedureRules.RungVat : KingdomProcedureRules.RungSlab;
		}

		/// <summary>The lodged savant's name, or null when the hall has nobody who knows the work.
		/// Derived from the crew the lodging machinery already placed &mdash; the hall assigns
		/// nobody, exactly as Addendum 6 says a great work never does.</summary>
		private static string SavantAt(KingdomSystem System)
		{
			return Simulation.City.KingdomResidents.HeadName(System);
		}
	}
}
