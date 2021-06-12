using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoolGame
{
	[Library( "trigger_white_area" )]
	public partial class TriggerWhiteArea : BaseTrigger
	{
		public WhiteAreaQuad Quad { get; set; }

		public void MakeAreaQuad()
		{
			Quad = new WhiteAreaQuad
			{
				RenderBounds = CollisionBounds,
				Position = Position
			};
		}

		public override void Spawn()
		{
			base.Spawn();

			Game.Instance.WhiteArea = this;

			Transmit = TransmitType.Always;
		}

		public override void StartTouch( Entity other )
		{
			base.StartTouch( other );
		}
	}
}
