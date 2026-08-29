using System;
using System.Collections.Generic;
using System.Text;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_FounderBasin
	{
		public bool HasPendingRite => HasAnyReceiptState;

		public void ClearPendingRite()
		{
			TransientKind = KingdomFoundingKind.None;
			TransientPhase = KingdomFoundingPhase.None;
			TransientTransaction = null;
			TransientBasinID = null;
			TransientOwnerKind = KingdomFoundingOwnerKind.None;
			TransientPayloadDigest = null;
			TransientAuthority = null;
			TransientRealmFaction = null;
			TransientName = null;
			TransientVocation = null;
			TransientVillageFaction = null;
			TransientVillageDisplay = null;
			TransientZone = null;
			TransientRiteX = 0;
			TransientRiteY = 0;
			TransientOriginalVolume = 0;
			TransientOriginalMax = 0;
			TransientCommittedVolume = 0;
			TransientCommittedMax = 0;
			TransientOriginalComponents = null;
			TransientCommittedComponents = null;
			TransientChronicle = false;
			TransientChronicleStage = 0;
			TransientChronicleEvent = null;
			TransientChronicleDisposition = KingdomChronicleDisposition.None;
			TransientExternalBinding = null;
			TransientVillageEffectMask = 0;
			TransientVillageEffectState = 0;
			TransientVillageEffectBefore = 0;
			TransientVillageEffectBeforeCarry = 0;
			TransientVillageEffectAfter = 0;
			TransientVillageEffectAfterCarry = 0;
			TransientVillageEffectDigest = null;
			if (ParentObject == null)
			{
				return;
			}
			ParentObject.RemoveProperty(KindKey);
			ParentObject.RemoveProperty(PhaseKey);
			ParentObject.RemoveProperty(TransactionKey);
			ParentObject.RemoveProperty(BasinIDKey);
			ParentObject.RemoveProperty(OwnerKindKey);
			ParentObject.RemoveProperty(OwnerNonceReceiptKey);
			ParentObject.RemoveProperty(PayloadDigestKey);
			ParentObject.RemoveProperty(AuthorityKey);
			ParentObject.RemoveProperty(RealmFactionKey);
			ParentObject.RemoveProperty(NameKey);
			ParentObject.RemoveProperty(VocationKey);
			ParentObject.RemoveProperty(VillageFactionKey);
			ParentObject.RemoveProperty(VillageDisplayKey);
			ParentObject.RemoveProperty(ZoneKey);
			ParentObject.RemoveProperty(RiteXKey);
			ParentObject.RemoveProperty(RiteYKey);
			ParentObject.RemoveProperty(OriginalVolumeKey);
			ParentObject.RemoveProperty(OriginalMaxKey);
			ParentObject.RemoveProperty(CommittedVolumeKey);
			ParentObject.RemoveProperty(CommittedMaxKey);
			ParentObject.RemoveProperty(OriginalComponentsKey);
			ParentObject.RemoveProperty(CommittedComponentsKey);
			ParentObject.RemoveProperty(ChronicleKey);
			ParentObject.RemoveProperty(ChronicleStageKey);
			ParentObject.RemoveProperty(ChronicleEventKey);
			ParentObject.RemoveProperty(ChronicleDispositionKey);
			ParentObject.RemoveProperty(ExternalBindingKey);
			ParentObject.RemoveProperty(VillageEffectStateKey);
			ParentObject.RemoveProperty(VillageEffectBeforeKey);
			ParentObject.RemoveProperty(VillageEffectBeforeCarryKey);
			ParentObject.RemoveProperty(VillageEffectAfterKey);
			ParentObject.RemoveProperty(VillageEffectAfterCarryKey);
			ParentObject.RemoveProperty(VillageEffectDigestKey);
		}

		private static Dictionary<string, int> Copy(Dictionary<string, int> Source)
		{
			return Source == null ? null : new Dictionary<string, int>(Source);
		}

		private static string EncodeComponents(Dictionary<string, int> Components)
		{
			if (Components == null)
			{
				return null;
			}
			List<string> keys = new List<string>(Components.Keys);
			keys.Sort(StringComparer.Ordinal);
			StringBuilder encoded = new StringBuilder();
			foreach (string key in keys)
			{
				if (encoded.Length > 0)
				{
					encoded.Append(';');
				}
				encoded.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(key ?? "")))
					.Append(':').Append(Components[key]);
			}
			return encoded.ToString();
		}

		private static Dictionary<string, int> DecodeComponents(string Encoded)
		{
			return KingdomFoundingTransactionRules.TryDecodeComponents(Encoded, out var result)
				? result : null;
		}
	}
}
