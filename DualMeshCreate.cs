/*
 * From https://www.redblobgames.com/x/2312-dual-mesh/
 * Copyright 2017, 2023 Red Blob Games <redblobgames@gmail.com>
 * License: Apache v2.0 <http://www.apache.org/licenses/LICENSE-2.0.html>
 *
 * Helper functions for building a TriangleMesh.
 * Translation from JavaScript to C# partially with Claude 3.7
 *
 * The TriangleMesh constructor takes points, delaunator output,
 * and a count of the number of boundary points. The boundary points
 * must be the prefix of the points array.
 *
 * To have equally spaced points added around a rectangular boundary,
 * pass in a boundary with the rectangle size and the boundary
 * spacing. If using Poisson disc points, I recommend √2 times the
 * spacing used for Poisson disc.
 */

using System;
using System.Collections.Generic;
using DelaunatorSharp;

public struct Bounds
{
    public double Left   { get; set; }
    public double Top    { get; set; }
    public double Width  { get; set; }
    public double Height { get; set; }
}

public static class MeshCreator
{
    public static void CheckTriangleInequality(List<Point> points, Delaunator delaunator)
    {
        int[] triangles = delaunator.Triangles;
        int[] halfedges = delaunator.Halfedges;

        int badAngleLimit = 30;
        int[] summary = new int[badAngleLimit];
        int count = 0;

        for (int s = 0; s < triangles.Length; s++)
        {
            int r0 = triangles[s];
            int r1 = triangles[TriangleMesh.S_Next_S(s)];
            int r2 = triangles[TriangleMesh.S_Next_S(TriangleMesh.S_Next_S(s))];

            Point p0 = points[r0];
            Point p1 = points[r1];
            Point p2 = points[r2];

            double[] d0 = new[] { p0.X - p1.X, p0.Y - p1.Y };
            double[] d2 = new[] { p2.X - p1.X, p2.Y - p1.Y };

            double dotProduct = d0[0] * d2[0] + d0[1] * d2[1]; // Fixed the bug in the original code
            double angleDegrees = double.RadiansToDegrees(Math.Acos(dotProduct));

            if (angleDegrees < badAngleLimit)
            {
                summary[(int)angleDegrees]++;
                count++;
            }
        }

        // NOTE: a much faster test would be the ratio of the inradius to
        // the circumradius, but as I'm generating these offline, I'm not
        // worried about speed right now

        // TODO: consider adding circumcenters of skinny triangles to the point set
        if (count > 0)
        {
            Console.WriteLine($"  bad angles: {string.Join(" ", summary)}");
        }
    }

    
    /// Add vertices evenly along the boundary of the mesh just barely
    /// inside the given boundary rectangle.
    ///
    /// The boundarySpacing parameter should be roughly √2 times the
    /// poisson disk minDistance spacing or √½ the maxDistance spacing.
    ///
    /// They need to be inside and not outside so that these points can be
    /// used with the poisson disk libraries I commonly use. The libraries
    /// require that all points be inside the range.
    ///
    /// Since these points are slightly inside the boundary, the triangle
    /// mesh will not fill the boundary. Generate exterior boundary points
    /// if you need to fill the boundary.
    ///
    /// I use a *slight* curve so that the Delaunay triangulation doesn't
    /// make long thin triangles along the boundary.
    ///

    public static List<Point> GenerateInteriorBoundaryPoints(Bounds bounds, double boundarySpacing)
    {
        // https://www.redblobgames.com/x/2314-poisson-with-boundary/
        const double epsilon = 1e-4;
        const double curvature = 1.0;

        int W = (int)Math.Ceiling((bounds.Width - 2 * curvature) / boundarySpacing);
        int H = (int)Math.Ceiling((bounds.Height - 2 * curvature) / boundarySpacing);

        var points = new List<Point>();

        // Top and bottom
        for (int q = 0; q < W; q++)
        {
            double t = q / (double)W;
            double dx = (bounds.Width - 2 * curvature) * t;
            double dy = epsilon + curvature * 4 * Math.Pow(t - 0.5, 2);

            points.Add(new Point(bounds.Left + curvature + dx, bounds.Top + dy));
            points.Add(new Point(bounds.Left + bounds.Width - curvature - dx, bounds.Top + bounds.Height - dy));
        }

        // Left and right
        for (int r = 0; r < H; r++)
        {
            double t = r / (double)H;
            double dy = (bounds.Height - 2 * curvature) * t;
            double dx = epsilon + curvature * 4 * Math.Pow(t - 0.5, 2);

            points.Add(new Point(bounds.Left + dx, bounds.Top + bounds.Height - curvature - dy));
            points.Add(new Point(bounds.Left + bounds.Width - dx, bounds.Top + curvature + dy));
        }

        return points;
    }

    /// Add vertices evenly along the boundary of the mesh
    /// outside the given boundary rectangle.
    ///
    /// The boundarySpacing parameter should be roughly √2 times the
    /// poisson disk minDistance spacing or √½ the maxDistance spacing.
    ///
    /// If using poisson disc selection, the interior boundary points will
    /// be to keep the points separated and the exterior boundary points
    /// will be to make sure the entire map area is filled.
    ///

    public static List<Point> GenerateExteriorBoundaryPoints(Bounds bounds, double boundarySpacing)
    {
        // https://www.redblobgames.com/x/2314-poisson-with-boundary/
        const double curvature = 1.0;
        double diagonal = boundarySpacing / Math.Sqrt(2);

        var points = new List<Point>();

        int W = (int)Math.Ceiling((bounds.Width - 2 * curvature) / boundarySpacing);
        int H = (int)Math.Ceiling((bounds.Height - 2 * curvature) / boundarySpacing);

        // Top and bottom
        for (int q = 0; q < W; q++)
        {
            double t = q / (double)W;
            double dx = (bounds.Width - 2 * curvature) * t + boundarySpacing / 2;

            points.Add(new Point(bounds.Left + dx, bounds.Top - diagonal));
            points.Add(new Point(bounds.Left + bounds.Width - dx, bounds.Top + bounds.Height + diagonal));
        }

        // Left and right
        for (int r = 0; r < H; r++)
        {
            double t = r / (double)H;
            double dy = (bounds.Height - 2 * curvature) * t + boundarySpacing / 2;

            points.Add(new Point(bounds.Left - diagonal, bounds.Top + bounds.Height - dy));
            points.Add(new Point(bounds.Left + bounds.Width + diagonal, bounds.Top + dy));
        }

        // Corners
        points.Add(new Point(bounds.Left - diagonal, bounds.Top - diagonal));
        points.Add(new Point(bounds.Left + bounds.Width + diagonal, bounds.Top - diagonal));
        points.Add(new Point(bounds.Left - diagonal, bounds.Top + bounds.Height + diagonal));
        points.Add(new Point(bounds.Left + bounds.Width + diagonal, bounds.Top + bounds.Height + diagonal));

        return points;
    }
}
