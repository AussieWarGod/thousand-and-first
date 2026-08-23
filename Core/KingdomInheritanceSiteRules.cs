using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Read-only worldgen facts for one possible inherited-seat surface zone.</summary>
	internal sealed class KingdomInheritanceSiteCandidate
	{
		internal string ZoneId = "";

		internal string TerrainBlueprint = "";

		internal string TerrainTag = "";

		internal int TerrainRank;

		internal int Tier;

		internal bool Mutable;

		internal bool Built;

		internal bool Water;

		internal bool Special;

		internal bool HasMapNote;

		internal bool HasGeneratedLocation;

		internal bool HasZoneBuilder;

		internal bool HasExplicitName;

		internal bool HasReservedZoneProperty;
	}

	internal enum KingdomInheritanceSiteFault
	{
		None = 0,
		NullInput = 1,
		NoSafeSite = 2
	}

	/// <summary>
	/// Pure, order-independent selection for the one inherited seat. No target seed, runtime RNG,
	/// directory order, or wall clock enters the rank.
	/// </summary>
	internal static class KingdomInheritanceSiteRules
	{
		internal const string WorldId = "JoppaWorld";

		internal const int SurfaceDepth = 10;

		internal const int MinTier = 1;

		internal const int MaxTier = 4;

		internal const int MaxTerrainRank = 3;

		internal static bool TrySelect(IList<KingdomInheritanceSiteCandidate> Candidates,
			string LegacyId, string OldGroundZoneId, string PreferredTerrainBlueprint,
			out KingdomInheritanceSiteCandidate Selected, out KingdomInheritanceSiteFault Fault)
		{
			Selected = null;
			Fault = KingdomInheritanceSiteFault.None;
			if (Candidates == null || string.IsNullOrEmpty(LegacyId))
			{
				Fault = KingdomInheritanceSiteFault.NullInput;
				return false;
			}

			for (int i = 0; i < Candidates.Count; i++)
			{
				KingdomInheritanceSiteCandidate candidate = Candidates[i];
				if (!IsSafe(candidate))
				{
					continue;
				}
				if (Selected == null || Compare(candidate, Selected, LegacyId, OldGroundZoneId,
					PreferredTerrainBlueprint) < 0)
				{
					Selected = candidate;
				}
			}
			if (Selected == null)
			{
				Fault = KingdomInheritanceSiteFault.NoSafeSite;
				return false;
			}
			return true;
		}

		internal static bool IsSafe(KingdomInheritanceSiteCandidate Candidate)
		{
			return Candidate != null
				&& IsCanonicalSurfaceZoneId(Candidate.ZoneId)
				&& Candidate.Mutable
				&& !Candidate.Built
				&& !Candidate.Water
				&& !Candidate.Special
				&& !Candidate.HasMapNote
				&& !Candidate.HasGeneratedLocation
				&& !Candidate.HasZoneBuilder
				&& !Candidate.HasExplicitName
				&& !Candidate.HasReservedZoneProperty
				&& Candidate.Tier >= MinTier
				&& Candidate.Tier <= MaxTier
				&& Candidate.TerrainRank >= 0
				&& Candidate.TerrainRank <= MaxTerrainRank
				&& IsStableTerrainToken(Candidate.TerrainBlueprint)
				&& IsStableTerrainToken(Candidate.TerrainTag);
		}

		internal static bool IsCanonicalSurfaceZoneId(string ZoneId)
		{
			int x;
			int y;
			return TrySurfaceCoordinates(ZoneId, out x, out y);
		}

		internal static bool TrySurfaceCoordinates(string ZoneId, out int X, out int Y)
		{
			return TryCoordinates(ZoneId, requireSurface: true, out X, out Y);
		}

		private static int Compare(KingdomInheritanceSiteCandidate A,
			KingdomInheritanceSiteCandidate B, string LegacyId, string OldGroundZoneId,
			string PreferredTerrainBlueprint)
		{
			int result = BoolRank(A.ZoneId == OldGroundZoneId).CompareTo(BoolRank(B.ZoneId == OldGroundZoneId));
			if (result != 0)
			{
				return result;
			}
			result = BoolRank(A.TerrainBlueprint == PreferredTerrainBlueprint)
				.CompareTo(BoolRank(B.TerrainBlueprint == PreferredTerrainBlueprint));
			if (result != 0)
			{
				return result;
			}
			result = A.TerrainRank.CompareTo(B.TerrainRank);
			if (result != 0)
			{
				return result;
			}
			result = DistanceFrom(A.ZoneId, OldGroundZoneId).CompareTo(
				DistanceFrom(B.ZoneId, OldGroundZoneId));
			if (result != 0)
			{
				return result;
			}
			result = A.Tier.CompareTo(B.Tier);
			if (result != 0)
			{
				return result;
			}
			ulong ah = StableHash(LegacyId, A.ZoneId);
			ulong bh = StableHash(LegacyId, B.ZoneId);
			result = ah.CompareTo(bh);
			return result != 0 ? result : string.CompareOrdinal(A.ZoneId, B.ZoneId);
		}

		private static int BoolRank(bool Preferred)
		{
			return Preferred ? 0 : 1;
		}

		private static int DistanceFrom(string ZoneId, string OldGroundZoneId)
		{
			int x;
			int y;
			int oldX;
			int oldY;
			if (!TryCoordinates(ZoneId, requireSurface: true, out x, out y)
				|| !TryCoordinates(OldGroundZoneId, requireSurface: false, out oldX, out oldY))
			{
				return int.MaxValue;
			}
			return Math.Abs(x - oldX) + Math.Abs(y - oldY);
		}

		private static bool TryCoordinates(string ZoneId, bool requireSurface, out int X, out int Y)
		{
			X = -1;
			Y = -1;
			if (string.IsNullOrEmpty(ZoneId))
			{
				return false;
			}
			string[] parts = ZoneId.Split('.');
			if (parts.Length != 6 || parts[0] != WorldId)
			{
				return false;
			}
			int px;
			int py;
			int zx;
			int zy;
			int z;
			if (!ParseCanonicalInt(parts[1], out px) || !ParseCanonicalInt(parts[2], out py)
				|| !ParseCanonicalInt(parts[3], out zx) || !ParseCanonicalInt(parts[4], out zy)
				|| !ParseCanonicalInt(parts[5], out z)
				|| px < 0 || px >= 80 || py < 0 || py >= 25
				|| zx < 0 || zx > 2 || zy < 0 || zy > 2
				|| z < 0 || z > 49 || (requireSurface && z != SurfaceDepth))
			{
				return false;
			}
			X = px * 3 + zx;
			Y = py * 3 + zy;
			return true;
		}

		private static bool ParseCanonicalInt(string Text, out int Value)
		{
			Value = 0;
			return !string.IsNullOrEmpty(Text)
				&& int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		internal static bool IsStableTerrainToken(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > KingdomSealRecord.MaxIdChars)
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z')
					&& !(c >= '0' && c <= '9') && c != '_' && c != '-')
				{
					return false;
				}
			}
			return true;
		}

		private static ulong StableHash(string LegacyId, string ZoneId)
		{
			const ulong offset = 14695981039346656037UL;
			const ulong prime = 1099511628211UL;
			ulong hash = offset;
			HashInto(ref hash, LegacyId, prime);
			hash ^= 124;
			hash *= prime;
			HashInto(ref hash, ZoneId, prime);
			return hash;
		}

		private static void HashInto(ref ulong Hash, string Value, ulong Prime)
		{
			if (Value == null)
			{
				return;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				Hash ^= (byte)c;
				Hash *= Prime;
				Hash ^= (byte)(c >> 8);
				Hash *= Prime;
			}
		}
	}
}
