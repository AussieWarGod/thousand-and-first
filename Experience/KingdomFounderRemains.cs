using System;
using System.Collections.Generic;
using System.Reflection;
using XRL;
using XRL.UI;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>
	/// Death-token bridge carried by the former player body. Vanilla's <see cref="Corpse"/> builds
	/// the corpse during <see cref="BeforeDeathRemovalEvent"/>; this part waits for the following
	/// <see cref="OnDeathRemovalEvent"/>, then finds that generated item by vanilla's exact
	/// <c>SourceID</c> and gives only that item the journal interaction.
	/// </summary>
	[Serializable]
	public sealed class r_KingdomFounderRemains : IPart
	{
		private string DeathToken;
		private string FounderName;

		public r_KingdomFounderRemains()
		{
		}

		internal r_KingdomFounderRemains(string DeathToken, string FounderName)
		{
			this.DeathToken = DeathToken;
			this.FounderName = FounderName;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == OnDeathRemovalEvent.ID;
		}

		public override bool HandleEvent(OnDeathRemovalEvent E)
		{
			try
			{
				AttachToGeneratedCorpse();
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: founder remains bridge failed", ex);
				ThousandAndFirst.KingdomLog.Log("succession: founder corpse bridge failed ("
					+ ex.GetType().Name + ": " + ex.Message + ")");
			}
			return base.HandleEvent(E);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomFounderRemains),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomFounderRemains),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		private void AttachToGeneratedCorpse()
		{
			if (ParentObject == null || string.IsNullOrEmpty(DeathToken)
				|| string.Equals(ParentObject.Physics?.LastDamagedByType, "Vaporized", StringComparison.Ordinal))
			{
				return;
			}
			string sourceId = ParentObject.IDIfAssigned;
			if (string.IsNullOrEmpty(sourceId))
			{
				return;
			}
			IInventory drops = ParentObject.GetDropInventory();
			if (drops is Cell cell)
			{
				AttachFirst(cell.Objects, sourceId);
				return;
			}
			if (drops is Inventory inventory)
			{
				AttachFirst(inventory.Objects, sourceId);
			}
		}

		private void AttachFirst(IEnumerable<GameObject> Objects, string SourceId)
		{
			if (Objects == null)
			{
				return;
			}
			foreach (GameObject item in Objects)
			{
				if (!GameObject.Validate(item)
					|| !string.Equals(item.GetStringProperty("SourceID"), SourceId, StringComparison.Ordinal))
				{
					continue;
				}
				if (item.GetPart<r_KingdomFounderKnowledge>() == null)
				{
					item.AddPart(new r_KingdomFounderKnowledge(DeathToken, FounderName));
				}
				ThousandAndFirst.KingdomLog.Log("succession: founder knowledge attached to corpse SourceID=" + SourceId);
				return;
			}
		}
	}

	/// <summary>One exact founder's forgotten journal, readable from their generated corpse.</summary>
	[Serializable]
	public sealed class r_KingdomFounderKnowledge : IPart
	{
		private const string ReadCommand = "TAFReadFounderKnowledge";

		private string DeathToken;
		private string FounderName;
		private bool Used;

		public r_KingdomFounderKnowledge()
		{
		}

		internal r_KingdomFounderKnowledge(string DeathToken, string FounderName)
		{
			this.DeathToken = DeathToken;
			this.FounderName = FounderName;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID || ID == InventoryActionEvent.ID;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (!Used && !string.IsNullOrEmpty(DeathToken))
			{
				E.AddAction("Read founder's memory", "read the founder's memory", ReadCommand, null, 'r');
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command != ReadCommand)
			{
				return base.HandleEvent(E);
			}
			if (Used || E.Actor == null || !E.Actor.IsPlayer())
			{
				return false;
			}
			if (Popup.ShowYesNo(ThousandAndFirst.KingdomSuccessionRules.CorpseReadPrompt(FounderName))
				!= DialogResult.Yes)
			{
				return false;
			}
			try
			{
				ThousandAndFirst.KingdomSuccession succession = The.Game?.GetSystem<ThousandAndFirst.KingdomSuccession>();
				int revealed;
				if (succession == null || !succession.TryRestoreFounderKnowledge(DeathToken, FounderName, out revealed))
				{
					Popup.Show("The remains answer no succession record. Nothing changes.");
					return false;
				}
				Used = true;
				Popup.Show(ThousandAndFirst.KingdomSuccessionRules.CorpseReadLine(revealed, 0));
				E.RequestInterfaceExit();
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: founder corpse reading failed", ex);
				ThousandAndFirst.KingdomLog.Log("succession: founder corpse reading failed ("
					+ ex.GetType().Name + ": " + ex.Message + ")");
				Popup.Show("The memories do not open. Nothing changes.");
			}
			return base.HandleEvent(E);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomFounderKnowledge),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomFounderKnowledge),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}
	}
}
