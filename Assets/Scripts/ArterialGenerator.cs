using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;

public class ArterialGenerator {
   public Vector2Int regionIdx;
   public ArrayList arterialPoints;
   public Dictionary<Vector2Int, ArrayList> arterialPointsByRegion;
   public int initSpacingInChunks = 5;

   private ArrayList patchHighways;
   private Bounds bounds;
   private Bounds innerPatchBounds;
   private Bounds outerPatchBounds;
   private Dictionary<Vector2Int, float> patchDensitySnapshotsMap; // chunkIdx -> density
   private Dictionary<Vector2Int, CoordRandom> rands; // regionIdx -> CoordRandom

   public CoordRandom rand;

   public IMesh mesh;
   public ArrayList edges;
   public ArrayList vertices;
   public Dictionary<Vertex, List<Vertex>> neighbors;

   private float costLimit = 100f;
   private float vertexReward = 16f;
   private float vertexRewardRatio = 0.35f; //ratio of euclidean distance

   private Pathfinding pathfinding = new Pathfinding();

   public ArterialGenerator(Dictionary<Vector2Int, float> patchDensitySnapshotsMap, Bounds bounds, Vector2Int regionIdx) {
      this.regionIdx = regionIdx;
      arterialPoints = new ArrayList();
      arterialPointsByRegion = new Dictionary<Vector2Int, ArrayList>();
      this.patchHighways = new ArrayList();

      this.bounds = bounds;
      float regionSize = WorldManager.regionDim * WorldManager.chunkSize;
      //innerPatchBounds = bounds;
      //outerPatchBounds = new Bounds(3 * regionSize, bounds.xMin - 1 * regionSize, bounds.zMin - 1 * regionSize);
      innerPatchBounds = new Bounds(3 * regionSize, bounds.xMin - regionSize, bounds.zMin - regionSize);
      outerPatchBounds = new Bounds(5 * regionSize, bounds.xMin - 2 * regionSize, bounds.zMin - 2 * regionSize);

      this.patchDensitySnapshotsMap = patchDensitySnapshotsMap;
      rands = new Dictionary<Vector2Int, CoordRandom>();
      //rand = new CoordRandom(regionIdx);

      edges = new ArrayList();
      vertices = new ArrayList();
      neighbors = new Dictionary<Vertex, List<Vertex>>();
   }

