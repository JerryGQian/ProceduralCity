using System;
using System.Collections.Generic;
using System.Threading;
using DbscanImplementation;

namespace DbscanDemo
{
    public class Program
    {
        public static void Main()
        {
            var features = new MyFeatureDataSource().GetFeatureData();

            RunOfflineDbscan(features);
        }

        /// <summary>
        /// Most basic usage of Dbscan implementation, with no eventing and async mechanism
        /// </summary>
        /// <param name="features">Features provided</param>
        private static void RunOfflineDbscan(List<MyFeature> features)
        {
            var simpleDbscan = new DbscanAlgorithm<MyFeature>(EuclidienDistance);

            var result = simpleDbscan.ComputeClusterDbscan(allPoints: features.ToArray(),
                epsilon: .01, minimumPoints: 10);

            Console.WriteLine($"Noise: {result.Noise.Count}");

            Console.WriteLine($"# of Clusters: {result.Clusters.Count}");
        }

        /// <summary>
        /// Euclidien distance function
        /// </summary>
        /// <param name="feature1"></param>
        /// <param name="feature2"></param>
        /// <returns></returns>
        private static double EuclidienDistance(MyFeature feature1, MyFeature feature2)
        {
            return Math.Sqrt(
                    ((feature1.X - feature2.X) * (feature1.X - feature2.X)) +
                    ((feature1.Y - feature2.Y) * (feature1.Y - feature2.Y))
                );
        }

    }

}
