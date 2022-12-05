using Sandbox;

namespace Facepunch.Pool
{
	[Library( "white_area_quad" )]
	public partial class WhiteAreaQuad : RenderEntity
	{
		[Net] public BBox Bounds { get; set; }
		public Material Material = Material.Load( "materials/pool_white_area.vmat" );
		public bool IsEnabled { get; set; }

		public override void DoRender( SceneObject sceneObject  )
		{
			if ( IsEnabled  )
			{
				var vb = new VertexBuffer();
				vb.Init( true );
				vb.AddCube( Position + RenderBounds.Center, RenderBounds.Size.WithZ( 1f ), Rotation.Identity );
				vb.Draw( Material );
			}
		}
	}
}
