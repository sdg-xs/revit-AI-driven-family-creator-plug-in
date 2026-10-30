using Autodesk.Revit.DB;
using FamilyCreator.Generation;

namespace FamilyCreator.Revit;

internal static class NativeGeometry
{
    internal static Solid Build(SolidPart part)
    {
        var loops = new List<CurveLoop>();
        XYZ? previousX = null;
        try
        {
            foreach (var profile in part.Profiles)
            {
                var loop = new CurveLoop();
                loops.Add(loop);
                if (profile.Kind == "circle")
                {
                    var center = Point(profile.Center);
                    var normal = new XYZ(profile.Normal[0], profile.Normal[1], profile.Normal[2]).Normalize();
                    var projected = previousX is null ? XYZ.Zero : previousX - normal * previousX.DotProduct(normal);
                    var x = projected.GetLength() > 1e-6 ? projected.Normalize()
                        : (Math.Abs(normal.DotProduct(XYZ.BasisZ)) < 0.9 ? XYZ.BasisZ : XYZ.BasisY).CrossProduct(normal).Normalize();
                    previousX = x;
                    var y = normal.CrossProduct(x).Normalize();
                    var radius = UnitUtils.ConvertToInternalUnits(profile.Radius, UnitTypeId.Millimeters);
                    loop.Append(Arc.Create(center, radius, 0, Math.PI, x, y));
                    loop.Append(Arc.Create(center, radius, Math.PI, 2 * Math.PI, x, y));
                }
                else
                {
                    var vertices = profile.Vertices.Select(Point).ToArray();
                    for (var i = 0; i < vertices.Length; i++) loop.Append(Line.CreateBound(vertices[i], vertices[(i + 1) % vertices.Length]));
                }
                if (!loop.HasPlane()) throw new InvalidOperationException("Profile vertices must lie on one plane.");
            }
            var solid = GeometryCreationUtilities.CreateLoftGeometry(loops, new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId));
            if (solid.Faces.Size == 0 || solid.Volume <= 1e-9)
            {
                solid.Dispose();
                throw new InvalidOperationException("The loft did not form a solid volume.");
            }
            return solid;
        }
        catch (Exception e) { throw new InvalidOperationException($"Cannot build part '{part.Name}': {e.Message}", e); }
        finally { foreach (var loop in loops) loop.Dispose(); }
    }

    internal static PreviewMesh Tessellate(Solid solid, string color)
    {
        var vertices = new List<double[]>();
        foreach (Face face in solid.Faces)
        {
            using var mesh = face.Triangulate(0.5);
            if (vertices.Count + mesh.NumTriangles * 3 > 300000) throw new InvalidOperationException("The native preview is too complex. Simplify the geometry.");
            for (var i = 0; i < mesh.NumTriangles; i++)
            {
                var triangle = mesh.get_Triangle(i);
                for (var j = 0; j < 3; j++)
                {
                    var p = triangle.get_Vertex(j);
                    vertices.Add([p.X * 304.8, p.Y * 304.8, p.Z * 304.8]);
                }
            }
        }
        return new PreviewMesh(color, vertices.ToArray());
    }

    private static XYZ Point(double[] p) => new(p[0] / 304.8, p[1] / 304.8, p[2] / 304.8);
}
