using System;
using System.Collections.Generic;
using System.Linq;

namespace SoftwareCamera
{
    public class ValueRange
    {
        public double Min { get; }
        public double Max { get; }

        public ValueRange(double min, double max)
        {
            if (min > max)
                throw new ArgumentException($"Min ({min}) can't be greater than max ({max}).");

            Min = min;
            Max = max;
        }

        public bool Contains(double value) => value >= Min && value <= Max;

        public bool Overlaps(ValueRange other) => Min <= other.Max && other.Min <= Max;
    }

    public class CameraSpec
    {
        public ValueRange Distance { get; }
        public ValueRange Light { get; }

        public CameraSpec(ValueRange distance, ValueRange light)
        {
            Distance = distance ?? throw new ArgumentNullException(nameof(distance));
            Light = light ?? throw new ArgumentNullException(nameof(light));
        }

        public bool CanHandle(double distance, double light)
        {
            return Distance.Contains(distance) && Light.Contains(light);
        }

        public bool Overlaps(CameraSpec other)
        {
            return Distance.Overlaps(other.Distance) && Light.Overlaps(other.Light);
        }
    }

    public static class CameraCoverage
    {
        // Splits the desired area into cells along every camera edge and then checks that the center of each cell is covered by some camera
        public static bool CamerasSuffice(CameraSpec desired, List<CameraSpec> cameras)
        {
            if (desired == null)
                throw new ArgumentNullException(nameof(desired));
            if (cameras == null)
                throw new ArgumentNullException(nameof(cameras));

            // Cameras outside the desired area can't help, so just skip em
            List<CameraSpec> useful = cameras.Where(c => c.Overlaps(desired)).ToList();
            if (useful.Count == 0)
                return false;

            List<double> distancePoints = GetSamplePoints(desired.Distance, useful.Select(c => c.Distance));
            List<double> lightPoints = GetSamplePoints(desired.Light, useful.Select(c => c.Light));

            foreach (double distance in distancePoints)
            {
                foreach (double light in lightPoints)
                {
                    bool covered = useful.Any(c => c.CanHandle(distance, light));
                    if (!covered)
                        return false;
                }
            }

            return true;
        }

        // Returns the positions of cameras that don't overlap the desired area at all
        public static List<int> FindUnusedCameras(CameraSpec desired, List<CameraSpec> cameras)
        {
            if (desired == null)
                throw new ArgumentNullException(nameof(desired));
            if (cameras == null)
                throw new ArgumentNullException(nameof(cameras));

            var unused = new List<int>();
            for (int i = 0; i < cameras.Count; i++)
            {
                if (!cameras[i].Overlaps(desired))
                    unused.Add(i);
            }

            return unused;
        }

        // Returns the midpoint between each pair of neighboring edges on one axis
        private static List<double> GetSamplePoints(ValueRange target, IEnumerable<ValueRange> cameraRanges)
        {
            // Single value, nothing to split up
            if (target.Min == target.Max)
                return new List<double> { target.Min };

            var boundaries = new List<double> { target.Min, target.Max };

            // Edges outside the target don't matter
            foreach (ValueRange range in cameraRanges)
            {
                if (target.Contains(range.Min))
                    boundaries.Add(range.Min);
                if (target.Contains(range.Max))
                    boundaries.Add(range.Max);
            }

            boundaries = boundaries.Distinct().OrderBy(b => b).ToList();

            var midpoints = new List<double>();
            for (int i = 0; i < boundaries.Count - 1; i++)
            {
                midpoints.Add((boundaries[i] + boundaries[i + 1]) / 2);
            }

            return midpoints;
        }
    }

    public class Program
    {
        public static void Main()
        {
            var desired = Camera(1, 10, 1, 5);

            Check("Three cameras cover it together", true,
                desired,
                Camera(1, 6, 1, 5),
                Camera(5, 10, 1, 3),
                Camera(5, 10, 3, 5));

            Check("Missing far distance + bright light", false,
                desired,
                Camera(1, 6, 1, 5),
                Camera(5, 10, 1, 3));

            Check("Cameras meet exactly at an edge", true,
                desired,
                Camera(1, 5, 1, 5),
                Camera(5, 10, 1, 5));

            Check("Tiny gap between cameras", false,
                desired,
                Camera(1, 4.99, 1, 5),
                Camera(5, 10, 1, 5));

            CheckUnused("Camera outside the range is reported", new List<int> { 1 },
                desired,
                Camera(1, 10, 1, 5),
                Camera(20, 30, 1, 5));
        }

        private static CameraSpec Camera(double minDist, double maxDist, double minLight, double maxLight)
        {
            return new CameraSpec(new ValueRange(minDist, maxDist), new ValueRange(minLight, maxLight));
        }

        private static void Check(string name, bool expected, CameraSpec desired, params CameraSpec[] cameras)
        {
            bool actual = CameraCoverage.CamerasSuffice(desired, cameras.ToList());
            string result = actual == expected ? "PASS" : "FAIL";
            Console.WriteLine($"{result}  {name} (expected {expected}, got {actual})");
        }

        private static void CheckUnused(string name, List<int> expected, CameraSpec desired, params CameraSpec[] cameras)
        {
            List<int> actual = CameraCoverage.FindUnusedCameras(desired, cameras.ToList());
            string result = actual.SequenceEqual(expected) ? "PASS" : "FAIL";
            Console.WriteLine($"{result}  {name} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");
        }
    }
}
