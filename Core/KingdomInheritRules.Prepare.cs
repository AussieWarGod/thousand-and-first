using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritRules
	{
		internal static bool TryPrepare(IList<string> Keys, IList<int> X, IList<int> Y,
			IList<int> Conditions, KingdomRules.InheritedState State, int InterregnumRoll,
			out KingdomInheritPlacement Placement, out KingdomInheritFault Fault)
		{
			Placement = null;
			KingdomInheritPlan normalized;
			if (!TryNormalize(Keys, X, Y, Conditions, out normalized, out Fault))
			{
				return false;
			}
			KingdomInheritPlan inherited;
			if (!TryApplyState(normalized, State, InterregnumRoll, out inherited, out Fault))
			{
				return false;
			}
			return TryFit(inherited, TargetWidth, TargetHeight, out Placement, out Fault);
		}

		/// <summary>Current seals retain their witnessed zone-relative frame. Legacy spatial-v0
		/// records continue through the anchor-proxy path above.</summary>
		internal static bool TryPrepare(KingdomSealRecord Record,
			KingdomRules.InheritedState State, int InterregnumRoll,
			out KingdomInheritPlacement Placement, out KingdomInheritFault Fault)
		{
			Placement = null;
			Fault = KingdomInheritFault.None;
			if (Record == null)
			{
				Fault = KingdomInheritFault.NullInput;
				return false;
			}
			if (Record.SpatialVersion == 0)
				return TryPrepare(Record.WorkKeys, Record.WorkX, Record.WorkY,
					Record.WorkConditions, State, InterregnumRoll, out Placement, out Fault);
			return TryPrepareSpatial(Record, State, InterregnumRoll, out Placement, out Fault);
		}

		private static bool TryPrepareSpatial(KingdomSealRecord Record,
			KingdomRules.InheritedState State, int InterregnumRoll,
			out KingdomInheritPlacement Placement, out KingdomInheritFault Fault)
		{
			Placement = null;
			Fault = KingdomInheritFault.None;
			KingdomInheritanceSpatialFault spatialFault;
			if (!KingdomInheritanceSpatialRules.TryValidate(Record.WorkKeys, Record.WorkX,
				Record.WorkY, Record.WorkConditions, Record.WorkSnapshots,
				Record.WorkSnapshotHashes, Record.SpatialWidth, Record.SpatialHeight,
				Record.SpatialEntrySide, Record.SpatialEntryX, Record.SpatialEntryY,
				Record.StreetX, Record.StreetY, out spatialFault))
			{
				Fault = KingdomInheritFault.Malformed;
				return false;
			}
			if (!KingdomRules.IsKnownState(State))
			{
				Fault = KingdomInheritFault.InvalidState;
				return false;
			}
			if (InterregnumRoll < 0 || InterregnumRoll > 99)
			{
				Fault = KingdomInheritFault.InterregnumRollOutOfRange;
				return false;
			}

			KingdomInheritWork[] source = new KingdomInheritWork[Record.WorkKeys.Count];
			for (int i = 0; i < source.Length; i++)
			{
				string key = Record.WorkKeys[i];
				string encoded = Record.WorkSnapshots[i];
				string hash = Record.WorkSnapshotHashes[i];
				KingdomInheritWorkState workState = KingdomInheritWorkState.Standing;
				if (!IsInheritableKey(key))
				{
					key = MemoryKey;
					encoded = "";
					hash = "";
					workState = KingdomInheritWorkState.Memory;
				}
				else if (encoded.Length > 0)
				{
					ArchitectureLayoutSnapshot snapshot;
					if (!KingdomArchitectureRules.TryDecodeSnapshot(encoded, out snapshot, out _))
					{
						Fault = KingdomInheritFault.Malformed;
						return false;
					}
					// A first-basin binding proves old authority, not permission to mint that
					// authority in another world. The whole work becomes a named memory.
					if (IsFoundingHeartKey(key)
						|| KingdomInheritanceSpatialRules.HasExistingAuthority(snapshot))
					{
						key = MemoryKey;
						encoded = "";
						hash = "";
						workState = KingdomInheritWorkState.Memory;
					}
				}
				else if (IsFoundingHeartKey(key))
				{
					key = MemoryKey;
					workState = KingdomInheritWorkState.Memory;
				}
				source[i] = new KingdomInheritWork(key, Record.WorkX[i], Record.WorkY[i],
					workState == KingdomInheritWorkState.Memory ? 0 : Record.WorkConditions[i],
					workState, encoded, hash);
			}
			KingdomInheritPlan sourcePlan = new KingdomInheritPlan(source,
				KingdomInheritanceSpatialRules.Width, KingdomInheritanceSpatialRules.Height);
			bool[] faded = State == KingdomRules.InheritedState.Faded
				? Select(sourcePlan, FadedDerelictPercent, InterregnumRoll, false) : null;
			bool[] ruins = State == KingdomRules.InheritedState.Ruins
				? Select(sourcePlan, KingdomRules.StandingPercent(State, InterregnumRoll),
					InterregnumRoll, true) : null;
			KingdomInheritWork[] transformed = new KingdomInheritWork[source.Length];
			for (int i = 0; i < source.Length; i++)
			{
				KingdomInheritWork work = source[i];
				string key = work.Key;
				int condition = work.Condition;
				KingdomInheritWorkState workState = work.State;
				string encoded = work.ArchitectureSnapshot;
				string hash = work.ArchitectureHash;
				if (workState != KingdomInheritWorkState.Memory)
				{
					if (State == KingdomRules.InheritedState.Held)
					{
						condition = Min(condition, HeldConditionCeiling);
						workState = KingdomInheritWorkState.Standing;
					}
					else if (State == KingdomRules.InheritedState.Faded)
					{
						bool derelict = faded[i];
						condition = Min(condition, derelict
							? FadedDerelictConditionCeiling : FadedStandingConditionCeiling);
						workState = derelict ? KingdomInheritWorkState.Derelict
							: KingdomInheritWorkState.Standing;
					}
					else if (KingdomRules.AllWorksSurvive(State))
					{
						condition = Min(condition, AbandonedDerelictConditionCeiling);
						workState = KingdomInheritWorkState.Derelict;
					}
					else if (ruins[i])
					{
						condition = Min(condition, RuinsDerelictConditionCeiling);
						workState = KingdomInheritWorkState.Derelict;
					}
					else
					{
						key = RubbleKey;
						condition = 0;
						workState = KingdomInheritWorkState.Rubble;
						encoded = "";
						hash = "";
					}
				}
				transformed[i] = new KingdomInheritWork(key, work.X, work.Y, condition,
					workState, encoded, hash);
			}

			Rect[] occupied = new Rect[transformed.Length];
			for (int i = 0; i < transformed.Length; i++)
				if (!TryPreparedRect(transformed[i], out occupied[i]))
				{
					Fault = KingdomInheritFault.ImpossibleFootprint;
					return false;
				}
			int heartX;
			int heartY;
			ChooseHeart(transformed, KingdomInheritanceSpatialRules.Width,
				KingdomInheritanceSpatialRules.Height, 0, 0, out heartX, out heartY);
			int cairnX;
			int cairnY;
			int entryX = Record.SpatialEntryX;
			int entryY = Record.SpatialEntryY;
			if (Record.StreetX.Count > 0)
			{
				if (!TryStreetCairn(occupied, Record.StreetX, Record.StreetY,
					heartX, heartY, out cairnX, out cairnY))
				{
					Fault = KingdomInheritFault.NoEntry;
					return false;
				}
			}
			else if (!TryEntry(occupied, heartX, heartY, TargetWidth, TargetHeight,
				out cairnX, out cairnY, out entryX, out entryY))
			{
				Fault = KingdomInheritFault.NoEntry;
				return false;
			}
			KingdomInheritWork[] result = new KingdomInheritWork[transformed.Length + 1];
			Array.Copy(transformed, result, transformed.Length);
			result[transformed.Length] = new KingdomInheritWork(FounderCairnKey,
				cairnX, cairnY, 0, KingdomInheritWorkState.Memory);
			if (transformed.Length == 0) { heartX = cairnX; heartY = cairnY; }
			Placement = new KingdomInheritPlacement(result, entryX, entryY, cairnX, cairnY,
				heartX, heartY, RemainingEngineChecks, Record.SpatialVersion,
				Record.StreetX, Record.StreetY);
			return true;
		}

		private static bool TryPreparedRect(KingdomInheritWork Work, out Rect Rect)
		{
			Rect = default(Rect);
			if (Work == null) return false;
			if (Work.ArchitectureSnapshot.Length == 0)
				return TryRect(Work.Key, Work.X, Work.Y, out Rect);
			ArchitectureLayoutSnapshot snapshot;
			KingdomInheritanceSpatialRules.Rect exact;
			if (!KingdomArchitectureRules.TryDecodeSnapshot(Work.ArchitectureSnapshot,
				out snapshot, out _) || !KingdomInheritanceSpatialRules.TrySnapshotRect(snapshot,
					Work.X, Work.Y, out exact)) return false;
			Rect.X1 = exact.X1;
			Rect.Y1 = exact.Y1;
			Rect.X2 = exact.X2;
			Rect.Y2 = exact.Y2;
			return true;
		}

		private static bool TryStreetCairn(Rect[] Occupied, IList<int> StreetX,
			IList<int> StreetY, int HeartX, int HeartY, out int CairnX, out int CairnY)
		{
			CairnX = 0;
			CairnY = 0;
			bool[,] street = new bool[TargetWidth, TargetHeight];
			for (int i = 0; i < StreetX.Count; i++) street[StreetX[i], StreetY[i]] = true;
			int best = int.MaxValue;
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			for (int i = 0; i < StreetX.Count; i++)
			{
				for (int d = 0; d < 4; d++)
				{
					int x = StreetX[i] + dx[d];
					int y = StreetY[i] + dy[d];
					if (x < 1 || y < 1 || x >= TargetWidth - 1
						|| y >= TargetHeight - 1 || street[x, y]
						|| IsOccupied(Occupied, x, y)) continue;
					int score = Distance(x, y, HeartX, HeartY);
					if (score < best || (score == best
						&& (y < CairnY || (y == CairnY && x < CairnX))))
					{
						best = score;
						CairnX = x;
						CairnY = y;
					}
				}
			}
			return best != int.MaxValue;
		}

	}
}
