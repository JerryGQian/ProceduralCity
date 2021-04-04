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
   public Dictionary<(Vector2, Vector2), bool> localSegmentsPending = new Dictionary<(Vector2, Vector2), bool>();
   public List<(Vector2, Vector2)> localSegments = new List<(Vector2, Vector2)>();

   private ArterialPathfinding pathfinding = new ArterialPathfinding();
   private float primaryDir;
   private float secondaryDir;

   public Dictionary<Vector2Int, List<(Vector2, Vector2)>> chunkHash = new Dictionary<Vector2Int, List<(Vector2, Vector2)>>();
   private static int chunkWidth = 6;
   private static float extendLength = 3;
   private float roadDensityThreshold = 0.2f;

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
            //Debug.Log(verts.Count + " " + primaryIdx + " " + temp.Count);
            // copy over temp
            for (int i = 0; i < temp.Count; i++) {
               //Debug.Log("loop: " + (i + primaryIdx) + " " + i);
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
      }
   }

   public void GenArea() {
      // Get path segments for all arterial edges
      GenArterialPaths();

      // Find area orthogonal vector pair
      CalcOrthDirs();
      Debug.Log("primaryDir: " + primaryDir + " " + secondaryDir);

      // Find seeds
      for (int i = 0; i < verts.Count - 1; i++) {
         //Debug.Log("Seeds for: " + (Vector2)verts[i] + " " + (Vector2)verts[i + 1]);
         GenEdgeSeeds((Vector2)verts[i], (Vector2)verts[i + 1]);
      }
      if (verts.Count >= 3) {
         //Debug.Log("Seeds for: " + (Vector2)verts[verts.Count-1] + " " + (Vector2)verts[0]);
         GenEdgeSeeds((Vector2)verts[verts.Count - 1], (Vector2)verts[0]);
      }
      //string str = "";
      for (int i = 0; i < seeds.Count; i++) {
         Vector2 s = seeds[i].Item1;
         AddVertToChunkHash(Vector2.zero, s);
         seedSet.Add(s);
         //str += s + ", "; 
      }
      
      //Debug.Log("---> " + str);

      // Gen local roads
      for (int i = 0; i < seeds.Count; i++) {
         Vector2 seed = seeds[i].Item1;
         if (usedSeedSet.Contains(seed)) continue;

         usedSeedSet.Add(seed);
         float sa = seeds[i].Item2;

         //float angle = Mathf.Abs(seedAngle - primaryDir) > Mathf.Abs(seedAngle - secondaryDir) ? primaryDir : secondaryDir;
         float angle = 0;
         int dir = 1;
         if (sa > primaryDir + 315 && sa <= primaryDir + 45)
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

         //Debug.Log(sa + " " + angle);
         //EstablishLocal(seeds[i].Item1, angle, dir, new ArrayList());
         (Vector2, ArrayList, bool) res = EstablishLocal(seed, angle, dir, new ArrayList());
         bool keepExtending = res.Item3;
         int x = 0;
         while (keepExtending && x < 100) {
            //Debug.Log(x);
            res = EstablishLocal(res.Item1, angle, dir, res.Item2);
            keepExtending = res.Item3;
            x++;
         }
      }

      foreach (KeyValuePair<(Vector2, Vector2), bool> seg in localSegmentsPending) {
         if (seg.Value) {
            localSegments.Add(seg.Key);
         }
      }

   }

   private (Vector2, ArrayList, bool) EstablishLocal(Vector2 v, float angle, int dir, ArrayList history) {
      float rad = Util.Angle2Radians(angle);
      //Debug.Log(atan);
      Vector2 delta = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
      Vector2 nextV = v + extendLength * dir * delta / delta.magnitude;
      bool established = false; // ie not pending
      bool hitSeed = false;
      List<(Vector2, Vector2)> nearbyList = GetVertListAt(nextV);
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
         if (minDist < 100 && nextV != minV && v != minV) {
            established = true;
            nextV = minV;
            // Set src and its src's to established (EXISTING CHAIN)
            Vector2 prevSrc = minV;
            Vector2 src = GetVertSrcOf(minV);
            while (src != Vector2.zero) {
               if (localSegmentsPending.ContainsKey((src, prevSrc))) {
                  localSegmentsPending[(src, prevSrc)] = true;
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

      // Set past to established (CURRENT CHAIN)
      if ((TerrainGen.IsWaterAt(nextV) || established) && history.Count > 0) {
         ArrayList tempHistory = new ArrayList();
         for (int i = history.Count - 1; i >= 0; i--) {
            //Debug.Log(i + " " + history.Count);
            ((Vector2, Vector2), bool) tup = (((Vector2, Vector2), bool))history[i];
            if (tup.Item2) break; // can stop if remaining if already true
            ((Vector2, Vector2), bool) trueTup = (tup.Item1, true);
            tempHistory.Add(trueTup);
            localSegmentsPending[tup.Item1] = true;
         }
         history = tempHistory;
      }

      // Add nextV edge to structs
      if (bounds.InBounds(nextV) && !TerrainGen.IsWaterAt(nextV)) {
         // Update datastructs, continue extension only if density high enough
         Vector2Int nextVInt = new Vector2Int((int)nextV.x, (int)nextV.y);
         if (hitSeed || TerrainGen.CalculateDensityAtChunk(Util.W2C(nextV)) > roadDensityThreshold) {
            localSegmentsPending[(v, nextV)] = established;
            AddVertToChunkHash(v, nextV);
            history.Add(((v, nextV), established));
         }         
         return (nextV, history, !hitSeed);
      }

      return (nextV, history, false);
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
   }

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


   // CHUNK HASH UTIL
   public void AddVertToChunkHash(Vector2 src, Vector2 vert) {
      Vector2Int chunk = W2AC(vert);//HashChunkGrouping(Util.W2C(vert));
      if (!chunkHash.ContainsKey(chunk)) {
         chunkHash[chunk] = new List<(Vector2, Vector2)>();
      }
      chunkHash[chunk].Add((src, vert));
   }

   public List<(Vector2, Vector2)> GetVertListAt(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);//HashChunkGrouping(Util.W2C(vert));
      if (!chunkHash.ContainsKey(chunk)) {
         return null;
      }

      return chunkHash[chunk];
   }

   public bool ChunkHashContains(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);//HashChunkGrouping(Util.W2C(vert));
      if (!chunkHash.ContainsKey(chunk)) {
         return false;
      }
      foreach ((Vector2, Vector2) tup in chunkHash[chunk]) {
         if (tup.Item2 == vert) return true;
      }
      return false;
   }
   public Vector2 GetVertSrcOf(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);//HashChunkGrouping(Util.W2C(vert));
      if (!chunkHash.ContainsKey(chunk)) {
         return Vector2.zero;
      }

      foreach ((Vector2, Vector2) tup in chunkHash[chunk]) {
         if (tup.Item2 == vert) return tup.Item1;
      }

      return Vector2.zero;
   }

   public bool DoesChunkContainVert(Vector2 vert) {
      Vector2Int chunk = W2AC(vert);// HashChunkGrouping(Util.W2C(vert));
      return chunkHash.ContainsKey(chunk);
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
   /*public Vector2Int HashChunkGrouping(Vector2Int chunk) {
      if (chunk.x < 0) {
         chunk.x -= 1;
      }
      if (chunk.y < 0) {
         chunk.y -= 1;
      }
      return chunk / chunkWidth;
   }*/
}
