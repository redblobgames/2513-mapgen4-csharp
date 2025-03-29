/*
 * From https://www.redblobgames.com/x/2312-dual-mesh/
 * Copyright 2017, 2023 Red Blob Games <redblobgames@gmail.com>
 * License: Apache v2.0 <http://www.apache.org/licenses/LICENSE-2.0.html>
 *
 * Translation from JavaScript to C# partially with Claude 3.7
 */

using System;
using System.Collections.Generic;
using DelaunatorSharp;

/// Initialization parameters that come from Delaunator
public struct MeshInitializer
{
    public List<Point> Points;
    public int[] Triangles;
    public int[] Halfedges;
    public int? NumBoundaryPoints;
    public int? NumSolidSides;
}

/// Represent a triangle-polygon dual mesh with:
///   - Regions (r)
///   - Sides (s)
///   - Triangles (t)
///
/// Each element has an id:
///   - 0 <= r < numRegions
///   - 0 <= s < numSides
///   - 0 <= t < numTriangles
///
/// Naming convention: y_name_x takes x (r, s, t) as input and produces
/// y (r, s, t) as output.

public class TriangleMesh
{
    public static int T_From_S(int s) { return s / 3; }
    public static int S_Prev_S(int s) { return (s % 3 == 0) ? s + 2 : s - 1; }
    public static int S_Next_S(int s) { return (s % 3 == 2) ? s - 2 : s + 1; }

    // public data
    public int NumSides           { get; private set; }
    public int NumSolidSides      { get; private set; }
    public int NumRegions         { get; private set; }
    public int NumSolidRegions    { get; private set; }
    public int NumTriangles       { get; private set; }
    public int NumSolidTriangles  { get; private set; }
    public int NumBoundaryRegions { get; private set; }

    // internal data that has accessors
    private int[] _halfedges;
    private int[] _triangles;
    private int[] _s_of_r;
    private List<Point> _vertex_t;
    private List<Point> _vertex_r;

    /// Constructor that takes mesh information from Delaunator and constructs the rest.
    public TriangleMesh(MeshInitializer init)
    {
        NumBoundaryRegions = init.NumBoundaryPoints ?? 0;
        NumSolidSides = init.NumSolidSides ?? 0;
        Update(init);
    }

    /// Copy constructor for a TriangleMesh
    public TriangleMesh(TriangleMesh other)
    {
        NumSides           = other.NumSides;
        NumSolidSides      = other.NumSolidSides;
        NumRegions         = other.NumRegions;
        NumSolidRegions    = other.NumSolidRegions;
        NumTriangles       = other.NumTriangles;
        NumSolidTriangles  = other.NumSolidTriangles;
        NumBoundaryRegions = other.NumBoundaryRegions;

        _halfedges         = other._halfedges;
        _triangles         = other._triangles;
        _s_of_r            = other._s_of_r;
        _vertex_t          = other._vertex_t;
        _vertex_r          = other._vertex_r;
    }

    /// Update internal data structures from Delaunator
    public void Update(MeshInitializer init)
    {
        _halfedges = init.Halfedges;
        _triangles = init.Triangles;
        _vertex_r  = init.Points;
        _Update();
    }

    /// Update internal data structures to match the input mesh.
    ///
    /// Use if you have updated the triangles/halfedges with
    /// Delaunator and want the dual mesh to match the updated data.
    /// Note that this does not update boundary regions or ghost
    /// elements.
    private void _Update()
    {
        NumSides          = _triangles.Length;
        NumRegions        = _vertex_r.Count;
        NumSolidRegions   = NumRegions - 1; // TODO: only if there are ghosts
        NumTriangles      = NumSides / 3;
        NumSolidTriangles = NumSolidSides / 3;

        // Construct an index for finding sides connected to a region
        _s_of_r = new int[NumRegions];
        for (int s = 0; s < _triangles.Length; s++)
        {
            int endpoint = _triangles[S_Next_S(s)];
            if (_s_of_r[endpoint] == 0 || _halfedges[s] == -1)
            {
                _s_of_r[endpoint] = s;
            }
        }

        // Construct triangle coordinates
        _vertex_t = new List<Point>();
        for (int s = 0; s < _triangles.Length; s += 3)
        {
            Point a = _vertex_r[_triangles[s]];
            Point b = _vertex_r[_triangles[s + 1]];
            Point c = _vertex_r[_triangles[s + 2]];

            if (Is_Ghost_S(s))
            {
                // ghost triangle center is just outside the unpaired side
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double scale = 10 / double.Hypot(dx, dy); // go 10units away from side
                _vertex_t.Add(new Point(
                                  0.5 * (a.X + b.X) + dy * scale,
                                  0.5 * (a.Y + b.Y) - dx * scale
                              ));
            }
            else
            {
                // solid triangle center is at the centroid
                _vertex_t.Add(new Point(
                                  (a.X + b.X + c.X) / 3,
                                  (a.Y + b.Y + c.Y) / 3
                              ));
            }
        }
    }

    /// Construct ghost elements to complete the graph.
    ///

