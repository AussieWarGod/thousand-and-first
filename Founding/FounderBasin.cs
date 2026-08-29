using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;
using XRL.World.Parts;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently.
namespace XRL.World.Parts
{
	[Serializable]
	public partial class r_FounderBasin : IPart
	{
		// The shipped part had no serialized fields. IPart's default writer is positional, so adding
		// even one field would make an older zero-field payload unreadable. Receipt state therefore
		// lives on this exact basin GameObject's already-version-tolerant int/string property maps.
		// The nonserialized backing exists only for the unattached carrier used by debug wishes.
		private const string ReceiptPrefix = "r_TAF_FoundingReceipt_";
		private const string OwnerNonceKey = "r_TAF_FounderBasinOwnerNonce";
		private const string KindKey = ReceiptPrefix + "Kind";
		private const string PhaseKey = ReceiptPrefix + "Phase";
		private const string TransactionKey = ReceiptPrefix + "Transaction";
		private const string BasinIDKey = ReceiptPrefix + "BasinID";
		private const string OwnerKindKey = ReceiptPrefix + "OwnerKind";
		private const string OwnerNonceReceiptKey = ReceiptPrefix + "OwnerNonce";
		private const string PayloadDigestKey = ReceiptPrefix + "PayloadDigest";
		private const string AuthorityKey = ReceiptPrefix + "Authority";
		private const string RealmFactionKey = ReceiptPrefix + "RealmFaction";
		private const string NameKey = ReceiptPrefix + "Name";
		private const string VocationKey = ReceiptPrefix + "Vocation";
		private const string VillageFactionKey = ReceiptPrefix + "VillageFaction";
		private const string VillageDisplayKey = ReceiptPrefix + "VillageDisplay";
		private const string ZoneKey = ReceiptPrefix + "Zone";
		private const string RiteXKey = ReceiptPrefix + "RiteX";
		private const string RiteYKey = ReceiptPrefix + "RiteY";
		private const string OriginalVolumeKey = ReceiptPrefix + "OriginalVolume";
		private const string OriginalMaxKey = ReceiptPrefix + "OriginalMax";
		private const string CommittedVolumeKey = ReceiptPrefix + "CommittedVolume";
		private const string CommittedMaxKey = ReceiptPrefix + "CommittedMax";
		private const string OriginalComponentsKey = ReceiptPrefix + "OriginalComponents";
		private const string CommittedComponentsKey = ReceiptPrefix + "CommittedComponents";
		private const string ChronicleKey = ReceiptPrefix + "Chronicle";
		private const string ChronicleStageKey = ReceiptPrefix + "ChronicleStage";
		private const string ChronicleEventKey = ReceiptPrefix + "ChronicleEvent";
		private const string ChronicleDispositionKey = ReceiptPrefix + "ChronicleDisposition";
		private const string ExternalBindingKey = ReceiptPrefix + "ExternalBinding_v1";

		[NonSerialized] private KingdomFoundingKind TransientKind;
		[NonSerialized] private KingdomFoundingPhase TransientPhase;
		[NonSerialized] private string TransientTransaction;
		[NonSerialized] private string TransientBasinID;
		[NonSerialized] private KingdomFoundingOwnerKind TransientOwnerKind;
		[NonSerialized] private string TransientOwnerNonce;
		[NonSerialized] private string TransientPayloadDigest;
		[NonSerialized] private string TransientAuthority;
		[NonSerialized] private string TransientRealmFaction;
		[NonSerialized] private string TransientName;
		[NonSerialized] private string TransientVocation;
		[NonSerialized] private string TransientVillageFaction;
		[NonSerialized] private string TransientVillageDisplay;
		[NonSerialized] private string TransientZone;
		[NonSerialized] private int TransientRiteX;
		[NonSerialized] private int TransientRiteY;
		[NonSerialized] private int TransientOriginalVolume;
		[NonSerialized] private int TransientOriginalMax;
		[NonSerialized] private int TransientCommittedVolume;
		[NonSerialized] private int TransientCommittedMax;
		[NonSerialized] private Dictionary<string, int> TransientOriginalComponents;
		[NonSerialized] private Dictionary<string, int> TransientCommittedComponents;
		[NonSerialized] private bool TransientChronicle;
		[NonSerialized] private int TransientChronicleStage;
		[NonSerialized] private string TransientChronicleEvent;
		[NonSerialized] private KingdomChronicleDisposition TransientChronicleDisposition;
		[NonSerialized] private string TransientExternalBinding;
		[NonSerialized] private string TransientCompletion;

