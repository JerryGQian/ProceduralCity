using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;
using csDelaunay;

public class WorldBuilder : MonoBehaviour {
   public WorldManager wm;
   public GameObject yellowCube;
   public GameObject orangeCube;
   public GameObject purpleCube;
   public GameObject blueCube;
   public GameObject grayCube;
   public GameObject chunkMeshPrefab;
   public GameObject roadMeshPrefab;

   public static Dictionary<(Vector2, Vector2), bool> builtHighways = new Dictionary<(Vector2, Vector2), bool>();
   public static HashSet<(Vector2, Vector2)> builtArterial = new HashSet<(Vector2, Vector2)>();
   public static Dictionary<(Vector2, Vector2), ArrayList> builtArterialSegments = new Dictionary<(Vector2, Vector2), ArrayList>();
   public static Dictionary<string, GameObject> builtAreas = new Dictionary<string, GameObject>();
   //public static Dictionary<Vector2Int, GameObject> chunkMeshes = new Dictionary<Vector2Int, GameObject>();
   public static Dictionary<string, GameObject> roadMeshes = new Dictionary<string, GameObject>();

   public static Vector2 SignalVector = new Vector2(999999, 999999); // Unique vector that signals something, eg a jump in highways
   // Spatial hashing of highway vertices for intersection management, <chunkIdx, List<vertex,(parent vertices)>>
   public static Dictionary<Vector2Int, List<(Vector2, (Vector2Int, Vector2Int))>> highwayVertChunkHash = new Dictionary<Vector2Int, List<(Vector2, (Vector2Int, Vector2Int))>>();
   private static int hashChunkWidth = 3;

   public void BuildRegionMarkers(Vector2Int regionIdx) {
      if (Settings.renderRegionMarkers) {
         //visualize regions
         Region region = WorldManager.regions[regionIdx];
         Bounds regionBounds = region.bounds;

         DrawRegionMarker(regionIdx, regionBounds.xMin, regionBounds.zMin);
         DrawRegionMarker(regionIdx, regionBounds.xMin, regionBounds.zMax);
         DrawRegionMarker(regionIdx, regionBounds.xMax, regionBounds.zMax);
         DrawRegionMarker(regionIdx, regionBounds.xMax, regionBounds.zMin);
      }
   }
   public void DrawRegionMarker(Vector2Int regionIdx, float x, float z) {
      GameObject boundMarker = Instantiate(purpleCube, new Vector3(x, 20, z), Quaternion.AngleAxis(0, Vector3.up));
      boundMarker.name = "BoundMarker " + regionIdx;
      Transform bmtrans = boundMarker.GetComponent<Transform>();
      bmtrans.localScale = new Vector3(10, 10, 10);
   }

   public void BuildDensityCenterMarker(Vector2 v) {
      GameObject marker = Instantiate(orangeCube, new Vector3(v.x, 25, v.y), Quaternion.identity);
      Transform trans = marker.GetComponent<Transform>();
      trans.localScale = new Vector3(15, 4, 15);
   }

