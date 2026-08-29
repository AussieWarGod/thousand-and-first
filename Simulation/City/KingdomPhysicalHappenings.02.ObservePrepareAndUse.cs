using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomPhysicalHappenings
	{
		private static Evidence Observe(KingdomSystem system,
			KingdomHappeningOperation operation)
		{
			Zone zone = ExactLoadedZone(operation.ZoneId);
			GameObject fixture = FindById(zone, operation.FixtureObjectId);
			bool fixtureExact = GameObject.Validate(fixture) && fixture.CurrentCell != null
				&& fixture.Blueprint == operation.FixtureBlueprint
				&& fixture.CurrentCell.X == operation.FixtureX
				&& fixture.CurrentCell.Y == operation.FixtureY
				&& FunctionalFixture(operation.Kind, fixture);
			List<GameObject> bodies = new List<GameObject>(operation.Participants.Length);
			bool participantsExact = zone != null;
			bool allArrived = participantsExact;
			for (int i = 0; i < operation.Participants.Length; i++)
			{
				KingdomHappeningParticipant row = operation.Participants[i];
				GameObject body;
				string bound;
				bool exact = KingdomResidents.TryResolveBoundBody(system, row.ResidentId, false,
					out body, out bound) && string.Equals(bound, operation.ZoneId,
					StringComparison.Ordinal) && string.Equals(body.IDIfAssigned, row.ObjectId,
					StringComparison.Ordinal) && NameOf(body) == row.Name && body.Brain != null
					&& PostReceipt(body) == PostReceipt(row)
					&& (body.GetStringProperty(KingdomLodging.HomePlotIdProperty) ?? "") == row.Home;
				if (exact && operation.Phase != KingdomHappeningLifecyclePhase.Prepared)
				{
					exact = ExactBodyReceipt(body, operation, row);
				}
				participantsExact &= exact;
				bodies.Add(exact ? body : null);
				Cell target = zone == null ? null : zone.GetCell(row.TargetX, row.TargetY);
				allArrived &= exact && target != null && body.CurrentCell == target;
			}
			bool useExact = fixtureExact && operation.Phase != KingdomHappeningLifecyclePhase.Holding
				&& operation.Phase != KingdomHappeningLifecyclePhase.Ready
				? true : fixtureExact && FunctionalUseExact(operation,
					new Evidence(zone, fixture, bodies, fixtureExact, participantsExact,
						allArrived, false));
			return new Evidence(zone, fixture, bodies, fixtureExact, participantsExact,
				allArrived, useExact);
		}

		private static bool Prepare(KingdomHappeningOperation operation, Evidence evidence)
		{
			if (!evidence.FixtureExact || !evidence.ParticipantsExact) return false;
			string standingFixture = evidence.Fixture.GetStringProperty(FixtureTokenProperty);
			if (!string.IsNullOrEmpty(standingFixture)
				&& standingFixture != operation.EventId) return false;
			evidence.Fixture.SetStringProperty(FixtureTokenProperty, operation.EventId);
			for (int i = 0; i < operation.Participants.Length; i++)
			{
				GameObject body = evidence.Bodies[i];
				KingdomHappeningParticipant row = operation.Participants[i];
				string standing = body.GetStringProperty(TokenProperty);
				if (!string.IsNullOrEmpty(standing) && standing != operation.EventId) return false;
				// Even a complete stage-and-restore span breaks continuous service. The monotone
				// endpoint witness survives restoration of the same post and body.
				if (operation.Kind != KingdomPhysicalHappeningKind.CommunalRite
					&& !KingdomStations.TouchAvailability(body)) return false;
				body.SetStringProperty(TokenProperty, operation.EventId);
				body.SetStringProperty(PostReceiptProperty, PostReceipt(row));
				body.SetStringProperty(AnchorReceiptProperty, row.Anchor);
				body.SetStringProperty(HomeReceiptProperty, row.Home);
				body.SetStringProperty(TargetReceiptProperty, row.TargetX.ToString(
					CultureInfo.InvariantCulture) + "," + row.TargetY.ToString(
					CultureInfo.InvariantCulture));
				body.SetStringProperty(FixtureReceiptProperty, operation.FixtureObjectId);
				body.SetStringProperty(OriginalReceiptProperty, row.OriginalX.ToString(
					CultureInfo.InvariantCulture) + "," + row.OriginalY.ToString(
					CultureInfo.InvariantCulture));
				body.SetIntProperty(WandersReceiptProperty, row.Wanders ? 1 : 0);
				body.SetIntProperty(RandomReceiptProperty, row.WandersRandomly ? 1 : 0);
				body.SetIntProperty(StayingReceiptProperty, row.Staying ? 1 : 0);
				Cell target = evidence.Zone.GetCell(row.TargetX, row.TargetY);
				body.Brain.Wanders = false;
				body.Brain.WandersRandomly = false;
				body.Brain.Stay(target);
				if (body.CurrentCell != target && !HasOwnedGoal(body, operation.EventId))
					body.Brain.PushGoal(new KingdomHappeningMoveTo(operation.EventId, target));
				if (!ExactBodyReceipt(body, operation, row)) return false;
			}
			return evidence.Fixture.GetStringProperty(FixtureTokenProperty) == operation.EventId;
		}

		private static bool StampUse(KingdomHappeningOperation operation, Evidence evidence)
		{
			GameObject fixture = evidence.Fixture;
			if (!GameObject.Validate(fixture)
				|| fixture.GetStringProperty(FixtureTokenProperty) != operation.EventId) return false;
			string standing = fixture.GetStringProperty(FixtureUseProperty);
			if (standing == operation.EventId) return FunctionalUseExact(operation, evidence);
			if (standing == operation.EventId + ":attempt")
			{
				if (!FunctionalUseExact(operation, evidence)) return false;
				fixture.SetStringProperty(FixtureUseProperty, operation.EventId);
				return fixture.GetStringProperty(FixtureUseProperty) == operation.EventId;
			}
			if (!string.IsNullOrEmpty(standing)) return false;
			fixture.SetStringProperty(FixtureUseProperty, operation.EventId + ":attempt");
			if (!PerformFunctionalUse(operation, evidence)) return false;
			fixture.SetStringProperty(FixtureUseProperty, operation.EventId);
			return fixture.GetStringProperty(FixtureUseProperty) == operation.EventId
				&& FunctionalUseExact(operation, evidence);
		}

		private static bool PerformFunctionalUse(KingdomHappeningOperation operation,
			Evidence evidence)
		{
			if (!evidence.FixtureExact || evidence.Bodies.Count == 0
				|| !GameObject.Validate(evidence.Bodies[0])) return false;
			GameObject actor = evidence.Bodies[0];
			switch (operation.Kind)
			{
			case KingdomPhysicalHappeningKind.Wedding:
				Chair chair = evidence.Fixture.GetPart<Chair>();
				return chair != null && chair.SitDown(actor)
					&& actor.GetEffect<Sitting>()?.SittingOn == evidence.Fixture;
			case KingdomPhysicalHappeningKind.Funeral:
				Shrine shrine = evidence.Fixture.GetPart<Shrine>();
				return shrine != null && shrine.PrayAtShrine(actor, Silent: true);
			case KingdomPhysicalHappeningKind.Feast:
				Campfire fire = evidence.Fixture.GetPart<Campfire>();
				return fire != null && fire.IsReady(UseCharge: true)
					&& RadiatesHeatEvent.Check(evidence.Fixture);
			case KingdomPhysicalHappeningKind.CommunalRite:
				Chair riteSeat = evidence.Fixture.GetPart<Chair>();
				if (riteSeat != null) return riteSeat.SitDown(actor)
					&& actor.GetEffect<Sitting>()?.SittingOn == evidence.Fixture;
				LiquidVolume riteBasin = evidence.Fixture.GetPart<LiquidVolume>();
				return riteBasin != null && riteBasin.MaxVolume > 0
					&& GetStorableDramsEvent.GetFor(evidence.Fixture, "water",
						LiquidVolume: riteBasin) == riteBasin.MaxVolume - riteBasin.Volume;
			case KingdomPhysicalHappeningKind.Raising:
				LiquidVolume basin = evidence.Fixture.GetPart<LiquidVolume>();
				return basin != null && basin.MaxVolume > 0
					&& GetStorableDramsEvent.GetFor(evidence.Fixture, "water",
						LiquidVolume: basin) == basin.MaxVolume - basin.Volume;
			default:
				return false;
			}
		}

		private static bool FunctionalUseExact(KingdomHappeningOperation operation,
			Evidence evidence)
		{
			if (!evidence.FixtureExact
				|| evidence.Fixture.GetStringProperty(FixtureUseProperty)
					!= operation.EventId) return false;
			if (operation.Kind != KingdomPhysicalHappeningKind.Wedding
				&& (operation.Kind != KingdomPhysicalHappeningKind.CommunalRite
					|| evidence.Fixture.GetPart<Chair>() == null)) return true;
			return evidence.Bodies.Count > 0 && GameObject.Validate(evidence.Bodies[0])
				&& evidence.Bodies[0].GetEffect<Sitting>()?.SittingOn == evidence.Fixture;
		}
	}
}
