using System;

namespace DQB2IslandEditor.DataPK
{
    //Settings for one mountain. Percentages are 0-100.
    public record MountainParams(
        byte BaseLayer,          //Bottom of the mountain; nothing is placed at or below this height.
        byte MaxLayer,           //Height of the main plateau.
        int CoveragePercent,     //How much of the selection the base spreads over.
        int PlateauPercent,      //How wide the flat tops are, relative to the selection.
        bool SecondPlateau,      //Add a lower second plateau beside the main one.
        int SecondHeightPercent, //Height of the second plateau, between BaseLayer and MaxLayer.
        int RoughnessPercent,    //How lumpy the outline and slopes are.
        int Seed);

    //Builds a heightmap for a mountain with flat plateau tops over a rectangle of blocks.
    //The mountain is made of "blobs": each is flat out to its plateau radius, then slopes down in an
    //S-curve to its base radius. Each column takes the highest blob. Perlin noise is added to every
    //blob's distance before it's cut off, so the outline and plateau edges come out lumpy.
    //Positions are -1 to 1 across the selection on each axis, so blobs stretch to fit its shape.
    public static class MountainGenerator
    {
        private const double SHAPE_POWER = 2.5;    //Superellipse exponent: 2 is an ellipse, higher is squarer.
        private const double OUTLINE_NOISE = 0.45; //Max change to the outline at 100% roughness, in -1..1 units.
        private const double SLOPE_BUMPS = 4;      //Max layers of bumps on the slopes at 100% roughness.
        private const double EDGE_SLOPE = 3.0;     //Layers per block the mountain may rise from the selection edge.

        //With two plateaus: how far each sits from the middle along the long axis, and the low base under them.
        private const double PLATEAU_OFFSET = 0.45;
        private const double SECOND_SIZE = 0.8;     //Second plateau's size relative to the main one.
        private const double BASE_HEIGHT = 0.35;    //Height of the shared base, as a fraction of the main plateau.

        private record Blob(double CenterX, double CenterZ, double PlateauRadius, double BaseRadius, double Height);

        //Top layer of the mountain for every column, or -1 where the column stays untouched.
        public static int[,] HeightMap(int width, int depth, MountainParams p)
        {
            var random = new Random(p.Seed);
            var outline = new PerlinNoise(random);
            var bumps = new PerlinNoise(random);

            double coverage = p.CoveragePercent / 100.0;
            double plateau = Math.Clamp(p.PlateauPercent / 100.0, 0.02, 0.9) * coverage;
            double roughness = p.RoughnessPercent / 100.0;
            double range = p.MaxLayer - p.BaseLayer;

            Blob[] blobs;
            if (p.SecondPlateau)
            {
                //Plateaus at either end of the long axis (tilted a little), with a saddle between them,
                //on a wide low base so the whole selection is still covered.
                double tilt = (random.NextDouble() - 0.5) * 0.6;
                double side = random.Next(2) == 0 ? 1 : -1; //Which end gets the main plateau.
                double ax = width >= depth ? Math.Cos(tilt) : Math.Sin(tilt);
                double az = width >= depth ? Math.Sin(tilt) : Math.Cos(tilt);
                ax *= side * PLATEAU_OFFSET; az *= side * PLATEAU_OFFSET;
                double mainPlateau = plateau * 0.6, secondPlateau = mainPlateau * SECOND_SIZE;
                double mainBase = coverage * 0.6, secondBase = mainBase * SECOND_SIZE;
                blobs = new[]
                {
                    new Blob(0, 0, 0, coverage, p.BaseLayer + range * BASE_HEIGHT),
                    new Blob(ax, az, mainPlateau, mainBase, p.MaxLayer),
                    new Blob(-ax, -az, secondPlateau, secondBase, p.BaseLayer + range * p.SecondHeightPercent / 100.0),
                };
            }
            else blobs = new[] { new Blob(0, 0, plateau, coverage, p.MaxLayer) };

            //Noise wavelengths in blocks, so features stay the same size however big the selection is.
            double outlineScale = 1.0 / 24, bumpScale = 1.0 / 10;
            //Noise is in -1..1 units, which are wider on the long side; scale it to the short side.
            double unitsPerBlock = 2.0 / Math.Min(width, depth);
            double outlineAmount = roughness * OUTLINE_NOISE * Math.Min(1, unitsPerBlock * 48);

            var heights = new int[width, depth];
            for (int x = 0; x < width; x++)
                for (int z = 0; z < depth; z++)
                {
                    double u = (x + 0.5) / width * 2 - 1;
                    double v = (z + 0.5) / depth * 2 - 1;
                    double lump = outlineAmount * outline.Fractal(x * outlineScale, z * outlineScale, 4);
                    double bump = roughness * SLOPE_BUMPS * bumps.Fractal(x * bumpScale, z * bumpScale, 3);

                    double height = p.BaseLayer;
                    foreach (var blob in blobs)
                    {
                        double d = Distance(u, v, blob.CenterX, blob.CenterZ) - lump;
                        //0 at the base radius, 1 at the plateau edge and inside it.
                        double t = Math.Clamp((blob.BaseRadius - d) / (blob.BaseRadius - blob.PlateauRadius), 0, 1);
                        double slope = 0.5 - 0.5 * Math.Cos(Math.PI * t);
                        //Bumps fade out at the foot and on the plateau, so the tops stay flat.
                        double h = p.BaseLayer + slope * (blob.Height - p.BaseLayer) + bump * Math.Sin(Math.PI * t);
                        height = Math.Max(height, h);
                    }

                    //Never build a wall at the edge of the selection.
                    int edge = Math.Min(Math.Min(x, width - 1 - x), Math.Min(z, depth - 1 - z));
                    height = Math.Min(height, p.BaseLayer + edge * EDGE_SLOPE);

                    int top = (int)Math.Round(Math.Min(height, p.MaxLayer));
                    heights[x, z] = top > p.BaseLayer ? top : -1;
                }
            return heights;
        }

