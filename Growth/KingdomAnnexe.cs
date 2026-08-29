using System;
using System.Collections.Generic;

using ThousandAndFirst;

// XRL.World.Parts, for the reason r_KingdomPlot, r_KingdomSeed, r_KingdomMirrorGate and the lab's
// four all state: GamePartBlueprint resolves a part named in XML as exactly
// "XRL.World.Parts.<Name>" and tries no other name. Only the parts move; everything they do lives
// in ThousandAndFirst.KingdomAnnexe below.
namespace XRL.World.Parts
{
	/// <summary>
	/// The becoming annexe: a clean room, a book, and the city's claim to write in it. The chrome
	/// half of the body doctrine, and the city's one purpose (Addendum 22 A1, Design B).
	/// <para>
	/// It builds no cybernetics of its own and it should not. Vanilla's becoming nooks already
	/// install, remove and price every implant in the game; what they will not do is admit a
	/// mutant, because <c>CyberneticsTerminal.IsAuthorized</c> asks whether the subject is on the
	/// Eaters' rolls (<c>XRL/UI/CyberneticsTerminal.cs:481-488</c>). So the annexe answers that
	/// question rather than replacing the machine that asks it, and the whole shipped cybernetics
	/// system works untouched.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomBecomingAnnexe : IPart
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
			E.AddAction("Register", "read the city's rolls", "r_OpenAnnexeRegister", null, 'g', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenAnnexeRegister" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("annexe register", delegate
				{
					KingdomAnnexe.OpenRegister(ParentObject, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// One person's enrolment, carried on the person, answering the one question vanilla asks.
	/// <para>
	/// <b>The part holds a claim, not an answer.</b> What it stores is which city wrote the roll
	/// and under what id; whether the roll still STANDS is read live off the realm's own cities.
	/// That is what makes secession bite without a single line of secession code: the rolls ride
	/// <c>KingdomSettlement.KeepersRoster</c>, the container that secession, rejoin, exile and
	/// return already move whole (Addendum 22 B1/B6), so a city that walks out takes the book with
	/// it and the machines go back to asking.
	/// </para>
	/// <para>
	/// <b>It only ever raises the answer, never lowers it.</b> <c>IsTrueKinEvent.Check</c> seeds
	/// from the genotype and hands the seed to every handler
	/// (<c>XRL/World/IsTrueKinEvent.cs:32-47</c>); writing <c>false</c> here would let a lapsed
	/// roll un-Kin a True Kin who happened to be carrying this part. It cannot, because this never
	/// writes <c>false</c>.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomEnrolled : IPart
	{
		/// <summary>The <c>GeneID</c> the roll was written under. The person, not the office &mdash;
		/// an heir is a different creature with a different id, so Addendum 21's succession
		/// honesty rule holds without anything here knowing what succession is.</summary>
		public string Who = "";

		/// <summary>The person as the founder reads them, kept for prose so no sentence anywhere
		/// has to hold a reference to a creature that may be dead.</summary>
		public string Named = "";

		/// <summary>The city that wrote it, for the telling.</summary>
		public string City = "";

		/// <summary>When, for the register's rows and the chronicle.</summary>
		public long Tick;

		/// <summary>Whether the founder has already been told this roll lapsed. STANDARDS 7b's
		/// once-flag, cleared the moment the roll is held again &mdash; a rejoin is not a second
		/// piece of bad news.</summary>
		public bool LapseAnnounced;

		/// <summary>Optional exact reciprocal-purpose authority that paid for this enrolment.</summary>
		public string PurposePairId = "";
		public long PurposePairEpoch;
		public string PurposeOperationId = "";
		public string PurposeAuthorityId = "";

		// Tick the live read was last taken on, and what it said. The event fires from every
		// tonic, every terminal and every haggle in the game, so the read past this happens at
		// most once per tick per creature. Deliberately NOT serialized: a cached answer is not
		// state, and a save that carried one would answer for a realm it was not asked about.
		[NonSerialized]
		private long CachedTick = -1L;

		[NonSerialized]
		private bool CachedHeld;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == PooledEvent<IsTrueKinEvent>.ID;
		}

		/// <summary>
		/// The whole override, and it is four lines because the door Freehold installed is four
		/// lines wide. <c>E.Object</c> is checked because a pooled event is dispatched to the
		/// object's parts and a part that answered for somebody else would be answering a question
		/// it was not asked.
		/// </summary>
		public override bool HandleEvent(IsTrueKinEvent E)
		{
			if (E.Object == ParentObject)
			{
				E.IsTrueKin = KingdomAnnexeRules.AnswersTrueKin(E.IsTrueKin, Held());
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Whether the realm still keeps this roll.
		/// <para>
		/// Preconditions: none. Side effects: may say the lapse line once. Failure mode: never
		/// throws &mdash; a failed read answers "not held", which closes a door rather than
		/// opening one, and this runs inside the engine's own event dispatch (STANDARDS 9).
		/// </para>
		/// </summary>
		public bool Held()
		{
			if (The.Game == null || string.IsNullOrEmpty(Who))
			{
				return false;
			}
			long now = The.Game.TimeTicks;
			if (CachedTick == now)
			{
				return CachedHeld;
			}
			bool held = false;
			KingdomSystem.Guard("enrolment read", delegate
			{
				held = KingdomAnnexe.RealmHolds(Who);
			});
			CachedTick = now;
			CachedHeld = held;
			if (held)
			{
				// Unsaid in silence, exactly as the mirror-gate's brownout is: coming back is not
				// a second piece of news, and a founder who rejoins should be able to be told
				// again if it ever happens twice.
				LapseAnnounced = false;
			}
			else if (!LapseAnnounced && ParentObject != null && ParentObject.IsPlayer())
			{
				LapseAnnounced = true;
				KingdomSystem.Guard("enrolment lapse", delegate
				{
					KingdomAnnexe.AnnounceLapse(City);
				});
			}
			return held;
		}

#if !TAF_TESTS
		/// <summary>
		/// This part has used named fields since its first shipped version. Keep that wire
		/// unframed: its first bytes are the named-field count, so inserting a marker now would
		/// make deployed saves unreadable. Missing names retain their field initializers; unknown
		/// names are consumed and ignored. The purpose-authority names are therefore additive,
		/// while an old record acquires no authority it never carried.
		/// </summary>
		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomEnrolled));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomEnrolled));
			Who = Who ?? "";
			Named = Named ?? "";
			City = City ?? "";
			PurposePairId = PurposePairId ?? "";
			PurposeOperationId = PurposeOperationId ?? "";
			PurposeAuthorityId = PurposeAuthorityId ?? "";
			CachedTick = -1L;
		}
#endif
	}
}
