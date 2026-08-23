using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Tinkering;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>
	/// Kingdom Mode's one death-time crossover. It is an <see cref="IPlayerSystem"/> so the
	/// <see cref="AfterDieEvent"/> registration follows whichever real body is the player, and it
	/// does all interregnum work synchronously before <c>GameObject.Die</c> asks <c>IsPlayer</c>
	/// again.
	/// </summary>
	[Serializable]
	public sealed class KingdomSuccession : IPlayerSystem
	{
		private const int SerializationMagic = 1414746963;
		private const int CurrentSerializationVersion = 1;
		private const int MaxSealAccessionTokenChars = KingdomSuccessionRules.MaxDeathTokenChars;
		private const int MaxPendingRiteChronicleChars = 2048;
		private const int MaxPendingRepairCreedsChars = 1024;

		private int SerializationVersion = CurrentSerializationVersion;
		private int SuccessionOrdinal;
		private string PendingDeathToken;
		private string CompletedDeathToken;
		private InterregnumPhase PendingPhase;
		private long PendingDueTick;
		private NewsRoad PendingRoad;
		private int PendingDays;
		private string PendingSealAccessionToken;
		private string PendingSealRiteChronicle;
		private bool PendingSealAccessionReady;
		private int PendingAccessionRepairResidentId;
		private string PendingAccessionRepairFounderName;
		private string PendingAccessionRepairHeirName;
		private bool PendingAccessionRepairSeated;
		private long PendingAccessionRepairArrivedTick;
		private string PendingAccessionRepairKeptCreeds;
		private bool SuccessionDisabled;

		[NonSerialized]
		private bool LoadFailed;

		[NonSerialized]
		private bool DeathChroniclePublished;

		[NonSerialized]
		private bool AccessionOwnershipCommitted;

		public override bool WantFieldReflection => false;

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterGameLoadedEvent.ID);
		}

		public override void RegisterPlayer(GameObject Player, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterDieEvent.ID);
		}

		public override bool HandleEvent(AfterDieEvent E)
		{
			DeathChroniclePublished = false;
			AccessionOwnershipCommitted = false;
			try
			{
				HandleFounderDeath(E);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession failed closed", ex);
				KingdomLog.Log("succession: failed closed (" + ex.GetType().Name + ": " + ex.Message + ")");
				bool founderStillControls = ReferenceEquals(The.Player, E?.Dying);
				if (!AccessionOwnershipCommitted && founderStillControls)
				{
					TryTerminalAfterOuterFailure(E);
					TryTellFailure("The charter could not be carried through the mourning rite.\n\nThe line ends here.");
				}
				else
				{
					TryCompletePendingSealAccession("post-accession exception");
					TryTellFailure("Control left the founder, but a later accession step failed. The line was not ended; any complete profile handoff stays queued for a safe retry.");
				}
			}
			finally
			{
				DeathChroniclePublished = false;
				AccessionOwnershipCommitted = false;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			if (!KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled))
			{
				KingdomLog.Log("succession: saved state could not be read; succession is disabled for this save");
				TryTellFailure("The kingdom's succession record could not be read. No death will be redirected by it in this save.");
			}
			else
			{
				TryCompletePendingAccessionRepair("game load");
				TryCompletePendingSealAccession("game load");
			}
			return base.HandleEvent(E);
		}

		public override void BeforeSave()
		{
			if (KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled))
			{
				TryCompletePendingAccessionRepair("BeforeSave");
				TryCompletePendingSealAccession("BeforeSave");
			}
		}

		public override void Write(SerializationWriter Writer)
		{
			SuccessionDisabled = SuccessionDisabled || LoadFailed;
			if (SuccessionDisabled)
			{
				ClearDisabledSavedState();
			}
			SerializationVersion = CurrentSerializationVersion;
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomSuccession),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Read(SerializationReader Reader)
		{
			try
			{
				int magic = Reader.ReadInt32();
				int version = Reader.ReadInt32();
				if (magic != SerializationMagic || version != CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst succession save block.");
				}
				Reader.ReadNamedFields(this, typeof(KingdomSuccession),
					BindingFlags.Instance | BindingFlags.NonPublic);
				SerializationVersion = CurrentSerializationVersion;
				ValidateSavedState();
			}
			catch
			{
				LoadFailed = true;
				SuccessionDisabled = true;
				throw;
			}
		}

		/// <summary>Reveals only notes stamped by one exact founder death. Called by the corpse part;
		/// quests and every unstamped journal entry are outside this method by construction.</summary>
		internal bool TryRestoreFounderKnowledge(string DeathToken, string FounderName, out int Revealed)
		{
			Revealed = 0;
			if (string.IsNullOrEmpty(DeathToken))
			{
				return false;
			}
			string attribute = KingdomSuccessionRules.FounderAttribute(DeathToken);
			foreach (IBaseJournalEntry entry in JournalAPI.GetAllNotes())
			{
				if (entry == null || entry.Revealed || entry.Attributes == null
					|| !entry.Attributes.Contains(attribute))
				{
					continue;
				}
				entry.Reveal("the remains of " + (string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName), Silent: true);
				if (entry.Revealed)
				{
					Revealed++;
				}
			}
			return true;
		}

		private void HandleFounderDeath(AfterDieEvent E)
		{
			XRLGame game = The.Game;
			GameObject founder = E?.Dying;
			if (game == null || founder == null
				|| !KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled)
				|| !ReferenceEquals(The.Player, founder))
			{
				return;
			}
			bool mode = KingdomSuccessionRules.ModeOn(game.gameMode,
				game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey));
			if (!mode)
			{
				return;
			}
			KingdomSystem system = game.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded)
			{
				return;
			}

			string founderName = founder.BaseDisplayNameStripped;
			if (PendingAccessionRepairResidentId != 0)
			{
				TryCompletePendingAccessionRepair("before another death");
				if (PendingAccessionRepairResidentId != 0)
				{
					KingdomLog.Log("succession: unresolved accession repair could not precede another death");
					PublishFounderDeath(system, founderName, E);
					EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
					return;
				}
			}
			if (!string.IsNullOrEmpty(PendingSealAccessionToken))
			{
				TryCompletePendingSealAccession("before another death");
				if (!string.IsNullOrEmpty(PendingSealAccessionToken))
				{
					PublishFounderDeath(system, founderName, E);
					EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
					return;
				}
			}
			string founderId = founder.ID;
			long deathTick = game.TimeTicks < 0L ? 0L : game.TimeTicks;
			string token = KingdomSuccessionRules.FounderDeathToken(
				SuccessionOrdinal + 1, deathTick, founderId);
			if (string.IsNullOrEmpty(token) || token.Length > MaxSealAccessionTokenChars)
			{
				KingdomLog.Log("succession: exact founder-death token exceeded its persistence bound");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			SuccessionAttemptVerdict attempt = KingdomSuccessionRules.JudgeAttempt(
				token, PendingDeathToken, CompletedDeathToken);
			if (attempt != SuccessionAttemptVerdict.Begin)
			{
				KingdomLog.Log("succession: death ignored by idempotence gate " + attempt + " token=" + token);
				return;
			}

			List<HeirRuntime> heirs;
			if (!TryReadHeirs(system, out heirs))
			{
				KingdomLog.Log("succession: the complete resident law could not be read; no partial roll was used");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomHeir[] candidates = new KingdomHeir[heirs.Count];
			for (int i = 0; i < heirs.Count; i++)
			{
				candidates[i] = heirs[i].Rule;
			}
			int chosenIndex;
			if (!KingdomSuccessionRules.TryChooseHeir(candidates, SuccessionLaw.Seniority, null, out chosenIndex))
			{
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.NoHeir, E);
				return;
			}

			HeirRuntime chosen = heirs[chosenIndex];
			if ((chosen.Rule.KeptCreeds ?? "").Length > MaxPendingRepairCreedsChars)
			{
				KingdomLog.Log("succession: chosen heir's creed history exceeded the repair bound");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			GameObject heirBody;
			string heirZoneId;
			if (!KingdomResidents.TryResolveBoundBody(system, chosen.Rule.ResidentId, LoadZone: true,
				out heirBody, out heirZoneId))
			{
				KingdomLog.Log("succession: law chose resident " + chosen.Rule.ResidentId + " ("
					+ chosen.Rule.Name + "), but that exact bound body was unreachable; no substitute was tried");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if (!GameObject.Validate(heirBody) || !heirBody.IsAlive || heirBody.CurrentCell == null
				|| ReferenceEquals(heirBody, founder))
			{
				KingdomLog.Log("succession: exact heir failed final body/cell validation; no substitute was tried");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomCityBook heirBook;
			int heirResidentId;
			if (!KingdomResidents.TryLocate(system, heirBody, out heirBook, out heirResidentId)
				|| heirResidentId != chosen.Rule.ResidentId)
			{
				KingdomLog.Log("succession: exact heir lost its resident row after body resolution");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			bool heirWasSeated = ReferenceEquals(heirBook, system.City);

			int newsDays;
			NewsRoad newsRoad;
			JudgeActualNews(system, founder.CurrentZone, out newsDays, out newsRoad);
			long dueTick = KingdomSuccessionRules.NewsDueTick(deathTick, newsDays);
			bool heldOffice = chosen.Rule.HoldsOffice;
			string heirCreed = heirBody.GetStringProperty(KingdomCreed.CreedProperty);

			PendingDeathToken = token;
			PendingPhase = InterregnumPhase.WordOnTheRoad;
			PendingDueTick = dueTick;
			PendingRoad = newsRoad;
			PendingDays = newsDays;
			r_KingdomFounderRemains remains = new r_KingdomFounderRemains(token, founderName);
			founder.AddPart(remains);
			PublishFounderDeath(system, founderName, E);
			MessageQueue.AddPlayerMessage("Word of " + founderName + "'s death is on the road to {{C|"
				+ (system.SeatName ?? "the settlement") + "}}.");

			long advance = KingdomSuccessionRules.WorldTicksUntilDue(game.TimeTicks, dueTick);
			if (advance > 0L)
			{
				game.TimeTicks = dueTick;
			}
			PendingPhase = InterregnumPhase.RiteDue;

			// After SetBody returns and the explicit global player-system sweep succeeds, no mod
			// action dispatches or yields before the prebuilt resident snapshots are published.
			KingdomResidentRow formerRow = default(KingdomResidentRow);
			// Unknown is repair-required. RefusedClean is trustworthy only when TryAccede
			// returns it after re-reading both exact original carriers, or when the
			// publication boundary was never entered at all.
			KingdomAccessionOutcome accession = KingdomAccessionOutcome.RepairRequired;
			bool accessionSeated = heirWasSeated;
			bool founderRestored = false;
			bool heirContinuationRegistrationsExact = false;
			KingdomPlayerBodyTransfer forward = SetPlayerBodyAndRebindAll(game, founder,
				heirBody, "accession");
			if (forward.MayPublishAccession)
			{
				heirContinuationRegistrationsExact = true;
				try
				{
					accession = KingdomAccessionOutcome.RepairRequired;
					accession = KingdomResidents.TryAccede(system, heirBody,
						out formerRow, out accessionSeated);
					if (accession == KingdomAccessionOutcome.RefusedClean)
					{
						accession = KingdomAccessionOutcome.RepairRequired;
						accession = KingdomResidents.TryAccede(system, heirBody,
							out formerRow, out accessionSeated);
					}
					if (accession == KingdomAccessionOutcome.RepairRequired
						&& formerRow.ResidentId == chosen.Rule.ResidentId)
					{
						accession = KingdomAccessionOutcome.RepairRequired;
						accession = KingdomResidents.TryRepairAccession(system, heirBody,
							chosen.Rule.ResidentId, accessionSeated, formerRow.Name,
							formerRow.ArrivedTick, formerRow.KeptCreeds, out formerRow);
					}
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: accession publish retry failed", ex);
				}
			}
			else
			{
				// Any thrown, short-circuited, misdirected, or incompletely rebound forward
				// transfer is not an accession. No resident carrier has been touched yet.
				KingdomLog.Log("succession: forward body transfer was not a clean globally rebound heir transfer; restoring founder control");
				KingdomPlayerBodyTransfer rollback = SetPlayerBodyAndRebindAll(game, heirBody,
					founder, "accession rollback");
				founderRestored = rollback.TargetControls;
				heirContinuationRegistrationsExact = rollback.OriginalControls
					&& rollback.RegistrationsExact;
				accession = founderRestored ? KingdomAccessionOutcome.RefusedClean
					: KingdomAccessionOutcome.RepairRequired;
			}
			if (accession != KingdomAccessionOutcome.Committed)
			{
				if (accession == KingdomAccessionOutcome.RepairRequired)
				{
					if (!KingdomSuccessionRules.MayQueueAccessionRepair(
						ReferenceEquals(The.Player, heirBody),
						heirContinuationRegistrationsExact))
					{
						FailCatastrophicBodyTransfer(system, founder, founderName, remains, E,
							"the failed body transfer ended on neither a globally rebound founder nor the exact heir");
						return;
					}
					AccessionOwnershipCommitted = true;
					QueueAccessionRepair(chosen.Rule, founderName, accessionSeated);
					TryPrepareRepairableHeir(heirBody);
					KingdomLog.Log("succession: accession carriers need repair; control remains with the heir");
					TryTellFailure("Control passed from the founder, but the resident accession carriers did not converge. The line remains open and the exact repair is queued.");
					return;
				}
				KingdomLog.Log("succession: CRITICAL accession publish failed immediately after SetBody; rolling control back to the dying founder");
				if (!founderRestored)
				{
					KingdomPlayerBodyTransfer rollback = SetPlayerBodyAndRebindAll(game,
						heirBody, founder, "accession rollback");
					founderRestored = rollback.TargetControls;
					heirContinuationRegistrationsExact = rollback.OriginalControls
						&& rollback.RegistrationsExact;
				}
				if (!KingdomSuccessionRules.MayTerminalAfterAccessionFailure(
					accession == KingdomAccessionOutcome.RefusedClean, founderRestored))
				{
					if (!KingdomSuccessionRules.MayQueueAccessionRepair(
						ReferenceEquals(The.Player, heirBody),
						heirContinuationRegistrationsExact))
					{
						FailCatastrophicBodyTransfer(system, founder, founderName, remains, E,
							"the clean resident refusal could not restore founder control or prove the exact heir");
						return;
					}
					// SetBody may change control and then throw. Never terminalize a lineage after
					// control left the dying founder; resident-law repair remains a separate task.
					AccessionOwnershipCommitted = true;
					QueueAccessionRepair(chosen.Rule, founderName, accessionSeated);
					TryPrepareRepairableHeir(heirBody);
					KingdomLog.Log("succession: CRITICAL founder control could not be restored; line remains open for accession repair");
					TryTellFailure("Control passed from the founder, but the resident accession record could not be published or rolled back. The line remains open and requires repair.");
					return;
				}
				try
				{
					founder.RemovePart(remains);
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: founder-remains rollback failed", ex);
				}
				PendingDeathToken = null;
				PendingPhase = InterregnumPhase.None;
				PendingDueTick = 0L;
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}

			CompleteAccession(game, system, heirBody, founderName, formerRow, token,
				newsRoad, newsDays, heldOffice, heirCreed, heirZoneId, "accession");
		}

		private void FailCatastrophicBodyTransfer(KingdomSystem System, GameObject Founder,
			string FounderName, r_KingdomFounderRemains Remains, AfterDieEvent Death,
			string Reason)
		{
			// A third or unproved controller is not the chosen heir. Never aim the persisted
			// resident roll-forward token at that body. End and disable this succession record.
			AccessionOwnershipCommitted = true;
			SuccessionDisabled = true;
			PendingDeathToken = "";
			PendingPhase = InterregnumPhase.None;
			PendingDueTick = 0L;
			PendingRoad = NewsRoad.Seat;
			PendingDays = 0;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSeated = false;
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";
			try
			{
				Founder?.RemovePart(Remains);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic succession remains cleanup failed", ex);
			}
			PublishFounderDeath(System, FounderName, Death);
			try
			{
				KingdomChronicle.Record(System,
					KingdomSuccessionRules.DynastyEndChronicle(System.SeatName, FounderName));
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic dynasty-end chronicle failed", ex);
			}
			try
			{
				string failure;
				if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out failure))
				{
					KingdomLog.Log("succession: catastrophic terminal seal attempt failed closed ("
						+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic terminal seal attempt threw", ex);
			}
			KingdomLog.Log("succession: CATASTROPHIC body-transfer refusal; succession disabled ("
				+ Reason + ")");
			TryTellFailure("The body transfer ended in an unproved controller state. The dynasty has ended, succession is disabled for this save, and no resident identity was applied to the uncontrolled body.");
		}

		private KingdomPlayerBodyTransfer SetPlayerBodyAndRebindAll(XRLGame Game,
			GameObject Original, GameObject Target, string Context)
		{
			KingdomPlayerBodyTransfer result = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				Original, Target,
				body => Game.Player.SetBody(body),
				() => The.Player,
				Game.Systems,
				(system, body) =>
				{
					IPlayerSystem playerSystem = system as IPlayerSystem;
					if (playerSystem != null)
					{
						playerSystem.RegisterPlayer(body,
							EventUnregistrar.Get(body, playerSystem));
					}
				},
				(system, body) =>
				{
					IPlayerSystem playerSystem = system as IPlayerSystem;
					if (playerSystem != null)
					{
						playerSystem.RegisterPlayer(body,
							EventRegistrar.Get(body, playerSystem));
					}
				});
			if (result.Failure != null)
			{
				MetricsManager.LogError("ThousandAndFirst: " + Context
					+ " body transfer or global player-system rebind failed", result.Failure);
			}
			if (!result.RegistrationsExact)
			{
				KingdomLog.Log("succession: " + Context
					+ " could not prove " + result.RegistrationFailures
					+ " player-system registration operation(s)");
			}
			return result;
		}

		private void QueueAccessionRepair(KingdomHeir Heir, string FounderName, bool Seated)
		{
			PendingPhase = InterregnumPhase.RiteDue;
			PendingAccessionRepairResidentId = Heir.ResidentId;
			PendingAccessionRepairFounderName = BoundPendingName(FounderName);
			PendingAccessionRepairHeirName = BoundPendingName(Heir.Name);
			PendingAccessionRepairSeated = Seated;
			PendingAccessionRepairArrivedTick = Heir.ArrivedTick;
			PendingAccessionRepairKeptCreeds = BoundPendingCreeds(Heir.KeptCreeds);
		}

		private void TryCompletePendingAccessionRepair(string Context)
		{
			if (PendingAccessionRepairResidentId == 0)
			{
				return;
			}
			try
			{
				XRLGame game = The.Game;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				GameObject heir = The.Player;
				if (game == null || system == null || !system.Founded
					|| !GameObject.Validate(heir) || !heir.IsAlive)
				{
					KingdomLog.Log("succession: pending accession repair cannot prove its exact controlled resident during "
						+ Context);
					return;
				}
				KingdomResidentRow formerRow = default(KingdomResidentRow);
				KingdomAccessionOutcome outcome = KingdomResidents.TryRepairAccession(system,
					heir, PendingAccessionRepairResidentId, PendingAccessionRepairSeated,
					PendingAccessionRepairHeirName, PendingAccessionRepairArrivedTick,
					PendingAccessionRepairKeptCreeds, out formerRow);
				if (outcome != KingdomAccessionOutcome.Committed)
				{
					KingdomLog.Log("succession: pending accession repair remains after " + Context);
					TryPrepareRepairableHeir(heir);
					return;
				}
				string founderName = string.IsNullOrEmpty(PendingAccessionRepairFounderName)
					? "the founder" : PendingAccessionRepairFounderName;
				string token = PendingDeathToken ?? "";
				bool heldOffice = string.Equals(system.OfficeHolderName, formerRow.Name,
					StringComparison.Ordinal);
				string heirCreed = heir.GetStringProperty(KingdomCreed.CreedProperty);
				CompleteAccession(game, system, heir, founderName, formerRow, token,
					PendingRoad, PendingDays, heldOffice, heirCreed,
					heir.CurrentZone?.ZoneID ?? "", "accession repair " + Context);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: pending accession repair failed closed", ex);
				KingdomLog.Log("succession: pending accession repair remains after " + Context
					+ " (" + ex.GetType().Name + ")");
			}
		}

		private void CompleteAccession(XRLGame Game, KingdomSystem System, GameObject Heir,
			string FounderName, KingdomResidentRow FormerRow, string Token, NewsRoad Road,
			int Days, bool HeldOffice, string HeirCreed, string HeirZoneId, string Context)
		{
			// Player body and resident law have committed. No later presentation, knowledge,
			// chronicle, or profile failure may declare the line ended.
			AccessionOwnershipCommitted = true;
			PendingPhase = InterregnumPhase.Reigning;
			CompletedDeathToken = Token;
			SuccessionOrdinal++;
			PendingSealAccessionToken = Token;
			PendingSealAccessionReady = false;
			PendingSealRiteChronicle = BoundPendingRite("the charter passed from " + FounderName
				+ " to " + FormerRow.Name + " at " + (System.SeatName ?? "the settlement") + ".");
			try
			{
				PendingSealRiteChronicle = BoundPendingRite(KingdomSuccessionRules.RiteChronicle(
					System.SeatName, FounderName, FormerRow.Name, Road, Days));
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: accession rite telling fell back", ex);
			}
			PendingDeathToken = null;
			PendingDueTick = 0L;
			PendingDays = 0;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSeated = false;
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";

			TryFinishAccessionBodyCleanup(Heir);
			TryPrepareRepairableHeir(Heir);

			int regard = 0;
			try
			{
				bool creedMatches = !string.IsNullOrEmpty(System.DeclaredCreed)
					&& string.Equals(HeirCreed, System.DeclaredCreed,
						StringComparison.OrdinalIgnoreCase);
				bool creedLeft = KingdomCreedRules.KeptHolds(FormerRow.KeptCreeds,
					System.DeclaredCreed);
				regard = KingdomSuccessionRules.AccessionRegard(FormerRow.ArrivedTick,
					Game.TimeTicks, creedMatches, creedLeft, HeldOffice);
				if (!TryResetPersonalKnowledge(System, Token, regard))
				{
					KingdomLog.Log("succession: honesty reset rolled back after accession; successor remains seated with prior knowledge intact");
					TryTellFailure("The charter changed hands, but the successor's personal records could not be opened safely. Nothing in them was changed.");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: post-accession honesty step failed", ex);
				KingdomLog.Log("succession: post-accession honesty step failed without reversing accession");
			}

			TryCompletePendingSealAccession(Context);
			try
			{
				TryTell(KingdomSuccessionRules.RiteAttendedPopup(
					System.SeatName, FounderName, FormerRow.Name, Road, Days));
				KingdomLog.Log("succession: " + FounderName + " -> " + FormerRow.Name + " token="
					+ Token + " heirZone=" + HeirZoneId + " road=" + Road + " days=" + Days
					+ " regard=" + regard);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: accession telling failed after commit", ex);
			}
		}

		private static void TryPrepareRepairableHeir(GameObject Heir)
		{
			try
			{
				PrepareSuccessor(Heir);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor body preparation failed", ex);
				KingdomLog.Log("succession: successor body preparation remains pending ("
					+ ex.GetType().Name + ")");
			}
			try
			{
				Heir.RequirePart<KingdomCharterPart>().EnsureAbility();
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor charter ability failed", ex);
				KingdomLog.Log("succession: successor charter ability remains pending ("
					+ ex.GetType().Name + ")");
			}
		}

		private static void TryFinishAccessionBodyCleanup(GameObject Heir)
		{
			try
			{
				KingdomStations.Post(Heir, 0, KingdomWorkKind.Other);
				Heir.RemoveIntProperty(KingdomResidents.ResidentIdProperty);
				Heir.RemoveIntProperty("KingdomCitizen");
				Heir.RemoveIntProperty("KingdomBorn");
				Heir.RemoveStringProperty("KingdomName");
				Heir.RemoveStringProperty(KingdomLodging.HomePlotIdProperty);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor resident cleanup failed", ex);
			}
		}

		private static string BoundPendingName(string Name)
		{
			string value = string.IsNullOrEmpty(Name) ? "the founder" : Name;
			return value.Length <= KingdomSealRecord.MaxNameChars
				? value : value.Substring(0, KingdomSealRecord.MaxNameChars);
		}

		private static string BoundPendingCreeds(string KeptCreeds)
		{
			string value = KeptCreeds ?? "";
			return value.Length <= MaxPendingRepairCreedsChars
				? value : value.Substring(0, MaxPendingRepairCreedsChars);
		}

		private static bool TryReadHeirs(KingdomSystem System, out List<HeirRuntime> Result)
		{
			Result = new List<HeirRuntime>();
			if (!ReadHeirs(System.City, System.OfficeHolderName, Result))
			{
				return false;
			}
			if (System.Away != null)
			{
				if (!ReadHeirs(System.Away.City, System.Away.OfficeHolderName, Result))
				{
					return false;
				}
			}
			return true;
		}

		private static bool ReadHeirs(KingdomCityBook Book, string OfficeHolder, List<HeirRuntime> Result)
		{
			KingdomCityState state;
			KingdomCityFault fault = default(KingdomCityFault);
			if (Book == null || !Book.TryRead(out state, out fault))
			{
				KingdomLog.Log("succession: a city book could not be read while choosing the heir (" + fault + ")");
				return false;
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row) || row.Standing != KingdomResidentStanding.Resident)
				{
					continue;
				}
				KingdomHeir rule = new KingdomHeir(row.Name, row.ArrivedTick, null, row.KeptCreeds,
					onTheRoll: true,
					holdsOffice: !string.IsNullOrEmpty(OfficeHolder)
						&& string.Equals(row.Name, OfficeHolder, StringComparison.Ordinal),
					row.BoundZoneId, row.ResidentId);
				Result.Add(new HeirRuntime(rule));
			}
			return true;
		}

		private static void JudgeActualNews(KingdomSystem System, Zone DeathZone, out int Days, out NewsRoad Road)
		{
			string deathZoneId = DeathZone?.ZoneID;
			string seatZoneId = (System.ClaimedZones != null && System.ClaimedZones.Count > 0)
				? System.ClaimedZones[0] : null;
			string deathWorld = null;
			string seatWorld = null;
			int dwx = 0;
			int dwy = 0;
			int dzx = 0;
			int dzy = 0;
			int dz = 0;
			int swx = 0;
			int swy = 0;
			int szx = 0;
			int szy = 0;
			int sz = 0;
			bool deathParsed = TryParseZone(deathZoneId,
				out deathWorld, out dwx, out dwy, out dzx, out dzy, out dz);
			bool seatParsed = TryParseZone(seatZoneId,
				out seatWorld, out swx, out swy, out szx, out szy, out sz);
			bool onSeatedGround = !string.IsNullOrEmpty(deathZoneId)
				&& System.ClaimedZones != null && System.ClaimedZones.Contains(deathZoneId);
			bool sameWorld = onSeatedGround || (deathParsed && seatParsed
				&& string.Equals(deathWorld, seatWorld, StringComparison.Ordinal));
			int dx = 0;
			int dy = 0;
			int depth = 0;
			if (sameWorld && !onSeatedGround)
			{
				dx = SaturatedDifference((long)dwx * 3L + dzx, (long)swx * 3L + szx);
				dy = SaturatedDifference((long)dwy * 3L + dzy, (long)swy * 3L + szy);
				depth = SaturatedDifference(dz, sz);
			}
			bool arch = ArchAnswersSeat(System, DeathZone);
			KingdomSuccessionRules.JudgeNews(arch, sameWorld, dx, dy, depth, out Days, out Road);
		}

		private static int SaturatedDifference(long A, long B)
		{
			long difference = A >= B ? A - B : B - A;
			return difference >= int.MaxValue ? int.MaxValue : (int)difference;
		}

		private static bool TryParseZone(string ZoneId, out string World, out int WorldX,
			out int WorldY, out int ZoneX, out int ZoneY, out int ZoneZ)
		{
			World = null;
			WorldX = 0;
			WorldY = 0;
			ZoneX = 0;
			ZoneY = 0;
			ZoneZ = 0;
			if (string.IsNullOrEmpty(ZoneId))
			{
				return false;
			}
			try
			{
				return ZoneID.Parse(ZoneId, out World, out WorldX, out WorldY,
					out ZoneX, out ZoneY, out ZoneZ);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("succession: zone id could not price news (" + ex.GetType().Name + ")");
				return false;
			}
		}

		private static bool ArchAnswersSeat(KingdomSystem System, Zone Zone)
		{
			try
			{
				if (Zone == null || The.Game == null || !KingdomPower.Enabled)
				{
					return false;
				}
				KingdomGateRow[] rows;
				int dropped;
				KingdomMirrorGateRules.TryParseRegister(
					The.Game.GetStringGameState(KingdomMirrorGateRules.RegisterStateKey, ""), out rows, out dropped);
				foreach (GameObject obj in Zone.GetObjects())
				{
					r_KingdomMirrorGate gate = obj?.GetPart<r_KingdomMirrorGate>();
					if (gate == null || gate.Dark)
					{
						continue;
					}
					KingdomMirrorGate.Anchor(gate);
					int here = KingdomMirrorGateRules.IndexOfKey(rows, gate.LocationKey);
					if (here < 0)
					{
						continue;
					}
					int there = KingdomMirrorGateRules.IndexOfKey(rows, rows[here].Partner);
					if (there >= 0 && string.Equals(rows[there].City, System.SeatName,
						StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
				return false;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("succession: arch news fact unavailable (" + ex.GetType().Name + ")");
				return false;
			}
		}

		private static void RecordFounderDeath(KingdomSystem System, string FounderName, AfterDieEvent E)
		{
			string cause = !string.IsNullOrEmpty(E.ThirdPersonReason) ? E.ThirdPersonReason
				: (!string.IsNullOrEmpty(E.Reason) ? E.Reason : "died, and no one living can say how");
			KingdomChronicle.RecordDisputed(System,
				KingdomSuccessionRules.FallenChronicle(FounderName, System.SeatName, cause),
				KingdomSuccessionRules.FallenRumour(FounderName, System.SeatName));
		}

		private void PublishFounderDeath(KingdomSystem System, string FounderName, AfterDieEvent E)
		{
			if (DeathChroniclePublished || System == null || E == null)
			{
				return;
			}
			DeathChroniclePublished = true;
			KingdomSystem.Guard("succession founder death", delegate
			{
				RecordFounderDeath(System, FounderName, E);
			});
		}

		private void EndDynasty(KingdomSystem System, string FounderName, SuccessionVerdict Verdict,
			AfterDieEvent Death)
		{
			PublishFounderDeath(System, FounderName, Death);
			KingdomChronicle.Record(System,
				KingdomSuccessionRules.DynastyEndChronicle(System.SeatName, FounderName));
			string sealFailure;
			if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out sealFailure))
			{
				KingdomLog.Log("succession: terminal seal attempt failed closed ("
					+ (string.IsNullOrEmpty(sealFailure) ? "unknown failure" : sealFailure) + ")");
			}
			Popup.Show(KingdomSuccessionRules.DynastyEndPopup(System.SeatName, Verdict));
			KingdomLog.Log("succession: terminal verdict " + Verdict + "; player body unchanged");
		}

		private void TryTerminalAfterOuterFailure(AfterDieEvent Death)
		{
			try
			{
				XRLGame game = The.Game;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				GameObject founder = Death?.Dying;
				if (game == null || system == null || !system.Founded || founder == null
					|| !ReferenceEquals(The.Player, founder)
					|| !KingdomSuccessionRules.ModeOn(game.gameMode,
						game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey)))
				{
					return;
				}
				PublishFounderDeath(system, founder.BaseDisplayNameStripped, Death);
				string failure;
				if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out failure))
				{
					KingdomLog.Log("succession: outer-failure terminal seal attempt failed closed ("
						+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: outer succession terminal attempt failed", ex);
			}
		}

		private static void PrepareSuccessor(GameObject Heir)
		{
			The.Game.PlayerName = Heir.Render.DisplayName;
			Heir.SetIntProperty("Renamed", 1);
			if (Heir.Brain != null)
			{
				Heir.Brain.PartyLeader = null;
				Heir.Brain.Goals.Clear();
				Heir.Brain.Factions = "";
				Heir.Brain.Allegiance.Clear();
				Heir.Brain.Allegiance["Player"] = 100;
				Heir.Brain.FactionFeelings.Clear();
			}
			Heir.RemovePart<GivesRep>();
			Heir.RemovePart<r_KingdomCitizenLegacy>();
		}

		private static bool TryResetPersonalKnowledge(KingdomSystem System, string Token, int RealmRegard)
		{
			List<JournalSnapshot> journal = new List<JournalSnapshot>();
			foreach (IBaseJournalEntry entry in JournalAPI.GetAllNotes())
			{
				if (entry != null)
				{
					journal.Add(new JournalSnapshot(entry));
				}
			}
			List<TinkerData> recipes = (TinkerData.KnownRecipes == null)
				? new List<TinkerData>() : new List<TinkerData>(TinkerData.KnownRecipes);
			Reputation oldReputation = The.Game.PlayerReputation;
			try
			{
				string attribute = KingdomSuccessionRules.FounderAttribute(Token);
				foreach (JournalSnapshot snapshot in journal)
				{
					IBaseJournalEntry entry = snapshot.Entry;
					if (!entry.Revealed || !KingdomSuccessionRules.Forgets(KindOf(entry), entry.Forgettable()))
					{
						continue;
					}
					if (entry.Attributes == null)
					{
						entry.Attributes = new List<string>();
					}
					if (!entry.Attributes.Contains(attribute))
					{
						entry.Attributes.Add(attribute);
					}
					entry.Forget(fast: true);
				}
				RevealRealmGround(System);
				TinkerData.KnownRecipes?.Clear();
				Reputation next = new Reputation();
				next.Init();
				Faction realm = Factions.GetIfExists(System.KingdomFactionName);
				if (realm != null)
				{
					next.Set(realm, RealmRegard);
				}
				The.Game.PlayerReputation = next;
				next.InitFeeling();
				return true;
			}
			catch (Exception ex)
			{
				for (int i = 0; i < journal.Count; i++)
				{
					try
					{
						journal[i].Restore();
					}
					catch (Exception restoreEx)
					{
						MetricsManager.LogError("ThousandAndFirst: journal rollback entry failed", restoreEx);
					}
				}
				try
				{
					if (TinkerData.KnownRecipes != null)
					{
						TinkerData.KnownRecipes.Clear();
						TinkerData.KnownRecipes.AddRange(recipes);
					}
				}
				catch (Exception recipeEx)
				{
					MetricsManager.LogError("ThousandAndFirst: recipe rollback failed", recipeEx);
				}
				try
				{
					The.Game.PlayerReputation = oldReputation;
					oldReputation?.InitFeeling();
				}
				catch (Exception reputationEx)
				{
					MetricsManager.LogError("ThousandAndFirst: reputation rollback failed", reputationEx);
				}
				MetricsManager.LogError("ThousandAndFirst: succession honesty reset rolled back", ex);
				return false;
			}
		}

		private static void RevealRealmGround(KingdomSystem System)
		{
			HashSet<string> ground = new HashSet<string>(StringComparer.Ordinal);
			if (System.ClaimedZones != null)
			{
				ground.UnionWith(System.ClaimedZones);
			}
			if (System.Away != null && System.Away.ClaimedZones != null)
			{
				ground.UnionWith(System.Away.ClaimedZones);
			}
			foreach (JournalMapNote note in JournalAPI.MapNotes)
			{
				if (note != null && ground.Contains(note.ZoneID) && !note.Revealed)
				{
					note.Reveal("the kingdom's chart", Silent: true);
				}
			}
		}

		private static JournalKind KindOf(IBaseJournalEntry Entry)
		{
			if (Entry is JournalAccomplishment) return JournalKind.Accomplishment;
			if (Entry is JournalMapNote) return JournalKind.MapNote;
			if (Entry is JournalGeneralNote) return JournalKind.GeneralNote;
			if (Entry is JournalVillageNote) return JournalKind.VillageNote;
			if (Entry is JournalRecipeNote) return JournalKind.RecipeNote;
			if (Entry is JournalSultanNote) return JournalKind.SultanNote;
			return JournalKind.Observation;
		}

		private void TryCompletePendingSealAccession(string Context)
		{
			if (string.IsNullOrEmpty(PendingSealAccessionToken))
			{
				return;
			}
			if (!PendingSealAccessionReady && !TryPublishPendingAccessionRite(Context))
			{
				return;
			}
			string token = PendingSealAccessionToken;
			string failure;
			if (KingdomSeal.TryStartSuccessorGeneration(token, out failure))
			{
				PendingSealAccessionToken = "";
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = false;
				return;
			}
			KingdomLog.Log("succession: pending profile accession remains after " + Context + " ("
				+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
		}

		private bool TryPublishPendingAccessionRite(string Context)
		{
			if (PendingSealAccessionReady)
			{
				return true;
			}
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded || string.IsNullOrEmpty(PendingSealRiteChronicle))
			{
				KingdomLog.Log("succession: pending accession rite cannot publish during " + Context);
				return false;
			}
			try
			{
				KingdomChronicle.Record(system, PendingSealRiteChronicle);
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = true;
				return true;
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: pending accession rite failed", ex);
				KingdomLog.Log("succession: pending accession rite remains after " + Context
					+ " (" + ex.GetType().Name + ")");
				return false;
			}
		}

		private static string BoundPendingRite(string Text)
		{
			string value = string.IsNullOrEmpty(Text)
				? "the charter passed to the successor after the founder's death." : Text;
			return value.Length <= MaxPendingRiteChronicleChars
				? value : value.Substring(0, MaxPendingRiteChronicleChars);
		}

		private void ValidateSavedState()
		{
			if (SuccessionDisabled)
			{
				ClearDisabledSavedState();
				return;
			}
			PendingDeathToken = PendingDeathToken ?? "";
			CompletedDeathToken = CompletedDeathToken ?? "";
			PendingSealAccessionToken = PendingSealAccessionToken ?? "";
			string stateFailure;
			if (!KingdomSuccessionRules.TryValidateSavedState(SuccessionOrdinal,
				PendingDeathToken, CompletedDeathToken, PendingPhase, PendingDueTick,
				PendingRoad, PendingDays, PendingAccessionRepairResidentId != 0,
				PendingSealAccessionToken, out stateFailure))
			{
				throw new InvalidOperationException("The saved succession state is invalid: "
					+ stateFailure + ".");
			}
			if (PendingSealAccessionToken != null
				&& PendingSealAccessionToken.Length > MaxSealAccessionTokenChars)
			{
				throw new InvalidOperationException("The saved profile-accession token is out of bounds.");
			}
			PendingSealRiteChronicle = PendingSealRiteChronicle ?? "";
			PendingAccessionRepairFounderName = PendingAccessionRepairFounderName ?? "";
			PendingAccessionRepairHeirName = PendingAccessionRepairHeirName ?? "";
			PendingAccessionRepairKeptCreeds = PendingAccessionRepairKeptCreeds ?? "";
			if (PendingAccessionRepairResidentId < 0
				|| PendingAccessionRepairFounderName.Length > KingdomSealRecord.MaxNameChars
				|| PendingAccessionRepairHeirName.Length > KingdomSealRecord.MaxNameChars
				|| PendingAccessionRepairKeptCreeds.Length > MaxPendingRepairCreedsChars
				|| PendingAccessionRepairArrivedTick < 0L
				|| (PendingAccessionRepairResidentId != 0
					&& (string.IsNullOrEmpty(PendingDeathToken)
						|| PendingDeathToken.Length > MaxSealAccessionTokenChars
						|| string.IsNullOrEmpty(PendingAccessionRepairHeirName))))
			{
				throw new InvalidOperationException("The saved accession repair identity is invalid.");
			}
			if (PendingAccessionRepairResidentId == 0)
			{
				PendingAccessionRepairFounderName = "";
				PendingAccessionRepairHeirName = "";
				PendingAccessionRepairSeated = false;
				PendingAccessionRepairArrivedTick = 0L;
				PendingAccessionRepairKeptCreeds = "";
			}
			if (PendingSealRiteChronicle.Length > MaxPendingRiteChronicleChars)
			{
				throw new InvalidOperationException("The saved accession rite chronicle is out of bounds.");
			}
			if (PendingSealAccessionToken.Length == 0)
			{
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = false;
			}
			else if (!PendingSealAccessionReady && PendingSealRiteChronicle.Length == 0)
			{
				// Compatibility with the first save shape: it only queued the token after the
				// rite chronicle had already been published.
				PendingSealAccessionReady = true;
			}
			else if (PendingSealAccessionReady)
			{
				PendingSealRiteChronicle = "";
			}
		}

		private void ClearDisabledSavedState()
		{
			int completedOrdinal;
			long completedTick;
			if (!KingdomSuccessionRules.TryReadDeathToken(CompletedDeathToken,
				out completedOrdinal, out completedTick))
			{
				CompletedDeathToken = "";
				SuccessionOrdinal = 0;
				PendingPhase = InterregnumPhase.None;
			}
			else
			{
				SuccessionOrdinal = completedOrdinal;
				PendingPhase = InterregnumPhase.Reigning;
			}
			PendingDeathToken = "";
			PendingDueTick = 0L;
			PendingRoad = NewsRoad.Seat;
			PendingDays = 0;
			PendingSealAccessionToken = "";
			PendingSealRiteChronicle = "";
			PendingSealAccessionReady = false;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSeated = false;
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";
		}

		private static void TryTellFailure(string Text)
		{
			try
			{
				Popup.Show(Text);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession failure telling failed", ex);
			}
		}

		private static void TryTell(string Text)
		{
			try
			{
				Popup.Show(Text);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession telling failed", ex);
			}
		}

		private sealed class HeirRuntime
		{
			internal readonly KingdomHeir Rule;

			internal HeirRuntime(KingdomHeir Rule)
			{
				this.Rule = Rule;
			}
		}

		private sealed class JournalSnapshot
		{
			internal readonly IBaseJournalEntry Entry;
			private readonly bool Revealed;
			private readonly string LearnedFrom;
			private readonly List<string> Attributes;

			internal JournalSnapshot(IBaseJournalEntry Entry)
			{
				this.Entry = Entry;
				Revealed = Entry.Revealed;
				LearnedFrom = Entry.LearnedFrom;
				Attributes = Entry.Attributes == null ? null : new List<string>(Entry.Attributes);
			}

			internal void Restore()
			{
				Entry.Revealed = Revealed;
				Entry.LearnedFrom = LearnedFrom;
				Entry.Attributes = Attributes == null ? null : new List<string>(Attributes);
				Entry.Updated();
			}
		}
	}
}
