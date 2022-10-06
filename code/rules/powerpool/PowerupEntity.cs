using Sandbox;
using System.Linq;

namespace Facepunch.Pool
{
	public partial class PowerupEntity : ModelEntity
	{
		[Net] public Powerup Powerup { get; private set; }

		public override void Spawn()
		{
			SetModel( "models/pool/pool_ball.vmdl" );
			SetupPhysicsFromModel( PhysicsMotionType.Dynamic, true );

			Tags.Add( "powerup" );

			Transmit = TransmitType.Always;

			var powerups = TypeLibrary.GetDescriptions<Powerup>().ToList();
			Powerup = Rand.FromList( powerups ).Create<Powerup>();
			Powerup.OnSpawn( this );

			base.Spawn();
		}
	}
}
