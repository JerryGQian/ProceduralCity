using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;

public class ArterialGenerator {
   public Vector2Int regionIdx;
   public ArrayList initialArterialPoints = new ArrayList();
   public ArrayList arterialPoints;
   public Dictionary<Vector2Int, ArrayList> arterialPointsByRegion;
   public int initSpacingInChunks = 4;
   public float standardAngleTolerance = 45f;
   public float spawnAngleTolerance = 120f;
   public int standardMaxLength = 100;
   public int spawnMaxLength = 130;

   private Bounds bounds;
   private Bounds innerPatchBounds;
   private Bounds outerPatchBounds;
   private Dictionary<Vector2Int, Bounds> regionBounds = new Dictionary<Vector2Int, Bounds>();
   private Dictionary<Vector2Int, float> patchDensitySnapshotsMap; // chunkIdx -> density
   private Dictionary<Vector2Int, CoordRandom> rands; // regionIdx -> CoordRandom

   public CoordRandom rand;

   private ArrayList patchHighways;
   private HashSet<Vector2> patchHighwaysSet;
   private Dictionary<Vector2, Vector2> chainStarts = new Dictionary<Vector2, Vector2>(); // map from highway exit position to highway angle
   public IMesh mesh;
   private ArrayList tempEdges;
   public ArrayList edges;
   public ArrayList vertices;
   private Dictionary<Vector2, ArrayList> regionToVertices = new Dictionary<Vector2, ArrayList>();
   public Dictionary<Vector2, List<Vector2>> neighbors;
   private Dictionary<(Vector2, Vector2), Edge> vertToEdge = new Dictionary<(Vector2, Vector2), Edge>();
   private Dictionary<Vector2, int> vecToVerticesIdx = new Dictionary<Vector2, int>();
   private HashSet<(Vector2, Vector2)> backboneEdges = new HashSet<(Vector2, Vector2)>();
   private Dictionary<Vector2, ArrayList> backboneVerticesPQs = new Dictionary<Vector2, ArrayList>(); // ArrayList of PriorityQueue<Vector2>
   private Dictionary<Vector2, ArrayList> looseEnds = new Dictionary<Vector2, ArrayList>(); // ArrayList of ArrayLists
   private Dictionary<Vector2, int> backboneEdgeCount = new Dictionary<Vector2, int>();

   private Vector2 angleBase = new Vector2(1, 0);

   private float costLimit = 85f;
   private float vertexReward = 20f;
   private float vertexRewardRatio = 0.3f; //ratio of euclidean distance

   private Pathfinding pathfinding = new Pathfinding();

   public ArterialGenerator(Dictionary<Vector2Int, float> patchDensitySnapshotsMap, Bounds bounds, Vector2Int regionIdx) {
      this.regionIdx = regionIdx;
      arterialPoints = new ArrayList();
      arterialPointsByRegion = new Dictionary<Vector2Int, ArrayList>();
      this.patchHighways = new ArrayList();
      patchHighwaysSet = new HashSet<Vector2>();

      this.bounds = bounds;
      float regionSize = WorldManager.regionDim * WorldManager.chunkSize;
      //innerPatchBounds = bounds;
      //outerPatchBounds = new Bounds(3 * regionSize, bounds.xMin - 1 * regionSize, bounds.zMin - 1 * regionSize);
      innerPatchBounds = new Bounds(3 * regionSize, bounds.xMin - regionSize, bounds.zMin - regionSize);
      outerPatchBounds = new Bounds(5 * regionSize, bounds.xMin - 2 * regionSize, bounds.zMin - 2 * regionSize);

      this.patchDensitySnapshotsMap = patchDensitySnapshotsMap;
      rands = new Dictionary<Vector2Int, CoordRandom>();

      tempEdges = new ArrayList();
      edges = new ArrayList();
      vertices = new ArrayList();
      neighbors = new Dictionary<Vector2, List<Vector2>>();
   }

