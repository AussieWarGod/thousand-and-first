using System;
using System.Globalization;
using HarmonyLib;
using XRL;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>The one fixed, bounded interruption this witness raises. Never escapes production:
	/// KingdomSubsidenceReleaseDriver.Resume catches it and refuses the release.</summary>
	internal sealed class KingdomSubsidenceRungReleaseCutException : Exception
	{
		internal KingdomSubsidenceRungReleaseCutException()
			: base(KingdomSubsidenceRungReleaseCut.CutMessage) { }
	}

	internal enum KingdomSubsidenceRungReleaseCutMode : byte { None, Cut, Spent, Observe }

	/// <summary>Harness-only scope around one exact D5 rung release. Inactive by default: with no
	/// armed scope the patch returns before touching the port, so every unrelated Resume runs on
	/// the original port. Armed, it swaps in a wrapper that DELEGATES Plan/Current/TryObserve/
	/// Publish and performs the ACTUAL Write through the same port; it counts only writes the real
	/// port admitted, and cuts strictly AFTER the second one. Nothing here writes durable state.</summary>
	internal static class KingdomSubsidenceRungReleaseCut
	{
		internal const string CutMessage =
			"native subsidence rung release interruption after its second exact receipt write";
		internal const int WriteBudget = 2;

		private static XRLGame Game;
		private static KingdomSystem System;
		private static KingdomCityBook City;
		private static string SettlementId, WorkObjectId, CompanionObjectId, DeclaredStepId;
		private static long ExpectedSequence;
		private static int Index;
		private static Wrapper Held;

		internal static KingdomSubsidenceRungReleaseCutMode Mode { get; private set; }
		internal static int Writes { get; private set; }
		internal static int Throws { get; private set; }
		internal static string Fields { get; private set; }
		internal static string StepId { get; private set; }
		internal static string Fault { get; private set; }
		internal static bool PublishedIntent { get; private set; }
		internal static bool PublishedReleased { get; private set; }
		internal static int CompanionWrites { get; private set; }
		internal static string CompanionFields { get; private set; }
		internal static bool CompanionPublishedIntent { get; private set; }
		internal static bool CompanionPublishedReleased { get; private set; }
		internal static bool Armed { get { return Mode != KingdomSubsidenceRungReleaseCutMode.None; } }

		/// <summary>Save side. The step id is minted inside the pass, so it is latched from the exact
		/// live durable Active operation at the declared sequence and never varies afterwards.</summary>
		internal static void ArmCut(XRLGame game, KingdomSystem system, string workObjectId,
			int index, long sequence)
		{
			Arm(KingdomSubsidenceRungReleaseCutMode.Cut, game, system, workObjectId, index, sequence, null);
		}

		/// <summary>Cold-load side. Passive: it never throws and never changes an outcome; it only
		/// records the exact original port's writes and its durable Released publication.</summary>
		internal static void ArmObserver(XRLGame game, KingdomSystem system, string workObjectId,
			int index, long sequence, string stepId, string companionWorkObjectId = null)
		{
			Require(!string.IsNullOrEmpty(stepId), "the observed release scope needs its exact step");
			Arm(KingdomSubsidenceRungReleaseCutMode.Observe, game, system, workObjectId, index, sequence,
				stepId, companionWorkObjectId);
		}

		/// <summary>Idempotent. Drops the scope and the wrapper; recorded evidence is retained.</summary>
		internal static void Disarm()
		{
			Mode = KingdomSubsidenceRungReleaseCutMode.None;
			Game = null; System = null; City = null; Held = null;
			SettlementId = null; WorkObjectId = null; CompanionObjectId = null; DeclaredStepId = null;
			ExpectedSequence = 0L; Index = -1;
		}

		private static void Arm(KingdomSubsidenceRungReleaseCutMode mode, XRLGame game,
			KingdomSystem system, string workObjectId, int index, long sequence, string stepId,
			string companionWorkObjectId = null)
		{
			Require(Mode == KingdomSubsidenceRungReleaseCutMode.None, "a release scope is already armed");
			Require(game != null && ReferenceEquals(The.Game, game) && system != null
				&& ReferenceEquals(game.GetSystem<KingdomSystem>(), system) && system.City != null
				&& !string.IsNullOrEmpty(system.CurrentSettlementId)
				&& !string.IsNullOrEmpty(workObjectId) && index >= 0 && sequence >= 1L,
				"the release scope is not an exact game, settlement, work and index");
			Require(companionWorkObjectId == null || mode == KingdomSubsidenceRungReleaseCutMode.Observe
				&& index == 0 && !string.IsNullOrEmpty(companionWorkObjectId)
				&& string.CompareOrdinal(workObjectId, companionWorkObjectId) < 0,
				"the companion scope is not the next exact work of a passive observer");
			Game = game; System = system; City = system.City;
			SettlementId = KingdomChronicle.SettlementId(system);
			Require(!string.IsNullOrEmpty(SettlementId), "the release scope has no settlement identity");
			WorkObjectId = workObjectId; CompanionObjectId = companionWorkObjectId;
			Index = index; ExpectedSequence = sequence;
			DeclaredStepId = stepId; StepId = stepId;
			Writes = 0; Throws = 0; Fields = null; Fault = null;
			PublishedIntent = false; PublishedReleased = false; Held = null;
			CompanionWrites = 0; CompanionFields = null;
			CompanionPublishedIntent = false; CompanionPublishedReleased = false;
			Mode = mode;
		}

		/// <summary>Returns the wrapper only when the whole scope key matches; null leaves the
		/// original port in the caller's hands, untouched.</summary>
		internal static IKingdomSubsidenceReleasePort Scoped(IKingdomSubsidenceReleasePort port, int index)
		{
			try
			{
				if (!Armed || port == null || port is Wrapper) return null;
				string objectId = Subject(index);
				if (objectId == null) return null;
				if (Game == null || !ReferenceEquals(The.Game, Game) || System == null
					|| !ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
					|| City == null || !ReferenceEquals(System.City, City)) return null;
				KingdomSubsidenceRungPlan plan = port.Plan;
				if (plan == null || plan.Works == null || index < 0 || index >= plan.Works.Count
					|| plan.SettlementId != SettlementId
					|| plan.Works[index].ObjectId != objectId) return null;
				if (!KingdomSubsidenceStepCodec.TryDecode(City.SubsidenceModel,
					out KingdomSubsidenceStepBook book) || book.Sequence != ExpectedSequence
					|| book.Active == null || book.Active.Id != plan.StepId) return null;
				if (DeclaredStepId != null && DeclaredStepId != plan.StepId) return null;
				if (StepId != null && StepId != plan.StepId) return null;
				StepId = plan.StepId;
				if (Held == null || !Held.Wraps(port, index)) Held = new Wrapper(port, index);
				return Held;
			}
			catch (Exception) { return null; }
		}

		private static string Subject(int index)
		{
			if (index == Index) return WorkObjectId;
			return Mode == KingdomSubsidenceRungReleaseCutMode.Observe && index == Index + 1
				? CompanionObjectId : null;
		}

		private static void Wrote(int field, int index)
		{
			if (index != Index)
			{
				if (Subject(index) == null) return;
				CompanionWrites++;
				CompanionFields = (CompanionFields == null ? "" : CompanionFields + ",")
					+ field.ToString(CultureInfo.InvariantCulture);
				return;
			}
			Writes++;
			Fields = (Fields == null ? "" : Fields + ",") + field.ToString(CultureInfo.InvariantCulture);
			if (Mode != KingdomSubsidenceRungReleaseCutMode.Cut || Writes < WriteBudget) return;
			Mode = KingdomSubsidenceRungReleaseCutMode.Spent;
			Throws++;
			throw new KingdomSubsidenceRungReleaseCutException();
		}

		private static void Published(KingdomSubsidenceRungPlan next, int index)
		{
			try
			{
				string objectId = Subject(index);
				if (objectId == null || next == null || next.Works == null || index < 0
					|| index >= next.Works.Count || next.Works[index].ObjectId != objectId) return;
				KingdomSubsidenceReleasePhase phase = next.Works[index].ReleasePhase;
				if (phase != KingdomSubsidenceReleasePhase.Intent
					&& phase != KingdomSubsidenceReleasePhase.Released) return;
				// Re-prove the publication from the parent's own bytes; a returned true is not the fact.
				// The whole canonical durable rung wire must BE this next plan - same owner, work and
				// phase under the declared sequence - because a phase alone is no publication receipt.
				if (!KingdomSubsidenceStepCodec.TryDecode(City.SubsidenceModel,
					out KingdomSubsidenceStepBook book) || book.Sequence != ExpectedSequence
					|| book.Active == null || book.Active.Id != StepId
					|| !KingdomSubsidenceRungCodec.TryEncode(next, out string canonical)
					|| book.Active.RungModel != canonical) return;
				if (index == Index)
				{
					if (phase == KingdomSubsidenceReleasePhase.Intent) PublishedIntent = true;
					else PublishedReleased = true;
				}
				else
				{
					if (phase == KingdomSubsidenceReleasePhase.Intent) CompanionPublishedIntent = true;
					else CompanionPublishedReleased = true;
				}
			}
			catch (Exception error)
			{
				if (Fault == null) Fault = "release publication observation refused after " + error.GetType().Name;
			}
		}

		private static void Require(bool condition, string failure)
		{
			if (!condition) throw new InvalidOperationException("native rung release cut: " + failure);
		}

		private sealed class Wrapper : IKingdomSubsidenceReleasePort
		{
			private readonly IKingdomSubsidenceReleasePort Inner;
			private readonly int ObservedIndex;
			internal Wrapper(IKingdomSubsidenceReleasePort inner, int index)
			{ Inner = inner; ObservedIndex = index; }
			internal bool Wraps(IKingdomSubsidenceReleasePort port, int index)
			{ return ReferenceEquals(Inner, port) && ObservedIndex == index; }
			public KingdomSubsidenceRungPlan Plan { get { return Inner.Plan; } }
			public bool Current { get { return Inner.Current; } }

			public bool TryObserve(out KingdomSubsidenceWearReceipt receipt)
			{
				return Inner.TryObserve(out receipt);
			}

			public bool Publish(KingdomSubsidenceRungPlan expected, KingdomSubsidenceRungPlan next)
			{
				if (!Inner.Publish(expected, next)) return false;
				Published(next, ObservedIndex);
				return true;
			}

			public bool Write(int field, KingdomSubsidenceWearReceipt expected,
				KingdomSubsidenceWearReceipt target)
			{
				// The real port performs the actual write and re-measures it; only an admitted
				// write is counted, and the cut is raised strictly after that write succeeded.
				if (!Inner.Write(field, expected, target)) return false;
				Wrote(field, ObservedIndex);
				return true;
			}
		}
	}

	/// <summary>Attribute-scoped patch in the house convention. With no armed scope this is a no-op
	/// and the received port reference is left exactly as production passed it.</summary>
	[HarmonyPatch(typeof(KingdomSubsidenceReleaseDriver), "Resume")]
	internal static class KingdomSubsidenceRungReleaseCutPatch
	{
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static void Prefix(ref IKingdomSubsidenceReleasePort port, int index)
		{
			IKingdomSubsidenceReleasePort scoped = KingdomSubsidenceRungReleaseCut.Scoped(port, index);
			if (scoped != null) port = scoped;
		}
	}
}
