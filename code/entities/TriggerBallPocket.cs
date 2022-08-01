using Sandbox;
using SandboxEditor;

namespace Facepunch.Pool
{
	[Library( "trigger_ball_pocket" )]
	[Title( "Pocket Trigger" )]
	[HammerEntity]
	public partial class TriggerBallPocket : BaseTrigger
	{
		public override void StartTouch( Entity other )
		{
			if ( other is PoolBall ball )
			{
				Log.Info( this + " (" + ball.Type + " / " + ball.Number + ") Distance: " + Position.Distance( ball.Position ) );
				ball.OnEnterPocket( this );
			}

			base.StartTouch( other );
		}
	}
}