   public void GenArterialLayout() {
      // gather all highways from patch together
      // and init data structures
      for (int i = -2; i <= 2; i++) {
         for (int j = -2; j <= 2; j++) {
            Region region;
            Vector2Int thisIdx = regionIdx + new Vector2Int(i, j);

            looseEnds.Add(thisIdx, new ArrayList());
            backboneVerticesPQs.Add(thisIdx, new ArrayList());
            for (int n = 0; n < 4; n++) {
               looseEnds[thisIdx].Add(new ArrayList());
               backboneVerticesPQs[thisIdx].Add(new PriorityQueue<Vector2>());
            }
            backboneEdgeCount.Add(thisIdx, 0);
            regionToVertices.Add(thisIdx, new ArrayList());

            if (WorldManager.regions.ContainsKey(thisIdx)) {
               region = WorldManager.regions[thisIdx];
               if (region != null) {
                  regionBounds.Add(region.regionIdx, region.bounds);
                  //Debug.Log("Added to regionBounds: " + region.regionIdx);
                  if (region.hwg != null) {
                     patchHighways.AddRange(region.hwg.highways);
                     foreach (ArrayList list in region.hwg.highways) {
                        foreach (Vector2 v in list) {
                           patchHighwaysSet.Add(v);
                        }
                     }
                  }
               }
            }
         }
      }

      // for each region in 5x5 patch, gen arterial points
      for (int i = -2; i <= 2; i++) {
         for (int j = -2; j <= 2; j++) {
            Vector2Int thisIdx = regionIdx + new Vector2Int(i, j);
            Bounds thisBounds = WorldManager.regions[thisIdx].bounds;
            // gen arterial points for each region
            for (int x = (int)thisBounds.xMin + WorldManager.chunkSize / 2; x < thisBounds.xMax; x += initSpacingInChunks * WorldManager.chunkSize) {
               for (int y = (int)thisBounds.zMin + WorldManager.chunkSize / 2; y < thisBounds.zMax; y += initSpacingInChunks * WorldManager.chunkSize) {
                  Vector2 point = new Vector2(x, y);
                  initialArterialPoints.Add(point);

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
                     regionToVertices[thisIdx].Add(point);

                     //add through density
                     float density = -1;
                     patchDensitySnapshotsMap.TryGetValue(Util.W2C(point), out density);
                     if (density > 0.9f) {
                        Debug.Log("Adding density chain start " + point);
                        Vector2 vDirection = Vector2.up;
                        chainStarts.Add(point, vDirection);
                     }
                  }
               }
            }
         }
      }
      // calculate arterial points
      /*for (int x = (int)outerPatchBounds.xMin + WorldManager.chunkSize / 2; x < outerPatchBounds.xMax; x += initSpacingInChunks * WorldManager.chunkSize) {
         for (int y = (int)outerPatchBounds.zMin + WorldManager.chunkSize / 2; y < outerPatchBounds.zMax; y += initSpacingInChunks * WorldManager.chunkSize) {
            Vector2 point = new Vector2(x, y);
            initialArterialPoints.Add(point);

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
      }*/

