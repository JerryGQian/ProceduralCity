using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Block {
   public ArrayList verts;
   public Bounds bounds;

   public Block(ArrayList list) {
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
   public bool IsSame(Block b) {
      HashSet<Vector2> set = new HashSet<Vector2>();
      foreach (Vector2 v in b.verts) {
         set.Add(v);
      }
      foreach (Vector2 v in verts) {
         if (!set.Contains(v)) {
            return false;
         }
      }
      return true;
   }
}
