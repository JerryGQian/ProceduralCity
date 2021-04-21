using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using csDelaunay;

public class Block {
   public ArrayList verts;
   public Bounds bounds;
   public List<Vector2> corners = new List<Vector2>();
   public List<Vector2> plotCenters = new List<Vector2>();
   private List<int> cornersIdx = new List<int>();
   private float primaryDir;
   private float secondaryDir;

   public Voronoi voronoi;
   public Dictionary<Vector2f, Site> sites;
   public List<Edge> edges;

   public Block(ArrayList list, float primaryDir, float secondaryDir) {
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

      this.primaryDir = primaryDir;
      this.secondaryDir = secondaryDir;
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

   ///////////////////////////////////////////////////////////////////
   // GENERATION FUNCTIONS ///////////////////////////////////////////
   ///////////////////////////////////////////////////////////////////
   ///
   public void GenBlock() {
      List<Vector2f> points = new List<Vector2f>();
      List<Vector2> startPlotCenters = new List<Vector2>();

      // find corners
      for (int i = 0; i < verts.Count - 1; i++) {
         Vector2 vc = (Vector2)verts[i];
         Vector2 vp = (Vector2)(i == 0 ? verts[verts.Count - 1] : verts[i - 1]);
         Vector2 vn = (Vector2)verts[i + 1];

         Vector2 diffP = vp - vc;
         Vector2 diffN = vn - vc;
         float angle = Mathf.Abs(Vector2.Angle(diffP, diffN));
         if (angle < 165) {
            corners.Add(vc);
            cornersIdx.Add(i);
            points.Add(new Vector2f(vc.x, vc.y));
         }
      }
      Vector2 avgCorner = Vector2.zero;
      foreach (Vector2 c in corners) {
         avgCorner += c;
      }
      avgCorner /= corners.Count;

      foreach (Vector2 c in corners) {
         Vector2 dir = (avgCorner - c).normalized;
         startPlotCenters.Add(c + dir * 3);
      }
      //corners = new List<Vector2>(tempCorners);

      // gen points
      
      if (true)
         for (int i = 0; i < startPlotCenters.Count - 1; i++) {
            Vector2 corner1 = startPlotCenters[i];
            Vector2 corner2 = startPlotCenters[i+1];
            plotCenters.Add(corner1);


            Vector2 diff = corner2 - corner1;
            Vector2 dir = diff.normalized;
            float lineDist = diff.magnitude;
            int numPlots = (int)(lineDist / 7);
            float plotWidth = lineDist / numPlots;

            //traverse corner to corner
            Vector2 cur = corner1 + (plotWidth / 2) * dir;
            float distTraversed = plotWidth / 2;
            plotCenters.Add(cur);
            //points.Add(new Vector2f(cur.x, cur.y));
            int plotsPlaced = 1;
            while (distTraversed < lineDist && plotsPlaced < numPlots) {
               cur += (plotWidth / 2) * dir;
               if (!bounds.InBounds(cur)) break;
               //points.Add(new Vector2f(cur.x, cur.y));
               plotCenters.Add(cur);
               plotsPlaced++;
            }
         }
      /*for (int i = 0; i < corners.Count - 1; i++) {
         int idx1 = cornersIdx[i];
         int idx2 = cornersIdx[i + 1];
         Vector2 corner1 = (Vector2)verts[idx1];
         Vector2 corner2 = (Vector2)verts[idx2];
         points.Add(new Vector2f(corner1.x, corner1.y));

         Vector2 diff = corner2 - corner1;
         Vector2 dir = diff.normalized;
         float lineDist = diff.magnitude;
         int numPlots = (int)(lineDist / 7);
         float plotWidth = lineDist / numPlots;

         //traverse corner to corner
         Vector2 cur = corner1 + (plotWidth / 2) * dir;
         float distTraversed = plotWidth / 2;
         points.Add(new Vector2f(cur.x, cur.y));
         int plotsPlaced = 1;
         while (distTraversed < lineDist && plotsPlaced < numPlots) {
            cur += (plotWidth / 2) * dir;
            points.Add(new Vector2f(cur.x, cur.y));
            plotCenters.Add(cur);
            plotsPlaced++;
         }
      }*/
      /*for (int i = 0; i < verts.Count; i++) {
         if (i % 6 == 0) {
            Vector2 v = (Vector2)verts[i];
            points.Add(new Vector2f(v.x, v.y));
         }
      }*/

      /*
      //List<Vector2f> p = CreateRandomPoint();


      Rectf rect = new Rectf(//0, 0, 20, 20);
          bounds.xMin - 500, bounds.zMin - 500,
          bounds.xMax + 500, bounds.zMax + 500);

      Voronoi voronoi = new Voronoi(points, rect, 0);

      // Now retreive the edges from it, and the new sites position if you used lloyd relaxtion
      sites = voronoi.SitesIndexedByLocation;
      edges = voronoi.Edges;

      //GenVoronoi(points);

      Debug.Log("Voronoi sites: " + sites.Count + " edges:" + edges.Count);
      */
      // place points along road side

      // building plots

      // extrude plots into buildings
   }

   private List<Vector2f> CreateRandomPoint() {
      // Use Vector2f, instead of Vector2
      // Vector2f is pretty much the same than Vector2, but like you could run Voronoi in another thread
      List<Vector2f> points = new List<Vector2f>();
      for (int i = 0; i < 10; i++) {
         points.Add(new Vector2f(Random.Range(0, 20), Random.Range(0, 20)));
      }

      return points;
   }

   private void GenVoronoi(List<Vector2f> points) {
      // Create sites (the center of polygons)
      //List<Vector2f> points = new List<Vector2f>();

      Rectf rect = new Rectf(
          bounds.xMin, bounds.zMin,
          bounds.xMax, bounds.zMax);
      Debug.Log("RECT:" + bounds.xMin + " " + bounds.zMin + " " +
          bounds.xMax + " " + bounds.zMax);

      // There is a two ways you can create the voronoi diagram: with or without the lloyd relaxation
      // Here I used it with 2 iterations of the lloyd relaxation
      voronoi = new Voronoi(points, rect, 0);

      sites = voronoi.SitesIndexedByLocation;
      edges = voronoi.Edges;
   }
}