   public void GenArterialLayout() {
      // gather all highways from patch together
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            Region region;
            Vector2Int thisIdx = regionIdx + new Vector2Int(i, j);
            if (WorldManager.regions.ContainsKey(thisIdx)) {
               region = WorldManager.regions[thisIdx];
               if (region != null && region.hwg != null) {
                  patchHighways.AddRange(region.hwg.highways);
               }
            }
         }
      }

      // calculate arterial points
      ArrayList initialPoints = new ArrayList();
      for (int x = (int)outerPatchBounds.xMin + WorldManager.chunkSize / 2; x < outerPatchBounds.xMax; x += initSpacingInChunks * WorldManager.chunkSize) {
         for (int y = (int)outerPatchBounds.zMin + WorldManager.chunkSize / 2; y < outerPatchBounds.zMax; y += initSpacingInChunks * WorldManager.chunkSize) {
            Vector2 point = new Vector2(x, y);
            initialPoints.Add(point);

            // adjustment away from highways
            point += CalcPointAdjustment(point);
            Vector2Int regionIdx = Util.W2R(point);

            // add randomness
            CoordRandom r;
            if (rands.ContainsKey(regionIdx)) {
               r = rands[regionIdx];
            }
            else {
               r = new CoordRandom(regionIdx);
            }
            point += Hp.arterialRandomness * r.NextVector2(-10, 10);

            if (ShouldGenPoint((int)point.x, (int)point.y)) {
               arterialPoints.Add(point);
            }
         }
      }

      // add highway exits
      foreach (ArrayList segments in patchHighways) {
         for (int i = 5; i < segments.Count-2; i++) {
            Vector2 v = (Vector2)segments[i];
            if (innerPatchBounds.InBounds(v) && i % 5 == 0) {
               //Debug.Log("Added hw point " + v);
               arterialPoints.Add(v);
            }
         }
      }

      // add randomness and adjustment from proximity to highways
      foreach (Vector2 point in arterialPoints) {
         if (!TerrainGen.IsWaterAt(point) && innerPatchBounds.InBounds(point)) {
            if (arterialPointsByRegion.ContainsKey(regionIdx)) {
               arterialPointsByRegion[regionIdx].Add(point);
            }
            else {
               arterialPointsByRegion[regionIdx] = new ArrayList() { point };
            }
            //arterialPoints.Add(newPoint);
         }
      }


      // Build arterial graph

      var points = new List<Vertex>();
      foreach (Vector2 v in arterialPoints) {
         points.Add(new Vertex(v.x, v.y));
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
         Vector2 vec1 = new Vector2((float)((Vertex)vertices[e.P0]).X, (float)((Vertex)vertices[e.P0]).Y);
         Vector2 vec2 = new Vector2((float)((Vertex)vertices[e.P1]).X, (float)((Vertex)vertices[e.P1]).Y);
         //if (ShouldGenEdge(vec1, vec2)) {
            edges.Add(e);

            // build neighbor map
            Vertex v0 = (Vertex)vertices[e.P0];
            Vertex v1 = (Vertex)vertices[e.P1];
            if (!neighbors.ContainsKey(v0)) {
               neighbors[v0] = new List<Vertex>();
            }
            neighbors[v0].Add(v1);
            Debug.Log("Adding " + v0.X + "," + v0.Y);
         //}
      }
      //Debug.Log(neighbors.Count);

      // Remove unecessary edges on cost basis
      foreach (Edge e in mesh.Edges) {
         Vertex v0 = (Vertex)vertices[e.P0];
         Vertex v1 = (Vertex)vertices[e.P1];
         //Debug.Log(e.P0 + " " + e.P1);
         (Vector2, Vector2) tup1 = (Util.VertexToVector2(v0), Util.VertexToVector2(v1));
         (Vector2, Vector2) tup2 = (Util.VertexToVector2(v1), Util.VertexToVector2(v0));

         if (!InPatchBounds(regionIdx, tup1.Item1, tup1.Item2)) {
            RemoveEdge(e);
            continue;
         }

         if (!WorldManager.edgeState.ContainsKey(tup1)) { //needs removal check
            if (ShouldRemoveEdge(e)) {
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



   }

   private bool ShouldGenPoint(int x, int y) {
      float density = -1;
      patchDensitySnapshotsMap.TryGetValue(Util.W2C(new Vector2(x, y)), out density);

      return !HighwayCollisionInChunkPatch(new Vector2(x, y))
         && !TerrainGen.IsWaterAt(x, y)
         && TerrainGen.GenerateTerrainAt(x, y) < 11
         && (density == -1 || density > 0.3f)
         && innerPatchBounds.InBounds(new Vector2(x,y));
   }

   private bool ShouldGenEdge(Vector2 v1, Vector2 v2) {
      return (bounds.InBounds(v1) || bounds.InBounds(v2));
         //&& (v1-v2).magnitude < 70;
   }

   // for intial point removal
   private bool HighwayCollisionInChunkPatch(Vector2 point) {
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            if (WorldBuilder.DoesChunkContainHighway(point + (WorldManager.chunkSize * new Vector2(i, j)))) {
               return true;
            }
         }
      }

      return false;
   }

   private Vector2 CalcPointAdjustment(Vector2 point) {
      int searchRadius = 6;
      Vector2 adjustment = Vector2.zero;
      ArrayList dirs = new ArrayList() { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
      ArrayList found = new ArrayList();
      for (int r = 2; r <= searchRadius; r++) {
         if (found.Count >= 2) {
            break;
         }
         else {
            foreach (Vector2 d in dirs) {
               Vector2 offset = r * d;
               if (WorldBuilder.DoesChunkContainHighway(point + (WorldManager.chunkSize * offset))) {
                  found.Add(offset);
                  if (found.Count >= 2) break;
               }
            }
         }
      }

      switch (found.Count) {
         case 0:
            break;
         case 1:
            adjustment += -1 * WorldManager.chunkSize / (2 * ((Vector2)found[0]).magnitude - 2.5f) * (Vector2)found[0];
            break;
         default:
            adjustment += -0.6f * WorldManager.chunkSize / (2 * ((Vector2)found[0]).magnitude - 2.5f) * (Vector2)found[0]
               - 0.6f * WorldManager.chunkSize / (2 * ((Vector2)found[1]).magnitude - 2.5f) * (Vector2)found[1];
            break;
      }

      return Hp.arterialGridding * adjustment;
   }

   private void RemoveEdge(Edge e) {
      for (int i = 0; i < edges.Count; i++) {
         if (IsSameEdge((Edge)edges[i], e)) {
            edges.RemoveAt(i);
         }
      }
   }

   public static bool InPatchBounds(Vector2Int regionIdx, Vector2 P0, Vector2 P1) {
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            if (WorldManager.regions[regionIdx + new Vector2Int(i, j)].bounds.InBounds(P0) || WorldManager.regions[regionIdx + new Vector2Int(i, j)].bounds.InBounds(P1)) {
               return true;
            }
         }
      }
      return false;
   }

   private bool ShouldRemoveEdge(Edge e) {
      Vertex v0 = (Vertex)vertices[e.P0];
      Vertex v1 = (Vertex)vertices[e.P1];
      float dist = Util.VertexDistance(v0, v1);

      Queue<(Vertex, float)> queue = new Queue<(Vertex, float)>();
      HashSet<Vertex> visited = new HashSet<Vertex>();
      visited.Add(v0);
      Debug.Log(v0.X + "," + v0.Y);
      foreach (Vertex v0n in neighbors[v0]) {
         if (!Util.IsSameVertex(v0n, v1)) {
            queue.Enqueue((v0n, Util.VertexDistance(v0, v0n) - (vertexRewardRatio * dist) - vertexReward ));
         }
      }
      while (queue.Count > 0) {
         (Vertex, float) tup = queue.Dequeue();
         Vertex v = tup.Item1;
         float cost = tup.Item2;
         visited.Add(v);

         if (v1.X == v.X && v1.Y == v.Y) {
            if (cost <= dist) {
               return true;
            }
         }

         foreach (Vertex vn in neighbors[v]) {
            float newCost = cost + Util.VertexDistance(v, vn) - (vertexRewardRatio * dist) - vertexReward;
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
}
