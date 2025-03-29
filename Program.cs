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

    [JSImport("canvas.drawPoints", "main.js")]
    internal static partial void DrawPoints(
        string color, double radius,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] coordinates
        );

    [JSImport("canvas.drawLineSegments", "main.js")]
    internal static partial void DrawLineSegments(
        string color, double lineWidth,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] coordinates
        );

    [JSImport("canvas.drawPolygon", "main.js")]
    internal static partial void DrawPolygon(
        string color,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] coordinates
        );

    [JSExport]
    internal static void RunDualMesh()
    {
        var bounds = new Bounds { Left = 50, Top = 50, Width = 900, Height = 900 };
        var spacing = 50;
        var points = MeshCreator.GenerateInteriorBoundaryPoints(bounds, spacing);
        var numBoundaryPoints = points.Count;

        // DualMeshTests.AddRandomPoints(points, (int)Math.Round((bounds.Width * bounds.Height) / (spacing * spacing)), bounds);

        // Create a jittered grid as a cheaper alternative to a poisson disc set
        var random = new Random();
        for (int q = 1; q < bounds.Width / spacing; q++)
        {
            for (int r = 1; r < bounds.Height / spacing; r++)
            {
                double x = bounds.Left + q * spacing + (random.NextDouble()-random.NextDouble()) * spacing/3;
                double y = bounds.Top + r * spacing + (random.NextDouble()-random.NextDouble()) * spacing/3;
                points.Add(new Point(x, y));
            }
        }

        _delaunator = new Delaunator([.. points]);
        var init = new MeshInitializer{ Points = points, Triangles = _delaunator.Triangles, Halfedges = _delaunator.Halfedges, NumBoundaryPoints = numBoundaryPoints };
        init = TriangleMesh.AddGhostStructure(init);
        var mesh = new TriangleMesh(init);

        /*
        // Draw triangles
        for (int t = 0; t < mesh.NumSolidTriangles; t++)
        {
            int[] r_out = mesh.R_Around_T(t);
            DrawPolygon("oklch(90% 0.03 " + random.Next(360) + "deg)",
                        r_out.SelectMany(r => new[] { mesh.X_Of_R(r), mesh.Y_Of_R(r) }).ToArray());
        }
        */

        // Draw regions
        for (int r = 0; r < mesh.NumSolidRegions; r++)
        {
            List<int> t_out = mesh.T_Around_R(r);
            DrawPolygon("oklch(90% 0.03 " + random.Next(360) + "deg)",
                        t_out.SelectMany(t => new[] { mesh.X_Of_T(t), mesh.Y_Of_T(t) }).ToArray());
        }

        var coordinates = new double[4 * mesh.NumSolidSides];
        for (int s = 0; s < mesh.NumSolidSides; s++)
        {
            int r1 = mesh.R_Begin_S(s);
            int r2 = mesh.R_End_S(s);
            coordinates[4*s] = mesh.X_Of_R(r1);
            coordinates[4*s+1] = mesh.Y_Of_R(r1);
            coordinates[4*s+2] = mesh.X_Of_R(r2);
            coordinates[4*s+3] = mesh.Y_Of_R(r2);
        }
        DrawLineSegments("black", 0.75, coordinates);

        for (int s = 0; s < mesh.NumSolidSides; s++)
        {
            int t1 = mesh.T_Inner_S(s);
            int t2 = mesh.T_Outer_S(s);
            coordinates[4*s] = mesh.X_Of_T(t1);
            coordinates[4*s+1] = mesh.Y_Of_T(t1);
            coordinates[4*s+2] = mesh.X_Of_T(t2);
            coordinates[4*s+3] = mesh.Y_Of_T(t2);
        }
        DrawLineSegments("white", 1.5, coordinates);

        coordinates = new double[2 * mesh.NumSolidTriangles];
        for (int t = 0; t < mesh.NumSolidTriangles; t++)
        {
            coordinates[2*t] = mesh.X_Of_T(t);
            coordinates[2*t+1] = mesh.Y_Of_T(t);
        }
        DrawPoints("hsl(240 50% 50%)", 3, coordinates);
        
        coordinates = new double[2 * mesh.NumSolidRegions];
        for (int r = 0; r < mesh.NumSolidRegions; r++)
        {
            coordinates[2*r] = mesh.X_Of_R(r);
            coordinates[2*r+1] = mesh.Y_Of_R(r);
        }
        DrawPoints("hsl(0 50% 50%)", 4, coordinates);
    }
}
