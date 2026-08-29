using System;
using System.Collections.Generic;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_FounderBasin
	{
		/// <summary>Stable identity of this physical basin. It survives save/load, but clone
		/// finalization always mints a different value even when the engine copies object IDs.</summary>
		public string OwnerNonce
		{
			get
			{
				if (ParentObject == null)
				{
					return TransientOwnerNonce;
				}
				return ParentObject.GetStringProperty(OwnerNonceKey);
			}
		}

		internal string EnsureOwnerNonce()
		{
			string current = OwnerNonce;
			if (KingdomFoundingTransactionRules.IsNonce(current))
			{
				return current;
			}
			// Never repair authority under a paid or malformed receipt: doing so would let a
			// copied/corrupt object become owner by being the first one touched.
			if (HasAnyReceiptState)
			{
				return current;
			}
			current = Guid.NewGuid().ToString("N");
			TransientOwnerNonce = current;
			ParentObject?.SetStringProperty(OwnerNonceKey, current);
			return current;
		}

		internal bool TryReadRawHeader(out int RawKind, out int RawPhase,
			out bool KindPresent, out bool PhasePresent)
		{
			if (ParentObject == null)
			{
				RawKind = (int)TransientKind;
				RawPhase = (int)TransientPhase;
				KindPresent = TransientKind != KingdomFoundingKind.None ||
					TransientPhase != KingdomFoundingPhase.None ||
					!string.IsNullOrEmpty(TransientTransaction);
				PhasePresent = KindPresent;
				return true;
			}
			KindPresent = ParentObject.TryGetIntProperty(KindKey, out RawKind);
			PhasePresent = ParentObject.TryGetIntProperty(PhaseKey, out RawPhase);
			return true;
		}

		internal bool TryReadRawChronicle(out int Raw)
		{
			if (ParentObject == null)
			{
				Raw = TransientChronicle ? 1 : 0;
				return true;
			}
			return ParentObject.TryGetIntProperty(ChronicleKey, out Raw);
		}

		internal bool TryReadRawChronicleDisposition(out int Raw)
		{
			if (ParentObject == null)
			{
				Raw = (int)TransientChronicleDisposition;
				return true;
			}
			return ParentObject.TryGetIntProperty(ChronicleDispositionKey, out Raw);
		}

		internal bool HasAnyReceiptState
		{
			get
			{
				if (ParentObject == null)
				{
					return TransientKind != KingdomFoundingKind.None ||
						TransientPhase != KingdomFoundingPhase.None ||
						!string.IsNullOrEmpty(TransientTransaction) ||
						!string.IsNullOrEmpty(TransientAuthority) ||
						!string.IsNullOrEmpty(TransientExternalBinding) ||
						TransientVillageEffectMask != 0 ||
						TransientChronicleDisposition != KingdomChronicleDisposition.None;
				}
				for (int i = 0; i < ReceiptKeys.Length; i++)
				{
					if (ParentObject.HasProperty(ReceiptKeys[i]))
					{
						return true;
					}
				}
				return false;
			}
		}

		internal bool HasReceiptPayloadBeyondHeader
		{
			get
			{
				if (ParentObject == null)
				{
					return !string.IsNullOrEmpty(TransientTransaction) ||
						!string.IsNullOrEmpty(TransientAuthority) ||
						!string.IsNullOrEmpty(TransientRealmFaction) ||
						!string.IsNullOrEmpty(TransientExternalBinding) ||
						TransientVillageEffectMask != 0 ||
						TransientChronicleDisposition != KingdomChronicleDisposition.None;
				}
				for (int i = 2; i < ReceiptKeys.Length; i++)
				{
					if (ParentObject.HasProperty(ReceiptKeys[i]))
					{
						return true;
					}
				}
				return false;
			}
		}

		internal bool TryGetOriginalComponents(out Dictionary<string, int> Components,
			out string Encoded)
		{
			return TryGetComponents(OriginalComponentsKey, TransientOriginalComponents,
				out Components, out Encoded);
		}

		internal bool TryGetCommittedComponents(out Dictionary<string, int> Components,
			out string Encoded)
		{
			return TryGetComponents(CommittedComponentsKey, TransientCommittedComponents,
				out Components, out Encoded);
		}

		internal bool HasCompleteReceiptSchema
		{
			get
			{
				if (ParentObject == null)
				{
					return TransientKind != KingdomFoundingKind.None &&
						!string.IsNullOrEmpty(TransientTransaction) &&
						!string.IsNullOrEmpty(TransientAuthority) &&
						TransientOriginalComponents != null &&
						TransientCommittedComponents != null;
				}
				string[] required = new string[]
				{
					KindKey, PhaseKey, TransactionKey, BasinIDKey, OwnerKindKey,
					OwnerNonceReceiptKey, PayloadDigestKey, AuthorityKey, RealmFactionKey,
					NameKey, ZoneKey, RiteXKey, RiteYKey, OriginalVolumeKey,
					OriginalMaxKey, CommittedVolumeKey, CommittedMaxKey,
					OriginalComponentsKey, CommittedComponentsKey, ChronicleKey,
					ChronicleStageKey, ChronicleEventKey
				};
				for (int i = 0; i < required.Length; i++)
				{
					if (!ParentObject.HasProperty(required[i]))
					{
						return false;
					}
				}
				return true;
			}
		}

		internal bool HasVocationField => ParentObject == null
			? TransientVocation != null : ParentObject.HasProperty(VocationKey);

		internal bool HasVillageFactionField => ParentObject == null
			? TransientVillageFaction != null : ParentObject.HasProperty(VillageFactionKey);

		internal bool HasVillageDisplayField => ParentObject == null
			? TransientVillageDisplay != null : ParentObject.HasProperty(VillageDisplayKey);

		internal bool HasExternalBindingField => ParentObject == null
			? TransientExternalBinding != null : ParentObject.HasProperty(ExternalBindingKey);

		private bool TryGetComponents(string Key, Dictionary<string, int> Transient,
			out Dictionary<string, int> Components, out string Encoded)
		{
			if (ParentObject == null)
			{
				Encoded = EncodeComponents(Transient);
				return KingdomFoundingTransactionRules.TryDecodeComponents(Encoded,
					out Components);
			}
			if (!ParentObject.TryGetStringProperty(Key, out Encoded))
			{
				Components = null;
				return false;
			}
			return KingdomFoundingTransactionRules.TryDecodeComponents(Encoded,
				out Components);
		}
	}
}
