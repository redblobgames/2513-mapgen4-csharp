/*
 * From https://github.com/redblobgames/maps/mapgen4/
 * Copyright 2017 Red Blob Games <redblobgames@gmail.com>
 * License: Apache v2.0 <http://www.apache.org/licenses/LICENSE-2.0.html>
 *
 * Mapgen4 JS ported to C#, without some of the bells and whistles
 *
 * Uses https://github.com/WardBenjamin/SimplexNoise (BSD-3 licensed)
 * Uses https://github.com/wolktocs/delaunator-csharp (ISC licensed)
 */

using System;
using System.Collections.Generic;
using SimplexNoise;

class Map
{
    // TODO: make these into sliders
    const double PARAM_NoisyCoastlines = 0.01;
    const double PARAM_Raininess = 0.9;
    const double PARAM_Evaporation = 0.5;
    const double PARAM_RainShadow = 0.5;
    const double PARAM_Flow = 0.2;
    const double PARAM_island = 0.5;

    public int Seed;
    public TriangleMesh Mesh;
    public double Spacing;
    public double WindAngleDegrees;
    public float[] Elevation_T;
    public float[] Elevation_R;
    public float[] Humidity_R;
    public float[] Moisture_T;
    public float[] Rainfall_R;
    public int[] S_Downslope_T;
    public int[] T_Order;
    public float[] Flow_T;
    public float[] Flow_S;

    private float[] _r_windPriority; // time at which the wind will hit region r
    private int[] _r_windOrder; // r values sorted to match wind direction

    public Map(TriangleMesh mesh, double spacing)
    {
        Mesh = mesh;
        Spacing = spacing;
        Seed = 287;
        Noise.Seed = Seed;
        WindAngleDegrees = 0;

        Elevation_T   = new float[mesh.NumTriangles];
        Elevation_R   = new float[mesh.NumRegions];
        Humidity_R    = new float[mesh.NumRegions];
        Moisture_T    = new float[mesh.NumTriangles];
        Rainfall_R    = new float[mesh.NumRegions];
        S_Downslope_T = new int[mesh.NumTriangles];
        T_Order       = new int[mesh.NumTriangles];
        Flow_T        = new float[mesh.NumTriangles];
        Flow_S        = new float[mesh.NumSides];

        CalculateWindOrder();
    }

    // Wrapper around the SimplexNoise library
    public float Noise2D(double x, double y)
    {
        // The SimplexNoise library API uses integers and internally
        // converts to floats, but we want to access the floats
        // directly, so this is a workaround -- multiply all the
        // values by 1000 and then divide by 1000 in the library's
        // scale parameter.
        const float scale = 1000.0f;
        x += 1.0; y += 1.0; // hack: because this library seems to have some weird output near 0
        float n = Noise.CalcPixel2D((int)(x*scale), (int)(y*scale), 1.0f/scale);
        // The SimplexNoise library API converts its -1.0 to +1.0
        // float into a value 0 to 255. I want to convert it back
        // to -1.0 to +1.0.
        return (n - 128.0f) / 128.0f;
    }

    public void AssignElevation()
    {
        AssignTriangleElevation();
        AssignRegionElevation();
    }

    public void AssignRivers()
    {
        AssignDownslope();
        AssignMoisture();
        AssignFlow();
    }