   public void BuildHighway(HighwayGenerator hwg, Vector2Int regionIdx) {
      ArrayList vert = new ArrayList();
      foreach (TriangleNet.Geometry.Vertex v in hwg.vertices) {
         vert.Add(v);
      }

      if (Settings.renderHighwayLayout) {
         foreach (TriangleNet.Geometry.Edge e in hwg.edges) {
            Vector2 P0 = new Vector2((float)((TriangleNet.Geometry.Vertex)vert[e.P0]).X, (float)((TriangleNet.Geometry.Vertex)vert[e.P0]).Y);
            Vector2 P1 = new Vector2((float)((TriangleNet.Geometry.Vertex)vert[e.P1]).X, (float)((TriangleNet.Geometry.Vertex)vert[e.P1]).Y);

            float x = (float)(P0.x + P1.x) / 2;
            float y = (float)(P0.y + P1.y) / 2;

            Vector2 vec = new Vector2(P1.x - P0.x, P1.y - P0.y);

            float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

            GameObject segment = Instantiate(yellowCube, new Vector3(x, 20, y), Quaternion.AngleAxis(angle, Vector3.up));
            segment.name = "HWEdge " + P0 + " " + P1;
            Transform trans = segment.GetComponent<Transform>();
            trans.localScale = new Vector3(vec.magnitude, 1, 8);
         }
      }

      if (Settings.renderHighwayPaths) {
         if (hwg.highways != null && hwg.highways.Count > 0) {
            foreach (ArrayList segments in hwg.highways) {
               Vector2 PStart = (Vector2)segments[0];
               Vector2 PEnd = (Vector2)segments[segments.Count - 1];
               Vector2Int PStartInt = new Vector2Int((int)PStart.x, (int)PStart.y);
               Vector2Int PEndInt = new Vector2Int((int)PEnd.x, (int)PEnd.y);
               if (!builtHighways.ContainsKey((PStartInt, PEndInt)) && !builtHighways.ContainsKey((PEndInt, PStartInt))) {
                  builtHighways[(PStartInt, PEndInt)] = true;
                  builtHighways[(PEndInt, PStartInt)] = true;

                  for (int i = 0; i < segments.Count - 1; i++) {
                     Vector2 P0 = (Vector2)segments[i];
                     Vector2 P1 = (Vector2)segments[i + 1];

                     // Path disconnect if signal, skip 2 segments!
                     if (P0 == SignalVector || P1 == SignalVector) {
                        continue;
                     }

                     // Add segment to road graph in WorldManager
                     WorldManager.AddToRoadGraph(P0, P1);


                     Vector2 topDownVec = new Vector2(P1.x - P0.x, P1.y - P0.y);
                     // 3D version of points with terrain height
                     Vector3 P0_3 = new Vector3(P0.x, TerrainGen.GenerateTerrainAt((int)P0.x, (int)P0.y), P0.y);
                     Vector3 P1_3 = new Vector3(P1.x, TerrainGen.GenerateTerrainAt((int)P1.x, (int)P1.y), P1.y);
                     float dist = (P0_3 - P1_3).magnitude;
                     float incline = Mathf.Atan2(topDownVec.magnitude, P0_3.y - P1_3.y) * Mathf.Rad2Deg + 90;

                     float x = (float)(P0.x + P1.x) / 2;
                     float y = (float)(P0.y + P1.y) / 2;

                     float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

                     float objHeight = (TerrainGen.GenerateTerrainAt((int)P0_3.x, (int)P0_3.z) + TerrainGen.GenerateTerrainAt((int)P1_3.x, (int)P1_3.z)) / 2;
                     if (objHeight < 0) {
                        objHeight = 1f;
                     }
                     GameObject segment = Instantiate(purpleCube, new Vector3(x, objHeight, y), Quaternion.AngleAxis(angle, Vector3.up));
                     segment.transform.eulerAngles = new Vector3(
                         segment.transform.eulerAngles.x,
                         angle + 180,
                         incline
                     );
                     segment.name = "HighwaySeg " + PStart + " " + PEnd;
                     Transform trans = segment.GetComponent<Transform>();
                     trans.localScale = new Vector3(dist, 1, Settings.highwayWidth); //topDownVec.magnitude

                  }

               }
            }
         }
      }
   }

