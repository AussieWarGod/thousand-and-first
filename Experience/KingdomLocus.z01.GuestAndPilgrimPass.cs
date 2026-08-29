using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomLocus
	{
		/// <summary>
		/// Brings travellers through on the cadence the pure rules define, whether or not anybody
		/// was here to see them, and resolves what became of them at the moment the founder is
		/// back to be told.
		/// <para>
		/// Addendum 8 clause 1: the road does not wait for the founder. A season away is a season
		/// of people arriving, waiting out their patience at a gate nobody answered, and going on
		/// &mdash; and clause 3 says what awareness gets is the dated news of it. So the backlog
		/// is resolved rather than collapsed: everyone whose patience ran out during the absence
		/// leaves one honest dated trace between them, and the only person still standing there
		/// is the one who arrived recently enough to still be waiting. That is at most one, and
		/// it is one because <c>GuestPatienceTicks</c> is shorter than
		/// <c>GuestIntervalTicks</c> rather than because a live object happened to be blocking
		/// the spawn.
		/// </para>
		/// </summary>
		private static void RunGuestPass(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long TimeTicks)
		{
			GameObject guest = FindGuest(Survey);
			if (guest != null)
			{
				bool offered = guest.GetIntProperty("KingdomGuestOffered") == 1;
				if (!offered && KingdomLocusRules.GuestShouldDepartUnattended(TimeTicks, System.GuestDepartTick))
				{
					DepartGuest(System, guest, Greeted: false);
				}
				return;
			}
			long effectiveDue = KingdomGuestLifecycle.EffectiveDue(System,
				KingdomLifecycleLane.PlainGuest, KingdomLocusRules.GuestIntervalTicks);
			if (effectiveDue <= 0L || TimeTicks < effectiveDue) return;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(
				effectiveDue, TimeTicks, KingdomLocusRules.GuestIntervalTicks,
				KingdomLocusRules.GuestPatienceTicks);
			Cell standingCell = passages.StandingSince > 0L ? HeartArrivalCell(Z) : null;
			long scheduleBefore = System.NextGuestTick > 0L ? System.NextGuestTick : 0L;
			long scheduleAfter = passages.StandingSince > 0L && standingCell == null
				? passages.StandingSince : passages.NextDueTick;
			int daysAgo = passages.Departed > 0
				? KingdomRules.ElapsedDays(TimeTicks - passages.LastDepartedTick) : 0;
			string chronicle = passages.Departed > 0
				? KingdomLocusRules.PassagesChronicleLine(passages.Departed,
					KingdomPresentation.Rich(System.KingdomDisplayName), daysAgo) : null;
			string ledger = passages.Departed > 0
				? KingdomLocusRules.PassagesLedgerNote(passages.Departed, daysAgo) : null;
			if (!KingdomGuestLifecycle.PublishPassages(System, Z,
				KingdomLifecycleLane.PlainGuest, TimeTicks, scheduleBefore, scheduleAfter,
				passages.Departed, passages.LastDepartedTick, passages.StandingSince,
				chronicle, ledger, null)) return;
			if (passages.StandingSince <= 0L)
			{
				return;
			}
			// Spawned at the tick they actually walked up, not at the tick the founder walked in,
			// so their patience is already partly spent and they leave when they were always
			// going to leave.
			if (standingCell != null) SpawnGuest(System, Z, standingCell, passages.StandingSince);
		}

		/// <summary>
		/// Renders one exact history-caused opportunity at the rite ground. True means the causal
		/// lane owns the gate this pass, including while travel, blockage, or receipt recovery waits;
		/// generic traffic must not step over it.
		/// </summary>
		private static bool RunPilgrimPass(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long TimeTicks)
		{
			Simulation.City.KingdomCityBook book = System.City;
			if (book == null) return false;
			book.Normalize();
			KingdomLocusRules.PilgrimState state =
				(KingdomLocusRules.PilgrimState)book.PilgrimState;
			if (state == KingdomLocusRules.PilgrimState.None) return false;

			if (!KingdomLocusRules.TryPilgrimWindow(book.PilgrimCauseTick,
				out long arrivalTick, out long departTick))
			{
				// Malformed causal evidence is evidence, not permission to erase the story and
				// let an unrelated generic roll take its place. Fail the shared authority closed;
				// a later migration can inspect the untouched CityBook fields.
				KingdomGuestLifecycle.QuarantineLegacyEvidence(System,
					"malformed causal-pilgrim window retained for migration");
				return true;
			}

			GameObject exact = FindCausalPilgrim(Survey, book);
			if (state == KingdomLocusRules.PilgrimState.Waiting && GameObject.Validate(exact))
			{
				// Reconcile the one marker before considering a new body. This is the placement
				// cut-point: a body added successfully but followed by an interrupted carrier write
				// is adopted, never followed by a replacement.
				book.PilgrimState = (int)KingdomLocusRules.PilgrimState.Standing;
				book.PilgrimObjectId = exact.ID;
				// This is the one pre-lifecycle body adoption case. The causal tick remains the
				// evidence; do not manufacture a parallel System clock for it.
				state = KingdomLocusRules.PilgrimState.Standing;
			}
			if (state == KingdomLocusRules.PilgrimState.Standing)
			{
				if (GameObject.Validate(exact))
				{
					book.PilgrimObjectId = exact.ID;
					if (TimeTicks < departTick) return true;
					ResolvePilgrim(System, book, exact, Greeted: false, departTick);
					return true;
				}
				// Never mint a replacement for an already-published body. Once its patience has
				// elapsed, the exact event receipt may settle the missing body's departure.
				if (TimeTicks >= departTick)
				{
					ResolvePilgrim(System, book, null, Greeted: false, departTick);
				}
				return true;
			}

			if (TimeTicks < arrivalTick) return true;
			if (TimeTicks >= departTick)
			{
				// The whole visit happened while its ground was away. It still has a date and cause;
				// it never manufactures a body merely because the founder came home late.
				ResolvePilgrim(System, book, null, Greeted: false, departTick);
				return true;
			}
			// A plain traveller who was already waiting when the third story was told keeps their
			// own patience. Resolve that exact body before the causal lane takes the gate; merely
			// suppressing RunGuestPass here would strand it forever and prevent the pilgrim too.
			GameObject traffic = FindGuest(Survey);
			if (GameObject.Validate(traffic))
			{
				bool offered = traffic.GetIntProperty("KingdomGuestOffered") == 1;
				if (!offered && KingdomLocusRules.GuestShouldDepartUnattended(
					TimeTicks, System.GuestDepartTick))
				{
					if (!DepartGuest(System, traffic, Greeted: false)) return true;
				}
				else return true;
			}
			Cell cell = HeartArrivalCell(Z);
			if (cell == null) return true; // blockage defers without spending the opportunity.
			if (string.IsNullOrEmpty(book.PilgrimName))
			{
				string planned;
				string namingFailure;
				if (!KingdomSemanticSelection.TryNameOnly(System,
					KingdomSemanticSelection.CausalPilgrimStream,
					KingdomSemanticSelection.PersonEventKind, book.PilgrimSequence,
					out planned, out namingFailure))
				{
					KingdomLog.Log("causal pilgrim waits: " + namingFailure);
					return true;
				}
				book.PilgrimName = planned;
			}
			string name = book.PilgrimName;
			KingdomGuestLifecycle.PublishSpawn(System, Z,
				KingdomLifecycleLane.PlainGuest, cell, TimeTicks, departTick,
				"r_KingdomGuestPilgrim", name, "the road that heard " + book.PilgrimCause,
				book.PilgrimSequence, 0, book.PilgrimCause, "causal-pilgrim",
				book.PilgrimPlaceName, null, null, null, null);
			return true;
		}

	}
}
