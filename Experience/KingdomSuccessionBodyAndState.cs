using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomSuccessionRules
	{

		/// <summary>
		/// Runs one engine body transfer and then repairs every player-system registration from
		/// the body identity that actually won. <c>GamePlayer.SetBody</c> assigns its body before
		/// it raises <c>AfterPlayerBodyChangeEvent</c>. That dispatch may throw, or may stop on a
		/// handler returning false even though <c>SetBody</c> itself returns normally. Therefore
		/// neither the event nor one system's handler is the transaction boundary: the explicit
		/// isolated sweep is. The delegates keep both engine fault shapes directly testable here.
		/// </summary>
		internal static KingdomPlayerBodyTransfer TrySetBodyAndRebindPlayerSystems<TBody, TSystem>(
			TBody Original, TBody Target, Action<TBody> SetBody, Func<TBody> ReadCurrentBody,
			IList<TSystem> Systems, Action<TSystem, TBody> Unregister,
			Action<TSystem, TBody> Register)
			where TBody : class where TSystem : class
		{
			Exception failure = null;
			if (Original == null || Target == null || SetBody == null || ReadCurrentBody == null
				|| Systems == null || Unregister == null || Register == null)
			{
				return new KingdomPlayerBodyTransfer(false, false, false, false, 1,
					new ArgumentNullException("body transfer seam"));
			}

			bool returnedClean = false;
			try
			{
				SetBody(Target);
				returnedClean = true;
			}
			catch (Exception ex)
			{
				// The assignment may already have happened. Read and repair below before returning.
				failure = ex;
			}

			TBody current;
			try
			{
				current = ReadCurrentBody();
			}
			catch (Exception ex)
			{
				if (failure == null)
				{
					failure = ex;
				}
				return new KingdomPlayerBodyTransfer(returnedClean, false, false, false,
					1, failure);
			}

			bool targetControls = ReferenceEquals(current, Target);
			bool originalControls = ReferenceEquals(current, Original);
			if (current == null)
			{
				if (failure == null)
				{
					failure = new InvalidOperationException(
						"The body transfer ended without a controlled body.");
				}
				return new KingdomPlayerBodyTransfer(returnedClean, false, false, false,
					1, failure);
			}

			int registrationFailures = 0;
			for (int i = 0; i < Systems.Count; i++)
			{
				TSystem system = Systems[i];
				if (system == null)
				{
					registrationFailures++;
					if (failure == null)
					{
						failure = new InvalidOperationException(
							"The player-system list contains a null entry.");
					}
					continue;
				}
				// A torn forward event can leave some systems on either participant. Remove both
				// non-current candidates before the de-duplicating exact registration.
				if (!ReferenceEquals(current, Original)
					&& !TryPlayerRegistration(delegate { Unregister(system, Original); },
						ref failure))
				{
					registrationFailures++;
				}
				if (!ReferenceEquals(Target, Original) && !ReferenceEquals(current, Target)
					&& !TryPlayerRegistration(delegate { Unregister(system, Target); },
						ref failure))
				{
					registrationFailures++;
				}
				if (!TryPlayerRegistration(delegate { Register(system, current); }, ref failure))
				{
					registrationFailures++;
				}
			}
			return new KingdomPlayerBodyTransfer(returnedClean, originalControls,
				targetControls, registrationFailures == 0, registrationFailures, failure);
		}

		private static bool TryPlayerRegistration(Action Operation, ref Exception Failure)
		{
			try
			{
				Operation();
				return true;
			}
			catch (Exception ex)
			{
				if (Failure == null)
				{
					Failure = ex;
				}
				return false;
			}
		}

		internal static bool MayTerminalAfterAccessionFailure(bool CarriersExactlyOriginal,
			bool FounderControls)
		{
			return CarriersExactlyOriginal && FounderControls;
		}

		internal static bool MayQueueAccessionRepair(bool ExactHeirControls,
			bool PlayerRegistrationsExact)
		{
			return ExactHeirControls && PlayerRegistrationsExact;
		}

		internal static bool SuccessionEnabled(bool CurrentReadFailed, bool PersistedDisabled)
		{
			return !CurrentReadFailed && !PersistedDisabled;
		}

		internal static bool TryValidateSavedState(int SuccessionOrdinal, string PendingDeathToken,
			string CompletedDeathToken, InterregnumPhase Phase, long DueTick, NewsRoad Road,
			int Days, bool HasAccessionRepair, string PendingSealToken, out string Failure)
		{
			Failure = "";
			if (SuccessionOrdinal < 0 || SuccessionOrdinal == int.MaxValue
				|| !Enum.IsDefined(typeof(InterregnumPhase), Phase)
				|| !Enum.IsDefined(typeof(NewsRoad), Road) || DueTick < 0L
				|| Days < 0 || Days > RumourDays)
			{
				Failure = "the succession counters or enums are out of bounds";
				return false;
			}
			string pending = PendingDeathToken ?? "";
			string completed = CompletedDeathToken ?? "";
			string seal = PendingSealToken ?? "";
			int pendingOrdinal;
			long pendingTick;
			int completedOrdinal;
			long completedTick;
			if ((pending.Length > 0 && !TryReadDeathToken(pending, out pendingOrdinal, out pendingTick))
				|| (completed.Length > 0
					&& !TryReadDeathToken(completed, out completedOrdinal, out completedTick))
				|| (seal.Length > 0 && !TryReadDeathToken(seal, out completedOrdinal, out completedTick)))
			{
				Failure = "a founder-death token is malformed or out of bounds";
				return false;
			}
			if ((SuccessionOrdinal == 0) != (completed.Length == 0)
				|| (completed.Length > 0
					&& (!TryReadDeathToken(completed, out completedOrdinal, out completedTick)
						|| completedOrdinal != SuccessionOrdinal))
				|| (seal.Length > 0 && seal != completed))
			{
				Failure = "the completed succession identity does not match its ordinal";
				return false;
			}

			bool pendingPhase = Phase == InterregnumPhase.WordOnTheRoad
				|| Phase == InterregnumPhase.RiteDue;
			if (pendingPhase != (pending.Length > 0)
				|| HasAccessionRepair && Phase != InterregnumPhase.RiteDue)
			{
				Failure = "the pending death identity does not match its phase";
				return false;
			}
			if (pendingPhase)
			{
				if (!TryReadDeathToken(pending, out pendingOrdinal, out pendingTick)
					|| pendingOrdinal != SuccessionOrdinal + 1 || pending == completed
					|| NewsDueTick(pendingTick, Days) != DueTick || !RoadFitsDays(Road, Days))
				{
					Failure = "the pending death schedule is incoherent";
					return false;
				}
			}
			else if (DueTick != 0L || Days != 0 || HasAccessionRepair)
			{
				Failure = "an idle or reigning succession carries pending schedule state";
				return false;
			}
			if (Phase == InterregnumPhase.Reigning && SuccessionOrdinal == 0)
			{
				Failure = "a reigning state has no completed succession";
				return false;
			}
			return true;
		}

		internal static bool TryReadDeathToken(string Token, out int Ordinal, out long DeathTick)
		{
			Ordinal = 0;
			DeathTick = 0L;
			if (string.IsNullOrEmpty(Token) || Token.Length > MaxDeathTokenChars)
			{
				return false;
			}
			string[] pieces = Token.Split(':');
			if (pieces.Length != 4 || pieces[0] != "v1"
				|| !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out Ordinal)
				|| Ordinal < 1
				|| !long.TryParse(pieces[2], NumberStyles.None, CultureInfo.InvariantCulture, out DeathTick)
				|| DeathTick < 0L || pieces[3].Length == 0)
			{
				Ordinal = 0;
				DeathTick = 0L;
				return false;
			}
			try
			{
				byte[] identity = Convert.FromBase64String(pieces[3]);
				string decoded = new UTF8Encoding(false, true).GetString(identity);
				return decoded.Length > 0 && Convert.ToBase64String(identity) == pieces[3]
					&& FounderDeathToken(Ordinal, DeathTick, decoded) == Token;
			}
			catch
			{
				Ordinal = 0;
				DeathTick = 0L;
				return false;
			}
		}

		private static bool RoadFitsDays(NewsRoad Road, int Days)
		{
			switch (Road)
			{
			case NewsRoad.Seat:
			case NewsRoad.Arch:
				return Days == 0;
			case NewsRoad.Road:
				return Days > 0 && Days <= RumourDays;
			case NewsRoad.Rumour:
				return Days == RumourDays;
			default:
				return false;
			}
		}

	}
}