   public void BuildArterial((Vector2, Vector2) edge, ArrayList segments, Bounds b) {
      if (Settings.renderArterialPaths) {
         (Vector2, Vector2) edgeRev = (edge.Item2, edge.Item1);
         if (segments.Count > 0 
            && (b.InBounds(edge.Item1) || b.InBounds(edge.Item2)) 
            && (!builtArterial.Contains(edge) || !builtArterial.Contains(edgeRev))) {

            if (!builtArterialSegments.ContainsKey(edge))
               builtArterialSegments.Add(edge, new ArrayList());
            if (!builtArterialSegments.ContainsKey(edgeRev))
               builtArterialSegments.Add(edgeRev, new ArrayList());
            for (int i = 0; i < segments.Count - 1; i++) {
               Vector2 P0 = (Vector2)segments[i];
               Vector2 P1 = (Vector2)segments[i + 1];

               // Path disconnect if signal, skip 2 segments!
               if (P0 == SignalVector || P1 == SignalVector) {
                  continue;
               }

               Vector2 topDownVec = new Vector2(P1.x - P0.x, P1.y - P0.y);
               // 3D version of points with terrain height
               Vector3 P0_3 = new Vector3(P0.x, TerrainGen.GenerateTerrainAt((int)P0.x, (int)P0.y), P0.y);
               Vector3 P1_3 = new Vector3(P1.x, TerrainGen.GenerateTerrainAt((int)P1.x, (int)P1.y), P1.y);
               float dist = (P0_3 - P1_3).magnitude;
               float incline = Mathf.Atan2(topDownVec.magnitude, P0_3.y - P1_3.y) * Mathf.Rad2Deg + 90;

               float x = (float)(P0.x + P1.x) / 2;
               float y = (float)(P0.y + P1.y) / 2;


               float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

               float objHeight = (TerrainGen.GenerateTerrainAt((int)P0_3.x, (int)P0_3.z) + TerrainGen.GenerateTerrainAt((int)P1_3.x, (int)P1_3.z)) / 2;
               if (objHeight < 0) {
                  objHeight = 1f;
               }
               GameObject segment = Instantiate(yellowCube, new Vector3(x, objHeight, y), Quaternion.AngleAxis(angle, Vector3.up));
               segment.transform.eulerAngles = new Vector3(
                   segment.transform.eulerAngles.x,
                   angle + 180,
                   incline
               );
               segment.name = "ArterialSeg " + edge.Item1 + " " + edge.Item2;
               Transform trans = segment.GetComponent<Transform>();
               trans.localScale = new Vector3(dist, 1, Settings.arterialRoadWidth);

               builtArterialSegments[edge].Add(segment);
               builtArterialSegments[edgeRev].Add(segment);
            }
            builtArterial.Add(edge);
            builtArterial.Add(edgeRev);
         }
      }
   }

   public void DestroyDistantArterial(Bounds b) {
      ArrayList edgeToRemove = new ArrayList();
      
      foreach ((Vector2, Vector2) edge in builtArterial) {
         (Vector2, Vector2) edgeRev = (edge.Item2, edge.Item1);
         if (!b.InBounds(edge.Item1) && !b.InBounds(edge.Item2)) {
            edgeToRemove.Add(edge);
            edgeToRemove.Add(edgeRev);
         }
      }

      foreach ((Vector2, Vector2) edge in edgeToRemove) {
         if (builtArterial.Contains(edge)) builtArterial.Remove(edge);
         if (builtArterialSegments.ContainsKey(edge)) {
            foreach (GameObject seg in builtArterialSegments[edge]) {
               Destroy(seg);
            }
            builtArterialSegments.Remove(edge);
         }
      }
   }

   public void BuildAreaDebug(Area area) {
      if (Settings.renderAreaDebug) {
         BuildAreaSeeds(area.seeds);
         BuildAreaIntersections(area);
      }
   }

   public void BuildAreaSeeds(List<(Vector2, float)> seeds) {
      if (Settings.renderAreaSeeds) {
         foreach ((Vector2, float) seed in seeds) {
            Vector2 v = seed.Item1;
            GameObject obj = Instantiate(blueCube, new Vector3(v.x, TerrainGen.GenerateTerrainAt((int)v.x, (int)v.y), v.y), Quaternion.identity);
            obj.name = "AreaSeed " + v + " " + seed.Item2;
            Transform trans = obj.GetComponent<Transform>();
            trans.localScale = new Vector3(2, 4, 2);
         }
      }
   }

