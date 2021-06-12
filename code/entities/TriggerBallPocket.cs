using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoolGame
{
	[Library( "trigger_ball_pocket" )]
	public partial class TriggerBallPocket : BaseTrigger
	{
		public override void StartTouch( Entity other )
		{
			if ( other is PoolBall ball )
			{
				ball.OnEnterPocket( this );
			}

			base.StartTouch( other );
		}
	}
}
