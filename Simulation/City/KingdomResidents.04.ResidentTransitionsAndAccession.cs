using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		/// <summary>Marks one exact resident dead in the row before memorial/report projections are
		/// written. The body is never removed; only its live binding is retired.</summary>
		internal static bool TryMarkDead(KingdomSystem System, GameObject Body,
			KingdomStandingCause Cause, out KingdomResidentRow FormerRow)
		{
			FormerRow = default(KingdomResidentRow);
			KingdomCityBook book;
			int residentId;
			if (!TryLocate(System, Body, out book, out residentId))
			{
				KingdomCityBook enrolled;
				int enrolledId;
				if (!TryEnsureRow(System, Body, out enrolled, out enrolledId)
					|| !TryLocate(System, Body, out book, out residentId)) return false;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			int index;
			KingdomResidentRow after;
			KingdomCityState next;
			if (!book.TryRead(out state, out fault)
				|| !state.TryResidentIndex(residentId, out index)
				|| !state.TryResident(index, out FormerRow)) return false;
			// Second death callbacks are ordinary engine noise. False means no new memorial row
			// may be appended; the first callback already committed the terminal transition.
			if (FormerRow.Standing == KingdomResidentStanding.Dead) return false;
			if (!KingdomResidentRules.TryTransition(FormerRow, KingdomBodyWitness.Killed, Cause,
				out after, out fault) || !state.TryWithResident(index, after, out next, out fault))
				return false;
			if (!PublishRowAndUnbind(System, book, state, next, residentId,
				KingdomUnbindCause.Death)) return false;
			ProjectCompatibility(System);
			return true;
		}

		/// <summary>Removes one exact emigrant by ResidentId. Same-name neighbours cannot be struck
		/// accidentally.</summary>
		internal static bool TryDepart(KingdomSystem System, GameObject Body,
			KingdomResidentDestructionAuthorization Authorization,
			KingdomResidentDepartureOperation Operation,
			out KingdomResidentRow FormerRow)
		{
			FormerRow = default(KingdomResidentRow);
			KingdomCityBook book;
			int residentId;
			KingdomCityState state;
			KingdomCityState next;
			KingdomCityFault fault;
			if (!TryLocate(System, Body, out book, out residentId) || book == null) return false;
			// Generic departure destroys the body after this commit. Re-prove the exact row
			// inside the mutation owner; outer candidate selection is never authority.
			if (!KingdomResidentTransitionAuthority.CanContinueJournaledCarrierRemoval(
				System, Body, Operation, Authorization)) return false;
			if (!book.TryRead(out state, out fault)
				|| !KingdomResidentRules.TryRemove(state, residentId, out next, out FormerRow,
					out fault)) return false;
			if (!PublishRowAndUnbind(System, book, state, next, residentId,
				KingdomUnbindCause.Abroad)) return false;
			ProjectCompatibility(System);
			return true;
		}

		private static bool PublishRowAndUnbind(KingdomSystem System, KingdomCityBook Book,
			KingdomCityState Original, KingdomCityState Advanced, int ResidentId,
			KingdomUnbindCause Cause)
		{
			KingdomBindingTable bindings;
			KingdomCityFault fault;
			if (!TryTable(System, out bindings)) return false;
			KingdomBinding held;
			if (!bindings.TryGet(ResidentId, KingdomBindingKind.Resident, out held))
				return SafePublish(Book, Advanced, "resident row transition");
			KingdomBindingTable nextBindings;
			KingdomBinding evicted;
			if (!bindings.TryUnbind(ResidentId, KingdomBindingKind.Resident, Cause,
				out nextBindings, out evicted, out fault)) return false;
			return PublishAccessionCarriers(Book, System.Bindings, Original, Advanced,
				bindings, nextBindings) == KingdomAccessionOutcome.Committed;
		}

		/// <summary>
		/// Takes one exact, bound resident out of the city model when that real body takes the
		/// charter. The returned row is the accession snapshot used for tenure and creed regard.
		/// <para>
		/// This is deliberately narrower than departure. The person has not died or emigrated:
		/// their body still stands, now as the player. What closes is the model's licence to render
		/// or mint that resident identity. Both replacement snapshots are built before either
		/// carrier is published; a failed second publish rolls the first back.
		/// </para>
		/// </summary>
		internal static KingdomAccessionOutcome TryAccede(KingdomSystem System, GameObject Body,
			out KingdomResidentRow formerRow, out string SettlementId)
		{
			formerRow = default(KingdomResidentRow);
			SettlementId = null;
			if (System == null || System.Bindings == null || !GameObject.Validate(Body) || !Body.IsAlive)
			{
				return KingdomAccessionOutcome.RefusedClean;
			}
			KingdomCityBook book;
			int residentId;
			if (!TryLocate(System, Body, out book, out residentId) || book == null || residentId == 0)
			{
				return KingdomAccessionOutcome.RefusedClean;
			}

			KingdomCityState current;
			KingdomCityFault fault;
			int rowIndex;
			if (!book.TryRead(out current, out fault)
				|| !current.TryResidentIndex(residentId, out rowIndex)
				|| !current.TryResident(rowIndex, out formerRow)
				|| formerRow.Standing != KingdomResidentStanding.Resident)
			{
				Refuse("accession row", fault);
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}

			KingdomBindingTable bindings;
			KingdomBinding held;
			string bodyZone = Body.CurrentZone?.ZoneID;
			if (!TryTable(System, out bindings)
				|| !bindings.TryGet(residentId, KingdomBindingKind.Resident, out held)
				|| string.IsNullOrEmpty(bodyZone)
				|| !string.Equals(held.ObjectId, Body.ID, StringComparison.Ordinal)
				|| !string.Equals(held.ZoneId, bodyZone, StringComparison.Ordinal)
				|| (!string.IsNullOrEmpty(formerRow.BoundZoneId)
					&& !string.Equals(formerRow.BoundZoneId, bodyZone, StringComparison.Ordinal)))
			{
				KingdomLog.Log("binding: accession refused; the chosen body is not the exact live resident binding");
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}

			// Accession is keyed on the row and binding only. Compatibility projections are rebuilt
			// after both durable carriers commit; they never veto or identify the heir.
			bool seated = ReferenceEquals(book, System.City);
			KingdomSettlement other = seated ? null : System.FindNonSeatSettlementByBook(book);
			SettlementId = seated ? System.City?.SettlementId : other?.City?.SettlementId;
			Dictionary<string, int> creedCounts = seated ? System.CreedCounts : other?.CreedCounts;
			Dictionary<string, int> creedPastCounts = seated ? System.CreedPastCounts : other?.CreedPastCounts;
			if (!KingdomIdentityRules.IsSettlementId(SettlementId) ||
				(!seated && other == null) || creedCounts == null || creedPastCounts == null)
			{
				KingdomLog.Log("binding: accession refused; the chosen resident's settlement tallies are unreadable");
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}
			if (KingdomGrowth.SuccessorMarketBlocked(Body,
				KingdomSurvey.ActiveFor(Body.CurrentZone))
				|| !KingdomResidentTransitionAuthority.CanAccede(System, Body, residentId))
			{
				KingdomLog.Log("binding: accession waits for an exact resident-scoped authority");
				return KingdomAccessionOutcome.RepairRequired;
			}
			Dictionary<string, int> nextCreedCounts = new Dictionary<string, int>(creedCounts);
			Dictionary<string, int> nextCreedPastCounts = new Dictionary<string, int>(creedPastCounts);
			string rollName = Body.GetStringProperty("KingdomName");
			string origin = Body.GetStringProperty("KingdomOrigin") ?? "";
			if (string.IsNullOrEmpty(rollName)
				|| !string.Equals(rollName, formerRow.Name, StringComparison.Ordinal)
				|| !string.Equals(origin, formerRow.Origin ?? "", StringComparison.Ordinal))
			{
				KingdomLog.Log("binding: accession refused; city row and living body disagree about the heir");
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}
			string citizenshipFailure;
			if (!KingdomCitizenship.CanRemove(System, Body, out citizenshipFailure))
			{
				KingdomLog.Log("binding: accession refused; citizenship cannot be removed exactly ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}
			DropCount(nextCreedCounts, Body.GetStringProperty(KingdomCreed.CreedProperty));
			List<string> pastCreeds = KingdomCreedRules.DecodeKept(
				Body.GetStringProperty(KingdomCreed.CreedPastProperty));
			for (int i = 0; i < pastCreeds.Count; i++)
			{
				DropCount(nextCreedPastCounts, pastCreeds[i]);
			}

			KingdomCityState nextCity;
			KingdomBindingTable nextBindings;
			KingdomBinding evicted;
			KingdomResidentRow removed;
			if (!KingdomResidentRules.TryRemove(current, residentId, out nextCity, out removed,
					out fault)
				|| !bindings.TryUnbind(residentId, KingdomBindingKind.Resident,
					KingdomUnbindCause.Accession, out nextBindings, out evicted, out fault))
			{
				Refuse("accession prepare", fault);
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}

			KingdomAccessionOutcome outcome = PublishAccessionCarriers(book, System.Bindings,
				current, nextCity, bindings, nextBindings);
			if (outcome != KingdomAccessionOutcome.Committed)
			{
				if (outcome == KingdomAccessionOutcome.RefusedClean)
				{
					formerRow = default(KingdomResidentRow);
				}
				return outcome;
			}
			if (!KingdomOfficeRuntime.TryObserveAccessionLoss(System, Body,
				out string officeFailure))
			{
				KingdomLog.Log("binding: accession office cleanup requires repair ("
					+ (officeFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			if (!KingdomPolityResidentTransition.TryConclude(System, Body, residentId,
				KingdomPolityResidentTransitionCause.Accession,
				out KingdomPolityResidentTransitionPreparation _, out string polityFailure))
			{
				KingdomLog.Log("binding: accession deed-figure conclusion requires repair ("
					+ (polityFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			if (seated)
			{
				System.CreedCounts = nextCreedCounts;
				System.CreedPastCounts = nextCreedPastCounts;
			}
			else
			{
				other.CreedCounts = nextCreedCounts;
				other.CreedPastCounts = nextCreedPastCounts;
			}
			ProjectCompatibility(System);
			if (!KingdomCitizenship.TryRemove(System, Body,
				KingdomCitizenshipRemovalReason.Accession, out citizenshipFailure))
			{
				KingdomLog.Log("binding: accession citizenship completion requires repair ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			if (!KingdomCitizenRite.TryRetireAccedingHost(System, Body,
				out string riteFailure))
			{
				KingdomLog.Log("binding: accession citizen-host cleanup requires repair ("
					+ (riteFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			FinishAccessionBody(Body, formerRow, residentId);
			return KingdomAccessionOutcome.Committed;
		}

	}
}
