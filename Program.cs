using System;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using DelaunatorSharp;

Console.WriteLine("Hello, Browser!");
DualMeshTests.RunTests();

partial class Mapgen4
{
    private static Delaunator _delaunator;

    [JSImport("dom.setInnerText", "main.js")]
    internal static partial void SetInnerText(string selector, string content);

    [JSExport]
    internal static void RunDualMesh()
    {
        var bounds = new Bounds { Left = 0, Top = 0, Width = 1000, Height = 1000 };
        var spacing = 50;
        var points = MeshCreator.GenerateInteriorBoundaryPoints(bounds, spacing);
        var numBoundaryPoints = points.Count;

        for (int q = 0; q < bounds.Width / spacing; q++) {
            for (int r = 0; r < bounds.Height / spacing; r++) {
                double x = bounds.Left + q * spacing;
                double y = bounds.Top + r * spacing;
                points.Add(new Point(x, y));
            }
        }

        _delaunator = new Delaunator([.. points]);
        var init = new MeshInitializer{ Points = points, Triangles = _delaunator.Triangles, Halfedges = _delaunator.Halfedges, NumBoundaryPoints = numBoundaryPoints };
        init = TriangleMesh.AddGhostStructure(init);
        var mesh = new TriangleMesh(init);
    }
}
