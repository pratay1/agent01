using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PhysicsSandbox.Rendering;

namespace PhysicsSandbox
{
    public class PhysicsCanvas : Canvas
    {
        public Renderer? Renderer { get; set; }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Renderer?.Render(dc);
        }
    }
}