    /// Fetch the desired elevation painted by the user;
    /// this will be mixed in with other noise. x,y are from 0.0 to 1.0.
    private double DesiredElevationAt(double x, double y)
    {
        // Until we have user-painting, use a default island shape

        // x,y are 0.0 to 1.0 but nx,ny are -1.0 to +1.0, where 0.0 is
        // the center of the map
        double nx = (x - 0.5) * 2.0;
        double ny = (y - 0.5) * 2.0;

        // First phase: set e to *fbm noise*, following
        // https://www.redblobgames.com/maps/terrain-from-noise/
        const int octaves = 5;
        double e = 0.0;
        double sumOfAmplitudes = 0.0;
        for (int octave = 0; octave < octaves; octave++)
        {
            float frequency = 1 << octave;
            double amplitude = double.Pow(0.5, octave);
            e += amplitude * Noise2D(nx*frequency, ny*frequency);
            sumOfAmplitudes += amplitude;
        }
        e /= sumOfAmplitudes;

        // Second phase: reshape to make it an island
        // https://www.redblobgames.com/maps/terrain-from-noise/#islands
        double distance = double.Max(double.Abs(nx), double.Abs(ny));
        e = 0.5 * (e + PARAM_island * (0.75 - 2 * distance * distance));

        // Clamp
        if (e < -1.0) { e = -1.0; }
        if (e > +1.0) { e = +1.0; }

        // Tweak
        if (e > 0.0) {
            double m = (0.5 * Noise2D(nx + 30, ny + 50)
                     + 0.5 * Noise2D(2*nx + 33, 2*ny + 55));
            // TODO: make some of these into parameters
            double mountain = double.Min(1.0, e * 5.0) * (1 - double.Abs(m) / 0.5);
            if (mountain > 0.0) {
                e = double.Max(e, double.Min(e * 3, mountain));
            }
        }
        return e;
        // In the original code I sampled from a low resolution bitmap
        // using bilinear interpolation to smooth things out
    }

    public void AssignTriangleElevation()
    {
        // TODO: precompute noise using the SimplexNoise library
        // and save everything indexed by triangle index; recompute
        // this when the seed changes (use a setter on the seed)

        for (int t = 0; t < Mesh.NumSolidTriangles; t++)
        {
            double e = DesiredElevationAt(Mesh.X_Of_T(t) / 1000, Mesh.Y_Of_T(t) / 1000);
            // TODO: e*e*e*e seems too steep for this, as I want this
            // to apply mostly at the original coastlines and not
            // elsewhere
            e += PARAM_NoisyCoastlines * (1 - e * e * e * e);
            // * (precomputed.Noise4_t[t] + precomputed.Noise5_t[t] / 2 + precomputed.Noise6_t[t] / 4);

            if (e < -1.0f) { e = -1.0f; }
            if (e > +1.0f) { e = +1.0f; }

            Elevation_T[t] = (float)e;
        }
    }

    public void AssignRegionElevation()
    {
        List<int> t_out = new();
        for (int r = 0; r < Mesh.NumRegions; r++)
        {
            int count = 0;
            float e = 0;
            bool water = false;

            foreach (int t in Mesh.T_Around_R(r, t_out))
            {
                e += Elevation_T[t];
                water = water || Elevation_T[t] < 0.0f;
                count++;
            }

            e /= count;
            if (water && e >= 0) { e = -0.001f; }

            Elevation_R[r] = e;
        }
    }

    // Construct both _r_windPriority and _r_windOrder based on WindAngleDegrees
    // TODO: put a setter on WindAngleDegrees that calls this function
    private void CalculateWindOrder()
    {
        double windAngleRadians = double.DegreesToRadians(WindAngleDegrees);
        double windAngleVecX = double.Cos(windAngleRadians),
            windAngleVecY = double.Sin(windAngleRadians); // TODO: use Numerics.Vector2?

        _r_windOrder = new int[Mesh.NumRegions];
        _r_windPriority = new float[Mesh.NumRegions];
        for (int r = 0; r < Mesh.NumRegions; r++)
        {
            _r_windOrder[r] = r;
            _r_windPriority[r] = (float)(Mesh.X_Of_R(r) * windAngleVecX
                                         + Mesh.Y_Of_R(r) * windAngleVecY);
        }

        // Sort R_Wind_Order based on windPriority values
        Array.Sort(_r_windOrder,
                   (r1, r2) => _r_windPriority[r1].CompareTo(_r_windPriority[r2]));
    }
    
