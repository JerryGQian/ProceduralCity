using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lot {
   public Vector2 src;
   public Vector2 center;
   public float width;
   public float height;
   public ArrayList extrema;
   public List<Vector2> verts = new List<Vector2>();

   public Lot(Vector2 src) {
      this.src = src;
      extrema = new ArrayList();
      for (int i = 0; i < 4; i++) {
         if (i == 0 || i == 1) {
            extrema.Add(float.PositiveInfinity);
         }
         else if (i == 2 || i == 3) {
            extrema.Add(float.NegativeInfinity);
         }
         /*if (i % 2 == 0) {
            extrema.Add(src.y);
         }
         else {
            extrema.Add(src.x);
         }*/
      }
   }

   public void Calc() {
      center = new Vector2(
         ((float)extrema[1] + (float)extrema[3]) / 2, 
         ((float)extrema[0] + (float)extrema[2]) / 2);

      height = Mathf.Abs((float)extrema[0] - (float)extrema[2]);
      width = Mathf.Abs((float)extrema[1] - (float)extrema[3]);
   }



}