        //Rounded distance from a center, scaled per side so that 1 is a superellipse touching the
        //middle of each edge of the [-1,1] square, wherever the center is.
        private static double Distance(double u, double v, double cx, double cz)
        {
            double du = (u - cx) / (u >= cx ? 1 - cx : 1 + cx);
            double dv = (v - cz) / (v >= cz ? 1 - cz : 1 + cz);
            return Math.Pow(Math.Pow(Math.Abs(du), SHAPE_POWER) + Math.Pow(Math.Abs(dv), SHAPE_POWER), 1 / SHAPE_POWER);
        }

        //Classic 2D Perlin noise, roughly -1 to 1.
        private class PerlinNoise
        {
            private readonly int[] perm = new int[512];

            public PerlinNoise(Random random)
            {
                var p = new int[256];
                for (int i = 0; i < 256; i++) p[i] = i;
                random.Shuffle(p);
                for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
            }

            //Octaves layered for a natural look.
            public double Fractal(double x, double z, int octaves)
            {
                double sum = 0, amplitude = 1, total = 0;
                for (int octave = 0; octave < octaves; octave++)
                {
                    sum += Sample(x, z) * amplitude;
                    total += amplitude;
                    x *= 2; z *= 2; amplitude *= 0.5;
                }
                return sum / total * 1.6; //Layered noise rarely reaches the extremes; stretch it back out.
            }

            private double Sample(double x, double z)
            {
                int xi = (int)Math.Floor(x), zi = (int)Math.Floor(z);
                double xf = x - xi, zf = z - zi;
                xi &= 255; zi &= 255;
                double u = Fade(xf), v = Fade(zf);
                int aa = perm[perm[xi] + zi], ab = perm[perm[xi] + zi + 1];
                int ba = perm[perm[xi + 1] + zi], bb = perm[perm[xi + 1] + zi + 1];
                double top = Lerp(Gradient(aa, xf, zf), Gradient(ba, xf - 1, zf), u);
                double bottom = Lerp(Gradient(ab, xf, zf - 1), Gradient(bb, xf - 1, zf - 1), u);
                return Lerp(top, bottom, v);
            }

            //Dot product with one of 8 gradient directions.
            private static double Gradient(int hash, double x, double z)
            {
                switch (hash & 7)
                {
                    case 0: return x + z;
                    case 1: return -x + z;
                    case 2: return x - z;
                    case 3: return -x - z;
                    case 4: return x;
                    case 5: return -x;
                    case 6: return z;
                    default: return -z;
                }
            }

            private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
            private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        }
    }
}
