using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DbscanImplementation;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;

// Given patch of regions, generates highways
public class HighwayGenerator {
   public IMesh mesh;
   ArrayList patchDensityCenters;
   

   public HighwayGenerator(ArrayList patchDensityCenters) {
      this.patchDensityCenters = patchDensityCenters;
   }

   public void GenHighway() {

      // cluster nearby points
      List<Vector2> features = new List<Vector2>();// = new MyFeatureDataSource().GetFeatureData();
      for (int i = 0; i < patchDensityCenters.Count; i++) {
         features.Add((Vector2)patchDensityCenters[i]);
      }

      var result = RunOfflineDbscan(features);

      // build highway graph

      var points = new List<Vertex>();
      foreach (int i in result.Clusters.Keys) {
         points.Add(new Vertex(result.Clusters[i][0].Feature.x, result.Clusters[i][0].Feature.y));
         /*foreach (DbscanImplementation.DbscanPoint<Vector2> v in result.Clusters[i]) {
            points.Add(new Vertex(v.Feature.x, v.Feature.y));
         }*/
      }

      /*points.Add(new Vertex(0, 0));
      points.Add(new Vertex(0, 1));
      points.Add(new Vertex(1, 1));
      points.Add(new Vertex(1, 0));*/


      // Generate a default mesher.
      var mesher = new GenericMesher(new Dwyer());

      // Generate mesh.
      mesh = mesher.Triangulate(points);

      /*foreach (var t in mesh.Triangles) {
         Debug.Log("Triangle: " + t);
         t.
         // Get the 3 vertices of the triangle.
         for (int i = 0; i < 3; i++) {
            var v = (Vertex)t.GetVertex(i);
            Debug.Log(v.X + " " + v.Y);
            //Console.WriteLine(v.Z);
         }
      }*/
      /*foreach (var e in mesh.Edges) {
         Debug.Log(e.P0 + " " + e.P1);
      }*/
      int n = 0;
      /*foreach (var v in mesh.Vertices) {
         Debug.Log("vert: " + n + " : " + v.X + " " + v.Y);
         n++;
      }*/
      

   }

   private static DbscanResult<Vector2> RunOfflineDbscan(List<Vector2> features) {
      var simpleDbscan = new DbscanAlgorithm<Vector2>(EuclideanDistance);

      var result = simpleDbscan.ComputeClusterDbscan(allPoints: features.ToArray(),
          epsilon: 40, minimumPoints: 1);

      //Debug.Log("Noise: " + result.Noise.Count);
      //Debug.Log("# of Clusters: " + result.Clusters.Count);
      foreach (int i in result.Clusters.Keys) {
         //Debug.Log(result.Clusters[i].Count);
         string s = "";
         foreach (DbscanImplementation.DbscanPoint<Vector2> v in result.Clusters[i]) {
            s += v.Feature + ", ";
         }
         //Debug.Log(s);
      }

      return result;
   }

   private static double EuclideanDistance(Vector2 feature1, Vector2 feature2) {
      return (feature1 - feature2).magnitude;
   }
}
