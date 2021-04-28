using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Area {
   public ArrayList verts;
   public Dictionary<(Vector2, Vector2), ArrayList> arterialSegments = new Dictionary<(Vector2, Vector2), ArrayList>();
   public Bounds bounds;
   public List<(Vector2, float)> seeds = new List<(Vector2, float)>();
   private HashSet<Vector2> seedSet = new HashSet<Vector2>();
   private HashSet<Vector2> usedSeedSet = new HashSet<Vector2>();

   // Graph, IDs, etc
   public Dictionary<(int, int), bool> localSegmentsPending = new Dictionary<(int, int), bool>();
   public List<(Vector2, Vector2)> localSegments = new List<(Vector2, Vector2)>();
   public Dictionary<Vector2, HashSet<Vector2>> localGraph = new Dictionary<Vector2, HashSet<Vector2>>();
   public Dictionary<Vector2, HashSet<Vector2>> roadGraph;
   private int id = 0; // id counter
   private Dictionary<int, HashSet<int>> localIdGraph = new Dictionary<int, HashSet<int>>();
   private Dictionary<int, Vector2> id2val = new Dictionary<int, Vector2>();
   private Dictionary<Vector2, int> val2id = new Dictionary<Vector2, int>();
   public HashSet<int> intersections = new HashSet<int>();
   private HashSet<int> pendingIntersections = new HashSet<int>(); // intersection set for extension phase
   private HashSet<int> intersectionNeighborSet = new HashSet<int>();
   private float optimizationSteps = 6;
   private float stepFraction = 0.3f;

   // Pathfinding/Directions
   private ArterialPathfinding pathfinding = new ArterialPathfinding();
   private float primaryDir;
   private float secondaryDir;

   // Chunk hashes
   private Dictionary<Vector2Int, List<(Vector2, Vector2)>> chunkHash = new Dictionary<Vector2Int, List<(Vector2, Vector2)>>();
   private HashSet<Vector2Int> wallChunkHash = new HashSet<Vector2Int>();
   private static int chunkWidth = 3;
   private static float extendLength = 3;
   private float roadDensityThreshold = 0.2f;

   // Blocks
   public List<Block> blocks = new List<Block>();

   public Area(ArrayList list) {
      verts = list;
      Order();

      // Calc bounds
      Vector2 xLim = new Vector2(999999999, -999999999);
      Vector2 yLim = new Vector2(999999999, -999999999);
      foreach (Vector2 v in verts) {
         // Get extrema
         if (v.x < xLim.x) xLim.x = v.x;
         if (v.x > xLim.y) xLim.y = v.x;
         if (v.y < yLim.x) yLim.x = v.y;
         if (v.y > yLim.y) yLim.y = v.y;
      }
      bounds = new Bounds(xLim.x, yLim.x, xLim.y, yLim.y);
   }

   ///////////////////////////////////////////////////////////////////
   // CORE INTERFACE FUNCTIONS ///////////////////////////////////////
   ///////////////////////////////////////////////////////////////////

   // cycles list until chosen vert is at beginning
   public void Order() {
      if (verts.Count > 0) {
         ArrayList temp = new ArrayList();

         Vector2 primaryV = (Vector2)verts[0];
         int primaryIdx = 0;
         for (int i = 0; i < verts.Count; i++) {
            Vector2 v = (Vector2)verts[i];
            if (CompareVec(v, primaryV) < 0) {
               primaryV = v;
               primaryIdx = i;
            }
         }
         if (primaryIdx > 0) {
            // add up to but not including primary
            for (int i = 0; i < primaryIdx; i++) {
               Vector2 v = (Vector2)verts[i];
               temp.Add(v);
            }
            // shift rest of array
            for (int i = 0; i < verts.Count - primaryIdx; i++) {
               verts[i] = verts[i + primaryIdx];
            }
            // copy over temp
            for (int i = 0; i < temp.Count; i++) {
               verts[i + verts.Count - primaryIdx] = temp[i];
            }
         }
      }
   }

   public string ToString() {
      string s = "";
      foreach (Vector2 v in verts) {
         s += v.ToString();
      }
      return s;
   }

   private int CompareVec(Vector2 v1, Vector2 v2) {
      if (v1.x < v2.x) {
         return -1;
      }
      else if (v1.x == v2.x) {
         if (v1.y < v2.y) {
            return -1;
         }
      }
      return 1;
   }
   public bool IsSame(Area a) {
      HashSet<Vector2> set = new HashSet<Vector2>();
      foreach (Vector2 v in a.verts) {
         set.Add(v);
      }
      foreach (Vector2 v in verts) {
         if (!set.Contains(v)) {
            return false;
         }
      }
      return true;
   }

   ///////////////////////////////////////////////////////////////////
   // GENERATION FUNCTIONS ///////////////////////////////////////////
   ///////////////////////////////////////////////////////////////////

   public void GenArea() {
      // Get path segments for all arterial edges
      GenArterialPaths();
      // Find area orthogonal vector pair
      CalcOrthDirs();
      // Find seeds
      for (int i = 0; i < verts.Count - 1; i++) {
         GenEdgeSeeds((Vector2)verts[i], (Vector2)verts[i + 1]);
      }
      if (verts.Count >= 3) {
         GenEdgeSeeds((Vector2)verts[verts.Count - 1], (Vector2)verts[0]);
      }
      for (int i = 0; i < seeds.Count; i++) {
         Vector2 s = seeds[i].Item1;
         AddVertToChunkHash(Vector2.zero, s);
         seedSet.Add(s);
      }

      //Debug.Log("Area: " + ToString());

      GenLocalRoads();
      GenBlocks();
   }

   // Returns list of arterial edges to generate paths later
   public void GenArterialPaths() {
      for (int i = 0; i < verts.Count - 1; i++) {
         //(Vector2, Vector2) tup = ((Vector2)verts[i], (Vector2)verts[i+1]);
         GenArterialPath((Vector2)verts[i], (Vector2)verts[i + 1]);
      }
      if (verts.Count >= 3) {  // (end to first)
         //(Vector2, Vector2) tup = ((Vector2)verts[verts.Count - 1], (Vector2)verts[0]);
         GenArterialPath((Vector2)verts[verts.Count - 1], (Vector2)verts[0]);
      }
   }

   private void GenArterialPath(Vector2 v1, Vector2 v2) {
      (Vector2, Vector2) tup;
      if (CompareVec(v1, v2) < 0) {
         tup = (v1, v2);
      }
      else {
         tup = (v2, v1);
      }
      if (WorldManager.arterialEdgeSet.Contains(tup)) {
         ArrayList segments = pathfinding.FindPath(tup.Item1, tup.Item2);
         arterialSegments[tup] = segments;
         arterialSegments[(tup.Item2, tup.Item1)] = segments;
      }
   }

   private void CalcOrthDirs() {
      float[] bins = new float[9];
      for (int i = 0; i < verts.Count - 1; i++) {
         Vector2 v1 = (Vector2)verts[i];
         Vector2 v2 = (Vector2)verts[i + 1];
         float angle = Vector2.Angle(v1 - v2, new Vector2(1, 0));
         float thisOrthDir = AngleToOrthDir(angle);
         int binIdx = (int)(thisOrthDir / 5);

         float weight = (v1 - v2).magnitude;

         bins[binIdx] += weight;
      }

      float max = 0;
      int maxIdx = 0;
      for (int i = 0; i < 9; i++) {
         if (bins[i] > max) {
            max = bins[i];
            maxIdx = i;
         }
      }
      float numer = 0;
      float denom = 0.0001f;
      for (int i = maxIdx - 4; i <= maxIdx + 4; i++) {
         if (i >= 0 && i < 9) {
            numer += bins[i] * i * 5;
            denom += bins[i];
         }

      }
      primaryDir = numer / denom;
      if (primaryDir < 8) {
         primaryDir = 0;
      }
      secondaryDir = primaryDir + 90;
      //Debug.Log(ToString() + " Dirs: " + primaryDir + " " + secondaryDir);
   }

   private void GenEdgeSeeds(Vector2 v1, Vector2 v2) {
      (Vector2, Vector2) tup;
      if (CompareVec(v1, v2) < 0) {
         tup = (v1, v2);
      }
      else {
         tup = (v2, v1);
      }

      Vector2 diff = v2 - v1;
      float angle = Vector2.Angle(diff, Vector2.right);
      float sign = Mathf.Sign(Vector2.right.x * diff.y - Vector2.right.y * diff.x);
      if (sign < 0) {
         angle = 360 - angle;
      }
      angle += 90;
      if (angle >= 360) {
         angle -= 360;
      }

      if (WorldManager.arterialEdgeSet.Contains(tup)) {
         //Debug.Log(v1 + " " + v2 + " " + angle + " " + sign);
         ArrayList segments = arterialSegments[tup];
         for (int i = 1; i < segments.Count - 1; i += 4) {
            seeds.Add(((Vector2)segments[i], angle)); //  + 90
         }
         for (int i = 0; i < segments.Count - 1; i++) {
            AddSegToWallChunkHash((Vector2)segments[i], (Vector2)segments[i + 1]);
         }
      }
      else {
         AddSegToWallChunkHash(tup.Item1, tup.Item2);
      }
   }

   // Gen local roads from seeds
   private void GenLocalRoads() {
      // Gen local roads from seeds
      bool evensDone = false;
      for (int i = 0; i < seeds.Count; i += 2) {
         Vector2 seed = seeds[i].Item1;
         float sa = seeds[i].Item2;
         //Debug.Log(i + " " + seed);
         if (!evensDone && i >= seeds.Count - 2) {
            evensDone = true;
            i = -1; // turns to +1 in next iteration (+=2)
         }
         if (usedSeedSet.Contains(seed)) continue;

         usedSeedSet.Add(seed);

         float angle = 0;
         int dir = 1;
         if (sa > primaryDir + 315 || sa <= primaryDir + 45)
            angle = primaryDir;
         else if (sa > primaryDir + 45 && sa <= primaryDir + 135)
            angle = secondaryDir;
         else if (sa > primaryDir + 135 && sa <= primaryDir + 225) {
            angle = primaryDir;
            dir = -1;
         }
         else if (sa > primaryDir + 225 && sa <= primaryDir + 315) {
            angle = secondaryDir;
            dir = -1;
         }

         Vector2 debugV = new Vector2(35.0f, -215.0f);
         bool debug = seed == debugV;

         (Vector2, ArrayList, bool) res = EstablishLocal(seed, angle, dir, new ArrayList(), debug);
         bool keepExtending = res.Item3;
         int x = 0; // hard extension limit
         while (keepExtending && x < 100) {
            res = EstablishLocal(res.Item1, angle, dir, res.Item2, debug);
            keepExtending = res.Item3;
            x++;
         }
      }

      // clean graph of pending segs that remain
      foreach (KeyValuePair<(int, int), bool> seg in localSegmentsPending) {
         if (!seg.Value) {
            // remove pending seg from graph
            localGraph[Id2Val(seg.Key.Item1)].Remove(Id2Val(seg.Key.Item2));
            localGraph[Id2Val(seg.Key.Item2)].Remove(Id2Val(seg.Key.Item1));
         }
      }

      // build id <-> vertex value relationship, for clean optimization
      foreach (KeyValuePair<Vector2, HashSet<Vector2>> pair in localGraph) {
         HashSet<int> idSet = new HashSet<int>();
         foreach (Vector2 n in pair.Value) {
            idSet.Add(Val2Id(n));
         }
         localIdGraph.Add(Val2Id(pair.Key), idSet);
      }

      // find intersections
      foreach (KeyValuePair<int, HashSet<int>> pair in localIdGraph) {
         if (pair.Value.Count >= 3) {
            intersections.Add(pair.Key);
            foreach (int nid in pair.Value) {
               intersectionNeighborSet.Add(nid);
            }
         }
      }

      // Interatively optimize intersections
      OptimizeIntersections();

      // finalize local segments
      HashSet<(Vector2, Vector2)> localSegmentSet = new HashSet<(Vector2, Vector2)>();
      localGraph.Clear();
      foreach (KeyValuePair<int, HashSet<int>> pair in localIdGraph) {
         int id1 = pair.Key;
         Vector2 v1 = Id2Val(id1);
         HashSet<Vector2> set = new HashSet<Vector2>();
         foreach (int id2 in pair.Value) {
            Vector2 v2 = Id2Val(id2);
            set.Add(v2);
            (int, int) idtup1 = (id1, id2);
            (int, int) idtup2 = (id2, id1);
            (Vector2, Vector2) tup1 = (v1, v2);
            (Vector2, Vector2) tup2 = (v2, v1);
            if ((localSegmentsPending.ContainsKey(idtup1) && localSegmentsPending[idtup1] ||
               localSegmentsPending.ContainsKey(idtup2) && localSegmentsPending[idtup2]) &&
               !localSegmentSet.Contains(tup1) && !localSegmentSet.Contains(tup2)) {
               localSegmentSet.Add(tup1);
            }
         }
         localGraph.Add(Id2Val(pair.Key), set);
      }
      localSegments = new List<(Vector2, Vector2)>(localSegmentSet);
      // add arterial ring to graph
      roadGraph = new Dictionary<Vector2, HashSet<Vector2>>(localGraph);

      Vector2 testVec = new Vector2(-50f, -180f);

      for (int i = 0; i < verts.Count - 1; i++) {
         Vector2 v1 = (Vector2)verts[i];
         Vector2 v2 = (Vector2)verts[i + 1];
         (Vector2, Vector2) tup = (v1, v2);

         if (arterialSegments.ContainsKey(tup)) {
            for (int j = 0; j < arterialSegments[tup].Count - 1; j++) {
               Vector2 sv1 = (Vector2)arterialSegments[tup][j];
               Vector2 sv2 = (Vector2)arterialSegments[tup][j + 1];
               if (!roadGraph.ContainsKey(sv1)) roadGraph.Add(sv1, new HashSet<Vector2>());
               if (!roadGraph.ContainsKey(sv2)) roadGraph.Add(sv2, new HashSet<Vector2>());
               roadGraph[sv1].Add(sv2);
               roadGraph[sv2].Add(sv1);
            }
         }
         else {
            if (!roadGraph.ContainsKey(v1)) roadGraph.Add(v1, new HashSet<Vector2>());
            if (!roadGraph.ContainsKey(v2)) roadGraph.Add(v2, new HashSet<Vector2>());
            roadGraph[v1].Add(v2);
            roadGraph[v2].Add(v1);
         }
      }
   }

   // v is current pos pre-extension, angle is extension direction, dir is positivity of direction +/-1
   private (Vector2, ArrayList, bool) EstablishLocal(Vector2 v, float angle, int dir, ArrayList history, bool debug = false) {
      float rad = Util.Angle2Radians(angle);
      Vector2 delta = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
      Vector2 nextV = v + extendLength * dir * delta / delta.magnitude;
      bool snapped = false; // snapped to existing point
      bool established = false; // ie not pending
      bool hitSeed = false;
      List<(Vector2, Vector2)> nearbyList = GetVertListAt(nextV);

      if (ContainsWall(nextV)) {
         return (nextV, history, false);
      }

      // if any in nearbyList, snap nextV to closest
      int x = 0;
      if (nearbyList != null && nearbyList.Count > 0) {
         float minDist = 100;
         Vector2 minV = Vector2.zero;
         foreach ((Vector2, Vector2) nearPair in nearbyList) {
            Vector2 nearV = nearPair.Item2;
            x++;
            if (x > 30) break;
            float d = (nearV - nextV).magnitude;
            if (d < minDist) {
               minDist = d;
               minV = nearV;
            }
         }
         //if (debug) Debug.Log(minDist);
         if (minDist < 100 && v != minV) {
            snapped = true;
            established = true;
            nextV = minV;
            // Set src and its src's to established (EXISTING CHAIN)
            Vector2 prevSrc = minV;
            Vector2 src = GetVertSrcOf(minV);
            while (src != Vector2.zero) {
               int srcId = Val2Id(src);
               int prevSrcId = Val2Id(prevSrc);
               if (localSegmentsPending.ContainsKey((srcId, prevSrcId))) {
                  localSegmentsPending[(srcId, prevSrcId)] = true;
               }
               prevSrc = src;
               src = GetVertSrcOf(src);
            }
            // if snap to seed, stop
            if (seedSet.Contains(nextV)) {
               hitSeed = true;
               usedSeedSet.Add(nextV);
            }
         }
      }

      if (TerrainGen.IsWaterAt(nextV)) established = true;

      // Add nextV edge to structs
      if (bounds.InBounds(nextV) && !TerrainGen.IsWaterAt(nextV)) {
         // Update datastructs, continue extension only if density high enough
         if (hitSeed || TerrainGen.CalculateDensityAtChunk(Util.W2C(nextV)) > roadDensityThreshold) {
            // Register with localGraph
            if (!localGraph.ContainsKey(v)) {
               localGraph.Add(v, new HashSet<Vector2>());
            }
            if (!localGraph.ContainsKey(nextV)) {
               localGraph.Add(nextV, new HashSet<Vector2>());
            }
            localGraph[v].Add(nextV);
            localGraph[nextV].Add(v);

            localSegmentsPending[(Val2Id(v), Val2Id(nextV))] = established;
            AddVertToChunkHash(v, nextV);
            history.Add(((v, nextV), false));
         }

         if (snapped && !seedSet.Contains(nextV)) {
            if (localGraph[nextV].Count >= 3) {
               pendingIntersections.Add(Val2Id(nextV));
            }
         }
      }

      // Set past to established (CURRENT CHAIN)
      if (established && history.Count > 0) {
         ArrayList tempHistory = new ArrayList();
         for (int i = history.Count - 1; i >= 0; i--) {
            ((Vector2, Vector2), bool) tup = (((Vector2, Vector2), bool))history[i];
            //if (i == history.Count - 1) Debug.Log("ESTABLISHING " + tup.Item1);
            if (tup.Item2) break; // can stop if remaining if already true
            ((Vector2, Vector2), bool) trueTup = (tup.Item1, true);
            tempHistory.Insert(0, trueTup);
            localSegmentsPending[(Val2Id(tup.Item1.Item1), Val2Id(tup.Item1.Item2))] = true;
         }
         history = tempHistory;
      }

      if (bounds.InBounds(nextV) && !TerrainGen.IsWaterAt(nextV)) {
         bool intersectionNearby = IntersectionNearby(nextV);
         return (nextV, history, !(hitSeed || (intersectionNearby && snapped)));
      }
      return (nextV, history, false);
   }

   private void OptimizeIntersections() {
      for (int i = 0; i < optimizationSteps; i++) {
         foreach (int centerId in intersections) {
            Vector2 center = id2val[centerId];

            HashSet<int> neighborIdSet = localIdGraph[centerId];
            List<Vector2> unsortedNeighborListCleaned = new List<Vector2>();
            foreach (int neighborId in neighborIdSet) {
               unsortedNeighborListCleaned.Add(Id2Val(neighborId));
            }
            List<Vector2> neighborValList = Util.SortNeighbors(unsortedNeighborListCleaned, center);
            List<int> neighborIdList = new List<int>();
            foreach (Vector2 v in neighborValList) {
               neighborIdList.Add(Val2Id(v));
            }

            // for each edge of intersection
            HashSet<Vector2> anchorEdges = new HashSet<Vector2>(); // anchor represents edges to not change!
            for (int c = 0; c < neighborIdList.Count; c++) {
               int p = c == 0 ? neighborIdList.Count - 1 : c - 1; // prev idx
               int n = c == neighborIdList.Count - 1 ? 0 : c + 1; // next idx
               Vector2 vc = Id2Val(neighborIdList[c]);
               if (anchorEdges.Contains(vc)) continue;
               Vector2 vp = Id2Val(neighborIdList[p]);
               Vector2 vn = Id2Val(neighborIdList[n]);
               Vector2 dirPrev = vp - center;
               Vector2 dirNext = vn - center;
               Vector2 dirCur = vc - center;

               // compare angle with other vertices, dont change if aligned
               List<Vector2> orthEdges = new List<Vector2>();
               for (int cc = 0; cc < neighborIdList.Count; cc++) {
                  Vector2 vcc = Id2Val(neighborIdList[cc]);
                  if (vcc == vc) continue;
                  Vector2 dirCurCur = vcc - center;
                  float compAngle = Vector2.Angle(dirCur, dirCurCur);
                  if (Mathf.Abs(compAngle % 90) < 3) orthEdges.Add(vcc); //consider negatives?
               }
               if (anchorEdges.Contains(vc) || orthEdges.Count > 0) {
                  // skip edge
                  foreach (Vector2 e in orthEdges)
                     anchorEdges.Add(vc);
                  continue;
               }

               float thetaPN = Util.CalcAngle(dirPrev, dirNext);
               float thetaP = Util.CalcAngle(dirPrev, dirCur);
               float thetaN = Util.CalcAngle(dirCur, dirNext);
               float thetaIdeal;
               if (thetaPN < 255) {
                  thetaIdeal = thetaPN / 2;
               }
               else {
                  HashSet<int> cNeighbors = localIdGraph[neighborIdList[c]];
                  Vector2 dirNeighbor = Vector2.zero;
                  foreach (int neighborId in cNeighbors) {
                     Vector2 neighbor = Id2Val(neighborId);
                     if (neighbor != center) {
                        dirNeighbor = neighbor - center;
                        break;
                     }
                  }

                  if (Util.CalcAngle(dirPrev, dirNeighbor) < Util.CalcAngle(dirNeighbor, dirNext)) {
                     thetaIdeal = thetaPN / 3;
                  }
                  else {
                     thetaIdeal = 2 * thetaPN / 3;
                  }
               }
               float thetaDiff = thetaIdeal - thetaP;
               float thetaNew = thetaP + (stepFraction * thetaDiff);
               float mag = 0.95f * dirCur.magnitude;

               Vector2 newDir = Util.Rotate(dirPrev, -thetaNew).normalized;
               Vector2 newCur = center + (mag * newDir);

               ChangeValOfId(neighborIdList[c], newCur);

               // continue smoothing down the line
               if (localIdGraph[neighborIdList[c]].Count == 2) {
                  Vector2 lineV = Vector2.zero;
                  int lineId = -1;
                  foreach (int nid in localIdGraph[neighborIdList[c]]) {
                     Vector2 nv = Id2Val(nid);
                     if (nv != center) {
                        lineV = nv;
                        lineId = nid;
                        break;
                     }
                  }

                  float thetaHinge = Mathf.Abs(Vector2.Angle(lineV - vc, center - vc));
                  if (lineV != Vector2.zero && lineId != -1
                     && !seedSet.Contains(lineV)
                     && thetaHinge > 110
                     && !intersections.Contains(lineId)
                     && !intersectionNeighborSet.Contains(lineId)
                  ) {

                     Vector2 dirLine = lineV - center;
                     float thetaLineP = Util.CalcAngle(dirPrev, dirLine);
                     float thetaLineDiff = thetaIdeal - thetaLineP;
                     float thetaLineNew = thetaP + (stepFraction * thetaLineDiff / 2);
                     Vector2 newLineDir = Util.Rotate(dirPrev, -thetaLineNew).normalized;
                     Vector2 newLineCur = center + (0.95f * (mag + (lineV - vc).magnitude) * newLineDir);

                     ChangeValOfId(lineId, newLineCur);
                  }
               }
            }


         }
      }
   }

   private void GenBlocks() {
      // search for blocks
      List<Vector2> intersectionList = new List<Vector2>();
      foreach (int id in intersections) {
         intersectionList.Add(Id2Val(id));
      }
      blocks = BlockFinder.FindBlocks(roadGraph, intersectionList, primaryDir, secondaryDir);
      //Debug.Log("Blocks: " + blocks.Count);

      // gens plots and buildings
      foreach (Block b in blocks) {
         b.GenBlock();
      }
      
   }


   ///////////////////////////////////////////////////////////////////
   // UTIL FUNCTIONS /////////////////////////////////////////////////
   ///////////////////////////////////////////////////////////////////

   private float AngleToOrthDir(float angle) {
      float dir = 0;
      if (angle >= 0 && angle <= 45) {
         dir = angle;
      }
      else if (angle <= 90) {
         dir = 90 - angle;
      }
      else if (angle <= 135) {
         dir = angle - 90;
      }
      else if (angle <= 180) {
         dir = 180 - angle;
      }
      // prevents 45 for binning purposes
      if (dir == 45f) {
         dir -= 0.01f;
      }
      return dir;
   }

   // Local vertex ID Value link
   private int Val2Id(Vector2 v) {
      if (val2id.ContainsKey(v)) return val2id[v];
      id++;
      val2id.Add(v, id);
      id2val.Add(id, v);
      return id;
   }
   public Vector2 Id2Val(int id) {
      if (!id2val.ContainsKey(id)) return Vector2.zero;
      return id2val[id];
   }
   private void ChangeValOfId(int id, Vector2 val) {
      val2id.Remove(id2val[id]);
      val2id[val] = id;
      id2val[id] = val;
   }


   // CHUNK HASH UTIL
   public void AddVertToChunkHash(Vector2 src, Vector2 vert) {
      Vector2Int chunk = W2AC(vert);
      if (!chunkHash.ContainsKey(chunk)) {
         chunkHash[chunk] = new List<(Vector2, Vector2)>();
      }
      chunkHash[chunk].Add((src, vert));
   }

   // for list of snaps in hash bucket
   public List<(Vector2, Vector2)> GetVertListAt(Vector2Int chunk) {
      if (!chunkHash.ContainsKey(chunk)) {
         return null;
      }
      return chunkHash[chunk];
   }

   public List<(Vector2, Vector2)> GetVertListAt(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);
      return GetVertListAt(chunk);
   }

   public void AddSegToWallChunkHash(Vector2 v1, Vector2 v2) {
      Vector2 diff = (v2 - v1);
      if (diff.magnitude > 2) {
         Vector2 step = diff / 4;
         for (int i = 0; i <= 4; i++) {
            Vector2 v = v1 + i * step;
            Vector2Int chunk = W2AC(v);
            wallChunkHash.Add(chunk);
         }
      }
   }

   // for list of wall segments in hash bucket
   public bool ContainsWall(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);
      return wallChunkHash.Contains(chunk);
   }

   public bool ChunkHashContains(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);
      if (!chunkHash.ContainsKey(chunk)) {
         return false;
      }
      foreach ((Vector2, Vector2) tup in chunkHash[chunk]) {
         if (tup.Item2 == vert) return true;
      }
      return false;
   }
   public Vector2 GetVertSrcOf(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);
      if (!chunkHash.ContainsKey(chunk)) {
         return Vector2.zero;
      }

      foreach ((Vector2, Vector2) tup in chunkHash[chunk]) {
         if (tup.Item2 == vert) return tup.Item1;
      }

      return Vector2.zero;
   }

   public bool DoesChunkContainVert(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);
      return chunkHash.ContainsKey(chunk);
   }

   public bool IntersectionNearby(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            Vector2Int thisChunk = chunk + new Vector2Int(i, j);
            List<(Vector2, Vector2)> vertList = GetVertListAt(thisChunk);
            if (vertList != null) {
               foreach ((Vector2, Vector2) pair in vertList) {
                  Vector2 point = pair.Item2;
                  float dist = (vert - point).magnitude;
                  if (point != vert && pendingIntersections.Contains(Val2Id(point)) && dist < 6f) {
                     return true;
                  }
               }
            }
         }
      }
      return false;
   }


   // world to area chunk (smaller than regular chunk)
   public static Vector2Int W2AC(Vector2 worldCoord) {
      int chunkSize = 5;
      if (worldCoord.x < 0) {
         worldCoord.x -= chunkWidth;
      }
      if (worldCoord.y < 0) {
         worldCoord.y -= chunkWidth;
      }
      return new Vector2Int((int)(worldCoord.x / chunkWidth), (int)(worldCoord.y / chunkWidth));
   }

}
