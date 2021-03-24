using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Area {
   public ArrayList verts;
   public Dictionary<(Vector2, Vector2), ArrayList> arterialSegments = new Dictionary<(Vector2, Vector2), ArrayList>();
   public List<(Vector2, float)> seeds = new List<(Vector2, float)>();
   private ArterialPathfinding pathfinding = new ArterialPathfinding();
   private float primaryDir;
   private float secondaryDir;

   public Area(ArrayList list) {
      verts = list;
      Order();
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
      for (int i = 0; i < verts.Count-1; i++) {
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
      Debug.Log("primaryDir: " + primaryDir);

      // Find seeds
      for (int i = 0; i < verts.Count-1; i++) {
         //(Vector2, Vector2) tup = ((Vector2)verts[i], (Vector2)verts[i + 1]);
         GenSeeds((Vector2)verts[i], (Vector2)verts[i + 1]);
         //Vector2 v = (Vector2)verts[i];

      }
   }

   private void GenSeeds(Vector2 v1, Vector2 v2) {
      (Vector2, Vector2) tup;
      if (CompareVec(v1, v2) < 0) {
         tup = (v1, v2);
      }
      else {
         tup = (v2, v1);
      }
      if (WorldManager.arterialEdgeSet.Contains(tup)) {
         ArrayList segments = arterialSegments[tup];
         for (int i = 1; i < segments.Count - 1; i += 4) {
            float angle = AngleToOrthDir(Vector2.Angle((Vector2)segments[i + 1] - (Vector2)segments[i - 1], new Vector2(1, 0)));
            seeds.Add(((Vector2)segments[i], angle + 90));
         }
         //ArrayList segments = pathfinding.FindPath(tup.Item1, tup.Item2);
         //arterialSegments[tup] = segments;
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

         //Debug.Log(thisOrthDir + " " + binIdx + " " + weight);
         bins[binIdx] += weight;
         //numer += thisOrthDir * weight;
         //denom += weight;
      }
      /*string s = "";
      for (int i = 0; i < 9; i++) {
         s += bins[i] + ", ";
      }
      Debug.Log(s);*/

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
}
