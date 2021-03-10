using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bounds {
   public float dim, xMin, xMax, zMin, zMax;

   public Bounds(float dim, float xMin, float zMin) {
      this.dim = dim;
      this.xMin = xMin;
      this.xMax = xMin + dim;
      this.zMin = zMin;
      this.zMax = zMin + dim;
   }

   public Vector2Int GetCornerVecInt() {
      return new Vector2Int((int)xMin, (int)zMin);
   }

   public Vector2Int GetCenter() {
      return new Vector2Int((int)(xMin + xMax) / 2, (int)(zMin + zMax) / 2);
   }

   public bool InBounds(Vector2 v) {
      return v.x >= xMin && v.x < xMax && v.y >= zMin && v.y < zMax;
   }

   public float DistFromCenter(Vector2 v) {
      return (GetCenter() - v).magnitude;
   }

   public static Bounds Merge(List<Bounds> list) {
      float xMin = float.MaxValue;
      float zMin = float.MaxValue;
      float xMax = float.MinValue;
      float zMax = float.MinValue;

      foreach (Bounds b in list) {
         if (b.xMin < xMin) {
            xMin = b.xMin;
         }
         if (b.zMin < zMin) {
            zMin = b.zMin;
         }
         if (b.xMax > xMax) {
            xMax = b.xMax;
         }
         if (b.zMax > zMax) {
            zMax = b.zMax;
         }
      }

      return new Bounds(Mathf.Max(xMax-xMin, zMax-zMin), xMin, zMin);
   }
}
