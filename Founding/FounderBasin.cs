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
	public class r_FounderBasin : IPart
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
		[NonSerialized] private string TransientCompletion;

		private static readonly string[] ReceiptKeys = new string[]
		{
			KindKey, PhaseKey, TransactionKey, BasinIDKey, OwnerKindKey,
			OwnerNonceReceiptKey, PayloadDigestKey, AuthorityKey, RealmFactionKey, NameKey,
			VocationKey, VillageFactionKey, VillageDisplayKey, ZoneKey, RiteXKey, RiteYKey,
			OriginalVolumeKey, OriginalMaxKey, CommittedVolumeKey, CommittedMaxKey,
			OriginalComponentsKey, CommittedComponentsKey, ChronicleKey,
			ChronicleStageKey, ChronicleEventKey, ChronicleDispositionKey
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

		public override void Initialize()
		{
			base.Initialize();
			EnsureOwnerNonce();
		}

		public override void ObjectLoaded()
		{
			base.ObjectLoaded();
			EnsureOwnerNonce();
		}

		public override bool CanGenerateStacked()
		{
			return false;
		}

		public override bool SameAs(IPart Part)
		{
			return ReferenceEquals(this, Part);
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			// GameObject copies string/int property maps before parts. Strip every receipt and
			// authority key from clone, including CopyID=true paths, then mint new physical owner.
			ClearPendingRite();
			ParentObject?.RemoveProperty(OwnerNonceKey);
			TransientOwnerNonce = null;
			EnsureOwnerNonce();
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID ||
				ID == InventoryActionEvent.ID || ID == CanBeReplicatedEvent.ID;
		}

		public override bool HandleEvent(CanBeReplicatedEvent E)
		{
			// Polygel and other ordinary replication routes ask this event before DeepCopy.
			// A paid receipt belongs to one basin and cannot be copied into a second claimant.
			return !HasAnyReceiptState && base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Found", "found a settlement", "r_FoundKingdom", null, 'f', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_FoundKingdom" && E.Actor != null && E.Actor.IsPlayer())
			{
				TransientCompletion = null;
				KingdomFoundingResult result = AttemptFounding(E.Actor);
				if (result.ChargesEnergy)
				{
					E.Actor.UseEnergy(KingdomGovernanceRules.NominalEnergyCost,
						KingdomGovernanceRules.EnergyReason("found place"));
					E.RequestInterfaceExit();
					string completion = TransientCompletion;
					TransientCompletion = null;
					if (!string.IsNullOrEmpty(completion))
					{
						KingdomSystem.Guard("founding completion presentation", delegate
						{
							Popup.Show(completion);
						});
					}
				}
				else
				{
					TransientCompletion = null;
				}
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The rite. It is the same rite the second time: the same basin, the same eight drams of
		/// fresh water, the same refusals. What changes is where it is performed &mdash; poured on
		/// ground the realm does not hold and does not border, while the realm already stands, it
		/// founds a second city rather than a first; poured on ground a living village already
		/// answers to, it asks instead of taking (<see cref="AttemptVillageCharter"/>); poured on
		/// ground anything else already answers to, it refuses outright &mdash; this rite has
		/// never had a way to claim ground without asking, it simply used to skip asking.
		/// </summary>
		/// <param name="Actor">The founder. The zone they are standing in is the site.</param>
		public KingdomFoundingResult AttemptFounding(GameObject Actor)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone site = Actor?.CurrentZone;
			if (HasPendingRite)
			{
				KingdomFoundingKind kind = PendingKind;
				string pendingName = PendingName;
				string pendingVocation = PendingVocation;
				string pendingVillage = PendingVillageDisplayName;
				KingdomFoundingResult resumed = KingdomFoundingTransaction.Resume(this, Actor, site);
				if (resumed.Committed)
				{
					TransientCompletion = CompletionText(system, kind, pendingName,
						pendingVocation, pendingVillage);
				}
				else
				{
					ShowFailure(resumed);
				}
				return resumed;
			}
			string siteFaction = site?.GetZoneProperty("faction") ?? "";
			bool siteFactionIsVillage = !string.IsNullOrEmpty(siteFaction) && Factions.GetIfExists(siteFaction)?.GetIntProperty("Village") == 1;
			KingdomRules.GroundClaimVerdict groundVerdict = KingdomRules.JudgeGroundFaction(siteFaction, system.KingdomFactionName, siteFactionIsVillage);
			if (groundVerdict == KingdomRules.GroundClaimVerdict.ForeignVillage)
			{
				return AttemptVillageCharter(system, Actor, site, siteFaction);
			}
			if (groundVerdict == KingdomRules.GroundClaimVerdict.ForeignOther)
			{
				Popup.Show("This ground already answers to someone else. Pouring here would not found anything; it would only spend the water.");
				return Refused();
			}
			// Ground the realm that put the founder out still holds is not ground to found on.
			// Its city goes on without them; taking it back is the return path, not the rite.
			// Judged before the water is measured, so a refusal never costs a dram.
			if (site != null && system.ExiledRealmHolds(site.ZoneID))
			{
				Popup.Show("This ground is {{C|" +
					KingdomPresentation.Rich(system.ExiledDisplayName) +
					"}}'s, and it is not yours to pour on any more. Ask it to take you back, or walk until the ground answers to nobody.");
				return Refused();
			}
			bool second = system.Founded;
			if (second)
			{
				// Judged before the water is measured, so a refusal never costs a dram.
				KingdomSettlement.SecondFoundingVerdict verdict = KingdomFounding.JudgeSite(system, site);
				if (verdict != KingdomSettlement.SecondFoundingVerdict.Allowed)
				{
					Popup.Show(KingdomSettlement.SecondFoundingRefusal(verdict,
						KingdomPresentation.Rich(system.KingdomDisplayName)));
					return Refused();
				}
			}
			LiquidVolume liquidVolume = ParentObject.GetPart<LiquidVolume>();
			int drams = KingdomLiquids.HasFreshWater(liquidVolume) ? liquidVolume.Volume : 0;
			if (drams < KingdomRules.FoundingCostDrams)
			{
				int volume = (liquidVolume != null && liquidVolume.Volume > 0) ? liquidVolume.Volume : 0;
				string reason;
				if (volume > 0 && drams == 0)
				{
					reason = " It holds " + volume + " drams, but the liquid is not pure water.";
				}
				else
				{
					reason = " It holds " + drams + ".";
				}
				Popup.Show("The rite asks for {{C|" + KingdomRules.FoundingCostDrams + " drams}} of fresh water pooled in the basin." + reason);
				return Refused();
			}
			string name = Popup.AskString(second ? "Name the second city." : "Name the settlement.",
				"", MaxLength: KingdomPresentationRules.MaxRawCodeUnits,
				ReturnNullForEscape: true);
			if (name == null)
			{
				return Refused();
			}
			if (!KingdomPresentationRules.TryNormalizeName(name, out name,
				out string nameFailure))
			{
				Popup.Show(nameFailure);
				return Refused();
			}
			if (second)
			{
				return FoundSecondCity(system, Actor, site, name);
			}
			KingdomFoundingResult result = KingdomFoundingTransaction.BeginFirst(
				this, Actor, site, name);
			if (!result.Committed)
			{
				ShowFailure(result);
				return result;
			}
			bool isRuin = KingdomRules.IsRuinSite(system.FoundingTerrainBlueprint);
			string verb = isRuin ? "reclaimed" : "founded";
			string openingLine = isRuin
				? "You pour the first water over ground the world already built, and those who came drink among walls that stood before you."
				: "You pour the first water, and those gathered drink.";
			TransientCompletion = openingLine + "\n\n{{C|" +
				KingdomPresentation.Rich(name) + "}} is " + verb +
				" on " + KingdomFounding.StyleGroundClause(system.Style) +
				". Your thirst is theirs; their water is yours.\n\nLive and drink.";
			return result;
		}

		/// <summary>
		/// The rite asks rather than takes here: <paramref name="VillageFactionName"/> already
		/// owns this ground, and nothing about that changes &mdash; not the zone's faction, not a
		/// single villager's allegiance, not one stone. What can change is standing, the same way
		/// it changes for any faction, sealed with the same water the founding rite spends. See
		/// <see cref="KingdomFounding.CharterVillage"/> for exactly what "chartered" means here,
		/// and why it stops short of a second city.
		/// </summary>
		/// <param name="System">The kingdom system.</param>
		/// <param name="VillageFactionName">The village's own faction name, read from the site's
		/// zone property before this was called.</param>
		private KingdomFoundingResult AttemptVillageCharter(KingdomSystem System,
			GameObject Actor, Zone Site, string VillageFactionName)
		{
			// GetIfExists, not Get: Factions.Get throws on an unknown name, and a zone's faction
			// property can name anything at all - including a faction from a mod that is no
			// longer installed. A stranger's zone must refuse the rite, not crash it.
			Faction villageFaction = Factions.GetIfExists(VillageFactionName);
			string villageName = villageFaction?.DisplayName ?? VillageFactionName;
			int reputation = The.Game.PlayerReputation.Get(VillageFactionName);
			bool alreadyChartered = System.GetStanding(VillageFactionName) >= KingdomRules.VillageCharterSealedStanding;
			KingdomRules.VillageCharterVerdict verdict = KingdomRules.JudgeVillageCharter(System.Founded, alreadyChartered, reputation);
			if (verdict != KingdomRules.VillageCharterVerdict.Allowed)
			{
				Popup.Show(KingdomRules.VillageCharterRefusal(verdict,
					KingdomPresentation.Rich(villageName)));
				return Refused();
			}
			LiquidVolume liquidVolume = ParentObject.GetPart<LiquidVolume>();
			int drams = KingdomLiquids.HasFreshWater(liquidVolume) ? liquidVolume.Volume : 0;
			if (drams < KingdomRules.FoundingCostDrams)
			{
				Popup.Show("Sealing a charter with {{C|" +
					KingdomPresentation.Rich(villageName) +
					"}} asks the same {{C|" + KingdomRules.FoundingCostDrams +
					" drams}} of fresh water the founding rite does. It holds " + drams + ".");
				return Refused();
			}
			if (Popup.ShowYesNo("Ask {{C|" + KingdomPresentation.Rich(villageName) +
				"}} to stand with {{C|" + KingdomPresentation.Rich(System.KingdomDisplayName) +
				"}}? Their ground stays theirs; nothing here is founded, claimed, or taken — only water, and a word kept.") != DialogResult.Yes)
			{
				return Refused();
			}
			KingdomFoundingResult result = KingdomFoundingTransaction.BeginVillageCharter(
				this, Actor, Site, VillageFactionName, villageName);
			if (!result.Committed)
			{
				ShowFailure(result);
				return result;
			}
			TransientCompletion = "You pour, and they drink.\n\n{{C|" +
				KingdomPresentation.Rich(villageName) +
				"}} stands with {{C|" + KingdomPresentation.Rich(System.KingdomDisplayName) +
				"}} now — their own place, their own people, and a covenant between you.\n\nLive and drink.";
			return result;
		}

		/// <summary>
		/// Commits the second city: its purpose, then the pour. The water is drawn only after the
		/// founding takes, so a refusal at the last moment leaves the basin as full as it was.
		/// </summary>
		private KingdomFoundingResult FoundSecondCity(KingdomSystem System, GameObject Actor,
			Zone Site, string Name)
		{
			string vocation = AskVocation(Name);
			if (vocation == null)
			{
				return Refused();
			}
			KingdomFoundingResult result = KingdomFoundingTransaction.BeginSecond(
				this, Actor, Site, Name, vocation);
			if (!result.Committed)
			{
				ShowFailure(result);
				return result;
			}
			bool isRuin = KingdomRules.IsRuinSite(System.FoundingTerrainBlueprint);
			string verb = isRuin ? "reclaimed" : "founded";
			string openingLine = isRuin
				? "You pour again, a long way from the first pouring, over ground the world already built, and those who walked out with you drink among walls that stood before them."
				: "You pour again, a long way from the first pouring, and those who walked out with you drink.";
			TransientCompletion = openingLine + "\n\n{{C|" +
				KingdomPresentation.Rich(Name) + "}} is " + verb +
				" on " + KingdomFounding.StyleGroundClause(System.Style) + ", " +
				KingdomSettlement.VocationClause(vocation) + ".\n\n{{C|" +
				KingdomPresentation.Rich(System.KingdomDisplayName) +
				"}} keeps its other ground without you. Come back to it and it will tell you what it did.";
			return result;
		}

		private static KingdomFoundingResult Refused()
		{
			return KingdomFoundingResult.From(KingdomFoundingOutcome.Refused,
				KingdomFoundingWaterDisposition.Untouched,
				KingdomFoundingProjection.None);
		}

		private static void ShowFailure(KingdomFoundingResult Result)
		{
			string detail = string.IsNullOrEmpty(Result.Failure)
				? "The rite did not commit."
				: Result.Failure;
			switch (Result.Water)
			{
			case KingdomFoundingWaterDisposition.RestoredExactly:
				Popup.Show(detail + "\n\nThe exact water was restored to this basin. Nothing was founded, sealed, or charged.");
				break;
			case KingdomFoundingWaterDisposition.HeldForRecovery:
				Popup.Show(detail + "\n\nThe pour has already published part of its promise. This basin holds its receipt; use it again on this same ground to finish. No time is charged until it does.");
				break;
			case KingdomFoundingWaterDisposition.RestorationFailed:
				Popup.Show(detail + "\n\nThe basin no longer matches its exact receipt. The rite is left pending and will not draw or charge again.");
				break;
			default:
				Popup.Show(detail + " Nothing has been poured or charged.");
				break;
			}
		}

		private static string CompletionText(KingdomSystem System, KingdomFoundingKind Kind,
			string Name, string Vocation, string VillageDisplayName)
		{
			switch (Kind)
			{
			case KingdomFoundingKind.VillageCharter:
				return "The interrupted covenant is sealed. {{C|" +
					KingdomPresentation.Rich(VillageDisplayName ?? Name ?? "the village") +
					"}} stands with {{C|" +
					KingdomPresentation.Rich(System.KingdomDisplayName) +
					"}}.\n\nLive and drink.";
			case KingdomFoundingKind.SecondCity:
				return "The interrupted pour takes. {{C|" +
					KingdomPresentation.Rich(Name ?? System.SeatName) +
					"}} stands as " + KingdomSettlement.VocationClause(Vocation) +
					", the realm's second city.\n\nLive and drink.";
			default:
				return "The interrupted first pour takes. {{C|" +
					KingdomPresentation.Rich(Name ?? System.KingdomDisplayName) +
					"}} stands, claimed and sealed.\n\nLive and drink.";
			}
		}

		/// <summary>
		/// Asks what the city is for. Every site offers the same readings, including the neutral
		/// one: terrain narrows what a place is good at, never whether it may exist.
		/// </summary>
		/// <param name="Name">The city's name, for the menu title.</param>
		/// <returns>A vocation from <see cref="KingdomSettlement.Vocations"/>, or null if the
		/// founder walked away from the question.</returns>
		private static string AskVocation(string Name)
		{
			string[] vocations = KingdomSettlement.Vocations;
			string[] options = new string[vocations.Length];
			for (int i = 0; i < vocations.Length; i++)
			{
				options[i] = "{{C|" + vocations[i] + "}} — " + KingdomSettlement.VocationBlurb(vocations[i]);
			}
			int picked = Popup.PickOption(Title: "What is " +
				KingdomPresentation.Rich(Name) + " for?", Intro: "A city is founded for something. Say it now, and the people who come will know what they came for.", Options: options, AllowEscape: true);
			if (picked < 0 || picked >= vocations.Length)
			{
				return null;
			}
			return vocations[picked];
		}
	}
}
