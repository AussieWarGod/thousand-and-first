using System;
using XRL.World;
using XRL.World.AI;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Zero-value, non-citizen custody and interaction bridge for one exact guest.</summary>
	[Serializable]
	public sealed class r_KingdomFirstGuestBody : IPart
	{
		private const string Command = "r_TAF_FirstGuestChoice";
		public int Version = 1;
		public string CandidateId;
		public string OpportunityId;
		public string SettlementId;
		public string ObjectId;
		public string Marker;
		public string ZoneId;
		public int OriginalBrainFlags;
		[NonSerialized] public AllegianceSet OriginalAllegiance;
		public bool HadNoXP;
		public int OriginalNoXP;
		public bool HadSuppressCorpseDrops;
		public int OriginalSuppressCorpseDrops;
		public bool HadCorpse;
		public int OriginalCorpseChance;
		public int OriginalBurntCorpseChance;
		public int OriginalVaporizedCorpseChance;
		public int OriginalBuildCorpseChance;
		public bool AuthorizedDeparture;
		public bool TerminalObserved;
		public bool Inert;

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("CanBeAngeredByBeingAttacked");
			Registrar.Register("CanBeAngeredByDamage");
			Registrar.Register("CanBeAngeredByFriendlyFire");
			Registrar.Register("CanBeAngeredByPropertyCrime");
			Registrar.Register("ApplyProselytize"); Registrar.Register("CanApplyBeguile");
			Registrar.Register("CanApplyDomination");
			Registrar.Register("CanHaveSmartUseConversation");
			Registrar.Register("IsConversationallyResponsive"); base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (!Inert && E != null && (E.ID == "CanBeAngeredByBeingAttacked"
				|| E.ID == "CanBeAngeredByDamage" || E.ID == "CanBeAngeredByFriendlyFire"
				|| E.ID == "CanBeAngeredByPropertyCrime" || E.ID == "ApplyProselytize"
				|| E.ID == "CanApplyBeguile" || E.ID == "CanApplyDomination"
				|| E.ID == "CanHaveSmartUseConversation"
				|| E.ID == "IsConversationallyResponsive")) return false;
			return base.FireEvent(E);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == CanSmartUseEvent.ID
				|| ID == CommandSmartUseEvent.ID || ID == BeforeDeathRemovalEvent.ID
				|| ID == OnDestroyObjectEvent.ID || ID == AdjustValueEvent.ID
				|| ID == GetIntrinsicValueEvent.ID || ID == GetExtrinsicValueEvent.ID
				|| ID == CanBeTradedEvent.ID || ID == CanBeReplicatedEvent.ID
				|| ID == CanBeDismemberedEvent.ID || ID == CanJoinPartyLeaderEvent.ID;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (!Inert && ThousandAndFirst.KingdomGrowth.CanUsePhysicalFirstGuest(
				ParentObject, E.Actor, CandidateId, OpportunityId))
				// Replace any inherited ConversationScript action whichever part dispatched first.
				E.AddAction("Chat", "speak with the first guest", Command,
					null, 'g', FireOnActor: false, Default: 5, Override: true);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CanSmartUseEvent E)
		{
			return Inert || !ThousandAndFirst.KingdomGrowth.CanUsePhysicalFirstGuest(
				ParentObject, E.Actor, CandidateId, OpportunityId) ? base.HandleEvent(E) : false;
		}

		public override bool HandleEvent(CommandSmartUseEvent E)
		{
			if (!Inert && ThousandAndFirst.KingdomGrowth.CanUsePhysicalFirstGuest(
				ParentObject, E.Actor, CandidateId, OpportunityId))
			{
				ThousandAndFirst.KingdomGrowth.OpenPhysicalFirstGuest(ParentObject, E.Actor,
					CandidateId, OpportunityId); return false;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == Command && !Inert && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomGrowth.OpenPhysicalFirstGuest(ParentObject, E.Actor,
					CandidateId, OpportunityId); E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			ObserveTerminal(); return base.HandleEvent(E);
		}

		public override bool HandleEvent(OnDestroyObjectEvent E)
		{
			ObserveTerminal(); return base.HandleEvent(E);
		}

		private void ObserveTerminal()
		{
			if (Inert || TerminalObserved) return;
			TerminalObserved = ThousandAndFirst.KingdomGrowth.ObservePhysicalFirstGuestRemoval(
				ParentObject, CandidateId, OpportunityId, AuthorizedDeparture);
		}

		public override bool HandleEvent(AdjustValueEvent E)
		{
			if (Inert) return base.HandleEvent(E);
			E.Value = 0.0; return false;
		}
		public override bool HandleEvent(GetIntrinsicValueEvent E)
		{
			if (Inert) return base.HandleEvent(E);
			E.Value = 0.0; return false;
		}
		public override bool HandleEvent(GetExtrinsicValueEvent E)
		{
			if (Inert) return base.HandleEvent(E);
			E.Value = 0.0; return false;
		}
		public override bool HandleEvent(CanBeTradedEvent E) { return Inert; }
		public override bool HandleEvent(CanBeReplicatedEvent E) { return Inert; }
		public override bool HandleEvent(CanBeDismemberedEvent E) { return Inert; }
		public override bool HandleEvent(CanJoinPartyLeaderEvent E) { return Inert; }
		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv); Inert = true;
			CandidateId = null; OpportunityId = null; ObjectId = null; Marker = null;
			ParentObject?.RemoveStringProperty("r_TAF_GrowthArrivalMarker");
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomFirstGuestBody));
			Writer.WriteComposite(OriginalAllegiance);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomFirstGuestBody));
			OriginalAllegiance = Reader.ReadComposite<AllegianceSet>();
			if (Version != 1 || OriginalAllegiance == null) Inert = true;
		}
	}
}