   public void BuildAreaIntersections(Area area) {
      if (Settings.renderAreaIntersections) {
         HashSet<int> intersections = area.intersections;
         foreach (int id in intersections) {
            Vector2 v = area.Id2Val(id);
            GameObject obj = Instantiate(orangeCube, new Vector3(v.x, TerrainGen.GenerateTerrainAt((int)v.x, (int)v.y), v.y), Quaternion.identity);
            obj.name = "AreaIntersection " + v;
            Transform trans = obj.GetComponent<Transform>();
            trans.localScale = new Vector3(1.5f, 2, 1.5f);
         }
      }
   }

   public void BuildAreaLocal(Area a) {
      if (Settings.renderAreaLocal) {
         string areaName = a.ToString();
         GameObject areaObj = new GameObject("Area " + areaName);

         GameObject roadMesh = Instantiate(roadMeshPrefab, new Vector3(0, 0, 0), Quaternion.identity);
         roadMesh.transform.SetParent(areaObj.transform);
         RoadMeshRenderer renderer = roadMesh.GetComponent<RoadMeshRenderer>();
         renderer.BuildMesh(a.localSegments, Settings.localRoadWidth);
         roadMeshes[areaName] = roadMesh;

         builtAreas[areaName] = areaObj;
      }
   }

   public void BuildAreaBlocks(Area a) {
      foreach (Block b in a.blocks) {
         if (Settings.renderBuildings) {
            GameObject parent = Instantiate(new GameObject());
            parent.name = "BlockPlots " + b.ToString();

            foreach (KeyValuePair<Vector2, Lot> lotp in b.lots) {
               Lot lot = lotp.Value;
               float density = TerrainGen.CalculateDensityAt(lot.center);
               if (lot.width > 20 || lot.height > 20 || density < 0.1f) continue;
               float height = 10 * density
                  - 3 + Random.Range(-1, 2);
               GameObject obj = Instantiate(
                       grayCube,
                       new Vector3(lot.center.x, height / 2 + TerrainGen.GenerateTerrainAt(new Vector2Int((int)lot.center.x, (int)lot.center.y)), lot.center.y),
                       Quaternion.AngleAxis(0, Vector3.up));
               Transform trans = obj.GetComponent<Transform>();
               trans.localScale =
                  new Vector3(
                     lot.width,
                     height,
                     lot.height);
               trans.SetParent(parent.transform);
            }
         }

         ///////////////////////////
         if (Settings.renderLotEdges) {
            foreach ((Vector2, Vector2) e in b.edges) {
               Vector2 P0 = e.Item1;
               Vector2 P1 = e.Item2;

               //if (wm.regions[regionIdx].bounds.InBounds(P0) || wm.regions[regionIdx].bounds.InBounds(P1)) {
               float dist = (P0 - P1).magnitude;
               //Debug.Log(P0 + " -> " + P1 + " " + dist);

               float x = (float)(P0.x + P1.x) / 2;
               float y = (float)(P0.y + P1.y) / 2;

               Vector2 vec = new Vector2(P1.x - P0.x, P1.y - P0.y);

               float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

               GameObject segment = Instantiate(yellowCube, new Vector3(x, 5, y), Quaternion.AngleAxis(angle, Vector3.up));
               segment.name = "BlockLotEdge " + P0 + " " + P1;
               Transform trans = segment.GetComponent<Transform>();
               //trans.position = new Vector3(x, 0, y);
               //trans.rotation = Quaternion.AngleAxis(angle, Vector3.up);
               trans.localScale = new Vector3(vec.magnitude, 1, .1f);

            }
         }

      }
   }
   private void GenPlotEdge(Vector2f p0, Vector2f p1) {
      Debug.Log("Plotting edge: " + p0 + " " + p1);
      float x0 = p0.x;
      float y0 = p0.y;
      float x1 = p1.x;
      float y1 = p1.y;

      float x = (float)(x0 + x1) / 2;
      float y = (float)(y0 + y1) / 2;
      //Debug.Log(x + " " + y + " ... " + y0 + " " + y1);

      Vector2 vec = new Vector2(x1 - x0, y1 - y0);

      float angle = Mathf.Atan2(x1 - x0, y1 - y0) * Mathf.Rad2Deg + 90;

      GameObject obj = Instantiate(
                    orangeCube,
                    new Vector3(x, 0, y),
                    Quaternion.AngleAxis(angle, Vector3.up));
      Transform trans = obj.GetComponent<Transform>();

      trans.localScale = new Vector3(vec.magnitude, 1, .5f);
   }



