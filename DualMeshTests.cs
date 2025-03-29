/*
 * From https://github.com/redblobgames/dual-mesh
 * Copyright 2017 Red Blob Games <redblobgames@gmail.com>
 * License: Apache v2.0 <http://www.apache.org/licenses/LICENSE-2.0.html>
 *
 * Tests to ensure the DualMesh structure is consistent.
 *
 * Translation from JavaScript to C# partially with Claude 3.7
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using DelaunatorSharp;

public class DualMeshTests
{
    public static void RunTests()
    {
        TestStructuralInvariants();
        TestHalfEdges1();
        TestHalfEdges2();
    }

    public static void AddRandomPoints(List<Point> points, int count, Bounds bounds)
    {
        var random = new Random();
        for (int i = 0; i < count; i++)
        {
            points.Add(new Point(
                           bounds.Left + random.NextDouble() * bounds.Width,
                           bounds.Top + random.NextDouble() * bounds.Height
                       ));
        }
    }
       
    static class Test
    {
        public static int Count = 0;

        public static void Equal(object a, object b)
        {
            string msg = $"{a} === {b}";
            if (Equals(a, b)) Pass(msg);
            else Fail(msg);
        }

        public static void Pass(string msg)
        {
            Console.WriteLine($"OK   {Count++} {msg}");
        }

        public static void Fail(string msg)
        {
            Console.Error.WriteLine($"FAIL {Count++} {msg}");
        }
    }

    ///
    /// Check mesh connectivity for a complete mesh (with ghost elements added)
    ///
    static void CheckMeshConnectivity(List<Point> points, int[] triangles, int[] halfedges)
    {
        // 1. make sure each side's opposite is back to itself
        // 2. make sure region-circulating starting from each side works
        int r_ghost = points.Count - 1;
        List<int> s_out = new();

        for (int s0 = 0; s0 < triangles.Length; s0++)
        {
            if (halfedges[halfedges[s0]] != s0)
            {
                Console.WriteLine($"FAIL _halfedges[_halfedges[{s0}]] !== {s0}");
            }

            int s = s0, count = 0;
            s_out.Clear();

            do
            {
                count++;
                s_out.Add(s);
                s = TriangleMesh.S_Next_S(halfedges[s]);

                if (count > 100 && triangles[s0] != r_ghost)
                {
                    Console.WriteLine($"FAIL to circulate around region with start side={s0} from region {triangles[s0]} to {triangles[TriangleMesh.S_Next_S(s0)]}, out_s={string.Join(",", s_out)}");
                    break;
                }
            } while (s != s0);
        }
    }

    static void TestStructuralInvariants()
    {
        var bounds = new Bounds { Left = 0, Top = 0, Width = 1000, Height = 1000 };
        var spacing = 450.0;
        var points = MeshCreator.GenerateInteriorBoundaryPoints(bounds, spacing);
        int numBoundaryPoints = points.Count;

        AddRandomPoints(points, 10, bounds);

        var delaunator = new Delaunator([.. points]);
        MeshCreator.CheckTriangleInequality(points, delaunator);

        var init = TriangleMesh.AddGhostStructure(
            new MeshInitializer{Points = points,
                                Triangles = delaunator.Triangles,
                                Halfedges = delaunator.Halfedges,
                                NumBoundaryPoints = numBoundaryPoints});
        CheckMeshConnectivity(init.Points, init.Triangles, init.Halfedges);

        var mesh = new TriangleMesh(init);

        var s_out = new List<int>();
        for (int s1 = 0; s1 < mesh.NumSides; s1++)
        {
            int s2 = mesh.S_Opposite_S(s1);
            Test.Equal(mesh.S_Opposite_S(s2), s1);
            Test.Equal(mesh.R_Begin_S(s1), mesh.R_End_S(s2));
            Test.Equal(mesh.T_Inner_S(s1), mesh.T_Outer_S(s2));
            Test.Equal(mesh.R_Begin_S(TriangleMesh.S_Next_S(s1)), mesh.R_Begin_S(s2));
        }

        for (int r = 0; r < mesh.NumRegions; r++)
        {
            mesh.S_Around_R(r, s_out);
            foreach (int s in s_out)
            {
                Test.Equal(mesh.R_Begin_S(s), r);
            }
        }

        for (int t = 0; t < mesh.NumTriangles; t++)
        {
            foreach (int s in mesh.S_Around_T(t))
            {
                Test.Equal(mesh.T_Inner_S(s), t);
            }
        }
    }

    static void TestHalfEdges1()
    {
        var points = new List<Point>
        {
            new(122, 270), new(181, 121), new(195, 852),
            new(204, 694), new(273, 525), new(280, 355),
            new(31, 946), new(319, 938), new(33, 625),
            new(344, 93), new(369, 793), new(38, 18),
            new(426, 539), new(454, 239), new(503, 51),
            new(506, 997), new(516, 661), new(532, 386),
            new(619, 889), new(689, 131), new(730, 511),
            new(747, 750), new(760, 285), new(856, 83),
            new(88, 479), new(884, 943), new(927, 696),
            new(960, 472), new(992, 253)
        };

        var random = new Random();
        for (int i = 0; i < points.Count; i++) {
            points[i] = new Point(points[i].X + random.NextDouble(), points[i].Y);
        }
        
        var delaunator = new Delaunator([.. points]);

        for (int i = 0; i < delaunator.Halfedges.Length; i++)
        {
            int i2 = delaunator.Halfedges[i];
            if (i2 != -1 && delaunator.Halfedges[i2] != i)
            {
                Test.Fail("invalid halfedge connection");
                return;
            }
        }

        Test.Pass("halfedges are valid");
    }

    static void TestHalfEdges2()
    {
        // NOTE: this is not a great test because the input data is
        // different each time; need to switch to a deterministic random
        // number generator
        var bounds = new Bounds { Left = 0, Top = 0, Width = 1000, Height = 1000 };
        var points = new List<Point>();
        AddRandomPoints(points, 250, bounds);
        var delaunator = new Delaunator([.. points]);

        for (int e1 = 0; e1 < delaunator.Halfedges.Length; e1++)
        {
            int e2 = delaunator.Halfedges[e1];
            if (e2 != -1 && delaunator.Halfedges[e2] != e1)
            {
                Test.Fail($"invalid halfedge connection; data set was {JsonSerializer.Serialize(points)}");
                return;
            }
        }

        Test.Pass("halfedges are valid");
    }
}
