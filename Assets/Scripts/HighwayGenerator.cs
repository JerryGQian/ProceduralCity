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
   public ArrayList highways;

   public IMesh mesh;
   public ArrayList edges;
   public ArrayList vertices;
   public Dictionary<Vertex, List<Vertex>> neighbors;
   public Vector2Int regionIdx;

   ArrayList patchDensityCenters;

   private float costLimit = 240f;
   private float vertexReward = 30f;
   private float vertexRewardRatio = 0.35f; //ratio of euclidean distance
   private float densityReward = 20f;

   private Pathfinding pathfinding = new Pathfinding();

   public HighwayGenerator(ArrayList patchDensityCenters, Vector2Int regionIdx) {
      this.patchDensityCenters = patchDensityCenters;
      highways = new ArrayList();
      edges = new ArrayList();
      vertices = new ArrayList();
      neighbors = new Dictionary<Vertex, List<Vertex>>();
      this.regionIdx = regionIdx;
   }

   //public void GenHighway() {
   public IEnumerator GenHighwayCoroutine() {
      WorldManager.GenHighwayState = true;
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
         (Vector2, Vector2) tup1 = (Util.VertexToVector2(v0), Util.VertexToVector2(v1));
         (Vector2, Vector2) tup2 = (Util.VertexToVector2(v1), Util.VertexToVector2(v0));

         if (!InPatchBounds(regionIdx, tup1.Item1, tup1.Item2)) {
            RemoveEdge(e);
            continue;
         }

         if (!WorldManager.edgeState.ContainsKey(tup1)) { //needs removal check
            if (ShouldRemoveEdge(e, densityLookup)) {
               // remove edge for first time
               WorldManager.edgeState[tup1] = false;
               WorldManager.edgeState[tup2] = false;
               RemoveEdge(e);
            }
            else {
               // keep edge, register with edgeState
               WorldManager.edgeState[tup1] = true;
               WorldManager.edgeState[tup2] = true;
            }
         }
         else { // remove if removed before
            if (!WorldManager.edgeState[tup1]) {
               RemoveEdge(e);
            }
         }
      }

      // Generate final highway segments for each edge
      // Uses A* search to pathfind
      int hwCount = 0;
      foreach (Edge e in edges) {
         hwCount++;
         (Vector2Int, Vector2Int) eVec = (Util.VertexToVector2Int((Vertex)vertices[e.P0]), Util.VertexToVector2Int((Vertex)vertices[e.P1]));
         Vertex v0 = (Vertex)vertices[e.P0];
         Vertex v1 = (Vertex)vertices[e.P1];

         // Skip pathfinding for edges that have been generated/built already
         if (WorldBuilder.builtHighways.ContainsKey((Util.VertexToVector2(v0), Util.VertexToVector2(v0))) &&
            WorldBuilder.builtHighways[(Util.VertexToVector2(v0), Util.VertexToVector2(v0))]) {
            //Debug.Log("Aborting pathfind since already built!");
            continue;
         }

         // A* pathfind from v0 to v1
         ArrayList segments = pathfinding.FindPath(Util.VertexToVector2(v0), Util.VertexToVector2(v1));

         // Removal of redundant paths
         int firstIdx = -1, secondIdx = -1;
         List<(Vector2, (Vector2Int, Vector2Int))> vertListFirst = null, vertListSecond = null;
         // traverse from beginning
         for (int i = 0; i < segments.Count - 1; i++) {
            Vector2 v = (Vector2)segments[i];

            if (WorldBuilder.DoesChunkContainHighway(v)) {
               firstIdx = i;
               vertListFirst = WorldBuilder.GetHighwayVertList(v);
               break;
            }
         }
         // traverse from end
         if (firstIdx >= 0) {
            for (int i = segments.Count - 1; i > 0; i--) {
               Vector2 v = (Vector2)segments[i];
               if (WorldBuilder.DoesChunkContainHighway(v)) {
                  secondIdx = i;
                  vertListSecond = WorldBuilder.GetHighwayVertList(v);
                  break;
               }
            }
         }

         // segment join logic
         if (vertListFirst != null && vertListSecond != null && firstIdx >= 0 && secondIdx >= 0) {
            // Direct connection with existing path
            bool done = false;
            // *--|____/*
            foreach ((Vector2, (Vector2Int, Vector2Int)) tup in vertListFirst) {
               if (eVec.Item2 == tup.Item2.Item2 || eVec.Item2 == tup.Item2.Item1) {
                  segments.RemoveRange(firstIdx, segments.Count - firstIdx);
                  segments.Insert(firstIdx, tup.Item1);
                  done = true;
                  break;
               }
            }
            if (!done) {
               // *\____|--*
               foreach ((Vector2, (Vector2Int, Vector2Int)) tup in vertListSecond) {
                  if (eVec.Item1 == tup.Item2.Item2 || eVec.Item1 == tup.Item2.Item1) {
                     segments.RemoveRange(0, secondIdx);

                     segments.Insert(0, tup.Item1);
                     done = true;
                     break;
                  }
               }
            }

            if (!done) {
               // No direct connection cases (needs paths from start and end)
               (bool, (Vector2Int, Vector2Int)) sameEdgeRes = DoListsContainSameEdge(vertListFirst, vertListSecond);
               if (sameEdgeRes.Item1) {
                  Vector2 con1 = FindVecWithEdge(sameEdgeRes.Item2, vertListFirst), con2 = FindVecWithEdge(sameEdgeRes.Item2, vertListSecond);
                  if (con1 == con2) { // same vert
                                      // *--|-----* pass through path
                     segments.RemoveAt(firstIdx);
                     segments.Insert(firstIdx, con1);
                  }
                  else { // diff vert
                         // *--|__|--* join at existing path
                         //Debug.Log(firstIdx + " " + secondIdx);
                     segments.RemoveRange(firstIdx, secondIdx - firstIdx + 1);
                     segments.Insert(firstIdx, con2);
                     segments.Insert(firstIdx, WorldBuilder.SignalVector);
                     segments.Insert(firstIdx, con1);
                  }
               }
               else {// not matched so edges different!
                  Vector2 con1 = vertListFirst[0].Item1, con2 = vertListSecond[0].Item1;
                  //neighbor or not
                  if (DoListsContainNeighbors(vertListFirst, vertListSecond)) {
                     // *--\./---*
                     segments.RemoveRange(firstIdx, secondIdx - firstIdx + 1);
                     segments.Insert(firstIdx, con2);
                     segments.Insert(firstIdx, WorldBuilder.SignalVector);
                     segments.Insert(firstIdx, con1);

                  }
                  else {
                     // *--|--|--* pass through paths
                     segments.RemoveAt(secondIdx);
                     segments.RemoveAt(firstIdx);
                     segments.Insert(firstIdx, con1);
                     segments.Insert(secondIdx, con2);
                  }
               }
            }
         }

         foreach (Vector2 v in segments) {
            WorldBuilder.AddHighwayVertToChunkHash(v, eVec);
         }

         if (segments != null) highways.Add(segments);

         if (hwCount % Settings.hwPathfindingIncrement == 0)
            yield return null;
      }
      WorldManager.GenHighwayState = false;
   }

   private void RemoveEdge(Edge e) {
      for (int i = 0; i < edges.Count; i++) {
         if (IsSameEdge((Edge)edges[i], e)) {
            edges.RemoveAt(i);
         }
      }
   }

   private bool DoListsContainNeighbors(List<(Vector2, (Vector2Int, Vector2Int))> list1, List<(Vector2, (Vector2Int, Vector2Int))> list2) {
      // build edge matching hashset
      HashSet<Vector2> vertSet = new HashSet<Vector2>();
      foreach ((Vector2, (Vector2Int, Vector2Int)) pair in list1) {
         vertSet.Add(pair.Item2.Item1);
         vertSet.Add(pair.Item2.Item2);
      }
      foreach ((Vector2, (Vector2Int, Vector2Int)) pair in list2) {
         if (vertSet.Contains(pair.Item2.Item1) || vertSet.Contains(pair.Item2.Item2)) {
            return true;
         }
      }

      return false;
   }

   private (bool, (Vector2Int, Vector2Int)) DoListsContainSameEdge(List<(Vector2, (Vector2Int, Vector2Int))> list1, List<(Vector2, (Vector2Int, Vector2Int))> list2) {
      // build edge matching hashset
      HashSet<(Vector2Int, Vector2Int)> edgeSet = new HashSet<(Vector2Int, Vector2Int)>();
      foreach ((Vector2, (Vector2Int, Vector2Int)) pair in list1) {
         edgeSet.Add(pair.Item2);
         edgeSet.Add((pair.Item2.Item2, pair.Item2.Item1));
      }
      // find matching edge (if any)
      foreach ((Vector2, (Vector2Int, Vector2Int)) pair in list2) {
         if (edgeSet.Contains(pair.Item2)) { // found same edge
            return (true, pair.Item2);
         }
      }
      return (false, (Vector2Int.zero, Vector2Int.zero));
   }

   private Vector2 FindVecWithEdge((Vector2Int, Vector2Int) edge, List<(Vector2, (Vector2Int, Vector2Int))> list) {
      foreach ((Vector2, (Vector2Int, Vector2Int)) pair in list) {
         if (Util.SameEdge(edge, pair.Item2)) {
            return pair.Item1;
         }
      }
      return Vector2.zero;
   }

   public static bool InPatchBounds(Vector2Int regionIdx, Vector2 P0, Vector2 P1) {
      for (int i = -3; i <= 3; i++) {
         for (int j = -3; j <= 3; j++) {
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
      float dist = Util.VertexDistance(v0, v1);

      Queue<(Vertex, float)> queue = new Queue<(Vertex, float)>();
      HashSet<Vertex> visited = new HashSet<Vertex>();
      visited.Add(v0);
      foreach (Vertex v0n in neighbors[v0]) {
         if (!Util.IsSameVertex(v0n, v1)) {
            queue.Enqueue((v0n, Util.VertexDistance(v0, v0n) - (vertexRewardRatio * dist) - vertexReward - densityReward * densityLookup[new Vector2((float)v0n.X, (float)v0n.Y)]));
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
            float newCost = cost + Util.VertexDistance(v, vn) - (vertexRewardRatio * dist) - vertexReward - densityReward * densityLookup[new Vector2((float)vn.X, (float)vn.Y)];
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

      return Util.IsSameVertex(e0v0, e1v0) && Util.IsSameVertex(e0v1, e1v1);  //e0v0.X == e1v0.X && e0v0.Y == e1v0.Y && e0v1.X == e1v1.X && e0v1.Y == e1v1.Y;
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
