using System;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>
	/// Exact receipt for TAF's one namespaced base-allegiance slot. This part never owns the
	/// Brain, its temporary allegiance chain, flags, leader, conversation, quests or lifecycle.
	/// </summary>
	[Serializable]
	public sealed class r_KingdomCitizenship : IPart
	{
		public int ReceiptVersion;
		public KingdomCitizenshipPhase Phase;
		public KingdomCitizenshipPriorKind PriorKind;
		public int PriorValue;
		public int AppliedValue;
		public string OwnerRealmId = "";
		public string OwnerSettlementId = "";
		public string FactionId = "";
		public string BodyObjectId = "";
		public int EnrollmentReason;
		public int RemovalReason;
		public long AppliedTick;
		public long RemovedTick;
		public bool NoticePublished;
		public string Fault = "";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID
				|| ID == BeforeDeathRemovalEvent.ID;
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			try
			{
				// Couple resident authority to allegiance cleanup before either receipt can
				// advance. The legacy reporter may fire first or second; both paths are
				// idempotent. Direct removal below also covers a foreign/no-current realm.
				KingdomOffices.RecordDeath(ParentObject, E.Killer);
				string failure;
				KingdomCitizenship.TryRemove(The.Game?.GetSystem<KingdomSystem>(), ParentObject,
					KingdomCitizenshipRemovalReason.Death, out failure);
			}
			catch (Exception ex)
			{
				// Death belongs to the engine. A civic cleanup may fail closed, never veto it.
				KingdomLog.Log("citizenship: death hook left its exact receipt pending ("
					+ ex.GetType().Name + ")");
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (Phase == KingdomCitizenshipPhase.LegacyPriorUnknown)
			{
				E.Postfix.Append("\n{{rules|Citizenship receipt: the legacy writer may already have "
					+ "erased the native base-faction mixture and changed allegiance flags. Those "
					+ "facts are irrecoverable and are not guessed; leaving only relinquishes the "
					+ "exact realm slot still proved here.}}");
			}
			else if (Phase == KingdomCitizenshipPhase.Diverged)
			{
				E.Postfix.Append("\n{{R|Citizenship receipt diverged: the realm no longer owns the "
					+ "allegiance value it recorded, so it will not overwrite the body's live state.}}");
			}
			return base.HandleEvent(E);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomCitizenship));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomCitizenship));
			OwnerRealmId = OwnerRealmId ?? "";
			OwnerSettlementId = OwnerSettlementId ?? "";
			FactionId = FactionId ?? "";
			BodyObjectId = BodyObjectId ?? "";
			Fault = Fault ?? "";
		}
	}
}