		private static readonly string[] ReceiptKeys = new string[]
		{
			KindKey, PhaseKey, TransactionKey, BasinIDKey, OwnerKindKey,
			OwnerNonceReceiptKey, PayloadDigestKey, AuthorityKey, RealmFactionKey, NameKey,
			VocationKey, VillageFactionKey, VillageDisplayKey, ZoneKey, RiteXKey, RiteYKey,
			OriginalVolumeKey, OriginalMaxKey, CommittedVolumeKey, CommittedMaxKey,
			OriginalComponentsKey, CommittedComponentsKey, ChronicleKey,
			ChronicleStageKey, ChronicleEventKey, ChronicleDispositionKey,
			ExternalBindingKey, VillageEffectStateKey, VillageEffectBeforeKey,
			VillageEffectBeforeCarryKey, VillageEffectAfterKey,
			VillageEffectAfterCarryKey, VillageEffectDigestKey
		};

		public KingdomFoundingKind PendingKind
		{
			get { return ParentObject == null ? TransientKind :
				(KingdomFoundingKind)ParentObject.GetIntProperty(KindKey); }
			set { TransientKind = value; ParentObject?.SetIntProperty(KindKey, (int)value); }
		}

		public KingdomFoundingOwnerKind PendingOwnerKind
		{
			get { return ParentObject == null ? TransientOwnerKind :
				(KingdomFoundingOwnerKind)ParentObject.GetIntProperty(OwnerKindKey); }
			set { TransientOwnerKind = value;
				ParentObject?.SetIntProperty(OwnerKindKey, (int)value); }
		}

		public string PendingOwnerNonce
		{
			get { return ParentObject == null ? TransientOwnerNonce :
				ParentObject.GetStringProperty(OwnerNonceReceiptKey); }
			set { TransientOwnerNonce = value;
				ParentObject?.SetStringProperty(OwnerNonceReceiptKey, value, RemoveIfNull: true); }
		}

		public string PendingPayloadDigest
		{
			get { return ParentObject == null ? TransientPayloadDigest :
				ParentObject.GetStringProperty(PayloadDigestKey); }
			set { TransientPayloadDigest = value;
				ParentObject?.SetStringProperty(PayloadDigestKey, value, RemoveIfNull: true); }
		}

		public string PendingAuthority
		{
			get { return ParentObject == null ? TransientAuthority :
				ParentObject.GetStringProperty(AuthorityKey); }
			set { TransientAuthority = value;
				ParentObject?.SetStringProperty(AuthorityKey, value, RemoveIfNull: true); }
		}

		public KingdomFoundingPhase PendingPhase
		{
			get { return ParentObject == null ? TransientPhase :
				(KingdomFoundingPhase)ParentObject.GetIntProperty(PhaseKey); }
			set { TransientPhase = value; ParentObject?.SetIntProperty(PhaseKey, (int)value); }
		}

		public string PendingTransactionID
		{
			get { return ParentObject == null ? TransientTransaction :
				ParentObject.GetStringProperty(TransactionKey); }
			set { TransientTransaction = value;
				ParentObject?.SetStringProperty(TransactionKey, value, RemoveIfNull: true); }
		}

		public string PendingBasinID
		{
			get { return ParentObject == null ? TransientBasinID :
				ParentObject.GetStringProperty(BasinIDKey); }
			set { TransientBasinID = value;
				ParentObject?.SetStringProperty(BasinIDKey, value, RemoveIfNull: true); }
		}

		public string PendingRealmFaction
		{
			get { return ParentObject == null ? TransientRealmFaction :
				ParentObject.GetStringProperty(RealmFactionKey); }
			set { TransientRealmFaction = value;
				ParentObject?.SetStringProperty(RealmFactionKey, value, RemoveIfNull: true); }
		}

		public string PendingName
		{
			get { return ParentObject == null ? TransientName : ParentObject.GetStringProperty(NameKey); }
			set { TransientName = value; ParentObject?.SetStringProperty(NameKey, value, RemoveIfNull: true); }
		}

		public string PendingVocation
		{
			get { return ParentObject == null ? TransientVocation : ParentObject.GetStringProperty(VocationKey); }
			set { TransientVocation = value; ParentObject?.SetStringProperty(VocationKey, value, RemoveIfNull: true); }
		}

		public string PendingVillageFaction
		{
			get { return ParentObject == null ? TransientVillageFaction : ParentObject.GetStringProperty(VillageFactionKey); }
			set { TransientVillageFaction = value; ParentObject?.SetStringProperty(VillageFactionKey, value, RemoveIfNull: true); }
		}

