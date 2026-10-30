using FamilyCreator.Generation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace FamilyCreator.Revit;

public sealed class GeometryPreview : Border
{
    private readonly Viewport3D viewport = new();
    private readonly PerspectiveCamera camera = new() { FieldOfView = 45, NearPlaneDistance = 0.01, FarPlaneDistance = 10000000 };
    private Point3D center;
    private double distance = 1000;
    private double yaw = -0.8;
    private double pitch = 0.5;
    private Point? drag;

    public GeometryPreview()
    {
        Child = viewport;
        Background = new SolidColorBrush(Color.FromRgb(239, 245, 250));
        CornerRadius = new CornerRadius(8);
        ClipToBounds = true;
        Focusable = true;
        viewport.Camera = camera;
        MouseLeftButtonDown += (_, e) => { drag = e.GetPosition(this); CaptureMouse(); Focus(); e.Handled = true; };
        MouseLeftButtonUp += (_, _) => { drag = null; ReleaseMouseCapture(); };
        LostMouseCapture += (_, _) => drag = null;
        MouseMove += (_, e) =>
        {
            if (drag is not Point start) return;
            var position = e.GetPosition(this);
            yaw -= (position.X - start.X) * 0.01;
            pitch = Math.Clamp(pitch + (position.Y - start.Y) * 0.01, -1.4, 1.4);
            drag = position;
            UpdateCamera();
        };
        MouseWheel += (_, e) => { distance = Math.Clamp(distance * (e.Delta > 0 ? 0.85 : 1.18), 1, 1000000); UpdateCamera(); e.Handled = true; };
        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Left: yaw -= 0.15; break;
                case Key.Right: yaw += 0.15; break;
                case Key.Up: pitch = Math.Min(1.4, pitch + 0.15); break;
                case Key.Down: pitch = Math.Max(-1.4, pitch - 0.15); break;
                case Key.Add: case Key.OemPlus: distance = Math.Max(1, distance * 0.85); break;
                case Key.Subtract: case Key.OemMinus: distance = Math.Min(1000000, distance * 1.18); break;
                default: return;
            }
            UpdateCamera(); e.Handled = true;
        };
    }

    public string Show(NativePreview preview)
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(145, 145, 145)));
        group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, 1, -2)));
        var bounds = Rect3D.Empty;
        foreach (var source in preview.Meshes)
        {
            var mesh = new MeshGeometry3D();
            foreach (var vertex in source.Vertices)
            {
                var point = new Point3D(vertex[0], vertex[1], vertex[2]);
                mesh.Positions.Add(point);
                mesh.TriangleIndices.Add(mesh.Positions.Count - 1);
                bounds.Union(point);
            }
            mesh.Freeze();
            var material = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString(source.Color)));
            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }
        group.Freeze();
        viewport.Children.Clear();
        viewport.Children.Add(new ModelVisual3D { Content = group });
        center = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
        distance = Math.Max(10, Math.Sqrt(bounds.SizeX * bounds.SizeX + bounds.SizeY * bounds.SizeY + bounds.SizeZ * bounds.SizeZ) * 2);
        yaw = -0.8; pitch = 0.5;
        UpdateCamera();
        return $"{bounds.SizeX:0.#} × {bounds.SizeY:0.#} × {bounds.SizeZ:0.#} mm · {preview.Recipe.Parts.Length} solids";
    }

    private void UpdateCamera()
    {
        var direction = new Vector3D(Math.Cos(yaw) * Math.Cos(pitch), Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch));
        camera.Position = center + direction * distance;
        camera.LookDirection = -direction;
        camera.UpDirection = new Vector3D(0, 0, 1);
    }
}
