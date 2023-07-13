using Sandbox;
using Sandbox.Menu;
using System;
using Sandbox.UI;
using System.Linq;
using System.Collections.Generic;

namespace Facepunch.Pool.Menu;

public class SceneRenderer : ScenePanel
{
	private SceneMap Map { get; set; }

	public SceneRenderer()
	{
		World = new SceneWorld();
		Map = new SceneMap( World, "maps/pool_lounge_v2" );

		Camera.BackgroundColor = Color.Black;
		Camera.FieldOfView = 60f;
		Camera.AmbientLightColor = Color.Black;
		Camera.Rotation = Rotation.LookAt( Vector3.Down );
	}

	public override void Tick()
	{
		Camera.Position = Vector3.Up * 100f;
		Camera.Position = Camera.Position.WithZ( 100f + MathF.Sin( Time.Now ) );

		base.Tick();
	}
}
