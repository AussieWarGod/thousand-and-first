using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free authority for the optional pre-founded start. Runtime may only move this
	/// receipt one phase at a time after measuring the corresponding physical object.
	/// </summary>
	public static partial class KingdomQuickstartRules
	{
		public const string ModeId = "KingdomQuickstart";
		public const string LocationSet = "TAFKingdomQuickstart";
		public const string ProfileState = "r_TAF_QuickstartProfile_v1";
		public const string ReceiptState = "r_TAF_QuickstartReceipt_v1";
		public const string QuarantineState = "r_TAF_QuickstartQuarantine_v1";
		public const string WorldReservationState = "r_TAF_QuickstartWorldReservation_v1";
		public const string AdvisorOption = "r_TAF_OptionQuickstartAdvisor";
		public const string GrantMarkerProperty = "r_TAF_QuickstartGrant_v1";

		/// <summary>
		/// The quickstart shelter's own reservation. Deliberately NOT
		/// <see cref="GrantMarkerProperty"/>: the grant recovery scan reads every object in the
		/// zone and refuses any that wears the grant marker with a value it did not mint, so a
		/// staked lot carrying that property would abort every later grant phase on the same boot.
		/// </summary>
		public const string ShelterMarkerProperty = "KingdomQuickstartShelter";

		/// <summary>Catalogue key of the lots the quickstart stakes at founding.</summary>
		public const string ShelterBuildKey = "tentrow";

		public const int StartCellX = 40;
		public const int StartCellY = 12;
		public const int WaterCellX = 28;
		public const int WaterCellY = 10;
		public const int LarderCellX = 28;
		public const int LarderCellY = 12;
		public const int StockpileCellX = 28;
		public const int StockpileCellY = 14;
		public const int AdvisorCellX = 28;
		public const int AdvisorCellY = 16;

		// The shelter lots: two Small plots (6x4) stacked west of the supply column, clear of the
		// reserved role cells at x=28, of the founder's start cell, and of the heart's extreme
		// survey (which begins at x=31 on an 80-wide zone), so neither row is ever marked yielding
		// and neither contends with a heart rung for its ground. Two tent rows carry three beds
		// each, six in all, so arrivals are not refused for want of room on the day the rows
		// finish.
		private static readonly KingdomPlotRules.PlotRect[] ShelterLots =
		{
			new KingdomPlotRules.PlotRect(21, 9, 26, 12),
			new KingdomPlotRules.PlotRect(21, 13, 26, 16)
		};

		public const int StarterWaterDrams = 24;
		public const int StarterFoodServings = 12;
		public const int StarterMud = 1;
		public const int StarterBrush = 3;
		public const int StarterTimber = 4;

		private const int MaximumWireLength = 4096;
		private const int MaximumFieldLength = 512;

		private static readonly KingdomQuickstartProfile[] Profiles =
		{
			new KingdomQuickstartProfile("marsh", "TAFQuickstartMarsh",
				"JoppaWorld.8.22.1.1.10", "Reedwake", "TerrainSaltmarsh", 8, 22),
			new KingdomQuickstartProfile("canyon", "TAFQuickstartCanyon",
				"JoppaWorld.14.17.1.1.10", "Riftside", "TerrainDesertCanyon", 14, 17),
			new KingdomQuickstartProfile("dunes", "TAFQuickstartDunes",
				"JoppaWorld.6.17.1.1.10", "Saltwake", "TerrainSaltdunes", 6, 17)
		};

		/// <summary>How many shelter lots the founding pass stakes.</summary>
		public static int ShelterLotCount
		{
			get { return ShelterLots.Length; }
		}

		/// <summary>
		/// One reserved shelter lot, by index. <c>PlotRect</c> is a value, so a caller reads a
		/// copy and no caller can move the reservation this authority declares.
		/// </summary>
		public static KingdomPlotRules.PlotRect ShelterLot(int Index)
		{
			if (Index < 0 || Index >= ShelterLots.Length)
				throw new ArgumentOutOfRangeException("Index", "The quickstart reserves "
					+ ShelterLots.Length + " shelter lots.");
			return ShelterLots[Index];
		}

		public static int ProfileCount
		{
			get { return Profiles.Length; }
		}

		internal static bool RequiresPreparedGround(int X, int Y)
		{
			bool apron = X >= 37 && X <= 44 && Y >= 10 && Y <= 15;
			bool supply = X >= 27 && X <= 30 && Y >= 9 && Y <= 17;
			bool approach = X >= 29 && X <= 37 && Y >= 11 && Y <= 13;
			// North heartbasin: rite (40,12), rect (38,11)-(43,14), doors (40/41,14),
			// margin Y=15 and authored lane endpoints Y=16.
			bool heartLanes = X >= 40 && X <= 41 && Y == 16;
			// Both shelter lots are bared with the rest of the camp, because the authored-ground
			// preflight refuses a lot holding a creature, an item, or open liquid.
			bool shelter = false;
			for (int i = 0; i < ShelterLots.Length; i++)
				if (ShelterLots[i].Contains(X, Y))
				{
					shelter = true;
					break;
				}
			return apron || supply || approach || heartLanes || shelter;
		}

		public static bool IsMode(string GameMode)
		{
			return string.Equals(GameMode, ModeId, StringComparison.Ordinal);
		}

		public static bool TryProfile(string Key, out KingdomQuickstartProfile Profile)
		{
			Profile = null;
			if (string.IsNullOrEmpty(Key)) return false;
			for (int i = 0; i < Profiles.Length; i++)
				if (string.Equals(Profiles[i].Key, Key, StringComparison.Ordinal))
				{
					Profile = Profiles[i];
					return true;
				}
			return false;
		}

		public static bool TryProfileForLocation(string LocationId,
			out KingdomQuickstartProfile Profile)
		{
			Profile = null;
			if (string.IsNullOrEmpty(LocationId)) return false;
			for (int i = 0; i < Profiles.Length; i++)
				if (string.Equals(Profiles[i].LocationId, LocationId,
					StringComparison.Ordinal))
				{
					Profile = Profiles[i];
					return true;
				}
			return false;
		}

		public static bool TryCreateReceipt(string ProfileKey, string ZoneId,
			out KingdomQuickstartReceipt Receipt)
		{
			Receipt = null;
			KingdomQuickstartProfile profile;
			if (!TryProfile(ProfileKey, out profile)
				|| !string.Equals(profile.ZoneId, ZoneId, StringComparison.Ordinal)) return false;
			Receipt = new KingdomQuickstartReceipt
			{
				ProfileKey = profile.Key,
				ZoneId = profile.ZoneId,
				Phase = KingdomQuickstartPhase.Reserved
			};
			return Valid(Receipt);
		}

		/// <summary>Advances exactly one phase and freezes only that phase's new identity.</summary>
		public static bool TryAdvance(KingdomQuickstartReceipt Current,
			KingdomQuickstartPhase Next, string Value,
			KingdomQuickstartAdvisorDisposition Advisor,
			out KingdomQuickstartReceipt Advanced)
		{
			Advanced = null;
			if (!Valid(Current) || (int)Next != (int)Current.Phase + 1) return false;
			KingdomQuickstartReceipt copy = Current.Copy();
			copy.Phase = Next;
			switch (Next)
			{
			case KingdomQuickstartPhase.Founded:
				if (!Bounded(Value) || Advisor != KingdomQuickstartAdvisorDisposition.Unresolved)
					return false;
				copy.FoodBlueprint = Value;
				break;
			case KingdomQuickstartPhase.WaterStocked:
				if (!Identity(Value) || Advisor != KingdomQuickstartAdvisorDisposition.Unresolved)
					return false;
				copy.WaterObjectId = Value;
				break;
			case KingdomQuickstartPhase.FoodStocked:
				if (!Identity(Value) || Advisor != KingdomQuickstartAdvisorDisposition.Unresolved)
					return false;
				copy.LarderObjectId = Value;
				break;
			case KingdomQuickstartPhase.MaterialsStocked:
				if (!Identity(Value) || Advisor != KingdomQuickstartAdvisorDisposition.Unresolved)
					return false;
				copy.StockpileObjectId = Value;
				break;
			case KingdomQuickstartPhase.AdvisorResolved:
				if (Advisor == KingdomQuickstartAdvisorDisposition.Included)
				{
					if (!Identity(Value)) return false;
					copy.AdvisorObjectId = Value;
				}
				else if (Advisor == KingdomQuickstartAdvisorDisposition.Omitted)
				{
					if (!string.IsNullOrEmpty(Value)) return false;
				}
				else return false;
				copy.AdvisorDisposition = Advisor;
				break;
			case KingdomQuickstartPhase.Complete:
				if (!string.IsNullOrEmpty(Value)
					|| Advisor != KingdomQuickstartAdvisorDisposition.Unresolved) return false;
				break;
			default:
				return false;
			}
			if (!Valid(copy)) return false;
			Advanced = copy;
			return true;
		}

		/// <summary>
		/// Decides one crash boundary without inspecting mutable quantities. Physical preparation
		/// is private until its single placement mutation; the receipt then publishes that exact
		/// placed object's identity.
		/// </summary>
		public static KingdomQuickstartRecoveryAction RecoveryAction(
			KingdomQuickstartPhase Current, KingdomQuickstartPhase Target,
			KingdomQuickstartGrantObservation Observation)
		{
			if (Target < KingdomQuickstartPhase.WaterStocked
				|| Target > KingdomQuickstartPhase.AdvisorResolved
				|| Current < KingdomQuickstartPhase.Founded
				|| Current > KingdomQuickstartPhase.Complete
				|| Observation == KingdomQuickstartGrantObservation.ForeignOrMalformed)
				return KingdomQuickstartRecoveryAction.Refuse;
			if ((int)Current + 1 == (int)Target)
				return Observation == KingdomQuickstartGrantObservation.Absent
					? KingdomQuickstartRecoveryAction.PreparePlaceAndPublish
					: KingdomQuickstartRecoveryAction.PublishExisting;
			if ((int)Current >= (int)Target)
				return Observation == KingdomQuickstartGrantObservation.ExactPlaced
					? KingdomQuickstartRecoveryAction.VerifyPublished
					: KingdomQuickstartRecoveryAction.Refuse;
			return KingdomQuickstartRecoveryAction.Refuse;
		}

		public static bool Valid(KingdomQuickstartReceipt Receipt)
		{
			KingdomQuickstartProfile profile;
			if (Receipt == null || !TryProfile(Receipt.ProfileKey, out profile)
				|| !string.Equals(Receipt.ZoneId, profile.ZoneId, StringComparison.Ordinal)
				|| (int)Receipt.Phase < (int)KingdomQuickstartPhase.Reserved
				|| (int)Receipt.Phase > (int)KingdomQuickstartPhase.Complete
				|| !Field(Receipt.FoodBlueprint) || !Field(Receipt.WaterObjectId)
				|| !Field(Receipt.LarderObjectId) || !Field(Receipt.StockpileObjectId)
				|| !Field(Receipt.AdvisorObjectId)) return false;
			bool founded = (int)Receipt.Phase >= (int)KingdomQuickstartPhase.Founded;
			bool water = (int)Receipt.Phase >= (int)KingdomQuickstartPhase.WaterStocked;
			bool food = (int)Receipt.Phase >= (int)KingdomQuickstartPhase.FoodStocked;
			bool materials = (int)Receipt.Phase >= (int)KingdomQuickstartPhase.MaterialsStocked;
			bool advisor = (int)Receipt.Phase >= (int)KingdomQuickstartPhase.AdvisorResolved;
			if (founded != !string.IsNullOrEmpty(Receipt.FoodBlueprint)
				|| water != !string.IsNullOrEmpty(Receipt.WaterObjectId)
				|| food != !string.IsNullOrEmpty(Receipt.LarderObjectId)
				|| materials != !string.IsNullOrEmpty(Receipt.StockpileObjectId)) return false;
			if (!advisor)
				return Receipt.AdvisorDisposition == KingdomQuickstartAdvisorDisposition.Unresolved
					&& string.IsNullOrEmpty(Receipt.AdvisorObjectId);
			return Receipt.AdvisorDisposition == KingdomQuickstartAdvisorDisposition.Included
				? !string.IsNullOrEmpty(Receipt.AdvisorObjectId)
				: Receipt.AdvisorDisposition == KingdomQuickstartAdvisorDisposition.Omitted
					&& string.IsNullOrEmpty(Receipt.AdvisorObjectId);
		}

		private static bool Field(string Value)
		{
			return Value != null && Value.Length <= MaximumFieldLength
				&& Value.IndexOf('\0') < 0;
		}

		private static bool Bounded(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Field(Value);
		}

		private static bool Identity(string Value)
		{
			return Bounded(Value) && Value.IndexOf('|') < 0 && Value.IndexOf('\r') < 0
				&& Value.IndexOf('\n') < 0;
		}

	}
}
