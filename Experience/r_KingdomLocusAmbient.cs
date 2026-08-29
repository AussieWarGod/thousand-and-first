using System;
using ThousandAndFirst;
using XRL;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Attended-only plumbing for the staffed civic locus. Every authority field is
	/// deliberately nonserialized: a loaded or thawed bench stays inert until the exact current
	/// ground, keeper, realm, option epoch, and accessibility settings are reconciled again.</summary>
	[Serializable]
	public sealed class r_KingdomLocusAmbient : IPart
	{
		[NonSerialized] public bool AuthorityEnabled;
		[NonSerialized] public string OwnerRealmId;
		[NonSerialized] public string OwnerSettlementId;
		[NonSerialized] public string OwnerZoneId;
		[NonSerialized] public int WorkId;
		[NonSerialized] public int KeeperResidentId;
		[NonSerialized] public string KeeperObjectId;
		[NonSerialized] public long ConfiguredTick;
		[NonSerialized] public bool HasUsed;
		[NonSerialized] public long LastUseTick;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == IdleQueryEvent.ID;
		}

		public override bool HandleEvent(IdleQueryEvent E)
		{
			if (E.Actor == null || The.Game == null) return base.HandleEvent(E);
			bool claimed = false;
			bool retire = true;
			KingdomSystem.Guard("locus ambient use", delegate
			{
				claimed = KingdomLocus.TryClaimAmbient(ParentObject, this, E.Actor,
					The.Game.TimeTicks, out retire);
			});
			if (retire) ParentObject?.RemovePart(this);
			return claimed ? false : base.HandleEvent(E);
		}
	}
}
