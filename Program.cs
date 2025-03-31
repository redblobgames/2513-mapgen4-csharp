using System;
using System.Runtime.InteropServices.JavaScript;
using System.Collections.Generic;
using System.Linq;
using DelaunatorSharp;

Console.WriteLine("Hello, Browser!");
DualMeshTests.RunTests();

partial class Mapgen4
{
    private static Delaunator _delaunator;

    [JSImport("canvas.drawPoint", "main.js")]
    internal static partial void DrawPoint(
        string color, double radius,
        double x, double y
    );

    [JSImport("canvas.drawLineSegment", "main.js")]
    internal static partial void DrawLineSegment(
        string color, double lineWidth,
        double x1, double y1, double x2, double y2
    );

    // NOTE: not obvious how to pass List<Point> so I'm flattening
    // on the C# side to List<double> and passing it as double[]
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
        // The interior boundary points are inside the 'bounds' rectangle; the exterior
        // boundary points are outside, and can be clipped later. The exterior points
        // are needed to complete the polygons at the edge of the map. See
        // <https://www.redblobgames.com/x/2312-dual-mesh/#boundary> for motivation.
        // When using a Poisson Disc library, the interior boundary points should be part
        // of the poisson disc set, and the exterior boundary points should be added later.
        var points = MeshCreator.GenerateInteriorBoundaryPoints(bounds, spacing);
        points.AddRange(MeshCreator.GenerateExteriorBoundaryPoints(bounds, spacing));
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

        // Draw polygon regions
        for (int r = 0; r < mesh.NumSolidRegions; r++)
        {
            List<int> t_out = mesh.T_Around_R(r);
            DrawPolygon("oklch(90% 0.03 " + random.Next(360) + "deg)",
                        t_out
                        .SelectMany(t => new[] { mesh.X_Of_T(t), mesh.Y_Of_T(t) })
                        .ToArray());
        }

        // Draw the black edges making the triangles
        for (int s = 0; s < mesh.NumSolidSides; s++)
        {
            int r1 = mesh.R_Begin_S(s);
            int r2 = mesh.R_End_S(s);
            DrawLineSegment(
                "black", 0.75,
                mesh.X_Of_R(r1), mesh.Y_Of_R(r1),
                mesh.X_Of_R(r2), mesh.Y_Of_R(r2)
            );
        }

        // Draw the white edges making the polygon regions
        for (int s = 0; s < mesh.NumSolidSides; s++)
        {
            int t1 = mesh.T_Inner_S(s);
            int t2 = mesh.T_Outer_S(s);
            DrawLineSegment(
                "white", 1.5,
                mesh.X_Of_T(t1), mesh.Y_Of_T(t1),
                mesh.X_Of_T(t2), mesh.Y_Of_T(t2)
            );
        }

        // Draw the blue points which represent triangle centers / region vertices
        for (int t = 0; t < mesh.NumSolidTriangles; t++)
        {
            DrawPoint("hsl(240 50% 50%)", 3, mesh.X_Of_T(t), mesh.Y_Of_T(t));
        }

        // Draw the red points which represent region centers / triangle vertices
        for (int r = 0; r < mesh.NumSolidRegions; r++)
        {
            DrawPoint("hsl(0 50% 50%)", 4, mesh.X_Of_R(r), mesh.Y_Of_R(r));
        }
    }
}
