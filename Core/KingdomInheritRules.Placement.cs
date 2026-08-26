using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritRules
	{
		private static bool TryBuildPlan(Candidate[] Candidates, int Count,
			out KingdomInheritPlan Plan, out KingdomInheritFault Fault)
		{
			Plan = EmptyPlan();
			Fault = KingdomInheritFault.None;
			if (Count == 0)
			{
				return true;
			}
			long minX = long.MaxValue;
			long minY = long.MaxValue;
			long maxX = long.MinValue;
			long maxY = long.MinValue;
			for (int i = 0; i < Count; i++)
			{
				Rect rect;
				if (Candidates[i] == null || !TryRect(Candidates[i].Key, Candidates[i].X, Candidates[i].Y, out rect))
				{
					Fault = KingdomInheritFault.ImpossibleFootprint;
					return false;
				}
				if (rect.X1 < minX) minX = rect.X1;
				if (rect.Y1 < minY) minY = rect.Y1;
				if (rect.X2 > maxX) maxX = rect.X2;
				if (rect.Y2 > maxY) maxY = rect.Y2;
				for (int j = 0; j < i; j++)
				{
					Rect earlier;
					if (!TryRect(Candidates[j].Key, Candidates[j].X, Candidates[j].Y, out earlier))
					{
						Fault = KingdomInheritFault.ImpossibleFootprint;
						return false;
					}
					if (Overlaps(rect, earlier))
					{
						Fault = KingdomInheritFault.Overlap;
						return false;
					}
				}
			}
			long width = maxX - minX + 1L;
			long height = maxY - minY + 1L;
			if (width < 1L || height < 1L || width > MaxRelativeSpan || height > MaxRelativeSpan)
			{
				Fault = KingdomInheritFault.RelativeRange;
				return false;
			}
			KingdomInheritWork[] works = new KingdomInheritWork[Count];
			for (int i = 0; i < Count; i++)
			{
				long relativeX = (long)Candidates[i].X - minX;
				long relativeY = (long)Candidates[i].Y - minY;
				if (relativeX < 0L || relativeX > MaxRelativeSpan || relativeY < 0L || relativeY > MaxRelativeSpan)
				{
					Fault = KingdomInheritFault.RelativeRange;
					return false;
				}
				works[i] = new KingdomInheritWork(Candidates[i].Key, (int)relativeX, (int)relativeY,
					Candidates[i].Condition, Candidates[i].State,
					Candidates[i].ArchitectureSnapshot, Candidates[i].ArchitectureHash);
			}
			Plan = new KingdomInheritPlan(works, (int)width, (int)height);
			return true;
		}

		private static bool TryRect(string Key, int AnchorX, int AnchorY, out Rect Rect)
		{
			Rect = default(Rect);
			Definition definition = Find(Key);
			if (definition == null || definition.Width < 1 || definition.Height < 1)
			{
				return false;
			}
			long x1 = (long)AnchorX - (definition.Width - 1) / 2;
			long y1 = (long)AnchorY - (definition.Height - 1) / 2;
			long x2 = x1 + definition.Width - 1L;
			long y2 = y1 + definition.Height - 1L;
			if (x1 < int.MinValue || y1 < int.MinValue || x2 > int.MaxValue || y2 > int.MaxValue)
			{
				return false;
			}
			Rect.X1 = (int)x1;
			Rect.Y1 = (int)y1;
			Rect.X2 = (int)x2;
			Rect.Y2 = (int)y2;
			return true;
		}

		private static bool Overlaps(Rect A, Rect B)
		{
			return A.X1 <= B.X2 && A.X2 >= B.X1 && A.Y1 <= B.Y2 && A.Y2 >= B.Y1;
		}

		private static bool IsOccupied(Rect[] Occupied, int X, int Y)
		{
			for (int i = 0; i < Occupied.Length; i++)
			{
				if (X >= Occupied[i].X1 && X <= Occupied[i].X2 && Y >= Occupied[i].Y1 && Y <= Occupied[i].Y2)
				{
					return true;
				}
			}
			return false;
		}

		private static void ChooseHeart(KingdomInheritWork[] Works, int PlanWidth, int PlanHeight,
			int OffsetX, int OffsetY, out int X, out int Y)
		{
			int centerX = OffsetX + PlanWidth / 2;
			int centerY = OffsetY + PlanHeight / 2;
			X = centerX;
			Y = centerY;
			int bestHeart = -1;
			int bestDistance = int.MaxValue;
			int bestIndex = -1;
			for (int i = 0; i < Works.Length; i++)
			{
				int heart = HeartRank(Works[i].Key);
				int distance = Distance(Works[i].X, Works[i].Y, centerX, centerY);
				if (heart > bestHeart || (heart == bestHeart && distance < bestDistance)
					|| (heart == bestHeart && distance == bestDistance
						&& (bestIndex < 0 || Before(Works[i], Works[bestIndex]))))
				{
					bestHeart = heart;
					bestDistance = distance;
					bestIndex = i;
					X = Works[i].X;
					Y = Works[i].Y;
				}
			}
		}

		private static int HeartRank(string Key)
		{
			switch (Key)
			{
				case "heartcourt": return 4;
				case "heartmoot": return 3;
				case "heartwaterstone": return 2;
				case "heartbasin": return 1;
				default: return 0;
			}
		}

		private static bool TryEntry(Rect[] Occupied, int HeartX, int HeartY, int Width, int Height,
			out int CairnX, out int CairnY, out int EntryX, out int EntryY)
		{
			CairnX = 0;
			CairnY = 0;
			EntryX = 0;
			EntryY = 0;
			int best = int.MaxValue;
			for (int y = SafeMargin; y < Height - SafeMargin; y++)
			{
				ConsiderEntry(Occupied, SafeMargin, y, SafeMargin + 1, y, 0, y,
					HeartX, HeartY, ref best, ref CairnX, ref CairnY, ref EntryX, ref EntryY);
				ConsiderEntry(Occupied, Width - 1 - SafeMargin, y, Width - 2 - SafeMargin, y, Width - 1, y,
					HeartX, HeartY, ref best, ref CairnX, ref CairnY, ref EntryX, ref EntryY);
			}
			for (int x = SafeMargin; x < Width - SafeMargin; x++)
			{
				ConsiderEntry(Occupied, x, SafeMargin, x, SafeMargin + 1, x, 0,
					HeartX, HeartY, ref best, ref CairnX, ref CairnY, ref EntryX, ref EntryY);
				ConsiderEntry(Occupied, x, Height - 1 - SafeMargin, x, Height - 2 - SafeMargin, x, Height - 1,
					HeartX, HeartY, ref best, ref CairnX, ref CairnY, ref EntryX, ref EntryY);
			}
			return best != int.MaxValue;
		}

		private static void ConsiderEntry(Rect[] Occupied, int CandidateCairnX, int CandidateCairnY,
			int InsideX, int InsideY, int CandidateEntryX, int CandidateEntryY, int HeartX, int HeartY,
			ref int Best, ref int CairnX, ref int CairnY, ref int EntryX, ref int EntryY)
		{
			if (IsOccupied(Occupied, CandidateCairnX, CandidateCairnY) || IsOccupied(Occupied, InsideX, InsideY))
			{
				return;
			}
			int score = Distance(CandidateCairnX, CandidateCairnY, HeartX, HeartY);
			if (score < Best)
			{
				Best = score;
				CairnX = CandidateCairnX;
				CairnY = CandidateCairnY;
				EntryX = CandidateEntryX;
				EntryY = CandidateEntryY;
			}
		}

	}
}
