using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DbscanImplementation;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;
using TriangleNet.Tools;

// Given patch of regions, generates highways
public class HighwayGenerator {
   public ArrayList highwaySegments;
   public IMesh mesh;
   public ArrayList edges;
   public ArrayList vertices;
   public Dictionary<Vertex, List<Vertex>> neighbors;
   //private HashSet<Vertex> visited;
   //public Dictionary<Vertex, List<Vertex>> neighbors = new Dictionary<Vertex, List<Vertex>>();
   ArrayList patchDensityCenters;

   private float costLimit = 240f;
   private float vertexReward = 20f;
   private float vertexRewardRatio = 0.35f; //ratio of euclidean distance
   private float densityReward = 25f;

   private Pathfinding pathfinding = new Pathfinding();


   public HighwayGenerator(ArrayList patchDensityCenters) {
      this.patchDensityCenters = patchDensityCenters;
      highwaySegments = new ArrayList();
      edges = new ArrayList();
      vertices = new ArrayList();
      neighbors = new Dictionary<Vertex, List<Vertex>>();
   }

   public void GenHighway() {
      Dictionary<Vector2, float> densityLookup = new Dictionary<Vector2, float>();

      // cluster nearby points with DBScan
      List<Vector2> features = new List<Vector2>();// = new MyFeatureDataSource().GetFeatureData();
      for (int i = 0; i < patchDensityCenters.Count; i++) {
         (Vector2, float) cd = ((Vector2, float))patchDensityCenters[i];
         features.Add(cd.Item1);
         densityLookup[cd.Item1] = cd.Item2;
      }

      var result = RunOfflineDbscan(features);

      // Build highway graph

      var points = new List<Vertex>();
      foreach (int i in result.Clusters.Keys) {
         points.Add(new Vertex(result.Clusters[i][0].Feature.x, result.Clusters[i][0].Feature.y));
      }

      // Generate a default mesher
      var mesher = new GenericMesher(new Dwyer());

      // Generate mesh (Delaunay Triangulation)
      mesh = mesher.Triangulate(points);

      // Init edge/vertex lists for mutation
      foreach (Vertex v in mesh.Vertices) {
         vertices.Add(v);
      }
      foreach (Edge e in mesh.Edges) {
         //Debug.Log(e.P0 + " " + e.P1);
         edges.Add(e);

         // build neighbor map
         Vertex v0 = (Vertex)vertices[e.P0];
         Vertex v1 = (Vertex)vertices[e.P1];
         if (!neighbors.ContainsKey(v0)) {
            neighbors[v0] = new List<Vertex>();
         }
         neighbors[v0].Add(v1);
      }

      // Remove unecessary edges on cost basis
      foreach (Edge e in mesh.Edges) {
         Vertex v0 = (Vertex)vertices[e.P0];
         Vertex v1 = (Vertex)vertices[e.P1];
         //Debug.Log(e.P0 + " " + e.P1);
         (Vector2, Vector2) tup1 = (VertexToVector2(v0), VertexToVector2(v1));
         (Vector2, Vector2) tup2 = (VertexToVector2(v1), VertexToVector2(v0));
         if (!WorldManager.edgeState.ContainsKey(tup1)) { //needs removal check
            if (ShouldRemoveEdge(e, densityLookup)) {
               WorldManager.edgeState[tup1] = false;
               WorldManager.edgeState[tup2] = false;
               for (int i = 0; i < edges.Count; i++) {
                  if (IsSameEdge((Edge)edges[i], e)) {
                     edges.RemoveAt(i);
                  }
               }
               //Debug.Log("Removing edge: " + ((Vertex)vertices[e.P0]).X + "," + ((Vertex)vertices[e.P0]).Y + " to " + ((Vertex)vertices[e.P1]).X + "," + ((Vertex)vertices[e.P1]).Y);
            }
            else {
               WorldManager.edgeState[tup1] = true;
               WorldManager.edgeState[tup2] = true;
            }
         }
         else { // remove if removed before
            if (!WorldManager.edgeState[tup1]) {
               for (int i = 0; i < edges.Count; i++) {
                  if (IsSameEdge((Edge)edges[i], e)) {
                     edges.RemoveAt(i);
                  }
               }
               //Debug.Log("Removing edge again: " + ((Vertex)vertices[e.P0]).X + "," + ((Vertex)vertices[e.P0]).Y + " to " + ((Vertex)vertices[e.P1]).X + "," + ((Vertex)vertices[e.P1]).Y);
            }
         }
      }

      // Generate final highway segments for each edge
      // Uses A* search to pathfind
      foreach (Edge e in edges) {
         Vertex v0 = (Vertex)vertices[e.P0];
         Vertex v1 = (Vertex)vertices[e.P1];

         // A* pathfind from v0 to v1
         List<Vector2> path = pathfinding.FindPath(VertexToVector2(v0), VertexToVector2(v1));
         if (path != null) Debug.Log("Path: " + path.Count);
      }
   }

