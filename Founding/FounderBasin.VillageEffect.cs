using System;

namespace XRL.World.Parts
{
	public partial class r_FounderBasin
	{
		private const string VillageEffectStateKey = ReceiptPrefix + "VillageEffectState_v1";
		private const string VillageEffectBeforeKey = ReceiptPrefix + "VillageEffectBefore_v1";
		private const string VillageEffectBeforeCarryKey =
			ReceiptPrefix + "VillageEffectBeforeCarry_v1";
		private const string VillageEffectAfterKey = ReceiptPrefix + "VillageEffectAfter_v1";
		private const string VillageEffectAfterCarryKey =
			ReceiptPrefix + "VillageEffectAfterCarry_v1";
		private const string VillageEffectDigestKey = ReceiptPrefix + "VillageEffectDigest_v1";

		[NonSerialized] private int TransientVillageEffectMask;
		[NonSerialized] private int TransientVillageEffectState;
		[NonSerialized] private int TransientVillageEffectBefore;
		[NonSerialized] private int TransientVillageEffectBeforeCarry;
		[NonSerialized] private int TransientVillageEffectAfter;
		[NonSerialized] private int TransientVillageEffectAfterCarry;
		[NonSerialized] private string TransientVillageEffectDigest;

		internal int PendingVillageEffectState
		{
			get { return ParentObject == null ? TransientVillageEffectState :
				ParentObject.GetIntProperty(VillageEffectStateKey); }
			set { TransientVillageEffectState = value; TransientVillageEffectMask |= 1;
				ParentObject?.SetIntProperty(VillageEffectStateKey, value); }
		}

		internal int PendingVillageEffectBefore
		{
			get { return ParentObject == null ? TransientVillageEffectBefore :
				ParentObject.GetIntProperty(VillageEffectBeforeKey); }
			set { TransientVillageEffectBefore = value; TransientVillageEffectMask |= 2;
				ParentObject?.SetIntProperty(VillageEffectBeforeKey, value); }
		}

		internal int PendingVillageEffectBeforeCarry
		{
			get { return ParentObject == null ? TransientVillageEffectBeforeCarry :
				ParentObject.GetIntProperty(VillageEffectBeforeCarryKey); }
			set { TransientVillageEffectBeforeCarry = value; TransientVillageEffectMask |= 4;
				ParentObject?.SetIntProperty(VillageEffectBeforeCarryKey, value); }
		}

		internal int PendingVillageEffectAfter
		{
			get { return ParentObject == null ? TransientVillageEffectAfter :
				ParentObject.GetIntProperty(VillageEffectAfterKey); }
			set { TransientVillageEffectAfter = value; TransientVillageEffectMask |= 8;
				ParentObject?.SetIntProperty(VillageEffectAfterKey, value); }
		}

		internal int PendingVillageEffectAfterCarry
		{
			get { return ParentObject == null ? TransientVillageEffectAfterCarry :
				ParentObject.GetIntProperty(VillageEffectAfterCarryKey); }
			set { TransientVillageEffectAfterCarry = value; TransientVillageEffectMask |= 16;
				ParentObject?.SetIntProperty(VillageEffectAfterCarryKey, value); }
		}

		internal string PendingVillageEffectDigest
		{
			get { return ParentObject == null ? TransientVillageEffectDigest :
				ParentObject.GetStringProperty(VillageEffectDigestKey); }
			set { TransientVillageEffectDigest = value; TransientVillageEffectMask |= 32;
				ParentObject?.SetStringProperty(VillageEffectDigestKey, value,
					RemoveIfNull: true); }
		}

		internal void ReadVillageEffect(out int state, out int before,
			out int beforeCarry, out int after, out int afterCarry, out string digest,
			out bool any, out bool complete)
		{
			if (ParentObject == null)
			{
				state = TransientVillageEffectState;
				before = TransientVillageEffectBefore;
				beforeCarry = TransientVillageEffectBeforeCarry;
				after = TransientVillageEffectAfter;
				afterCarry = TransientVillageEffectAfterCarry;
				digest = TransientVillageEffectDigest;
				any = TransientVillageEffectMask != 0;
				complete = TransientVillageEffectMask == 63;
				return;
			}
			int mask = 0;
			if (ParentObject.TryGetIntProperty(VillageEffectStateKey, out state)) mask |= 1;
			if (ParentObject.TryGetIntProperty(VillageEffectBeforeKey, out before)) mask |= 2;
			if (ParentObject.TryGetIntProperty(VillageEffectBeforeCarryKey,
				out beforeCarry)) mask |= 4;
			if (ParentObject.TryGetIntProperty(VillageEffectAfterKey, out after)) mask |= 8;
			if (ParentObject.TryGetIntProperty(VillageEffectAfterCarryKey,
				out afterCarry)) mask |= 16;
			if (ParentObject.TryGetStringProperty(VillageEffectDigestKey, out digest)) mask |= 32;
			any = mask != 0;
			complete = mask == 63;
		}
	}
}
