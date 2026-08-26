using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritRules
	{
		internal static bool TryApplyState(KingdomInheritPlan Source, KingdomRules.InheritedState State,
			int InterregnumRoll,
			out KingdomInheritPlan Plan, out KingdomInheritFault Fault)
		{
			Plan = EmptyPlan();
			Fault = KingdomInheritFault.None;
			try
			{
				if (Source == null)
				{
					Fault = KingdomInheritFault.NullInput;
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
				bool[] fadedDerelict = (State == KingdomRules.InheritedState.Faded)
					? Select(Source, FadedDerelictPercent, InterregnumRoll, PreferHeart: false)
					: null;
				bool[] ruinsStanding = (State == KingdomRules.InheritedState.Ruins)
					? Select(Source, KingdomRules.StandingPercent(State, InterregnumRoll), InterregnumRoll, PreferHeart: true)
					: null;
				Candidate[] transformed = new Candidate[Source.Count];
				for (int i = 0; i < Source.Count; i++)
				{
					KingdomInheritWork work = Source.WorkAt(i);
					if (work == null)
					{
						Fault = KingdomInheritFault.Malformed;
						return false;
					}
					Candidate candidate = new Candidate
					{
						Key = work.Key,
						X = work.X,
						Y = work.Y,
						Condition = work.Condition,
						State = work.State,
						ArchitectureSnapshot = work.ArchitectureSnapshot,
						ArchitectureHash = work.ArchitectureHash
					};
					if (work.State != KingdomInheritWorkState.Memory)
					{
						if (State == KingdomRules.InheritedState.Held)
						{
							candidate.Condition = Min(work.Condition, HeldConditionCeiling);
							candidate.State = KingdomInheritWorkState.Standing;
						}
						else if (State == KingdomRules.InheritedState.Faded)
						{
							bool derelict = fadedDerelict[i];
							candidate.Condition = Min(work.Condition, derelict
								? FadedDerelictConditionCeiling
								: FadedStandingConditionCeiling);
							candidate.State = derelict ? KingdomInheritWorkState.Derelict : KingdomInheritWorkState.Standing;
						}
						else if (KingdomRules.AllWorksSurvive(State))
						{
							candidate.Condition = Min(work.Condition, AbandonedDerelictConditionCeiling);
							candidate.State = KingdomInheritWorkState.Derelict;
						}
						else if (ruinsStanding[i])
						{
							candidate.Condition = Min(work.Condition, RuinsDerelictConditionCeiling);
							candidate.State = KingdomInheritWorkState.Derelict;
						}
						else
						{
							candidate.Key = RubbleKey;
							candidate.Condition = 0;
							candidate.State = KingdomInheritWorkState.Rubble;
							candidate.ArchitectureSnapshot = "";
							candidate.ArchitectureHash = "";
						}
					}
					transformed[i] = candidate;
				}
				Sort(transformed);
				return TryBuildPlan(transformed, transformed.Length, out Plan, out Fault);
			}
			catch
			{
				Plan = EmptyPlan();
				Fault = KingdomInheritFault.Malformed;
				return false;
			}
		}

		internal static bool TryFit(KingdomInheritPlan Plan, int Width, int Height,
			out KingdomInheritPlacement Placement, out KingdomInheritFault Fault)
		{
			Placement = null;
			Fault = KingdomInheritFault.None;
			try
			{
				if (Plan == null)
				{
					Fault = KingdomInheritFault.NullInput;
					return false;
				}
				if (Width != TargetWidth || Height != TargetHeight)
				{
					Fault = KingdomInheritFault.ImpossibleFootprint;
					return false;
				}
				int usableWidth = Width - WorkMargin * 2;
				int usableHeight = Height - WorkMargin * 2;
				if (Plan.Width > usableWidth || Plan.Height > usableHeight)
				{
					Fault = KingdomInheritFault.ImpossibleFootprint;
					return false;
				}
				int offsetX = WorkMargin + (usableWidth - Plan.Width) / 2;
				int offsetY = WorkMargin + (usableHeight - Plan.Height) / 2;
				KingdomInheritWork[] translated = new KingdomInheritWork[Plan.Count];
				Rect[] occupied = new Rect[Plan.Count];
				for (int i = 0; i < Plan.Count; i++)
				{
					KingdomInheritWork work = Plan.WorkAt(i);
					if (work == null || !TryRect(work.Key, work.X + offsetX, work.Y + offsetY, out occupied[i]))
					{
						Fault = KingdomInheritFault.ImpossibleFootprint;
						return false;
					}
					if (occupied[i].X1 < WorkMargin || occupied[i].Y1 < WorkMargin
						|| occupied[i].X2 >= Width - WorkMargin || occupied[i].Y2 >= Height - WorkMargin)
					{
						Fault = KingdomInheritFault.ImpossibleFootprint;
						return false;
					}
					for (int j = 0; j < i; j++)
					{
						if (Overlaps(occupied[i], occupied[j]))
						{
							Fault = KingdomInheritFault.Overlap;
							return false;
						}
					}
					translated[i] = new KingdomInheritWork(work.Key, work.X + offsetX, work.Y + offsetY,
						work.Condition, work.State);
				}
				int heartX;
				int heartY;
				ChooseHeart(translated, Plan.Width, Plan.Height, offsetX, offsetY, out heartX, out heartY);
				int cairnX;
				int cairnY;
				int entryX;
				int entryY;
				if (!TryEntry(occupied, heartX, heartY, Width, Height, out cairnX, out cairnY, out entryX, out entryY))
				{
					Fault = KingdomInheritFault.NoEntry;
					return false;
				}
				KingdomInheritWork[] result = new KingdomInheritWork[translated.Length + 1];
				for (int i = 0; i < translated.Length; i++)
				{
					result[i] = translated[i];
				}
				result[translated.Length] = new KingdomInheritWork(FounderCairnKey, cairnX, cairnY, 0,
					KingdomInheritWorkState.Memory);
				if (translated.Length == 0)
				{
					heartX = cairnX;
					heartY = cairnY;
				}
				Placement = new KingdomInheritPlacement(result, entryX, entryY, cairnX, cairnY,
					heartX, heartY, RemainingEngineChecks);
				return true;
			}
			catch
			{
				Placement = null;
				Fault = KingdomInheritFault.Malformed;
				return false;
			}
		}

	}
}