   private bool InPatchBounds(Vector2Int regionIdx, Vector2 P0, Vector2 P1) {
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            if (WorldManager.regions[regionIdx + new Vector2Int(i, j)].bounds.InBounds(P0) || WorldManager.regions[regionIdx + new Vector2Int(i, j)].bounds.InBounds(P1)) {
               return true;
            }
         }
      }
      return false;
   }

   private bool ShouldRemoveEdge(Edge e, Dictionary<Vector2, float> densityLookup) {
      Vertex v0 = (Vertex)vertices[e.P0];
      Vertex v1 = (Vertex)vertices[e.P1];
      float dist = VertexDistance(v0, v1);

      Queue<(Vertex, float)> queue = new Queue<(Vertex, float)>();
      HashSet<Vertex> visited = new HashSet<Vertex>();
      visited.Add(v0);
      foreach (Vertex v0n in neighbors[v0]) {
         if (!IsSameVertex(v0n, v1)) {
            queue.Enqueue((v0n, VertexDistance(v0, v0n) - (vertexRewardRatio * dist) - vertexReward - densityReward * densityLookup[new Vector2((float)v0n.X, (float)v0n.Y)]));
         }
      }
      while (queue.Count > 0) {
         (Vertex, float) tup = queue.Dequeue();
         Vertex v = tup.Item1;
         float cost = tup.Item2;
         visited.Add(v);

         if (v1.X == v.X && v1.Y == v.Y) {
            //Debug.Log("(" + ((Vertex)vertices[e.P0]).X + "," + ((Vertex)vertices[e.P0]).Y + ") to (" + ((Vertex)vertices[e.P1]).X + "," + ((Vertex)vertices[e.P1]).Y + ") Cost: " + cost + " dist: " + dist + " ratioReward" + (vertexRewardRatio * dist));
            if (cost <= dist) {
               return true;
            }
         }

         foreach (Vertex vn in neighbors[v]) {
            float newCost = cost + VertexDistance(v, vn) - (vertexRewardRatio * dist) - vertexReward - densityReward * densityLookup[new Vector2((float)vn.X, (float)vn.Y)];
            if (newCost <= costLimit && !visited.Contains(vn)) {
               queue.Enqueue((vn, newCost));
            }
         }
      }

      return false;
   }

   private bool IsSameEdge(Edge e0, Edge e1) {
      Vertex e0v0 = (Vertex)vertices[e0.P0];
      Vertex e0v1 = (Vertex)vertices[e0.P1];
      Vertex e1v0 = (Vertex)vertices[e1.P0];
      Vertex e1v1 = (Vertex)vertices[e1.P1];

      return IsSameVertex(e0v0, e1v0) && IsSameVertex(e0v1, e1v1);  //e0v0.X == e1v0.X && e0v0.Y == e1v0.Y && e0v1.X == e1v1.X && e0v1.Y == e1v1.Y;
   }

   private bool IsSameVertex(Vertex v0, Vertex v1) {
      return v0.X == v1.X && v0.Y == v1.Y;
   }

   private float VertexDistance(Vertex v0, Vertex v1) {
      return (new Vector2((float)v0.X, (float)v0.Y) - new Vector2((float)v1.X, (float)v1.Y)).magnitude;
   }

   private Vector2 VertexToVector2(Vertex vert) {
      return new Vector2((float)vert.X, (float)vert.Y);
   }

   private static DbscanResult<Vector2> RunOfflineDbscan(List<Vector2> features) {
      var simpleDbscan = new DbscanAlgorithm<Vector2>(EuclideanDistance);

      var result = simpleDbscan.ComputeClusterDbscan(allPoints: features.ToArray(),
          epsilon: 40, minimumPoints: 1);

      foreach (int i in result.Clusters.Keys) {
         string s = "";
         foreach (DbscanImplementation.DbscanPoint<Vector2> v in result.Clusters[i]) {
            s += v.Feature + ", ";
         }
      }

      return result;
   }

   private static double EuclideanDistance(Vector2 feature1, Vector2 feature2) {
      return (feature1 - feature2).magnitude;
   }
}