      // add highway exits
      foreach (ArrayList segments in patchHighways) {
         for (int i = 3; i < segments.Count - 2; i++) {
            Vector2 v = (Vector2)segments[i];
            if (innerPatchBounds.InBounds(v) && i % 4 == 0 && !TerrainGen.IsWaterAt(v)) {
               arterialPoints.Add(v);
               // calc avg angle and add to map
               Vector2 vPrev = (Vector2)segments[i - 1];
               Vector2 vNext = (Vector2)segments[i + 1];
               float angle = (Vector2.Angle(vPrev - v, angleBase) + Vector2.Angle(v - vNext, angleBase)) / 2;
               Vector2 vDirection = vNext - vPrev;
               chainStarts.Add(v, vDirection);
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
      int vertIdx = 0;
      foreach (Vertex v in mesh.Vertices) {
         vertices.Add(v);
         vecToVerticesIdx[Util.VertexToVector2(v)] = vertIdx;
         vertIdx++;
      }
      foreach (Edge e in mesh.Edges) {
         Vertex vert1 = (Vertex)vertices[e.P0];
         Vertex vert2 = (Vertex)vertices[e.P1];
         Vector2 vec1 = Util.VertexToVector2(vert1);//new Vector2((float)(vert1.X), (float)(vert1.Y));
         Vector2 vec2 = Util.VertexToVector2(vert2);//new Vector2((float)(vert2.X), (float)(vert2.Y));

         edges.Add(e);
         vertToEdge.Add((vec1, vec2), e);
         vertToEdge.Add((vec2, vec1), e);


         // build neighbor map
         if (!neighbors.ContainsKey(vec1)) {
            neighbors[vec1] = new List<Vector2>();
         }
         if (!neighbors.ContainsKey(vec2)) {
            neighbors[vec2] = new List<Vector2>();
         }
         neighbors[vec1].Add(vec2);
         neighbors[vec2].Add(vec1);
      }
      //Debug.Log(neighbors.Count);

      // Remove edges where both vertices are on highways or are too long or over water
      foreach (Edge e in mesh.Edges) {
         Vertex v0 = (Vertex)vertices[e.P0];
         Vertex v1 = (Vertex)vertices[e.P1];
         Vector2 vec0 = Util.VertexToVector2(v0);
         Vector2 vec1 = Util.VertexToVector2(v1);
         (Vector2, Vector2) tup1 = (vec0, vec1);
         (Vector2, Vector2) tup2 = (vec1, vec0);

         if (!WorldManager.edgeState.ContainsKey(tup1)) { //needs removal check
            if (IsFullHighwayEdge(e)) { //  || (vec0 - vec1).magnitude > 100
               // remove edge for first time
               WorldManager.edgeState[tup1] = false;
               WorldManager.edgeState[tup2] = false;
               RemoveEdge(e);
               continue;
            }

            /*
            // remove if middle is on water
            Vector2 mid = (vec0 + vec1) / 2;
            if (TerrainGen.IsWaterAt(mid)) {
               RemoveEdge(e);
            }*/

            // do not register positive! next loop still needs to check
         }
         else { // remove if removed before
            if (!WorldManager.edgeState[tup1]) {
               RemoveEdge(e);
               continue;
            }
         }

      }

      // Establish backbone chains starting from highway exits
      foreach (KeyValuePair<Vector2, Vector2> exit in chainStarts) {
         // Pick 2 edges with the most perpendicular angle
         Vector2 hwDirection = exit.Value;
         Vector2 hwVec = exit.Key;
         float min90Diff = 181;
         Vector2 maxVec = Vector2.zero;
         if (neighbors.ContainsKey(hwVec)) {
            foreach (Vector2 neighborVec in neighbors[hwVec]) {
               float diff = Vector2.Angle(hwDirection, neighborVec - hwVec);
               if (Mathf.Abs(90 - diff) < min90Diff) {
                  min90Diff = Mathf.Abs(90 - diff);
                  maxVec = neighborVec;
               }
            }

            if (maxVec != Vector2.zero) {
               //find opposite side
               float min180Diff = 181;
               Vector2 maxVec2 = Vector2.zero;
               foreach (Vector2 neighborVec2 in neighbors[hwVec]) {
                  float diff = Vector2.Angle(maxVec - hwVec, neighborVec2 - hwVec);
                  if (Mathf.Abs(180 - diff) < min180Diff) {
                     min180Diff = Mathf.Abs(180 - diff);
                     maxVec2 = neighborVec2;
                  }
               }

               Vector2Int thisRegionIdx = Util.W2R(hwVec);

               if (IntersectionCount(hwVec) < 4) {
                  AddBackboneVertex(hwVec, thisRegionIdx);

                  // Direction 1
                  if (regionBounds.ContainsKey(thisRegionIdx) && regionBounds[thisRegionIdx].InBounds(maxVec) && (maxVec - hwVec).magnitude < 100) {
                     backboneEdges.Add((hwVec, maxVec));
                     backboneEdges.Add((maxVec, hwVec));
                     //Debug.Log("EstChainFromHW " + hwVec + " " + maxVec);
                     EstablishChain(thisRegionIdx, maxVec, maxVec - hwVec, standardAngleTolerance, standardMaxLength);
                  }
                  else {
                     if (IsLooseEndReceiver(hwVec, thisRegionIdx) || regionBounds.ContainsKey(thisRegionIdx)) {
                        //Debug.Log("LOOSE END #: chain ended by regionBounds " + hwVec);
                        AddLooseEnd(hwVec, thisRegionIdx);
                     }
                  }

                  // Direction 2
                  if (regionBounds.ContainsKey(thisRegionIdx) && regionBounds[thisRegionIdx].InBounds(maxVec2) && (maxVec2 - hwVec).magnitude < 100) {
                     backboneEdges.Add((hwVec, maxVec2));
                     backboneEdges.Add((maxVec2, hwVec));
                     //Debug.Log("EstChainFromHW " + hwVec + " " + maxVec);
                     EstablishChain(thisRegionIdx, maxVec2, maxVec2 - hwVec, standardAngleTolerance, standardMaxLength);
                  }
                  else {
                     if (IsLooseEndReceiver(hwVec, thisRegionIdx) || regionBounds.ContainsKey(thisRegionIdx)) {
                        //Debug.Log("LOOSE END #: chain ended by regionBounds " + hwVec);
                        AddLooseEnd(hwVec, thisRegionIdx);
                     }
                  }
               }
            }
         }
      }

      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            Vector2 idx = regionIdx + new Vector2(i, j);
            //Debug.Log("SPAWNING CHAINS at " + idx + " " + backboneEdgeCount[idx]);
            if (backboneEdgeCount[idx] == 0) {
               Debug.Log("SPAWNING CHAINS at --- " + idx);
               SpawnBackboneChains(idx);
            }
         }
      }

      // Connect loose ends for inner patch
      ConnectLooseEnds(regionIdx + new Vector2Int(-1, 1), false, true, true, false); // top left
      ConnectLooseEnds(regionIdx + new Vector2Int(0, 1), false, true, true, false); // top mid
      ConnectLooseEnds(regionIdx + new Vector2Int(1, 1), false, false, true, false); // top right
      ConnectLooseEnds(regionIdx + new Vector2Int(-1, 0), false, true, true, false); // mid left
      ConnectLooseEnds(regionIdx, false, true, true, false); // mid mid **
      ConnectLooseEnds(regionIdx + new Vector2Int(1, 0), false, false, true, false); // mid right
      ConnectLooseEnds(regionIdx + new Vector2Int(-1, -1), false, true, false, false); // bot left
      ConnectLooseEnds(regionIdx + new Vector2Int(0, -1), false, true, false, false); // bot mid
      ConnectLooseEnds(regionIdx + new Vector2Int(1, -1), false, false, false, false); // bot right

      // Remove non-backbone edges
      foreach (Edge e in mesh.Edges) {
         Vertex v0 = (Vertex)vertices[e.P0];
         Vertex v1 = (Vertex)vertices[e.P1];

         if ((!innerPatchBounds.InBounds(Util.VertexToVector2(v0)) && !innerPatchBounds.InBounds(Util.VertexToVector2(v1))) || !backboneEdges.Contains((Util.VertexToVector2(v0), Util.VertexToVector2(v1)))) {
            //Debug.Log("Removing " + Util.VertexToVector2(v0) + " " + Util.VertexToVector2(v1));
            RemoveEdge(e);
         }
      }
   }

