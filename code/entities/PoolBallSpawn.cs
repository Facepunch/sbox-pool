using Sandbox;
using System;
using System.Collections.Generic;
using Editor;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Facepunch.Pool
{
	[Library( "pool_ball_spawn" )]
	[EditorModel( "models/pool/pool_ball.vmdl" )]
	[Title( "Ball Spawnpoint" )]
	[Model]
	[HammerEntity]
	public partial class PoolBallSpawn : ModelEntity
	{
		[Property]
		public PoolBallType Type { get; set; }

		[Property]
		public PoolBallNumber Number { get; set; }
	}
}