		public string PendingVillageDisplayName
		{
			get { return ParentObject == null ? TransientVillageDisplay : ParentObject.GetStringProperty(VillageDisplayKey); }
			set { TransientVillageDisplay = value; ParentObject?.SetStringProperty(VillageDisplayKey, value, RemoveIfNull: true); }
		}

		public string PendingZoneID
		{
			get { return ParentObject == null ? TransientZone : ParentObject.GetStringProperty(ZoneKey); }
			set { TransientZone = value; ParentObject?.SetStringProperty(ZoneKey, value, RemoveIfNull: true); }
		}

		public int PendingRiteX
		{
			get { return ParentObject == null ? TransientRiteX : ParentObject.GetIntProperty(RiteXKey); }
			set { TransientRiteX = value; ParentObject?.SetIntProperty(RiteXKey, value); }
		}

		public int PendingRiteY
		{
			get { return ParentObject == null ? TransientRiteY : ParentObject.GetIntProperty(RiteYKey); }
			set { TransientRiteY = value; ParentObject?.SetIntProperty(RiteYKey, value); }
		}

		public int PendingOriginalVolume
		{
			get { return ParentObject == null ? TransientOriginalVolume : ParentObject.GetIntProperty(OriginalVolumeKey); }
			set { TransientOriginalVolume = value; ParentObject?.SetIntProperty(OriginalVolumeKey, value); }
		}

		public int PendingOriginalMaxVolume
		{
			get { return ParentObject == null ? TransientOriginalMax : ParentObject.GetIntProperty(OriginalMaxKey); }
			set { TransientOriginalMax = value; ParentObject?.SetIntProperty(OriginalMaxKey, value); }
		}

		public int PendingCommittedVolume
		{
			get { return ParentObject == null ? TransientCommittedVolume : ParentObject.GetIntProperty(CommittedVolumeKey); }
			set { TransientCommittedVolume = value; ParentObject?.SetIntProperty(CommittedVolumeKey, value); }
		}

		public int PendingCommittedMaxVolume
		{
			get { return ParentObject == null ? TransientCommittedMax : ParentObject.GetIntProperty(CommittedMaxKey); }
			set { TransientCommittedMax = value; ParentObject?.SetIntProperty(CommittedMaxKey, value); }
		}

		public Dictionary<string, int> PendingOriginalComponents
		{
			get { return ParentObject == null ? Copy(TransientOriginalComponents) :
				DecodeComponents(ParentObject.GetStringProperty(OriginalComponentsKey)); }
			set
			{
				TransientOriginalComponents = Copy(value);
				ParentObject?.SetStringProperty(OriginalComponentsKey, EncodeComponents(value), RemoveIfNull: true);
			}
		}

		public Dictionary<string, int> PendingCommittedComponents
		{
			get { return ParentObject == null ? Copy(TransientCommittedComponents) :
				DecodeComponents(ParentObject.GetStringProperty(CommittedComponentsKey)); }
			set
			{
				TransientCommittedComponents = Copy(value);
				ParentObject?.SetStringProperty(CommittedComponentsKey, EncodeComponents(value), RemoveIfNull: true);
			}
		}

		public bool PendingChronicleRecorded
		{
			get { return ParentObject == null ? TransientChronicle : ParentObject.GetIntProperty(ChronicleKey) == 1; }
			set { TransientChronicle = value; ParentObject?.SetIntProperty(ChronicleKey, value ? 1 : 0); }
		}

		public int PendingChronicleStage
		{
			get { return ParentObject == null ? TransientChronicleStage :
				ParentObject.GetIntProperty(ChronicleStageKey); }
			set { TransientChronicleStage = value;
				ParentObject?.SetIntProperty(ChronicleStageKey, value); }
		}

		public string PendingChronicleEventID
		{
			get { return ParentObject == null ? TransientChronicleEvent :
				ParentObject.GetStringProperty(ChronicleEventKey); }
			set { TransientChronicleEvent = value;
				ParentObject?.SetStringProperty(ChronicleEventKey, value, RemoveIfNull: true); }
		}

		public KingdomChronicleDisposition PendingChronicleDisposition
		{
			get { return ParentObject == null ? TransientChronicleDisposition :
				(KingdomChronicleDisposition)ParentObject.GetIntProperty(
					ChronicleDispositionKey); }
			set { TransientChronicleDisposition = value;
				ParentObject?.SetIntProperty(ChronicleDispositionKey, (int)value); }
		}

		public string PendingExternalBinding
		{
			get { return ParentObject == null ? TransientExternalBinding :
				ParentObject.GetStringProperty(ExternalBindingKey); }
			set { TransientExternalBinding = value;
				ParentObject?.SetStringProperty(ExternalBindingKey, value, RemoveIfNull: true); }
		}
	}
}
