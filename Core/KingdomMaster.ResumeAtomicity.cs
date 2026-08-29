using System;
using System.Collections.Generic;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomMaster
	{
		private sealed partial class KingdomMasterResumePlan
		{
			private sealed class MasterResumeSources
			{
				internal readonly KingdomExperienceLedger ExperienceOwner;
				internal readonly bool ExperienceWasAbsent;
				private readonly KingdomJobRegistry JobsOwner;
				private readonly KingdomJobTable Jobs;
				private readonly byte[] Seat;
				private readonly byte[][] OtherSettlements;
				private readonly KingdomSettlement[] OtherOwners;
				private readonly string RealmId;
				private readonly bool Founded;
				private readonly KingdomMasterLatchValue MasterOption;
				private readonly long MasterOptionTick;
				private readonly long MasterResumeToken;
				private readonly long MasterAppliedResumeToken;
				private readonly long LastSliceTick;
				private readonly long ReifyTick;
				private readonly int ReifyThirdsSpent;
				private readonly int ReifyHeavySpent;
				private readonly long ReifyQuietUntilTick;

				private MasterResumeSources(KingdomSystem system, KingdomJobTable jobs,
					byte[] seat, byte[][] others, KingdomSettlement[] otherOwners)
				{
					JobsOwner = system.Jobs; Jobs = jobs; Seat = seat;
					OtherSettlements = others; OtherOwners = otherOwners;
					ExperienceWasAbsent = system.Experience == null;
					ExperienceOwner = system.Experience ?? new KingdomExperienceLedger();
					RealmId = system.RealmId; Founded = system.Founded;
					MasterOption = system.MasterOption;
					MasterOptionTick = system.MasterOptionTick;
					MasterResumeToken = system.MasterResumeToken;
					MasterAppliedResumeToken = system.MasterAppliedResumeToken;
					LastSliceTick = system.LastSliceTick; ReifyTick = system.ReifyTick;
					ReifyThirdsSpent = system.ReifyThirdsSpent;
					ReifyHeavySpent = system.ReifyHeavySpent;
					ReifyQuietUntilTick = system.ReifyQuietUntilTick;
				}

				internal static bool TryCapture(KingdomSystem system,
					List<KingdomSettlement> nonSeat, out MasterResumeSources sources)
				{
					sources = null;
					if (system?.Jobs == null || nonSeat == null || nonSeat.Count
						> KingdomSettlementTopologyRules.MaxNonSeatSettlements) return false;
					try
					{
						if (!KingdomArchivedSettlementCodec.TryEncode(system.Capture(),
							out byte[] seat, out string _)
							|| !system.Jobs.TryRead(out KingdomJobTable jobs,
								out KingdomCityFault _)) return false;
						byte[][] others = new byte[nonSeat.Count][];
						KingdomSettlement[] owners = new KingdomSettlement[nonSeat.Count];
						for (int i = 0; i < nonSeat.Count; i++)
						{
							owners[i] = nonSeat[i];
							if (owners[i] == null || !KingdomArchivedSettlementCodec.TryEncode(
								owners[i], out others[i], out string _)) return false;
						}
						sources = new MasterResumeSources(system, jobs, seat, others, owners);
						return true;
					}
					catch (Exception) { return false; }
				}

				internal bool JobsMatch(KingdomSystem system, KingdomJobTable target)
				{
					return system?.Jobs != null && ReferenceEquals(system.Jobs, JobsOwner)
						&& system.Jobs.TryRead(out KingdomJobTable current,
							out KingdomCityFault _) && KingdomJobTable.Exact(current, Jobs)
						&& system.Jobs.CanPublish(target, out KingdomCityFault _);
				}

				internal bool SeatMatches(KingdomSystem system)
				{
					try
					{
						return system != null && KingdomArchivedSettlementCodec.TryEncode(
							system.Capture(), out byte[] current, out string _)
							&& SameBytes(Seat, current);
					}
					catch (Exception) { return false; }
				}

				internal bool OtherMatches(List<KingdomSettlement> current, int index)
				{
					if (current == null || index < 0 || index >= OtherOwners.Length
						|| current.Count != OtherOwners.Length
						|| !ReferenceEquals(current[index], OtherOwners[index])) return false;
					return KingdomArchivedSettlementCodec.TryEncode(current[index],
						out byte[] bytes, out string _) && SameBytes(OtherSettlements[index], bytes);
				}

				internal bool CoreMatches(KingdomSystem system)
				{
					return system != null && !system.RealmRetirementBlocksWork
						&& ConfiguredEnabled && system.Founded == Founded
						&& string.Equals(system.RealmId, RealmId, StringComparison.Ordinal)
						&& system.MasterOption == MasterOption
						&& system.MasterOptionTick == MasterOptionTick
						&& system.MasterResumeToken == MasterResumeToken
						&& system.MasterAppliedResumeToken == MasterAppliedResumeToken
						&& system.LastSliceTick == LastSliceTick
						&& system.ReifyTick == ReifyTick
						&& system.ReifyThirdsSpent == ReifyThirdsSpent
						&& system.ReifyHeavySpent == ReifyHeavySpent
						&& system.ReifyQuietUntilTick == ReifyQuietUntilTick;
				}

				internal bool ExperienceMatches(KingdomSystem system,
					KingdomExperienceMasterResumePlan plan, out string failure)
				{
					failure = null;
					if (system == null || (ExperienceWasAbsent
						? system.Experience != null
						: !ReferenceEquals(system.Experience, ExperienceOwner))) return false;
					return KingdomExperienceRules.CanPublishMasterResume(
						ExperienceOwner, plan, out failure);
				}

				private static bool SameBytes(byte[] left, byte[] right)
				{
					if (left == null || right == null || left.Length != right.Length) return false;
					int difference = 0;
					for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
					return difference == 0;
				}
			}

			private bool Preflight(out string failure)
			{
				failure = null;
				if (Sources == null || Construction == null || ConstructionRoutes == null
					|| Seat == null || NonSeat == null || NonSeatPlans == null
					|| NonSeat.Count != NonSeatPlans.Count) return false;
				try
				{
					List<KingdomSettlement> currentNonSeat = System.NonSeatSettlements();
					bool[] matches = new bool[KingdomMasterPublicationGate.MaxParticipants];
					int count = 0;
					matches[count++] = Sources.JobsMatch(System, ConstructionRoutes);
					matches[count++] = Construction.CanPublish(out string _);
					matches[count++] = Sources.SeatMatches(System);
					for (int i = 0; i < NonSeat.Count; i++)
						matches[count++] = Sources.OtherMatches(currentNonSeat, i);
					matches[count++] = Trade == null ? System.TradeBook == null
						: Trade.MatchesSource(System.TradeBook);
					matches[count++] = Sources.CoreMatches(System);
					matches[count++] = Sources.ExperienceMatches(System, Experience,
						out string _);
					matches[count++] = KingdomPolityRules.CanPublishMasterResume(
						System.PolityLedger, System.PolityDispatch, Polity, out string _);
					return KingdomMasterPublicationGate.TryOpen(matches, count, -1, out failure);
				}
				catch (Exception exception)
				{
					failure = "master-resume preflight threw " + exception.GetType().Name;
					return false;
				}
			}

			private void PublishPrevalidated()
			{
				System.Jobs.PublishPrevalidated(ConstructionRoutes);
				Construction.PublishPrevalidated();
				Seat.Publish(System);
				for (int i = 0; i < NonSeatPlans.Count; i++)
					NonSeatPlans[i].Publish(NonSeat[i]);
				if (Trade != null) Trade.Publish(System.TradeBook);
				System.LastSliceTick = Seat.Now;
				System.ReifyTick = Seat.Now;
				System.ReifyQuietUntilTick = Seat.Now;
				if (Sources.ExperienceWasAbsent) System.Experience = Sources.ExperienceOwner;
				KingdomExperienceRules.PublishMasterResumePrevalidated(
					Sources.ExperienceOwner, Experience);
				KingdomPolityRules.PublishMasterResumePrevalidated(System.PolityLedger,
					System.PolityDispatch, Polity);
			}
		}
	}
}
