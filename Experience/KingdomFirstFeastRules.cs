using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Pure identity and validation law for one finite First Feast proposal.</summary>
	public static partial class KingdomFirstFeastRules
	{
		public const int MaxCandidates = 128;
		public const string DeedPrefix = "taf:experience:first-feast:deed:";
		public const string PracticePrefix = "taf:experience:first-feast:practice:";
		public const string AuthoredDeed =
			"a later adventure followed the First Guest's successful homecoming";
		public const string AuthoredDish = "the first-water share";
		public const string AuthoredIngredients =
			"vinewafer, starapple preserve, and clean water";
		public const string OfferedDedication =
			"the founding water and the hands that carried it";
		public const string ResidentDedication = "the residents who keep the city standing";
		public const string TravelerDedication =
			"the travelers received at the city's water";
		public const string RemembranceDedication =
			"the dead whose work still shelters the living";

		public static KingdomFirstFeastRecipeDisposition RecipeDisposition =>
			KingdomFirstFeastRecipeDisposition.NamedCookServiceSupersedes;

		public static bool TryBuildDeedId(KingdomFirstFeastDeed Deed, out string DeedId)
		{
			DeedId = null;
			if (!ValidSource(Deed, false)) return false;
			string digest = Digest(new string[] { Deed.SettlementId,
				Deed.GuestTerminalReceiptId, Deed.GuestTerminalDigest,
				Deed.GuestTerminalTick.ToString(System.Globalization.CultureInfo.InvariantCulture),
				Deed.AdventureEventId, Deed.AdventureFingerprint,
				Deed.DeedTick.ToString(System.Globalization.CultureInfo.InvariantCulture) });
			if (digest == null) return false;
			DeedId = DeedPrefix + digest;
			return KernelSemanticId.IsValid(DeedId) && DeedId.Length == DeedPrefix.Length + 64;
		}

		public static bool TryBuildPracticeId(string DeedId, out string PracticeId)
		{
			PracticeId = null;
			if (!DigestSuffix(DeedId, out string digest)) return false;
			PracticeId = PracticePrefix + digest;
			return KernelSemanticId.IsValid(PracticeId);
		}

		public static bool Valid(KingdomFirstFeastReceipt Row)
		{
			if (Row == null || Row.Version != KingdomFirstFeastReceipt.CurrentVersion
				|| Row.Generation != 1 || !Enum.IsDefined(typeof(KingdomFirstFeastPhase), Row.Phase)
				|| !Enum.IsDefined(typeof(KingdomFirstFeastChoice), Row.Choice)
				|| !KingdomExperienceRules.TypedId(Row.SettlementId, "taf:settlement:")
				|| !KingdomExperienceRules.CivicText(Row.SettlementName, true)
				|| !DigestSuffix(Row.DeedId, out string _)
				|| Row.DeedText != AuthoredDeed || Row.DeedTick < 0L
				|| !TerminalId(Row.GuestTerminalReceiptId)
				|| !LowerHex(Row.GuestTerminalDigest, 64) || Row.GuestTerminalTick < 0L
				|| !KernelSemanticId.IsValid(Row.AdventureEventId)
				|| Row.AdventureEventId.Length > KingdomChronicleReceiptRules.MaxEventIdChars
				|| !LowerHex(Row.AdventureFingerprint, 64)
				|| Row.DeedTick <= Row.GuestTerminalTick
				|| Row.ProposerResidentId <= 0 || Row.WitnessResidentId <= 0
				|| Row.ProposerResidentId == Row.WitnessResidentId
				|| !KingdomExperienceRules.CivicText(Row.ProposerName, true)
				|| !KingdomExperienceRules.CivicText(Row.WitnessName, true)
				|| Row.DishName != AuthoredDish || Row.Ingredients != AuthoredIngredients
				|| Row.OfferedDedication != OfferedDedication || Row.OfferedTick < Row.DeedTick
				|| Row.EnableEpoch < 1L) return false;
			if (Row.Phase == KingdomFirstFeastPhase.Quarantined)
				return BoundedResidue(Row) && KingdomExperienceRules.Text(Row.Fault, true);
			if (!string.IsNullOrEmpty(Row.Fault)) return false;
			if (Row.Phase == KingdomFirstFeastPhase.Offered)
				return Row.Choice == KingdomFirstFeastChoice.None && Row.DecidedTick == 0L
					&& string.IsNullOrEmpty(Row.AdaptedDedication)
					&& string.IsNullOrEmpty(Row.PracticeId);
			if (Row.DecidedTick < Row.OfferedTick) return false;
			if (Row.Phase == KingdomFirstFeastPhase.Archived)
				return Row.Choice == KingdomFirstFeastChoice.None
					&& string.IsNullOrEmpty(Row.AdaptedDedication)
					&& string.IsNullOrEmpty(Row.PracticeId);
			if (Row.Phase == KingdomFirstFeastPhase.Refused)
				return Row.Choice == KingdomFirstFeastChoice.Refuse
					&& string.IsNullOrEmpty(Row.AdaptedDedication)
					&& string.IsNullOrEmpty(Row.PracticeId);
			if (!TryBuildPracticeId(Row.DeedId, out string expected)
				|| Row.PracticeId != expected) return false;
			if (Row.Phase == KingdomFirstFeastPhase.Adopted)
				return Row.Choice == KingdomFirstFeastChoice.Adopt
					&& string.IsNullOrEmpty(Row.AdaptedDedication);
			return Row.Phase == KingdomFirstFeastPhase.Adapted
				&& Row.Choice == KingdomFirstFeastChoice.Adapt
				&& IsAdaptation(Row.AdaptedDedication);
		}

		public static bool IsAffirmative(KingdomFirstFeastReceipt Row)
		{
			return Valid(Row) && (Row.Phase == KingdomFirstFeastPhase.Adopted
				|| Row.Phase == KingdomFirstFeastPhase.Adapted);
		}

		public static string EffectiveDedication(KingdomFirstFeastReceipt Row)
		{
			if (!Valid(Row)) return null;
			return Row.Phase == KingdomFirstFeastPhase.Adapted
				? Row.AdaptedDedication : Row.OfferedDedication;
		}

		public static bool IsAdaptation(string Dedication)
		{
			return Dedication == ResidentDedication || Dedication == TravelerDedication
				|| Dedication == RemembranceDedication;
		}

		internal static bool ExactSource(KingdomFirstFeastReceipt Row,
			KingdomFirstFeastDeed Deed)
		{
			if (!Valid(Row) || !ValidSource(Deed, true)
				|| !TryBuildDeedId(Deed, out string id)) return false;
			return Row.SettlementId == Deed.SettlementId && Row.SettlementName == Deed.SettlementName
				&& Row.DeedId == id && Row.DeedText == Deed.DeedText && Row.DeedTick == Deed.DeedTick
				&& Row.GuestTerminalReceiptId == Deed.GuestTerminalReceiptId
				&& Row.GuestTerminalDigest == Deed.GuestTerminalDigest
				&& Row.GuestTerminalTick == Deed.GuestTerminalTick
				&& Row.AdventureEventId == Deed.AdventureEventId
				&& Row.AdventureFingerprint == Deed.AdventureFingerprint;
		}

		private static bool DigestSuffix(string DeedId, out string Digest)
		{
			Digest = null;
			if (!KernelSemanticId.IsValid(DeedId)
				|| !DeedId.StartsWith(DeedPrefix, StringComparison.Ordinal)) return false;
			string suffix = DeedId.Substring(DeedPrefix.Length);
			if (!LowerHex(suffix, 64)) return false;
			Digest = suffix; return true;
		}

		private static bool ValidSource(KingdomFirstFeastDeed D, bool RequireId)
		{
			return D != null && KingdomExperienceRules.TypedId(D.SettlementId, "taf:settlement:")
				&& KingdomExperienceRules.CivicText(D.SettlementName, true)
				&& D.DeedText == AuthoredDeed && D.GuestTerminalTick >= 0L
				&& D.DeedTick > D.GuestTerminalTick && TerminalId(D.GuestTerminalReceiptId)
				&& LowerHex(D.GuestTerminalDigest, 64)
				&& KernelSemanticId.IsValid(D.AdventureEventId)
				&& D.AdventureEventId.Length <= KingdomChronicleReceiptRules.MaxEventIdChars
				&& LowerHex(D.AdventureFingerprint, 64)
				&& (!RequireId || TryBuildDeedId(D, out string id) && id == D.DeedId);
		}

		private static bool TerminalId(string value) => value != null
			&& value.StartsWith("taf:growth-first-guest-terminal:", StringComparison.Ordinal)
			&& value.Length == KingdomGuestFeastRules.MaxTerminalReceiptIdBytes
			&& KernelSemanticId.IsValid(value);

		private static bool LowerHex(string value, int length)
		{
			if (value == null || value.Length != length) return false;
			for (int i = 0; i < value.Length; i++)
				if (!((value[i] >= '0' && value[i] <= '9')
					|| (value[i] >= 'a' && value[i] <= 'f'))) return false;
			return true;
		}

		private static string Digest(string[] fields)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
				{
					writer.Write("TAF-FIRST-FEAST-DEED-V2");
					for (int i = 0; i < fields.Length; i++) writer.Write(fields[i]);
					writer.Flush();
					using (SHA256 sha = SHA256.Create())
					{
						byte[] hash = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2"));
						return text.ToString();
					}
				}
			}
			catch { return null; }
		}

		private static bool BoundedResidue(KingdomFirstFeastReceipt R)
		{
			return KingdomExperienceRules.CivicText(R.AdaptedDedication, false)
				&& KingdomExperienceRules.CivicText(R.PracticeId, false)
				&& R.DecidedTick >= 0L;
		}
	}
}
