using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled shell for the five co-opted ceremonies
	/// (<see cref="KingdomCeremonyRules"/> owns the arithmetic and every hand-written line):
	/// the surveyor's plan staked ahead of a building and quoted when it rises, the raising
	/// ceremony that closes construction attended or not, the tastes and traits a settling
	/// notable carries, and the pattern-book a chartered caravan occasionally opens. Every entry
	/// point here is a single call for another system to make; none of them own a clock or a
	/// pass of their own.
	/// </summary>
	public static class KingdomCeremony
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionCeremony") != "No";

		/// <summary>String property carrying the surveyor's plan text from a staked marker
		/// through its scaffold to the moment the chronicle quotes it. Never present on a design
		/// raised without ever being staked (a direct commission) &mdash; absence is a normal
		/// state, not a fault.</summary>
		public const string SurveyorsPlanProperty = "KingdomSurveyorsPlan";

		// ==================================================================================
		// The surveyor's plan
		// ==================================================================================

		/// <summary>
		/// Writes the surveyor's plan onto a freshly staked marker: a lookable description
		/// framed as intention, and the same text stashed on a string property so it survives
		/// the marker's own destruction when the plan is realised. Call once, right after
		/// <c>r_KingdomPlanMarker.ApplyDesign</c>.
		/// </summary>
		/// <param name="Marker">The freshly created marker object.</param>
		/// <param name="Entry">The design staked.</param>
		/// <param name="SkinFlavor">The chosen skin's key, or null. Purely decorative &mdash;
		/// absence falls back to "plain stock" inside the template, never to missing text.</param>
		public static void StakePlan(GameObject Marker, KingdomRules.BuildEntry Entry, string SkinFlavor)
		{
			if (!Enabled || Marker == null || Entry == null)
			{
				return;
			}
			KingdomSystem.Guard("ceremony: stake plan", delegate
			{
				string text = KingdomCeremonyRules.SurveyorsPlanText(Entry.Category, Entry.Name, Entry.MinStage, SkinFlavor);
				Marker.SetStringProperty(SurveyorsPlanProperty, text);
				Marker.RequirePart<Description>().Short = text;
			});
		}

		/// <summary>
		/// Carries the staked plan's text from a marker onto the scaffold it becomes, the same
		/// way <c>KingdomDesign.StageSkin</c> carries the chosen skin. Call once, in
		/// <c>KingdomPlanMarker.Realize</c>, before the marker is destroyed.
		/// </summary>
		public static void TransferPlanQuote(GameObject Marker, GameObject Scaffold)
		{
			CarryPlanQuote(ReadPlanQuote(Marker), Scaffold);
		}

		/// <summary>
		/// The staked plan's text, off a marker or off the works that became one. Empty for
		/// anything raised without ever being staked, which is a normal state and not a fault.
		/// <para>
		/// Exists beside <see cref="TransferPlanQuote"/> for the plot path, which measures its rect
		/// out of the marker's own cell and so must take the marker down BEFORE the works that
		/// will carry the quote exists. Read first, carry after.
		/// </para>
		/// </summary>
		public static string ReadPlanQuote(GameObject From)
		{
			if (!Enabled || From == null)
			{
				return null;
			}
			return From.GetStringProperty(SurveyorsPlanProperty);
		}

		/// <summary>Writes a plan's text onto whatever will carry it to the raising. A blank text
		/// writes nothing, so a design nobody staked is left with no property rather than an empty
		/// one.</summary>
		public static void CarryPlanQuote(string Text, GameObject Onto)
		{
			if (!Enabled || Onto == null || string.IsNullOrEmpty(Text))
			{
				return;
			}
			KingdomSystem.Guard("ceremony: carry plan quote", delegate
			{
				Onto.SetStringProperty(SurveyorsPlanProperty, Text);
			});
		}

		// ==================================================================================
		// The raising ceremony
		// ==================================================================================

		/// <summary>
		/// Closes construction: while attended, freezes exact already-bound builders, walks them by
		/// vanilla pathing to the functional first basin, and lets the durable construction outbox
		/// name only those whose Ready receipt proves arrival. While unattended, it touches no body
		/// or fixture and leaves a plainer dated chronicle line and homecoming note instead. Replaces the deed and
		/// chronicle a completion used to write directly, and is called from <b>both</b> paths
		/// that raise a building &mdash; <c>r_KingdomScaffold.Complete</c> for a single-cell
		/// design and <c>KingdomPlots.Finish</c> for a plot one &mdash; because a house is not a
		/// lesser thing to raise than a palisade.
		/// </summary>
		/// <param name="System">The realm. Null or unfounded is a no-op &mdash; nothing here can
		/// fire before a settlement exists to own it.</param>
		/// <param name="Cell">The cell the finished building now stands on, read for its zone.
		/// May be null; the ceremony still records the deed with no crew found.</param>
		/// <param name="DisplayName">The finished building's name.</param>
		/// <param name="CompleteTick">The scaffold's own due tick, read before its destruction.</param>
		/// <param name="PlanQuote">The surveyor's plan text carried onto the scaffold, or null
		/// when this design was never staked as a plan.</param>
		public static void OnBuildingRaised(KingdomSystem System, Cell Cell, string DisplayName, long CompleteTick, string PlanQuote)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(DisplayName))
			{
				return;
			}
			KingdomSystem.Guard("ceremony: raising", delegate
			{
				System.RecordDeed("the " + DisplayName + " raised at " + KingdomPresentation.Rich(System.KingdomDisplayName));
				if (!Enabled)
				{
					KingdomChronicle.Record(System, "the " + DisplayName + " was raised at " + KingdomPresentation.Rich(System.KingdomDisplayName));
					MessageQueue.AddPlayerMessage("{{G|The " + DisplayName + " is complete.}}");
					return;
				}
				Zone zone = (Cell != null) ? Cell.ParentZone : null;
				bool window = KingdomCeremonyRules.IsAttended(CompleteTick, CurrentTicks());
				Zone physicalZone = window ? zone : null;
				string unattended = KingdomCeremonyRules.RaisingUnattendedChronicle(DisplayName,
					KingdomPresentation.Rich(System.SeatName), PlanQuote);
				long safe = CompleteTick > 0L ? CompleteTick : CurrentTicks();
				string dated = "a dated report for the " + Calendar.GetDay(safe) + " of "
					+ Calendar.GetMonth(safe) + ", " + Calendar.GetYear(safe)
					+ " AR said that " + unattended;
				// Legacy completions lack a construction outbox, but not physical truth. The
				// generic lifecycle freezes exact attendees and owns one RecordOnce chronicle.
				KingdomPhysicalQueueResult result = KingdomPhysicalHappenings.QueueGeneric(System,
					System.City, KingdomPhysicalHappeningKind.Raising, safe,
					KingdomCityRules.StableId(DisplayName + ":" + safe), 0, 0, physicalZone, null,
					KingdomCeremonyRules.RaisingAttendedChronicle(DisplayName, KingdomPresentation.Rich(System.SeatName),
						new List<string>(), PlanQuote), dated, "",
					KingdomCeremonyRules.RaisingLedgerNote(DisplayName),
					"{{G|The " + DisplayName + " is complete.}}", "", "", DisplayName,
					CurrentTicks());
				KingdomLog.Log("ceremony: legacy raised " + DisplayName + " physical="
					+ (result == KingdomPhysicalQueueResult.AttendedReady));
			});
		}

		/// <summary>
		/// Publishes frozen raising content before any sink callback, then dispatches each sink from
		/// its own durable disposition. Chronicle and ledger are inspectable/idempotent; deed and
		/// message are at-most-once and become Lost if reload observes an interrupted attempt.
		/// </summary>
		public static bool EnsureBuildingRaised(KingdomSystem System, Cell Cell,
			string DisplayName, long CompleteTick, string PlanQuote,
			ref KingdomConstructionJob Job)
		{
			if (System == null || !System.Founded || Job == null
				|| Job.Phase != KingdomConstructionPhase.Complete
				|| string.IsNullOrEmpty(DisplayName)) return false;
			string eventId = "construction:" + Job.Id + ":raised";
			if (Job.Outbox != null && Job.Outbox.EventId != eventId
				&& KingdomConstructionRules.OutboxSettled(Job.Outbox))
			{
				// A conversion first settles its strike telling, then later its raising telling.
				// Only a fully-settled prior event may yield the bounded active outbox slot.
				if (!KingdomConstruction.UpdateOutbox(ref Job, null)) return false;
			}
			if (Job.Outbox == null)
			{
				bool enabled = Enabled;
				bool attendedWindow = enabled && KingdomCeremonyRules.IsAttended(CompleteTick,
					CurrentTicks());
				bool attended = false;
				List<string> present = new List<string>();
				if (attendedWindow)
				{
					KingdomPhysicalQueueResult physical = KingdomPhysicalHappenings.QueueRaising(
						System, System.City, Job.Id, CompleteTick,
						Cell == null ? null : Cell.ParentZone, DisplayName, PlanQuote,
						CurrentTicks(), out string ignoredEventId, out string[] physicalNames);
					if (physical == KingdomPhysicalQueueResult.Pending) return false;
					if (physical == KingdomPhysicalQueueResult.Refused) return false;
					attended = physical == KingdomPhysicalQueueResult.AttendedReady;
					if (attended) present.AddRange(physicalNames);
				}
				string chronicle;
				string ledger = null;
				string message;
				int mode;
				if (!enabled)
				{
					mode = 1;
					chronicle = "the " + DisplayName + " was raised at "
						+ KingdomPresentation.Rich(System.KingdomDisplayName);
					message = "{{G|The " + DisplayName + " is complete.}}";
				}
				else if (attended)
				{
					// Mode 4 appends physical-attendance authority to legacy modes 1-3. It prevents
					// generic outbox recovery from publishing an attended raising without exact
					// Ready proof in the city sidecar.
					mode = 4;
					chronicle = KingdomCeremonyRules.RaisingAttendedChronicle(DisplayName,
						KingdomPresentation.Rich(System.SeatName), present, PlanQuote);
					message = KingdomCeremonyRules.RaisingAttendedMessage(DisplayName, present);
				}
				else
				{
					mode = 3;
					chronicle = KingdomCeremonyRules.RaisingUnattendedChronicle(DisplayName,
						KingdomPresentation.Rich(System.SeatName), PlanQuote);
					ledger = KingdomCeremonyRules.RaisingLedgerNote(DisplayName);
					message = "{{G|The " + DisplayName + " is complete.}}";
				}
				KingdomConstructionOutbox box = new KingdomConstructionOutbox
				{
					EventId = eventId,
					Mode = mode,
					Chronicle = chronicle,
					ChronicleState = KingdomConstructionSinkDisposition.Pending,
					Ledger = ledger,
					LedgerState = ledger == null
						? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending,
					Message = message,
					MessageState = KingdomConstructionSinkDisposition.Pending,
					Deed = "the " + DisplayName + " raised at " + KingdomPresentation.Rich(System.KingdomDisplayName),
					DeedState = KingdomConstructionSinkDisposition.Pending
				};
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			else if (Job.Outbox.EventId != eventId)
			{
				KingdomConstruction.Quarantine(ref Job,
					"The construction telling carries another event identity.");
				return false;
			}
			if (Job.Outbox.Mode == 4)
			{
				if (!Dispatch(System, ref Job)) return false;
				if (KingdomPhysicalHappenings.TryReadyRaising(System.City, Job.Id,
					CurrentTicks(), out string physicalEventId, out string[] ignoredNames))
					return KingdomPhysicalHappenings.AcknowledgeRaising(System, System.City,
						physicalEventId, CurrentTicks());
				return KingdomPhysicalHappenings.ReconcileSettledRaising(System, System.City,
					Job.Id, CurrentTicks());
			}
			return Dispatch(System, ref Job);
		}

		/// <summary>Resumes a published terminal outbox without recomputing option or content.</summary>
		public static bool DispatchPending(KingdomSystem System, ref KingdomConstructionJob Job)
		{
			if (System == null || Job == null || Job.Outbox == null) return false;
			if (Job.Outbox.Mode != 4) return Dispatch(System, ref Job);
			if (!Dispatch(System, ref Job)) return false;
			if (KingdomPhysicalHappenings.TryReadyRaising(System.City, Job.Id,
				CurrentTicks(), out string eventId, out string[] ignoredNames))
				return KingdomPhysicalHappenings.AcknowledgeRaising(System, System.City, eventId,
					CurrentTicks());
			return KingdomPhysicalHappenings.ReconcileSettledRaising(System, System.City,
				Job.Id, CurrentTicks());
		}

		public static bool EnsureRoadPaved(KingdomSystem System, int Cells,
			KingdomMaterial Material, ref KingdomConstructionJob Job)
		{
			if (System == null || Cells <= 0) return false;
			return EnsureRouteOutbox(System, "paved",
				KingdomRoadRules.PavedRecord(Cells, Material, KingdomPresentation.Rich(System.KingdomDisplayName)), null,
				KingdomRoadRules.PavedLine(Cells, Material, KingdomPresentation.Rich(System.SeatName)),
				"the paving of the ways at " + KingdomPresentation.Rich(System.SeatName), ref Job);
		}

		public static bool EnsureRoadPavedFromReceipt(KingdomSystem System,
			ref KingdomConstructionJob Job)
		{
			if (Job == null || Job.Route != KingdomConstructionRoute.RoadPaving) return false;
			List<KingdomConstructionCell> cells;
			KingdomMaterialDebitCost cost;
			if (!KingdomConstructionRules.TryDecodeCells(Job.Payload, out cells)
				|| Job.Claims == null || !KingdomMaterialDebitCost.TryParseClaim(
					Job.Claims.MaterialRequested, out cost)) return false;
			int found = 0;
			KingdomMaterial material = (KingdomMaterial)(-1);
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial candidate = (KingdomMaterial)i;
				if (cost.Materials.Get(candidate) <= 0) continue;
				found++;
				material = candidate;
			}
			if (found != 1 || !cost.Bits.IsEmpty() || !cost.Exotics.IsEmpty()) return false;
			return EnsureRoadPaved(System, cells.Count, material, ref Job);
		}

		public static bool EnsureTerminalClosed(KingdomSystem System,
			ref KingdomConstructionJob Job)
		{
			if (System == null || Job == null || !KingdomConstructionRules.IsTerminal(Job.Phase)
				|| Job.Phase == KingdomConstructionPhase.Complete) return false;
			if (Job.Outbox != null) return KingdomConstructionRules.OutboxSettled(Job.Outbox);
			KingdomConstructionOutbox box = new KingdomConstructionOutbox
			{
				EventId = "construction:" + Job.Id + ":closed", Mode = 1,
				ChronicleState = KingdomConstructionSinkDisposition.Skipped,
				LedgerState = KingdomConstructionSinkDisposition.Skipped,
				MessageState = KingdomConstructionSinkDisposition.Skipped,
				DeedState = KingdomConstructionSinkDisposition.Skipped
			};
			return KingdomConstruction.UpdateOutbox(ref Job, box);
		}

		/// <summary>Wear-owned caller freezes optional leak closure before removing its wear part.</summary>
		public static bool EnsureWearRepaired(KingdomSystem System, string WorkName,
			string LeakStoppedLine, ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(WorkName)) return false;
			string line = KingdomWearRules.RepairCompleteLine(WorkName);
			string held = string.IsNullOrEmpty(LeakStoppedLine) ? null
				: "{{G|" + XRL.Language.Grammar.InitCap(LeakStoppedLine) + "}}";
			string message = "{{G|" + line + "}}";
			if (held != null) message += "\n" + held;
			return EnsureRouteOutbox(System, "mended", line, held, message, null, ref Job);
		}

		/// <summary>Freeze Wear telling before its part-removal callback; do not dispatch yet.</summary>
		public static bool PrepareWearRepaired(KingdomSystem System, string WorkName,
			string LeakStoppedLine, ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(WorkName)) return false;
			string line = KingdomWearRules.RepairCompleteLine(WorkName);
			string held = string.IsNullOrEmpty(LeakStoppedLine) ? null
				: "{{G|" + XRL.Language.Grammar.InitCap(LeakStoppedLine) + "}}";
			string message = "{{G|" + line + "}}" + (held == null ? "" : "\n" + held);
			return PublishRouteOutbox(System, "mended", line, held, message, null, ref Job);
		}

		public static bool EnsureSocketRedressed(KingdomSystem System, string DisplayName,
			string SkinKey, ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(DisplayName)
				|| string.IsNullOrEmpty(SkinKey)) return false;
			return EnsureRouteOutbox(System, "redressed",
				"the " + DisplayName + " at " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ " was given a new coat, dressed as " + SkinKey,
				null, "{{G|The " + DisplayName + " is re-dressed.}}", null, ref Job);
		}

		public static bool PrepareSocketRedressed(KingdomSystem System, string DisplayName,
			string SkinKey, ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(DisplayName)
				|| string.IsNullOrEmpty(SkinKey)) return false;
			return PublishRouteOutbox(System, "redressed",
				"the " + DisplayName + " at " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ " was given a new coat, dressed as " + SkinKey,
				null, "{{G|The " + DisplayName + " is re-dressed.}}", null, ref Job);
		}

		public static bool EnsureSocketStaked(KingdomSystem System, string DisplayName,
			ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(DisplayName)) return false;
			return PublishRouteOutbox(System, "socket-staked",
				"the cleared ground at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " was staked again for "
					+ XRL.Language.Grammar.A(DisplayName), null,
				"{{G|The cleared plot is staked for " + XRL.Language.Grammar.A(DisplayName) + ".}}",
				null, ref Job) && Dispatch(System, ref Job);
		}

		private static bool EnsureRouteOutbox(KingdomSystem System, string Suffix,
			string Chronicle, string Ledger, string Message, string Deed,
			ref KingdomConstructionJob Job)
		{
			if (System == null || !System.Founded || Job == null
				|| Job.Phase != KingdomConstructionPhase.Complete
				|| string.IsNullOrEmpty(Suffix) || string.IsNullOrEmpty(Chronicle)) return false;
			return PublishRouteOutbox(System, Suffix, Chronicle, Ledger, Message, Deed,
				ref Job) && Dispatch(System, ref Job);
		}

		private static bool PublishRouteOutbox(KingdomSystem System, string Suffix,
			string Chronicle, string Ledger, string Message, string Deed,
			ref KingdomConstructionJob Job)
		{
			if (System == null || !System.Founded || Job == null
				|| string.IsNullOrEmpty(Suffix) || string.IsNullOrEmpty(Chronicle)) return false;
			string eventId = "construction:" + Job.Id + ":" + Suffix;
			if (Job.Outbox != null && Job.Outbox.EventId != eventId)
			{
				if (!KingdomConstructionRules.OutboxSettled(Job.Outbox)
					|| !KingdomConstruction.UpdateOutbox(ref Job, null)) return false;
			}
			if (Job.Outbox == null)
			{
				KingdomConstructionOutbox box = new KingdomConstructionOutbox
				{
					EventId = eventId, Mode = 1,
					Chronicle = Chronicle,
					ChronicleState = KingdomConstructionSinkDisposition.Pending,
					Ledger = Ledger,
					LedgerState = Ledger == null ? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending,
					Message = Message,
					MessageState = Message == null ? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending,
					Deed = Deed,
					DeedState = Deed == null ? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending
				};
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			return Job.Outbox.EventId == eventId;
		}

		private static bool Dispatch(KingdomSystem System, ref KingdomConstructionJob Job)
		{
			if (System == null || Job == null || Job.Outbox == null) return false;
			KingdomConstructionOutbox box = Job.Outbox.Copy();

			// Uninspectable sinks: an interrupted Attempting state is explicit loss, never retry.
			if (box.DeedState == KingdomConstructionSinkDisposition.Attempting)
			{
				box.DeedState = KingdomConstructionSinkDisposition.Lost;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			if (box.DeedState == KingdomConstructionSinkDisposition.Pending)
			{
				box.DeedState = KingdomConstructionSinkDisposition.Attempting;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				try
				{
					System.RecordDeed(box.Deed);
					box.DeedState = KingdomConstructionSinkDisposition.Delivered;
					if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				}
				catch { return false; }
			}

			// RecordOnce owns exact inspection and may be called again after an interrupted attempt.
			if (box.ChronicleState == KingdomConstructionSinkDisposition.Pending
				|| box.ChronicleState == KingdomConstructionSinkDisposition.Attempting)
			{
				box.ChronicleState = KingdomConstructionSinkDisposition.Attempting;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				try
				{
					if (!KingdomChronicle.RecordOnce(System, box.EventId + ":chronicle",
						box.Chronicle, Job.Route == KingdomConstructionRoute.WearRepair)) return false;
					box.ChronicleState = KingdomConstructionSinkDisposition.Delivered;
					if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				}
				catch { return false; }
			}

			if (box.LedgerState == KingdomConstructionSinkDisposition.Pending)
			{
				try
				{
					if (System.Ledger == null || System.Ledger.Notes == null) return false;
					if (!KingdomConstructionRules.TryFreezeLedger(System.Ledger.Notes, box.Ledger,
						out box.LedgerBeforeCount, out box.LedgerBeforeHash,
						out box.LedgerAfterCount, out box.LedgerAfterHash))
					{
						box.LedgerState = KingdomConstructionSinkDisposition.Lost;
						if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
					}
					else
					{
						box.LedgerState = KingdomConstructionSinkDisposition.Attempting;
						if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
					}
				}
				catch { return false; }
			}
			if (box.LedgerState == KingdomConstructionSinkDisposition.Attempting)
			{
				try
				{
					if (System.Ledger == null || System.Ledger.Notes == null) return false;
					KingdomConstructionCasAction action = KingdomConstructionRules.LedgerCasAction(
						System.Ledger.Notes, box.LedgerBeforeCount, box.LedgerBeforeHash,
						box.LedgerAfterCount, box.LedgerAfterHash);
					if (action == KingdomConstructionCasAction.Quarantine)
					{
						box.LedgerState = KingdomConstructionSinkDisposition.Lost;
						return KingdomConstruction.UpdateOutbox(ref Job, box);
					}
					if (action == KingdomConstructionCasAction.Apply)
					{
						System.Ledger.Note(box.Ledger);
						action = KingdomConstructionRules.LedgerCasAction(System.Ledger.Notes,
							box.LedgerBeforeCount, box.LedgerBeforeHash,
							box.LedgerAfterCount, box.LedgerAfterHash);
						if (action != KingdomConstructionCasAction.Confirm) return false;
					}
					box.LedgerState = KingdomConstructionSinkDisposition.Delivered;
					if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				}
				catch { return false; }
			}

			if (box.MessageState == KingdomConstructionSinkDisposition.Attempting)
			{
				box.MessageState = KingdomConstructionSinkDisposition.Lost;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			if (box.MessageState == KingdomConstructionSinkDisposition.Pending)
			{
				box.MessageState = KingdomConstructionSinkDisposition.Attempting;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				try
				{
					MessageQueue.AddPlayerMessage(box.Message);
					box.MessageState = KingdomConstructionSinkDisposition.Delivered;
					if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				}
				catch { return false; }
			}
			return KingdomConstructionRules.OutboxSettled(Job.Outbox);
		}

		// ==================================================================================
		// Notable tastes and leader traits
		// ==================================================================================

		/// <summary>
		/// Ceremony for a settling notable: states one or two tastes in prose and carries one
		/// virtue and one flaw, drawn once and never rerolled. Call from the office's own
		/// transition check (<c>KingdomOffices.UpdateOffice</c>) whenever a holder is newly
		/// named or the office passes to someone else &mdash; never on vacancy, which names
		/// nobody to settle in.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone the new holder was found standing in, read for which
		/// building categories already stand there. May be null; every taste then reads unmet.</param>
		/// <param name="Title">The office's own title, for the leader-trait line.</param>
		/// <param name="HolderName">The settler now holding office.</param>
		/// <param name="Holder">The settler themselves, for the quality-of-life vocabulary's own
		/// Prefers (Addendum 4). Null skips that half and changes nothing else.</param>
		/// <param name="QuartersKey">Design key of what they were housed in. Null is a notable
		/// nobody has housed yet, whose Prefers are simply their default.</param>
		/// <remarks>Side effect: writes <c>KingdomSystem.NotableShade</c>, which the level reads
		/// (<c>KingdomSubsidenceRules.SupportedLevel</c>). It REPLACES rather than accumulates
		/// &mdash; a settlement has one named notable, and what the place is worth to them is
		/// re-derived whenever the office changes hands.</remarks>
		public static void OnOfficeHolderNamed(KingdomSystem System, Zone Z, string Title, string HolderName, GameObject Holder = null, string QuartersKey = null)
		{
			if (!Enabled || System == null || !System.Founded || string.IsNullOrEmpty(HolderName))
			{
				return;
			}
			KingdomSystem.Guard("ceremony: notable settled", delegate
			{
				string settlementId = KingdomChronicle.SettlementId(System);
				if (!KingdomIdentityRules.IsSettlementId(settlementId)) return;
				ulong ordinal = CurrentOrdinal();

				int virtueIndex;
				int flawIndex;
				KingdomCeremonyRules.ChooseLeaderTraits(settlementId, ordinal, out virtueIndex, out flawIndex);
				string shownHolder = KingdomPresentation.Rich(HolderName);
				KingdomChronicle.Record(System, KingdomCeremonyRules.LeaderTraitChronicle(
					Title, shownHolder, KingdomPresentation.Rich(System.SeatName), virtueIndex,
					flawIndex));
				KingdomLog.Log("ceremony: leader traits " + HolderName + " virtue=" + virtueIndex + " flaw=" + flawIndex);

				List<int> tastes = KingdomCeremonyRules.ChooseTastes(settlementId, ordinal);
				List<bool> met = KingdomCeremonyRules.TastesMet(tastes, TasteOfferIn(Z));
				string tasteLine = KingdomCeremonyRules.TasteChronicle(shownHolder, tastes, met);
				KingdomChronicle.Record(System, tasteLine);
				MessageQueue.AddPlayerMessage("{{W|" + XRL.Language.Grammar.InitCap(tasteLine) + ".}}");
				// Addendum 4 routes a resident's met Prefers through this same machinery rather than
				// opening a second road to equilibrium: one shade, and one balance to keep. The
				// chronicle's own met-list is left exactly as it was, and the number goes where the
				// brief always meant it to -- onto the settlement, for the level to read
				// (KingdomSubsidenceRules.SupportedLevel). It replaces rather than accumulates:
				// one settlement has one named notable, and what the place is worth to them is
				// re-derived the next time the office changes hands.
				// The offer is read against the ground the quarters stand on, not the design key
				// alone -- underground there is no sky for a taste to be met by (QB-19).
				System.NotableShade = KingdomCeremonyRules.NotableShade(met, KingdomQolRules.PreferShade(KingdomQol.OfferOf(QuartersKey, Z), KingdomQol.ProfileOf(Holder)));
				KingdomLog.Log("ceremony: tastes " + HolderName + " shade=" + System.NotableShade);
			});
		}

		/// <summary>
		/// What this settlement offers a notable's stated tastes: one tag per built structure's
		/// category, read off the same <c>KingdomBuildKey</c>/<c>KingdomData</c> lookup the rest
		/// of the mod uses to recognise a completed work. Addendum 4's re-basing &mdash; the taste
		/// and the building meet in the shared vocabulary (<c>KingdomCeremonyRules.TastesMet</c>)
		/// rather than by a category-string comparison private to this file.
		/// </summary>
		/// <returns>Never null; empty for a zone with nothing standing, which meets no taste.
		/// </returns>
		private static string[] TasteOfferIn(Zone Z)
		{
			if (Z == null)
			{
				return KingdomQolRules.NoTags;
			}
			List<string> offer = new List<string>();
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.Built)
			{
				if (item.GetIntProperty("KingdomBuilt") != 1)
				{
					continue;
				}
				string buildKey = item.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (string.IsNullOrEmpty(buildKey))
				{
					continue;
				}
				KingdomRules.BuildEntry entry;
				if (!KingdomData.TryGetBuilding(buildKey, out entry))
				{
					continue;
				}
				string tag = KingdomCeremonyRules.CategoryTag(entry.Category);
				if (!string.IsNullOrEmpty(tag) && !offer.Contains(tag))
				{
					offer.Add(tag);
				}
			}
			return (offer.Count == 0) ? KingdomQolRules.NoTags : offer.ToArray();
		}

		// ==================================================================================
		// The pattern-book
		// ==================================================================================

		/// <summary>
		/// Freezes the optional offer into its owning CharterDelivery before that operation mutates
		/// resources. Only the seated city's stored keeper roster participates; catalogue rows are
		/// copied now, so reload and later catalogue drift cannot reroll labels or keys.
		/// </summary>
		public static KingdomTradePatternReceipt FreezePatternBook(KingdomSystem System,
			string SettlementId, long OperationSequence)
		{
			List<KingdomTradePatternDesign> frozen = new List<KingdomTradePatternDesign>();
			if (Enabled && System != null && System.Founded
				&& System.City != null && string.Equals(System.City.SettlementId,
					SettlementId, StringComparison.Ordinal))
			{
				List<KingdomCeremonyRules.BuildingKnowledge> knowledge =
					new List<KingdomCeremonyRules.BuildingKnowledge>();
				foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
				{
					knowledge.Add(new KingdomCeremonyRules.BuildingKnowledge
					{
						Key = entry.Key,
						Knowledge = KingdomZoning.GateFor(entry.Key).Knowledge,
						Label = entry.Name
					});
				}
				List<string> stored = KingdomZoningRules.DecodeRoster(System.KeepersRoster);
				List<KingdomCeremonyRules.ForeignDesign> candidates =
					KingdomCeremonyRules.ForeignDesigns(knowledge, stored);
				for (int i = 0; i < candidates.Count; i++)
				{
					KingdomCeremonyRules.ForeignDesign candidate = candidates[i];
					frozen.Add(new KingdomTradePatternDesign
					{
						BuildingKey = candidate.BuildingKey,
						LearnName = candidate.LearnName,
						Label = string.IsNullOrEmpty(candidate.Label)
							? candidate.LearnName : candidate.Label
					});
				}
			}
			return KingdomTradePatternRules.Freeze(SettlementId, OperationSequence, frozen);
		}

		/// <summary>One UI callback over the already-frozen operation-owned offer.</summary>
		public static int PickPatternBook(KingdomTradePatternReceipt Receipt)
		{
			if (Receipt?.Offers == null || Receipt.Offers.Count == 0) return -1;
			string[] options = new string[Receipt.Offers.Count];
			for (int i = 0; i < Receipt.Offers.Count; i++)
				options[i] = "{{W|" + Receipt.Offers[i].Label
					+ "}} {{K|(a foreign pattern)}}";
			return Popup.PickOption(Title: "A pattern-book, offered",
				Intro: "A caravan's driver spreads up to three foreign patterns and offers this settlement its pick of one. Nothing carried is spent, and the settlement's own catalogue loses nothing either way.",
				Options: options, AllowEscape: true);
		}

		private static ulong CurrentOrdinal()
		{
			return CurrentTicks() > 0L ? (ulong)CurrentTicks() : 0uL;
		}

		private static long CurrentTicks()
		{
			return (The.Game != null) ? The.Game.TimeTicks : 0L;
		}
	}
}
