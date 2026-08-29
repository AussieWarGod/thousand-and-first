using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Engine-free appointment, identity, graph, and recovery law for named cooks.</summary>
	public static partial class KingdomNamedCookRules
	{
		public const int CurrentVersion = 1;
		public const int MaxIdentityChars = 1024;
		public const int MaxNameChars = 192;
		public const int MaxFaultChars = 512;
		public const string IngredientBlueprint = "Salthopper Chip";
		public const string IngredientDisplayName = "salthopper chip";
		public const int IngredientAmount = 1;
		public const string EffectUnitType = "CookingDomainTaste_UnitDoNothing";
		public const string RecipeTile = "Items/sw_dried_food.bmp";
		public const string RecipeColor = "&Y";
		public const char RecipeDetail = 'w';

		public static KingdomNamedCookVerdict JudgeCandidate(bool Founded, bool OwnedCity,
			bool StandingResident, bool ExactBody, bool PlayerOrFollower, bool HasSharesRecipe,
			bool HasTeachesDish, bool HasCookMarker, bool HasOpenReceipt, string RealmId,
			string SettlementId, int ResidentId, string ResidentName, string BodyObjectId)
		{
			if (!Founded) return KingdomNamedCookVerdict.Unfounded;
			if (!OwnedCity) return KingdomNamedCookVerdict.NotOwnedCity;
			if (!StandingResident) return KingdomNamedCookVerdict.NotStandingResident;
			if (!ExactBody) return KingdomNamedCookVerdict.BodyNotExact;
			if (PlayerOrFollower) return KingdomNamedCookVerdict.PlayerOrFollower;
			if (HasSharesRecipe || HasTeachesDish)
				return KingdomNamedCookVerdict.NativeRecipeAlreadyPresent;
			if (HasCookMarker) return KingdomNamedCookVerdict.ForeignCookMarker;
			if (HasOpenReceipt) return KingdomNamedCookVerdict.OpenReceipt;
			if (!Bounded(RealmId, MaxIdentityChars)
				|| !Bounded(SettlementId, MaxIdentityChars) || ResidentId <= 0
				|| !Bounded(ResidentName, MaxNameChars)
				|| !Bounded(BodyObjectId, MaxIdentityChars))
				return KingdomNamedCookVerdict.MalformedIdentity;
			return KingdomNamedCookVerdict.Allowed;
		}

		public static bool TryPrepare(string RealmId, string SettlementId,
			string SettlementName, int ResidentId, string ResidentName, string BodyObjectId,
			int Generation, long Tick, out KingdomNamedCookReceipt Receipt, out string Failure)
		{
			Receipt = null;
			Failure = "";
			string realm = SingleLine(RealmId, MaxIdentityChars);
			string settlement = SingleLine(SettlementId, MaxIdentityChars);
			string city = SingleLine(SettlementName, MaxNameChars);
			string resident = SingleLine(ResidentName, MaxNameChars);
			string body = SingleLine(BodyObjectId, MaxIdentityChars);
			if (string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(settlement)
				|| string.IsNullOrEmpty(city) || ResidentId <= 0
				|| string.IsNullOrEmpty(resident) || string.IsNullOrEmpty(body)
				|| Generation <= 0 || Tick < 0L)
				return Fail("named-cook preparation lacks bounded exact identity", out Failure);

			string digest = Digest("TAF-NAMED-COOK-V1", realm, settlement,
				ResidentId.ToString(CultureInfo.InvariantCulture),
				Generation.ToString(CultureInfo.InvariantCulture));
			if (string.IsNullOrEmpty(digest))
				return Fail("named-cook identity digest was unavailable", out Failure);
			string recipeName = SingleLine("salt-crack of " + city, MaxNameChars);
			string recipeId = "taf:named-cook:v1:recipe:" + digest;
			string effectId = GuidText(digest);
			string graph = GraphFingerprint(recipeId, recipeName, resident, effectId);
			Receipt = new KingdomNamedCookReceipt
			{
				Version = CurrentVersion,
				Phase = KingdomNamedCookPhase.Prepared,
				Generation = Generation,
				RealmId = realm,
				SettlementId = settlement,
				SettlementName = city,
				ResidentId = ResidentId,
				ResidentName = resident,
				BodyObjectId = body,
				RecipeId = recipeId,
				RecipeDisplayName = recipeName,
				EffectId = effectId,
				GraphFingerprint = graph,
				DesignatedTick = Tick
			};
			return Validate(Receipt, out Failure);
		}

		public static bool Validate(KingdomNamedCookReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (Receipt == null || Receipt.Version != CurrentVersion
				|| !Enum.IsDefined(typeof(KingdomNamedCookPhase), Receipt.Phase))
				return Fail("unknown named-cook receipt version or phase", out Failure);
			if (Receipt.Phase == KingdomNamedCookPhase.None)
				return Empty(Receipt) || Fail("idle named-cook receipt carries residue", out Failure);
			if (Receipt.Phase == KingdomNamedCookPhase.Quarantined)
				return OptionalBounded(Receipt.RealmId, MaxIdentityChars)
					&& OptionalBounded(Receipt.SettlementId, MaxIdentityChars)
					&& OptionalBounded(Receipt.SettlementName, MaxNameChars)
					&& OptionalBounded(Receipt.ResidentName, MaxNameChars)
					&& OptionalBounded(Receipt.BodyObjectId, MaxIdentityChars)
					&& OptionalBounded(Receipt.RecipeId, MaxIdentityChars)
					&& OptionalBounded(Receipt.RecipeDisplayName, MaxNameChars)
					&& OptionalBounded(Receipt.EffectId, MaxIdentityChars)
					&& OptionalBounded(Receipt.GraphFingerprint, MaxIdentityChars)
					&& Bounded(Receipt.Fault, MaxFaultChars)
					|| Fail("quarantined named-cook receipt is not bounded", out Failure);
			if (Receipt.Generation <= 0 || Receipt.ResidentId <= 0
				|| Receipt.DesignatedTick < 0L || Receipt.ReleasedTick < 0L
				|| !Bounded(Receipt.RealmId, MaxIdentityChars)
				|| !Bounded(Receipt.SettlementId, MaxIdentityChars)
				|| !Bounded(Receipt.SettlementName, MaxNameChars)
				|| !Bounded(Receipt.ResidentName, MaxNameChars)
				|| !Bounded(Receipt.BodyObjectId, MaxIdentityChars)
				|| !Bounded(Receipt.RecipeId, MaxIdentityChars)
				|| !Bounded(Receipt.RecipeDisplayName, MaxNameChars)
				|| !Guid.TryParseExact(Receipt.EffectId, "D", out _)
				|| !Bounded(Receipt.GraphFingerprint, MaxIdentityChars))
				return Fail("named-cook receipt has malformed bounded evidence", out Failure);
			string digest = Digest("TAF-NAMED-COOK-V1", Receipt.RealmId,
				Receipt.SettlementId, Receipt.ResidentId.ToString(CultureInfo.InvariantCulture),
				Receipt.Generation.ToString(CultureInfo.InvariantCulture));
			if (Receipt.RecipeId != "taf:named-cook:v1:recipe:" + digest
				|| Receipt.EffectId != GuidText(digest)
				|| Receipt.RecipeDisplayName != "salt-crack of " + Receipt.SettlementName
				|| Receipt.GraphFingerprint != GraphFingerprint(Receipt.RecipeId,
					Receipt.RecipeDisplayName, Receipt.ResidentName, Receipt.EffectId))
				return Fail("named-cook identity or direct recipe graph diverged", out Failure);
			if (!string.IsNullOrEmpty(Receipt.Fault))
				return Fail("active named-cook receipt carries a fault", out Failure);
			if (IsVacant(Receipt.Phase))
				return Receipt.ReleasedTick >= Receipt.DesignatedTick
					|| Fail("released named-cook receipt lacks its terminal tick", out Failure);
			if (IsVacancyPrepared(Receipt.Phase))
				return Receipt.ReleasedTick == 0L
					|| Fail("prepared named-cook vacancy carries a terminal tick", out Failure);
			return Receipt.ReleasedTick == 0L
				|| Fail("open named-cook receipt carries a terminal tick", out Failure);
		}

		public static KingdomNamedCookReceipt Applied(KingdomNamedCookReceipt Receipt)
		{
			return Move(Receipt, KingdomNamedCookPhase.Prepared,
				KingdomNamedCookPhase.Applied, 0L, "");
		}

		public static KingdomNamedCookReceipt BeginRelease(KingdomNamedCookReceipt Receipt)
		{
			return BeginVacancy(Receipt, KingdomNamedCookVacancyCause.Released);
		}

		public static KingdomNamedCookReceipt Released(KingdomNamedCookReceipt Receipt,
			long Tick)
		{
			return CompleteVacancy(Receipt, Tick);
		}

		public static KingdomNamedCookReceipt Quarantined(KingdomNamedCookReceipt Receipt,
			string Fault)
		{
			if (Receipt == null || Receipt.Phase == KingdomNamedCookPhase.None) return null;
			KingdomNamedCookReceipt copy = Receipt.Copy();
			copy.Phase = KingdomNamedCookPhase.Quarantined;
			copy.ReleasedTick = 0L;
			copy.Fault = SingleLine(Fault, MaxFaultChars);
			if (string.IsNullOrEmpty(copy.Fault)) copy.Fault = "named-cook evidence diverged";
			return copy;
		}

		public static string TeachingText(KingdomNamedCookReceipt Receipt)
		{
			return Receipt == null ? "" : Receipt.ResidentName
				+ " offers to teach you the city recipe for "
				+ Receipt.RecipeDisplayName + ".";
		}

		private static KingdomNamedCookReceipt Move(KingdomNamedCookReceipt Receipt,
			KingdomNamedCookPhase From, KingdomNamedCookPhase To, long Tick, string Fault)
		{
			if (Receipt == null || Receipt.Phase != From) return null;
			KingdomNamedCookReceipt copy = Receipt.Copy();
			copy.Phase = To;
			copy.ReleasedTick = Tick;
			copy.Fault = Fault ?? "";
			string failure;
			return Validate(copy, out failure) ? copy : null;
		}

		private static string GraphFingerprint(string RecipeId, string RecipeName,
			string ChefName, string EffectId)
		{
			return "taf:named-cook:v1:graph:" + Digest("TAF-NAMED-COOK-GRAPH-V1",
				RecipeId, RecipeName, ChefName, IngredientBlueprint, IngredientDisplayName,
				IngredientAmount.ToString(CultureInfo.InvariantCulture), EffectUnitType,
				EffectId, RecipeTile, RecipeColor, RecipeDetail.ToString());
		}

		private static string GuidText(string Hex)
		{
			if (string.IsNullOrEmpty(Hex) || Hex.Length < 32) return "";
			string raw = Hex.Substring(0, 32);
			return raw.Substring(0, 8) + "-" + raw.Substring(8, 4) + "-"
				+ raw.Substring(12, 4) + "-" + raw.Substring(16, 4) + "-"
				+ raw.Substring(20, 12);
		}

		private static string Digest(params string[] Fields)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					for (int i = 0; i < Fields.Length; i++) writer.Write(Fields[i] ?? "");
					writer.Flush();
					using (SHA256 sha = SHA256.Create())
					{
						byte[] hash = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < hash.Length; i++)
							text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
						return text.ToString();
					}
				}
			}
			catch { return ""; }
		}

		private static string SingleLine(string Value, int Limit)
		{
			if (string.IsNullOrWhiteSpace(Value) || Limit < 1) return "";
			StringBuilder text = new StringBuilder(Math.Min(Value.Length, Limit));
			bool space = false;
			for (int i = 0; i < Value.Length && text.Length < Limit; i++)
			{
				char c = Value[i];
				if (char.IsControl(c) || char.IsWhiteSpace(c)) { space = text.Length > 0; continue; }
				if (space && text.Length < Limit) text.Append(' ');
				space = false;
				if (text.Length < Limit) text.Append(c);
			}
			return text.ToString().Trim();
		}

		private static bool Bounded(string Value, int Limit)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= Limit
				&& string.Equals(Value, SingleLine(Value, Limit), StringComparison.Ordinal);
		}

		private static bool OptionalBounded(string Value, int Limit)
		{
			return string.IsNullOrEmpty(Value) || Bounded(Value, Limit);
		}

		private static bool Empty(KingdomNamedCookReceipt R)
		{
			return R.Generation == 0 && R.ResidentId == 0 && R.DesignatedTick == 0L
				&& R.ReleasedTick == 0L && string.IsNullOrEmpty(R.RealmId)
				&& string.IsNullOrEmpty(R.SettlementId) && string.IsNullOrEmpty(R.SettlementName)
				&& string.IsNullOrEmpty(R.ResidentName) && string.IsNullOrEmpty(R.BodyObjectId)
				&& string.IsNullOrEmpty(R.RecipeId) && string.IsNullOrEmpty(R.RecipeDisplayName)
				&& string.IsNullOrEmpty(R.EffectId) && string.IsNullOrEmpty(R.GraphFingerprint)
				&& string.IsNullOrEmpty(R.Fault);
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