   public void DestroyDistantAreas(List<Area> currAreas) {
      HashSet<string> currAreasSet = new HashSet<string>();
      foreach (Area a in currAreas) {
         string areaName = a.ToString();
         currAreasSet.Add(areaName);
      }
      List<string> toDestroy = new List<string>();
      foreach (string areaName in builtAreas.Keys) {
         if (!currAreasSet.Contains(areaName)) {
            toDestroy.Add(areaName);
         }
      }
      foreach (string areaName in toDestroy) {
         //Debug.Log("destroyed Area: " + areaName);
         Destroy(builtAreas[areaName]);
         builtAreas.Remove(areaName);
      }
   }

   public void BuildArterialLayout(ArterialGenerator atg, Vector2Int regionIdx) {
      Region region = WorldManager.regions[regionIdx];
      if (Settings.renderArterialPoints) {
         foreach (KeyValuePair<Vector2Int, ArrayList> regionPair in region.atg.arterialPointsByRegion) {
            Region curRegion = WorldManager.regions[regionPair.Key];
            if (!curRegion.arterialLayoutBuilt) {
               curRegion.arterialLayoutBuilt = true;
               foreach (Vector2 point in atg.arterialPointsByRegion[regionPair.Key]) {
                  GameObject obj = Instantiate(purpleCube, new Vector3(point.x, 8, point.y), Quaternion.identity);
                  obj.name = "ArterialPoint " + point;
                  Transform trans = obj.GetComponent<Transform>();
                  trans.localScale = new Vector3(5, 5, 5);
               }
            }
         }
      }

      if (Settings.renderArterialEdges) {
         foreach ((Vector2, Vector2) e in atg.edges) {
            Vector2 P0 = e.Item1;
            Vector2 P1 = e.Item2;

            if (InPatchBounds(regionIdx, P0, P1)) {
               float x = (float)(P0.x + P1.x) / 2;
               float y = (float)(P0.y + P1.y) / 2;

               Vector2 vec = new Vector2(P1.x - P0.x, P1.y - P0.y);
               float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

               GameObject segment = Instantiate(yellowCube, new Vector3(x, 8, y), Quaternion.AngleAxis(angle, Vector3.up));
               segment.name = "ArterialEdge " + P0 + " " + P1;
               Transform trans = segment.GetComponent<Transform>();
               trans.localScale = new Vector3(vec.magnitude, 1, 3.5f);
            }
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

   //Highway collision
   public static void AddHighwayVertToChunkHash(Vector2 vert, (Vector2Int, Vector2Int) edge) {
      Vector2Int chunk = HashChunkGrouping(Util.W2C(vert));
      if (!highwayVertChunkHash.ContainsKey(chunk)) {
         highwayVertChunkHash[chunk] = new List<(Vector2, (Vector2Int, Vector2Int))>();
      }

      highwayVertChunkHash[chunk].Add((vert, edge));
   }

   public static List<(Vector2, (Vector2Int, Vector2Int))> GetHighwayVertList(Vector2 vert) {
      Vector2Int chunk = HashChunkGrouping(Util.W2C(vert));
      if (!highwayVertChunkHash.ContainsKey(chunk)) {
         return null;
      }

      return highwayVertChunkHash[chunk];
   }

   public static bool DoesChunkContainHighway(Vector2 vert) {
      Vector2Int chunk = HashChunkGrouping(Util.W2C(vert));
      return highwayVertChunkHash.ContainsKey(chunk);
   }
   public static Vector2Int HashChunkGrouping(Vector2Int chunk) {
      if (chunk.x < 0) {
         chunk.x -= 1;
      }
      if (chunk.y < 0) {
         chunk.y -= 1;
      }
      return chunk / hashChunkWidth;
   }


}
