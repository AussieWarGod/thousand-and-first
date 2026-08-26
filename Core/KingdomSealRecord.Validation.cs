using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealRecord
	{
		private static bool TryReadCollectionsAndValidate(int Schema, KingdomSealBody Body,
			KingdomSealRecord record, ref KingdomSealFault Fault, ref string Detail)
		{
			if (!ReadTokens(Body, KeyWorkKey, MaxWorks, out record.WorkKeys, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyWorkX, MaxWorks, 0, 255, out record.WorkX, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyWorkY, MaxWorks, 0, 255, out record.WorkY, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyWorkCondition, MaxWorks, 0, 100, out record.WorkConditions, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyRollName, MaxRoll, MaxNameChars, out record.RollNames, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyRollOrigin, MaxRoll, MaxNameChars, out record.RollOrigins, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyRollArrived, MaxRoll, MaxNameChars, out record.RollArrived, ref Fault, ref Detail)
				|| !ReadTokens(Body, KeyOriginKey, MaxTallies, out record.OriginKeys, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyOriginCount, MaxTallies, 0, 100000, out record.OriginCounts, ref Fault, ref Detail)
				|| !ReadTokens(Body, KeyCreedKey, MaxTallies, out record.CreedKeys, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyCreedCount, MaxTallies, 0, 100000, out record.CreedCounts, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyChronicle, MaxChronicle, MaxLineChars, out record.Chronicle, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyOutsider, MaxChronicle, MaxLineChars, out record.Outsider, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyDeadName, MaxDead, MaxNameChars, out record.DeadNames, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyDeadCause, MaxDead, MaxLineChars, out record.DeadCauses, ref Fault, ref Detail))
			{
				return false;
			}
			if (Schema >= 5)
			{
				if (!ReadInt(Body, KeySpatialVersion, 0,
					KingdomInheritanceSpatialRules.SpatialVersion, out record.SpatialVersion,
					ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialWidth, 0,
						KingdomInheritanceSpatialRules.Width, out record.SpatialWidth,
						ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialHeight, 0,
						KingdomInheritanceSpatialRules.Height, out record.SpatialHeight,
						ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialEntrySide,
						KingdomInheritanceSpatialRules.NoEntry, KingdomInheritanceSpatialRules.West,
						out record.SpatialEntrySide, ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialEntryX, 0,
						KingdomInheritanceSpatialRules.Width - 1, out record.SpatialEntryX,
						ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialEntryY, 0,
						KingdomInheritanceSpatialRules.Height - 1, out record.SpatialEntryY,
						ref Fault, ref Detail)
					|| !ReadTexts(Body, KeyWorkSnapshot, MaxWorks,
						KingdomInheritanceSpatialRules.MaxSnapshotChars, out record.WorkSnapshots,
						ref Fault, ref Detail)
					|| !ReadTokens(Body, KeyWorkSnapshotHash, MaxWorks,
						out record.WorkSnapshotHashes, ref Fault, ref Detail)
					|| !ReadInts(Body, KeyStreetX, KingdomInheritanceSpatialRules.MaxStreetCells,
						0, KingdomInheritanceSpatialRules.Width - 1, out record.StreetX,
						ref Fault, ref Detail)
					|| !ReadInts(Body, KeyStreetY, KingdomInheritanceSpatialRules.MaxStreetCells,
						0, KingdomInheritanceSpatialRules.Height - 1, out record.StreetY,
						ref Fault, ref Detail))
				{
					return false;
				}
				if (record.SpatialVersion == 0)
				{
					if (record.SpatialWidth != 0 || record.SpatialHeight != 0
						|| record.SpatialEntrySide != KingdomInheritanceSpatialRules.NoEntry
						|| record.SpatialEntryX != 0 || record.SpatialEntryY != 0
						|| record.WorkSnapshots.Count != 0 || record.WorkSnapshotHashes.Count != 0
						|| record.StreetX.Count != 0 || record.StreetY.Count != 0)
					{
						Fault = KingdomSealFault.OutOfBounds;
						Detail = "the legacy spatial proxy carries partial current geometry";
						return false;
					}
				}
				else
				{
					KingdomInheritanceSpatialFault spatialFault;
					if (!KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys, record.WorkX,
						record.WorkY, record.WorkConditions, record.WorkSnapshots,
						record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
						record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
						record.StreetX, record.StreetY, out spatialFault))
					{
						Fault = KingdomSealFault.OutOfBounds;
						Detail = "the sealed architecture or street graph is malformed: "
							+ spatialFault;
						return false;
					}
				}
			}

			// Parallel columns are a row or they are nothing. A reader that trusted the longest
			// would invent a work out of a default coordinate, which is the city book's own rule
			// (KingdomCityBook.Normalize) applied at the one boundary where the data is untrusted.
			if (record.WorkKeys.Count != record.WorkX.Count || record.WorkKeys.Count != record.WorkY.Count
				|| record.WorkKeys.Count != record.WorkConditions.Count)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's plan of works is ragged";
				return false;
			}
			if (record.RollNames.Count != record.RollOrigins.Count || record.RollNames.Count != record.RollArrived.Count)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's roll of settlers is ragged";
				return false;
			}
			if (record.OriginKeys.Count != record.OriginCounts.Count || record.CreedKeys.Count != record.CreedCounts.Count
				|| record.DeadNames.Count != record.DeadCauses.Count)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's tallies are ragged";
				return false;
			}
			if (HasDuplicate(record.OriginKeys) || HasDuplicate(record.CreedKeys))
			{
				Fault = KingdomSealFault.DuplicateKey;
				Detail = "the seal tallies the same origin or creed twice";
				return false;
			}
			int expectedVigour = KingdomRules.SealedVigour((GrowthStage)record.Stage, record.Population,
				record.Defence, record.StoredWater, record.Withered);
			if (record.Vigour != expectedVigour)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's vigour does not match the facts it carries";
				return false;
			}
			// A resolved seal must be resolved in both halves. Half a promotion would place a
			// settlement in a state nothing drew, which is exactly the silent guess the whole
			// format exists to make impossible.
			bool rolled = record.InterregnumRoll >= 0;
			bool stated = record.InheritedState >= 0;
			if (rolled != stated)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal is half-promoted: it has " + (rolled ? "a draw and no state" : "a state and no draw");
				return false;
			}
			if (record.Status == KingdomSealStatus.Promoted && !rolled)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal claims to be promoted and carries no draw";
				return false;
			}
			if (record.Status != KingdomSealStatus.Promoted && rolled)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal carries a draw it was never promoted to make";
				return false;
			}

			return true;
		}

	}
}
