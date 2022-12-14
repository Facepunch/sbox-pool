using Sandbox;
using System;

namespace Facepunch.Pool
{
	public partial class PoolCamera 
	{
		public void Update()
		{
			if ( Game.LocalPawn is Player player )
			{
				Camera.FieldOfView = Screen.CreateVerticalFieldOfView( 15f );
				Camera.Position = Camera.Position.LerpTo( player.Position, Time.Delta );
				Camera.Rotation = player.Rotation;
			}

			Camera.FirstPersonViewer = null;
		}
	}
}