    public void AssignRainfall()
    {
        List<int> r_out = new();
        foreach (int r in _r_windOrder)
        {
            int count = 0;
            float sum = 0.0f;

            foreach (int r_neighbor in Mesh.R_Around_R(r, r_out))
            {
                if (_r_windPriority[r_neighbor] < _r_windPriority[r])
                {
                    count++;
                    sum += Humidity_R[r_neighbor];
                }
            }

            double humidity = 0.0;
            double rainfall = 0.0;

            if (count > 0)
            {
                humidity = sum / count;
                rainfall += PARAM_Raininess * humidity;
            }

            if (Mesh.Is_Boundary_R(r))
            {
                humidity = 1.0;
            }

            if (Elevation_R[r] < 0.0f)
            {
                double evaporation = PARAM_Evaporation * -Elevation_R[r];
                humidity += evaporation;
            }

            if (humidity > 1.0 - Elevation_R[r])
            {
                double orographicRainfall = PARAM_RainShadow * (humidity - (1.0 - Elevation_R[r]));
                rainfall += PARAM_Raininess * orographicRainfall;
                humidity -= orographicRainfall;
            }

            Rainfall_R[r] = (float)rainfall;
            Humidity_R[r] = (float)humidity;
        }
    }

    /**
     * Use prioritized graph exploration to assign river flow direction
     *
     * T_Order will be pre-order in which the graph was traversed, so
     * roots of the tree always get visited before leaves; use reverse to
     * visit leaves before roots
     */
    private void AssignDownslope()
    {
        /* Use a priority queue, starting with the ocean triangles and
         * moving upwards using elevation as the priority, to visit all
         * the land triangles */
        PriorityQueue<int, float> queue = new();
        int queue_in = 0;

        Array.Fill(S_Downslope_T, -999);

        /* Part 1: non-shallow ocean triangles get downslope assigned to the lowest neighbor */
        int[] s_out = new int[3];
        for (int t = 0; t < Mesh.NumTriangles; t++)
        {
            if (Elevation_T[t] < -0.1f)
            {
                int s_best = -1;
                float e_best = Elevation_T[t];

                foreach (int s in Mesh.S_Around_T(t, s_out))
                {
                    float e = Elevation_T[Mesh.T_Outer_S(s)];

                    if (e < e_best)
                    {
                        e_best = e;
                        s_best = s;
                    }
                }

                T_Order[queue_in++] = t;
                S_Downslope_T[t] = s_best;
                queue.Enqueue(t, Elevation_T[t]);
            }
        }

        /* Part 2: land triangles get visited in elevation priority */
        for (int queue_out = 0; queue_out < Mesh.NumTriangles; queue_out++)
        {
            int t_current = queue.Dequeue();

            for (int j = 0; j < 3; j++)
            {
                int s = 3 * t_current + j;
                int t_neighbor = Mesh.T_Outer_S(s); // uphill from t_current

                if (S_Downslope_T[t_neighbor] == -999)
                {
                    S_Downslope_T[t_neighbor] = Mesh.S_Opposite_S(s);
                    T_Order[queue_in++] = t_neighbor;
                    queue.Enqueue(t_neighbor, Elevation_T[t_neighbor]);
                }
            }
        }
    }

    private void AssignMoisture()
    {
        int numTriangles = Mesh.NumTriangles;

        for (int t = 0; t < numTriangles; t++)
        {
            float moisture = 0.0f;

            for (int i = 0; i < 3; i++)
            {
                int s = 3 * t + i;
                int r = Mesh.R_Begin_S(s);
                moisture += Rainfall_R[r] / 3;
            }

            Moisture_T[t] = moisture;
        }
    }

    private void AssignFlow()
    {
        Array.Fill(Flow_S, 0.0f);

        for (int t = 0; t < Mesh.NumTriangles; t++)
        {
            if (Elevation_T[t] >= 0.0f)
            {
                Flow_T[t] = (float)(PARAM_Flow * Moisture_T[t] * Moisture_T[t]);
            }
            else
            {
                Flow_T[t] = 0.0f;
            }
        }

        for (int i = T_Order.Length - 1; i >= 0; i--)
        {
            int t_tributary = T_Order[i];
            int s_flow = S_Downslope_T[t_tributary];

            if (s_flow >= 0)
            {
                int t_trunk = Mesh.T_Outer_S(s_flow);
                Flow_T[t_trunk] += Flow_T[t_tributary];
                Flow_S[s_flow] += Flow_T[t_tributary]; // TODO: might be redundant

                if (Elevation_T[t_trunk] > Elevation_T[t_tributary] && Elevation_T[t_tributary] >= 0.0f)
                {
                    Elevation_T[t_trunk] = Elevation_T[t_tributary];
                }
            }
        }
    }
}
