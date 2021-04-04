using System;
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
   public int initSpacingInChunks = 4;
   public float standardAngleTolerance = 30f;
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
   // map from regionIdx to list of ChainStarts(highway exit position and highway angle) 
   private Dictionary<Vector2Int, List<ChainStart>> highwayChainStartsByRegion = new Dictionary<Vector2Int, List<ChainStart>>();
   private Dictionary<Vector2Int, List<ChainStart>> boundaryChainStartsByRegion = new Dictionary<Vector2Int, List<ChainStart>>();
   public ArrayList edges;
   public ArrayList arterialEdges = new ArrayList();
   private Dictionary<Vector2, ArrayList> regionToVertices = new Dictionary<Vector2, ArrayList>();
   public Dictionary<Vector2, List<Vector2>> neighbors;
   private HashSet<(Vector2, Vector2)> backboneEdges = new HashSet<(Vector2, Vector2)>();
   private Dictionary<Vector2, ArrayList> backboneVerticesPQs = new Dictionary<Vector2, ArrayList>(); // ArrayList of PriorityQueue<Vector2>
   private Dictionary<Vector2, ArrayList> looseEnds = new Dictionary<Vector2, ArrayList>(); // ArrayList of ArrayLists
   private Dictionary<Vector2, int> backboneEdgeCount = new Dictionary<Vector2, int>();

   private class ChainStart : IComparable {
      public Vector2 v;
      public Vector2 dir;
      public ChainStart(Vector2 v, Vector2 dir) {
         this.v = v;
         this.dir = dir;
      }

      public int CompareTo(object obj) {
         ChainStart objCS = (ChainStart)obj;
         if (v.x < objCS.v.x) {
            return -1;
         }
         else if (v.x == objCS.v.x) {
            if (v.y < objCS.v.y) {
               return -1;
            }
         }
         return 1;
      }
   }

   public ArterialGenerator(Dictionary<Vector2Int, float> patchDensitySnapshotsMap, Bounds bounds, Vector2Int regionIdx) {
      this.regionIdx = regionIdx;
      arterialPoints = new ArrayList();
      arterialPointsByRegion = new Dictionary<Vector2Int, ArrayList>();
      this.patchHighways = new ArrayList();
      patchHighwaysSet = new HashSet<Vector2>();

      this.bounds = bounds;
      float regionSize = WorldManager.regionDim * WorldManager.chunkSize;
      innerPatchBounds = new Bounds(3 * regionSize, bounds.xMin - regionSize, bounds.zMin - regionSize);
      outerPatchBounds = new Bounds(5 * regionSize, bounds.xMin - 2 * regionSize, bounds.zMin - 2 * regionSize);

      this.patchDensitySnapshotsMap = patchDensitySnapshotsMap;
      rands = new Dictionary<Vector2Int, CoordRandom>();

      edges = new ArrayList();
      //vertices = new ArrayList();
      neighbors = new Dictionary<Vector2, List<Vector2>>();
   }

   public void GenArterialLayout() {
      // gather all highways from patch together
      // and init data structures
      for (int i = -2; i <= 2; i++) {
         for (int j = -2; j <= 2; j++) {
            Region region;
            Vector2Int thisIdx = regionIdx + new Vector2Int(i, j);

            arterialPointsByRegion[thisIdx] = new ArrayList();
            looseEnds.Add(thisIdx, new ArrayList());
            backboneVerticesPQs.Add(thisIdx, new ArrayList());
            for (int n = 0; n < 4; n++) {
               looseEnds[thisIdx].Add(new ArrayList());
               backboneVerticesPQs[thisIdx].Add(new PriorityQueue<Vector2>());
            }
            backboneEdgeCount.Add(thisIdx, 0);
            regionToVertices.Add(thisIdx, new ArrayList());

            highwayChainStartsByRegion.Add(thisIdx, new List<ChainStart>());
            boundaryChainStartsByRegion.Add(thisIdx, new List<ChainStart>());

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

      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            Vector2Int idx = regionIdx + new Vector2Int(i, j);
            GenRegionLayout(idx);
         }
      }

      // Connect loose ends for inner patch
      /*ConnectLooseEnds(regionIdx + new Vector2Int(-1, 1), true, true); // top left
      ConnectLooseEnds(regionIdx + new Vector2Int(0, 1), true, true); // top mid
      ConnectLooseEnds(regionIdx + new Vector2Int(1, 1), false, true); // top right
      ConnectLooseEnds(regionIdx + new Vector2Int(-1, 0), true, true); // mid left
      ConnectLooseEnds(regionIdx, true, true); // mid mid **
      ConnectLooseEnds(regionIdx + new Vector2Int(1, 0), false, true); // mid right
      ConnectLooseEnds(regionIdx + new Vector2Int(-1, -1), true, false); // bot left
      ConnectLooseEnds(regionIdx + new Vector2Int(0, -1), true, false); // bot mid
      ConnectLooseEnds(regionIdx + new Vector2Int(1, -1), false, false); // bot right

      // Remove non-backbone edges TODO remove this later when things transitioned to arterialEdges
      ArrayList tempEdges = new ArrayList();
      foreach ((Vector2, Vector2) e in edges) {
         tempEdges.Add(e);
      } 
      foreach ((Vector2, Vector2) e in tempEdges) { //mesh.Edges         
         if ((!innerPatchBounds.InBounds(e.Item1) && !innerPatchBounds.InBounds(e.Item2)) || !backboneEdges.Contains((e.Item1, e.Item2))) {
            RemoveEdge(e);
         }
      }*/

      foreach ((Vector2, Vector2) e in edges) {
         arterialEdges.Add(e); // TODO remove this and refactor to "edges", they're the same
         WorldManager.AddToRoadGraph(e.Item1, e.Item2);
         WorldManager.AddToArterialEdgeSet(e.Item1, e.Item2);
      }
   }

   // Gen layout for a single region (uses global fields)
   private void GenRegionLayout(Vector2Int idx) {
      Bounds thisBounds = WorldManager.regions[idx].bounds;
      // gen arterial points for each region
      for (int x = (int)thisBounds.xMin + WorldManager.chunkSize / 2; x < thisBounds.xMax; x += initSpacingInChunks * WorldManager.chunkSize) {
         for (int y = (int)thisBounds.zMin + WorldManager.chunkSize / 2; y < thisBounds.zMax; y += initSpacingInChunks * WorldManager.chunkSize) {
            Vector2 point = new Vector2(x, y);
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

            if (ShouldGenPoint((int)point.x, (int)point.y, thisBounds)) {
               arterialPointsByRegion[idx].Add(point);
               regionToVertices[idx].Add(point);
            }
         }
      }

      // #########################################################

      // add region boundary seeds
      GenRegionBoundarySeeds(idx, 'N');
      GenRegionBoundarySeeds(idx, 'W');
      GenRegionBoundarySeeds(idx, 'S');
      GenRegionBoundarySeeds(idx, 'E');

      // add highway exit seeds
      foreach (ArrayList segments in patchHighways) {
         for (int i = 2; i < segments.Count - 2; i++) {
            Vector2 v = (Vector2)segments[i];
            if (idx == Util.W2R(v) /*innerPatchBounds.InBounds(v)*/ && i % 4 == 0 && !TerrainGen.IsWaterAt(v)) {
               //Debug.Log("AddingHWExit");
               arterialPointsByRegion[idx].Add(v);
               // calc avg angle and add to map
               Vector2 vPrev = (Vector2)segments[i - 1];
               Vector2 vNext = (Vector2)segments[i + 1];
               Vector2 hwDir = (vNext - vPrev).normalized;
               Vector2 vDirection1 = new Vector2(-hwDir.y, hwDir.x);
               Vector2 vDirection2 = -vDirection1;
               highwayChainStartsByRegion[idx].Add(new ChainStart(v, vDirection1));
               highwayChainStartsByRegion[idx].Add(new ChainStart(v, vDirection2));
            }
         }
      }

      /*// add randomness and adjustment from proximity to highways
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
      }*/

      // Build arterial graph
      var points = new List<Vertex>();
      foreach (Vector2 v in arterialPointsByRegion[idx]) {
         points.Add(new Vertex(v.x, v.y));
      }
      // Generate a default mesher
      var mesher = new GenericMesher(new Dwyer());
      // Generate mesh (Delaunay Triangulation)
      IMesh mesh = mesher.Triangulate(points);
      // Init edge/vertex lists for mutation
      ArrayList vertices = new ArrayList();
      foreach (Vertex v in mesh.Vertices) {
         vertices.Add(v);
      }
      foreach (Edge e in mesh.Edges) {
         Vertex vert1 = (Vertex)vertices[e.P0];
         Vertex vert2 = (Vertex)vertices[e.P1];
         Vector2 vec1 = Util.VertexToVector2(vert1);
         Vector2 vec2 = Util.VertexToVector2(vert2);

         edges.Add((vec1, vec2));

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

      // Remove edges where both vertices are on highways or are too long or over water
      foreach (Edge e in mesh.Edges) {
         Vertex v0 = (Vertex)vertices[e.P0];
         Vertex v1 = (Vertex)vertices[e.P1];
         Vector2 vec0 = Util.VertexToVector2(v0);
         Vector2 vec1 = Util.VertexToVector2(v1);
         (Vector2, Vector2) tup1 = (vec0, vec1);
         (Vector2, Vector2) tup2 = (vec1, vec0);

         if (!WorldManager.edgeState.ContainsKey(tup1)) { //needs removal check
            if (IsFullHighwayEdge(tup1)) { //  || (vec0 - vec1).magnitude > 100
               // remove edge for first time
               WorldManager.edgeState[tup1] = false;
               WorldManager.edgeState[tup2] = false;
               RemoveEdge(tup1);
               continue;
            }

            // remove if middle is on water
            Vector2 mid = (vec0 + vec1) / 2;
            if (TerrainGen.IsWaterAt(mid)) {
               RemoveEdge(tup1);
            }

            // do not register positive! next loop still needs to check
         }
         else { // remove if removed before
            if (!WorldManager.edgeState[tup1]) {
               RemoveEdge(tup1);
               continue;
            }
         }
      }


      // #########################################################

      GenNetworkFromSeeds(idx);

      /*foreach (ChainStart cs in highwayChainStartList) { //chainStarts foreach (KeyValuePair<Vector2, Vector2> exit in chainStartList)
                                                  // Pick 2 edges with the most perpendicular angle
         Vector2 hwDirection = cs.dir;
         Vector2 hwVec = cs.v;
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
               //Debug.Log(idx + " hw:" + hwVec + " " + maxVec + " " + maxVec2 + " " + min90Diff);

               Vector2Int thisRegionIdx = Util.W2R(hwVec);

               //Debug.Log("count " + idx + " " + IntersectionCount(hwVec));
               if (IntersectionCount(hwVec) < 4) {
                  AddBackboneVertex(hwVec, thisRegionIdx);

                  // Direction 1
                  if (regionBounds.ContainsKey(thisRegionIdx) && regionBounds[thisRegionIdx].InBounds(maxVec) && (maxVec - hwVec).magnitude < 100) {
                     backboneEdges.Add((hwVec, maxVec));
                     backboneEdges.Add((maxVec, hwVec));
                     WorldManager.AddToRoadGraph(hwVec, maxVec);
                     EstablishChain(thisRegionIdx, maxVec, maxVec - hwVec, standardAngleTolerance, standardMaxLength);
                  }
                  else {
                     if (IsLooseEndReceiver(hwVec, thisRegionIdx) || regionBounds.ContainsKey(thisRegionIdx)) {
                        AddLooseEnd(hwVec, thisRegionIdx);
                     }
                  }

                  // Direction 2
                  if (regionBounds.ContainsKey(thisRegionIdx) && regionBounds[thisRegionIdx].InBounds(maxVec2) && (maxVec2 - hwVec).magnitude < 100) {
                     backboneEdges.Add((hwVec, maxVec2));
                     backboneEdges.Add((maxVec2, hwVec));
                     WorldManager.AddToRoadGraph(hwVec, maxVec);
                     EstablishChain(thisRegionIdx, maxVec2, maxVec2 - hwVec, standardAngleTolerance, standardMaxLength);
                  }
                  else {
                     if (IsLooseEndReceiver(hwVec, thisRegionIdx) || regionBounds.ContainsKey(thisRegionIdx)) {
                        AddLooseEnd(hwVec, thisRegionIdx);
                     }
                  }
               }
            }
         }
      }


      if (backboneEdgeCount[idx] == 0) {
         SpawnBackboneChains(idx);
      }*/
   }

   // Places seeds on a region boundary side, translate to ...Uniform() function
   private void GenRegionBoundarySeeds(Vector2Int idx, char side) {
      Bounds b = WorldManager.regions[idx].bounds;
      List<Vector2> list = new List<Vector2>();
      Vector2 dir = Vector2.zero;
      switch (side) {
         case 'N':
            list = GenRegionBoundarySeedsUniform(idx + Vector2Int.up, 'S');
            break;
         case 'W':
            list = GenRegionBoundarySeedsUniform(idx, 'W');
            dir = Vector2.left;
            break;
         case 'S':
            list = GenRegionBoundarySeedsUniform(idx, 'S');
            dir = Vector2.up;
            break;
         case 'E':
            list = GenRegionBoundarySeedsUniform(idx + Vector2Int.left, 'W');
            dir = Vector2.left;
            break;
      }

      // Add seeds to data structs
      foreach (Vector2 v in list) {
         arterialPointsByRegion[idx].Add(v);
         ChainStart newSeed = new ChainStart(v, dir);
         boundaryChainStartsByRegion[idx].Add(newSeed);
      }
   }

   // Only serves West and South directions
   private List<Vector2> GenRegionBoundarySeedsUniform(Vector2Int idx, char side) {
      List<Vector2> list = new List<Vector2>();
      Bounds b = WorldManager.regions[idx].bounds;
      Vector2 basePoint = Vector2.zero;
      Vector2 offsetDir = Vector2.zero;
      int countIdx = 0;  // either 0 or 1

      switch (side) {
         case 'W':
            basePoint = b.GetCornerBottomRight();
            offsetDir = Vector2.up;
            countIdx = 0;
            break;
         case 'S':
            basePoint = b.GetCornerBottomLeft();
            offsetDir = Vector2.right;
            countIdx = 1;
            break;
      }

      CoordRandom cr = new CoordRandom(idx);
      int[] count = new int[2];
      count[0] = cr.Next(1, 4);
      count[1] = cr.Next(1, 4);

      // wastes count[0] num of random offsets if we are gening for South
      if (countIdx == 1) {
         for (int i = 0; i < count[0]; i++) {
            cr.Next(0, WorldManager.chunkSize * WorldManager.regionDim);
         }
      }
      for (int i = 0; i < count[countIdx]; i++) {
         int offset = cr.Next(0, WorldManager.chunkSize * WorldManager.regionDim);
         Vector2 newSeedV = basePoint + offset * offsetDir;
         // check if seed too close to existing seeds
         /*bool redo = false;
         foreach (ChainStart cs in boundaryChainStartsByRegion[idx]) {
            if ((cs.v - newSeedV).magnitude < 15) {
               redo = true;
            }
         }
         if (redo) {
            i--;
            continue;
         }*/
         // add newly found seed
         list.Add(newSeedV);
      }
      return list;
   }

   private void GenNetworkFromSeeds(Vector2Int idx) {
      List<ChainStart> boundarySeeds = boundaryChainStartsByRegion[idx];
      List<ChainStart> highwaySeeds = highwayChainStartsByRegion[idx];
      boundarySeeds.Sort();
      highwaySeeds.Sort();

      Queue<(Vector2, Vector2)> queue = new Queue<(Vector2, Vector2)>();
      HashSet<(Vector2, Vector2)> visited = new HashSet<(Vector2, Vector2)>();

      // for initial seed non-selected removal
      Dictionary<Vector2, List<Vector2>> highwaySelection = new Dictionary<Vector2, List<Vector2>>();
      foreach (ChainStart cs in highwaySeeds) {
         if (!highwaySelection.ContainsKey(cs.v)) {
            highwaySelection.Add(cs.v, new List<Vector2>());
         }
      }
      List<ChainStart> seeds = boundarySeeds;
      seeds.AddRange(highwaySeeds);
      // enqueue from seeds
      foreach (ChainStart cs in seeds) {
         // finds closest matching neighbor
         float minDiff = 181;
         Vector2 minVec = Vector2.zero;
         if (neighbors.ContainsKey(cs.v)) {
            foreach (Vector2 neighborVec in neighbors[cs.v]) {
               float diff = Vector2.Angle(cs.dir, neighborVec - cs.v);
               if (Mathf.Abs(diff) < minDiff) {
                  minDiff = Mathf.Abs(diff);
                  minVec = neighborVec;
               }
            }
         }
         if (minDiff < 181) { // only if found
            (Vector2, Vector2) tup = (cs.v, minVec);
            queue.Enqueue(tup);
            visited.Add(tup);
            visited.Add((tup.Item2, tup.Item1));
            // remove non-added edges
            // highway check to see if both sides have been added before performing removal
            if (highwaySelection.ContainsKey(cs.v)) {
               highwaySelection[cs.v].Add(minVec);
               if (highwaySelection[cs.v].Count >= 2) {
                  foreach (Vector2 neighborVec in neighbors[cs.v]) {
                     bool remove = true;
                     foreach (Vector2 selected in highwaySelection[cs.v]) {
                        if (neighborVec == selected) {
                           remove = false;
                           break;
                        }
                     }
                     if (remove) {
                        RemoveEdge(tup);
                     }
                  }
               }
            }
            else { // if not highway
               foreach (Vector2 neighborVec in neighbors[cs.v]) {
                  if (neighborVec != minVec) {
                     RemoveEdge(tup);
                  }
               }
            }
         }
      }

      // bfs to remove unwanted edges
      while (queue.Count > 0) {
         (Vector2, Vector2) tup = queue.Dequeue();
         Vector2 src = tup.Item1;
         Vector2 cur = tup.Item2;

      }
   }

   private void SpawnBackboneChains(Vector2Int idx) {
      //Debug.Log("SPAWNING CHAINS :" + regionToVertices[idx].Count);
      Vector2Int regionIdx = new Vector2Int((int)idx.x, (int)idx.y);
      if (arterialPointsByRegion[idx].Count >= 20) {
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][0]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][11]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][17]), new Vector2(1, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][2]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][12]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][18]), new Vector2(1, 1), spawnAngleTolerance, spawnMaxLength);
      }
      else if (arterialPointsByRegion[idx].Count >= 8) {
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][0]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][5]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][2]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][6]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
      }
      else if (arterialPointsByRegion[idx].Count >= 3) {
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][0]), new Vector2(0, 1), spawnAngleTolerance, spawnMaxLength);
         EstablishChainBidirectional(regionIdx, (Vector2)(arterialPointsByRegion[idx][2]), new Vector2(1, 0), spawnAngleTolerance, spawnMaxLength);
      }
   }

   private void AddBackboneVertex(Vector2 v, Vector2Int regionIdx) {
      int quadrant = GetQuadrant(v, regionIdx);
      if (!((PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][quadrant]).ContainsKey(v)) {
         float distFromCenter = (WorldManager.regions[regionIdx].bounds.GetCenter() - v).magnitude;
         ((PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][quadrant]).Insert(v, distFromCenter);
      }
   }

   private void ConnectLooseEnds(Vector2Int regionIdx, bool east, bool south) {
      //Debug.Log("ConnectLooseEnds for " + regionIdx);
      if (east) {
         ConnectLooseEndsFor(
            (ArrayList)looseEnds[regionIdx][1],
            (ArrayList)looseEnds[regionIdx + new Vector2(1, 0)][3],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][1],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx + new Vector2(1, 0)][3],
            true);
      }
      if (south) {
         ConnectLooseEndsFor(
            (ArrayList)looseEnds[regionIdx][2],
            (ArrayList)looseEnds[regionIdx + new Vector2(0, -1)][0],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx][2],
            (PriorityQueue<Vector2>)backboneVerticesPQs[regionIdx + new Vector2(0, -1)][0],
            false);
      }
   }



   private void ConnectLooseEndsFor(ArrayList ends1, ArrayList ends2, PriorityQueue<Vector2> pq1, PriorityQueue<Vector2> pq2, bool verticalOrHorizontal) {
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
      if (ends2.Count < ends1.Count) {
         for (int i = 0; i < ends1.Count - ends2.Count; i++) {
            Vector2 v = pq2.PeekFromMax(i);
            ends2.Add(v);
         }
      }

      // Sorts in ascending axis order
      ends1 = Util.SortVecArrayList(ends1, verticalOrHorizontal);
      ends2 = Util.SortVecArrayList(ends2, verticalOrHorizontal);

      for (int i = 0; i < ends1.Count; i++) {
         Vector2 v1 = (Vector2)ends1[i];
         Vector2 v2 = (Vector2)ends2[i];

         Vector2 mid = (v1 + v2) / 2;
         float dist = (v1 - v2).magnitude;
         //Debug.Log(!(patchHighwaysSet.Contains(v1) && patchHighwaysSet.Contains(minVec)));
         if (dist < 100 && !TerrainGen.IsWaterAt(mid) && !(patchHighwaysSet.Contains(v1) && patchHighwaysSet.Contains(v2))) {
            if (!backboneEdges.Contains((v1, v2)) && !backboneEdges.Contains((v2, v1))) {
               edges.Add((v1, v2));
               backboneEdges.Add((v1, v2));
               backboneEdges.Add((v2, v1));
            }
         }
      }
      /*for (int i = 0; i < ends1.Count; i++) {
         //foreach (Vector2 v1 in ends1) {
         Vector2 v1 = (Vector2)ends1[i];
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
         //Debug.Log(!(patchHighwaysSet.Contains(v1) && patchHighwaysSet.Contains(minVec)));
         if (minDist < 100 && !TerrainGen.IsWaterAt(mid) && !(patchHighwaysSet.Contains(v1) && patchHighwaysSet.Contains(minVec))) {
            if (!backboneEdges.Contains((v1, minVec)) && !backboneEdges.Contains((minVec, v1))) {
               edges.Add((v1, minVec));
               backboneEdges.Add((v1, minVec));
               backboneEdges.Add((minVec, v1));
            }
         }

         ends2.Remove(minVec);
      }*/

   }

   private bool ShouldGenPoint(int x, int y, Bounds b) {
      float density = -1;
      patchDensitySnapshotsMap.TryGetValue(Util.W2C(new Vector2(x, y)), out density);

      return !HighwayCollisionInChunkPatch(new Vector2(x, y))
         && !TerrainGen.IsWaterAt(x, y)
         && TerrainGen.GenerateTerrainAt(x, y) < 11
         && (density == -1 || density > 0.2f)
         && b.InBounds(new Vector2(x, y));
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

   private void RemoveEdge((Vector2, Vector2) e) {
      (Vector2, Vector2) e2 = (e.Item2, e.Item1);
      for (int i = 0; i < edges.Count; i++) {
         if (Util.SameEdge(((Vector2, Vector2))edges[i], e) || Util.SameEdge(((Vector2, Vector2))edges[i], e2)) {
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


   // Checks if both vertices of edge fall on highway exits.
   private bool IsFullHighwayEdge((Vector2, Vector2) e) {
      //Vertex v0 = (Vertex)vertices[e.P0];
      //Vertex v1 = (Vertex)vertices[e.P1];

      return patchHighwaysSet.Contains(e.Item1) && patchHighwaysSet.Contains(e.Item2);
   }

   private void EstablishChainBidirectional(Vector2Int regionIdx, Vector2 v, Vector2 vDirection, float angleTolerance, float maxLength) {
      EstablishChain(regionIdx, v, vDirection, angleTolerance, maxLength);
      EstablishChain(regionIdx, v, vDirection, -angleTolerance, maxLength);
   }

   // Recursively builds arterial backbone chain. Stays within region of origin.
   private void EstablishChain(Vector2Int regionIdx, Vector2 v, Vector2 vDirection, float angleTolerance, float maxLength) {
      //Debug.Log("Establishing chain!: " + regionIdx);
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

      // Next Point is IN BOUNDS
      if (regionBounds.ContainsKey(regionIdx) && regionBounds[regionIdx].InBounds(minAngleVec)) {
         // Traverse to see if new point is redundant
         bool redundant = false;
         float costLim = 2.5f * (v - minAngleVec).magnitude;
         Queue<(Vector2, float)> queue = new Queue<(Vector2, float)>();
         queue.Enqueue((v, 0));
         int i = 0;
         while (queue.Count > 0) {
            i++;
            (Vector2, float) curr = queue.Dequeue();
            if (WorldManager.roadGraph.ContainsKey(curr.Item1)) {
               HashSet<Vector2> neighbors = WorldManager.roadGraph[curr.Item1];
               foreach (Vector2 neighbor in neighbors) {
                  float newCost = curr.Item2 + (neighbor - curr.Item1).magnitude;
                  if (newCost < costLim) {
                     if (neighbor == minAngleVec) {
                        redundant = true;
                        break;
                     }
                     queue.Enqueue((neighbor, newCost));
                  }
               }
               if (redundant || i > 150) {
                  Debug.Log("redundant or 100: " + i);
                  queue.Clear();
                  break;
               }
            }
         }

         // Next Point is ANGLED ACCEPTABLY
         if (!redundant && minAngle < angleTolerance && minAngleVec != Vector2.zero && (v - minAngleVec).magnitude < maxLength) {
            backboneEdges.Add((v, minAngleVec));
            backboneEdges.Add((minAngleVec, v));
            WorldManager.AddToRoadGraph(v, minAngleVec);
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
      //Debug.Log("idx:" + Util.W2R(v) + " intersection" + v);
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
}