   private void SpawnBackboneChains(Vector2 idx) {
      Debug.Log("SPAWNING CHAINS :" + regionToVertices[idx].Count);
      Vector2Int regionIdx = new Vector2Int((int)idx.x, (int)idx.y);
      if (regionToVertices[idx].Count >= 20) {
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][0]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][11]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][17]), new Vector2(1, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][2]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][12]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][18]), new Vector2(1, 1), spawnAngleTolerance, spawnMaxLength);
      }
      else if (regionToVertices[idx].Count >= 8) {
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][0]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][5]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][2]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][6]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
      }
      else if (regionToVertices[idx].Count >= 3) {
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][0]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(regionToVertices[idx][2]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
      }
   }

   private void AddBackboneVertex(Vector2 v, Vector2Int regionIdx) {
      int quadrant = GetQuadrant(v, regionIdx);
      if (!((PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][quadrant]).ContainsKey(v)) {
         float distFromCenter = (WorldManager.regions[regionIdx].bounds.GetCenter() - v).magnitude;
         ((PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][quadrant]).Insert(v, distFromCenter);
      }
   }

   private void ConnectLooseEnds(Vector2Int regionIdx, bool north, bool east, bool south, bool west) {
      /*if (north) {
         //Debug.Log("ConnectLooseEnds " + regionIdx + " north");
         ConnectLooseEndsFor(
            (ArrayList)looseEnds[regionIdx + new Vector2(0, 1)][2],
            (ArrayList)looseEnds[regionIdx][0],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx + new Vector2(0, 1)][2],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][0]);
      }*/
      if (east) {
         //Debug.Log("ConnectLooseEnds " + regionIdx + " west");
         ConnectLooseEndsFor(
            (ArrayList)looseEnds[regionIdx][1],
            (ArrayList)looseEnds[regionIdx + new Vector2(1, 0)][3],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][1],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx + new Vector2(1, 0)][3]);
      }
      if (south) {
         //Debug.Log("ConnectLooseEnds " + regionIdx + " south");
         ConnectLooseEndsFor(
            (ArrayList)looseEnds[regionIdx][2],
            (ArrayList)looseEnds[regionIdx + new Vector2(0, -1)][0],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][2],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx + new Vector2(0, -1)][0]);
      }
      /*if (west) {
         //Debug.Log("ConnectLooseEnds " + regionIdx + " east");
         ConnectLooseEndsFor(
            (ArrayList)looseEnds[regionIdx + new Vector2(-1, 0)][1],
            (ArrayList)looseEnds[regionIdx][3],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx + new Vector2(-1, 0)][1],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][3]);
      }*/
   }



   private void ConnectLooseEndsFor(ArrayList ends1, ArrayList ends2, PriorityQueue<Vector2> pq1, PriorityQueue<Vector2> pq2) {
      // if empty list(s), pick from PQ
      int n = 1;
      if (ends1.Count == 0) {
         if (ends2.Count > 0) {
            n = Mathf.Min(ends2.Count, pq1.Count());
         }
         /*Debug.Log("usePQ ends1 added: " + n + " ends2C " + ends2.Count + " pq1C " + pq1.Count() + " " + Util.W2R(pq1.PeekFromMax(0)) + " " + GetQuadrant(pq1.PeekFromMax(0), Util.W2R(pq1.PeekFromMax(0))));*/
         for (int i = 0; i < n; i++) {
            Vector2 v = pq1.PeekFromMax(i);
            ends1.Add(v);
            //Debug.Log("usePQ adding:" + v);
         }
      }

      //Debug.Log("ends1 all: " + Util.List2String(ends1));
      foreach (Vector2 v1 in ends1) {
         //Debug.Log(" ends1 count: " + ends1.Count + " " + v1);
         float minDist = 999999;
         Vector2 minVec = new Vector2(0, 0);
         //Debug.Log("ends2 count: " + ends2.Count + " " + v1);
         foreach (Vector2 v2 in ends2) {
            //Debug.Log(" ends2: " + v2);
            float dist = (v1 - v2).magnitude;
            if (dist < minDist) {
               minDist = dist;
               minVec = v2;
            }
         }
         Vector2 mid = (v1 + minVec) / 2;
         if (minDist < 100 && !TerrainGen.IsWaterAt(mid)) {
            if (!backboneEdges.Contains((v1, minVec)) && !backboneEdges.Contains((minVec, v1))) {
               //Debug.Log("result: " + v1 + " <-> " + minVec);
               Edge newEdge = new Edge(vecToVerticesIdx[v1], vecToVerticesIdx[minVec]);
               edges.Add(newEdge);
               backboneEdges.Add((v1, minVec));
               backboneEdges.Add((minVec, v1));
               ends2.Remove(minVec);
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
         && (density == -1 || density > 0.2f)
         && outerPatchBounds.InBounds(new Vector2(x, y));
   }

   private bool ShouldGenEdge(Vector2 v1, Vector2 v2) {
      return (innerPatchBounds.InBounds(v1) || innerPatchBounds.InBounds(v2));
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
      Vector2 vec0 = new Vector2((float)v0.X, (float)v0.Y);
      Vector2 vec1 = new Vector2((float)v1.X, (float)v1.Y);
      float dist = Util.VertexDistance(v0, v1);

      float angle = Vector2.Angle(vec0, vec1);

      //Debug.Log(Vector2.Angle(vec0, vec1));
      //float angleRewardSum = 0;
      float angleReward = 0;
      foreach (Vector2 v1n in neighbors[vec1]) {
         //Vector2 nvec0 = new Vector2((float)v1.X, (float)v1.Y);
         //Vector2 nvec1 = new Vector2((float)v1n.X, (float)v1n.Y);
         float neighborAngle = Vector2.Angle(vec1, v1n);
         float diff = Mathf.Abs(angle - neighborAngle);
         float thisAngleReward = Mathf.Pow(0.09f * (diff - 45), 2);//20 / ((diff / 45) + 1);
         if (thisAngleReward > angleReward) {
            angleReward = thisAngleReward;
         }
      }

      //float angleReward = angleRewardSum / neighbors[v1].Count;
      Debug.Log(angleReward);

      Queue<(Vector2, float)> queue = new Queue<(Vector2, float)>();
      HashSet<Vector2> visited = new HashSet<Vector2>();
      visited.Add(vec0);
      //Debug.Log(v0.X + "," + v0.Y);
      foreach (Vector2 v0n in neighbors[vec0]) {
         if (v0n != vec1) {
            queue.Enqueue((v0n, (vec0 - v0n).magnitude - (vertexRewardRatio * dist) - vertexReward));
         }
      }
      while (queue.Count > 0) {
         (Vector2, float) tup = queue.Dequeue();
         Vector2 v = tup.Item1;
         float cost = tup.Item2;
         visited.Add(v);

         if (vec1 == v) {//(v1.X == v.X && v1.Y == v.Y) {
            if (cost <= dist - angleReward) {
               return true;
            }
         }

         foreach (Vector2 vn in neighbors[v]) {
            float newCost = cost + (v - vn).magnitude - (vertexRewardRatio * dist) - vertexReward;
            if (newCost <= costLimit && !visited.Contains(vn)) {
               queue.Enqueue((vn, newCost));
            }
         }
      }

      return false;
   }

   // Checks if both vertices of edge fall on highway exits.
   private bool IsFullHighwayEdge(Edge e) {
      Vertex v0 = (Vertex)vertices[e.P0];
      Vertex v1 = (Vertex)vertices[e.P1];

      return patchHighwaysSet.Contains(new Vector2((float)v0.X, (float)v0.Y)) && patchHighwaysSet.Contains(new Vector2((float)v1.X, (float)v1.Y));
   }

   private void EstablishChainBidirectional(Vector2Int regionIdx, Vector2 v, Vector2 vDirection, float angleTolerance, float maxLength) {
      EstablishChain(regionIdx, v, vDirection, angleTolerance, maxLength);
      EstablishChain(regionIdx, v, vDirection, -angleTolerance, maxLength);
   }

   // Recursively builds arterial backbone chain. Stays within region of origin.
   private void EstablishChain(Vector2Int regionIdx, Vector2 v, Vector2 vDirection, float angleTolerance, float maxLength) {
      AddBackboneVertex(v, regionIdx);
      backboneEdgeCount[regionIdx]++;

      int count = IntersectionCount(v);
      //Debug.Log("Establishing chain!: " + count + "/" + neighbors[v].Count + " " + v + " " + vDirection);
      if (count >= 4) {
         //Debug.Log("AAA Stopping because intersection limit reached! " + v);
         return;
      }

      float minAngleDiff = 999;
      Vector2 minAngleVec = Vector2.zero;
      foreach (Vector2 neighborVec in neighbors[v]) {
         if (!backboneEdges.Contains((v, neighborVec))) {
            float diff = Vector2.Angle(vDirection, neighborVec - v);
            if (diff < minAngleDiff) {
               minAngleDiff = diff;
               minAngleVec = neighborVec;
            }
         }
      }

      float minAngle = Vector2.Angle(v, minAngleVec);

      if (regionBounds.ContainsKey(regionIdx) && regionBounds[regionIdx].InBounds(minAngleVec)) {
         // Next Point is IN BOUNDS
         if (minAngle < angleTolerance && minAngleVec != Vector2.zero && (v - minAngleVec).magnitude < maxLength) {
            // Next Point is ANGLED ACCEPTABLY
            backboneEdges.Add((v, minAngleVec));
            backboneEdges.Add((minAngleVec, v));
            EstablishChain(regionIdx, minAngleVec, minAngleVec - v, angleTolerance, maxLength);
            // make vertices near region borders loose ends
            /*if (regionBounds[regionIdx].DistFromCenter(v) > 90) {
               AddLooseEnd(v, regionIdx);
            }*/
         }
         if (IsLooseEndReceiver(v, regionIdx)) {
            //Debug.Log("PreAdd LooseEnd Receiver: " + v);
            AddLooseEnd(v, regionIdx);
         }
      }
      else {
         // Next Points is NOT IN BOUNDS
         //Debug.Log("PreAdd LooseEnd OOB: " + v);
         AddLooseEnd(v, regionIdx);
      }

   }

   // receiver are all points in quadrants 0 and 3 (up and left)
   private bool IsLooseEndReceiver(Vector2 end, Vector2Int regionIdx) {
      float x = end.x - regionBounds[regionIdx].xMin;
      float y = end.y - regionBounds[regionIdx].zMin;
      int quadrant = GetQuadrant(end, regionIdx);

      return quadrant == 0 || quadrant == 3;
   }

   private void AddLooseEnd(Vector2 end, Vector2Int regionIdx) {
      //find quadrant N=0 W=1 S=2 E=3
      int quadrant = GetQuadrant(end, regionIdx);

      //Debug.Log("LOOSE END: chain ended by regionBounds " + end + " " + quadrant + " " + regionIdx);
      if (!((ArrayList)looseEnds[regionIdx][quadrant]).Contains(end)) {
         ((ArrayList)looseEnds[regionIdx][quadrant]).Add(end);
      }
   }

   private int GetQuadrant(Vector2 v, Vector2Int regionIdx) {
      //find quadrant N=0 W=1 S=2 E=3
      float x = v.x - regionBounds[regionIdx].xMin;
      float y = v.y - regionBounds[regionIdx].zMin;
      int quadrant = -1;
      if (y > x) { // <>/
         if (y > WorldManager.regionDim * WorldManager.chunkSize - x) { // \<>
            quadrant = 0;
         }
         else { // <>\
            quadrant = 3;
         }
      }
      else { // /<>
         if (y > WorldManager.regionDim * WorldManager.chunkSize - x) { // \<>
            quadrant = 1;
         }
         else { // <>\
            quadrant = 2;
         }
      }
      return quadrant;
   }

   private int IntersectionCount(Vector2 v) {
      int count = 0;
      foreach (Vector2 n in neighbors[v]) {
         //Debug.Log("AAA intersection check: " + v + " " + n + " " + (backboneEdges.Contains((v, n)) || backboneEdges.Contains((n, v))) );
         if (backboneEdges.Contains((v, n)) || backboneEdges.Contains((n, v))) {
            //Debug.Log("AAA counted! " + v);
            count++;
         }
      }
      //Debug.Log("Contains? " + patchHighwaysSet.Contains(new Vector2(-96, -99)));
      if (patchHighwaysSet.Contains(v)) {
         count += 2;
      }
      return count;
   }

   private bool IsSameEdge(Edge e0, Edge e1) {
      Vertex e0v0 = (Vertex)vertices[e0.P0];
      Vertex e0v1 = (Vertex)vertices[e0.P1];
      Vertex e1v0 = (Vertex)vertices[e1.P0];
      Vertex e1v1 = (Vertex)vertices[e1.P1];

      return Util.IsSameVertex(e0v0, e1v0) && Util.IsSameVertex(e0v1, e1v1);  //e0v0.X == e1v0.X && e0v0.Y == e1v0.Y && e0v1.X == e1v1.X && e0v1.Y == e1v1.Y;
   }
}