    public static MeshInitializer AddGhostStructure(MeshInitializer init)
    {
        int[] triangles = init.Triangles;
        int[] halfedges = init.Halfedges;
        int numSolidSides = triangles.Length;

        int numUnpairedSides = 0, firstUnpairedEdge = -1;
        Dictionary<int, int> s_unpaired_r = new(); // seed to side

        for (int s = 0; s < numSolidSides; s++)
        {
            if (halfedges[s] == -1)
            {
                numUnpairedSides++;
                s_unpaired_r[triangles[s]] = s;
                firstUnpairedEdge = s;
            }
        }

        int r_ghost = init.Points.Count;

        // C# syntax: initialize the new List with the existing points, and also add one more at the end
        List<Point> newpoints = new(init.Points) { new Point(double.NaN, double.NaN) };

        int[] r_newstart_s = new int[numSolidSides + 3 * numUnpairedSides];
        Array.Copy(triangles, r_newstart_s, numSolidSides);

        int[] s_newopposite_s = new int[numSolidSides + 3 * numUnpairedSides];
        Array.Copy(halfedges, s_newopposite_s, numSolidSides);

        for (int i = 0, s = firstUnpairedEdge;
             i < numUnpairedSides;
             i++, s = s_unpaired_r[r_newstart_s[S_Next_S(s)]])
        {
            // Construct a ghost side for s
            int s_ghost = numSolidSides + 3 * i;
            s_newopposite_s[s] = s_ghost;
            s_newopposite_s[s_ghost] = s;
            r_newstart_s[s_ghost] = r_newstart_s[S_Next_S(s)];

            // Construct the rest of the ghost triangle
            r_newstart_s[s_ghost + 1] = r_newstart_s[s];
            r_newstart_s[s_ghost + 2] = r_ghost;
            int k = numSolidSides + (3 * i + 4) % (3 * numUnpairedSides);
            s_newopposite_s[s_ghost + 2] = k;
            s_newopposite_s[k] = s_ghost + 2;
        }

        return new MeshInitializer
        {
            NumSolidSides = numSolidSides,
            NumBoundaryPoints = init.NumBoundaryPoints,
            Points = newpoints,
            Triangles = r_newstart_s,
            Halfedges = s_newopposite_s
        };
    }

    // Accessors
    public double X_Of_R(int r)    { return _vertex_r[r].X; }
    public double Y_Of_R(int r)    { return _vertex_r[r].Y; }
    public double X_Of_T(int t)    { return _vertex_t[t].X; }
    public double Y_Of_T(int t)    { return _vertex_t[t].Y; }

    public Point Pos_Of_R(int r)   { return _vertex_r[r]; }
    public Point Pos_Of_T(int t)   { return _vertex_t[t]; }

    public int R_Begin_S(int s)    { return _triangles[s]; }
    public int R_End_S(int s)      { return _triangles[S_Next_S(s)]; }

    public int T_Inner_S(int s)    { return T_From_S(s); }
    public int T_Outer_S(int s)    { return T_From_S(_halfedges[s]); }

    public int S_Opposite_S(int s) { return _halfedges[s]; }

    public int[] S_Around_T(int t, int[] s_out = null)
    {
        s_out ??= new int[3];
        for (int i = 0; i < 3; i++) { s_out[i] = 3 * t + i; }
        return s_out;
    }

    public int[] R_Around_T(int t, int[] r_out = null)
    {
        r_out ??= new int[3];
        for (int i = 0; i < 3; i++) { r_out[i] = _triangles[3 * t + i]; }
        return r_out;
    }

    public int[] T_Around_T(int t, int[] t_out = null)
    {
        t_out ??= new int[3];
        for (int i = 0; i < 3; i++) { t_out[i] = T_Outer_S(3 * t + i); }
        return t_out;
    }

    public List<int> S_Around_R(int r, List<int> s_out = null)
    {
        s_out ??= new List<int>();
        s_out.Clear();

        int s0 = _s_of_r[r];
        int incoming = s0;

        do
        {
            s_out.Add(_halfedges[incoming]);
            int outgoing = S_Next_S(incoming);
            incoming = _halfedges[outgoing];
        } while (incoming != -1 && incoming != s0);

        return s_out;
    }

    public List<int> R_Around_R(int r, List<int> r_out = null)
    {
        r_out ??= new List<int>();
        r_out.Clear();

        int s0 = _s_of_r[r];
        int incoming = s0;

        do
        {
            r_out.Add(R_Begin_S(incoming));
            int outgoing = S_Next_S(incoming);
            incoming = _halfedges[outgoing];
        } while (incoming != -1 && incoming != s0);

        return r_out;
    }

    public List<int> T_Around_R(int r, List<int> t_out = null)
    {
        t_out ??= new List<int>();
        t_out.Clear();

        int s0 = _s_of_r[r];
        int incoming = s0;

        do
        {
            t_out.Add(T_From_S(incoming));
            int outgoing = S_Next_S(incoming);
            incoming = _halfedges[outgoing];
        } while (incoming != -1 && incoming != s0);

        return t_out;
    }

    public int R_Ghost()             { return NumRegions - 1; }
    public bool Is_Ghost_S(int s)    { return s >= NumSolidSides; }
    public bool Is_Ghost_R(int r)    { return r == NumRegions - 1; }
    public bool Is_Ghost_T(int t)    { return Is_Ghost_S(3 * t); }
    public bool Is_Boundary_S(int s) { return Is_Ghost_S(s) && (s % 3 == 0); }
    public bool Is_Boundary_R(int r) { return r < NumBoundaryRegions; }
}
