using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour {

   public static int dim = 500; // deprecated
   public static int dimArea = dim * dim;

   public static int loadRadius = 5; //
   public static int chunkSize = 10; // units in length per chunk
   public static int regionDim = 32; // chunks in length per region, must be odd 24

   public WorldBuilder wb;
   public GameObject chunkMeshesParent;
   public static float[,] landValueMap = new float[dim, dim];
   public static float[,] terrainHeightMap = new float[dim, dim];
   public static float[,] waterDistanceMap = new float[dim, dim];
   public static float[] waterDistanceArr = new float[dimArea];
   public static Dictionary<(Vector2, Vector2), bool> edgeState = new Dictionary<(Vector2, Vector2), bool>();

   public static float waterDistanceLimit = 10;


   public static Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
   public static Dictionary<Vector2Int, GameObject> chunkMeshes = new Dictionary<Vector2Int, GameObject>();

   public static Dictionary<Vector2Int, Region> regions = new Dictionary<Vector2Int, Region>();

   // store generated arterial paths to avoid redundant generation
   public static Dictionary<Vector2Int, List<Vector2>> arterialPaths = new Dictionary<Vector2Int, List<Vector2>>();
   public static Dictionary<Vector2, HashSet<Vector2>> roadGraph = new Dictionary<Vector2, HashSet<Vector2>>();
   public static HashSet<(Vector2, Vector2)> arterialEdgeSet = new HashSet<(Vector2, Vector2)>(); // to confirm if segments are arterial or not
   public static Dictionary<string, Area> areas = new Dictionary<string, Area>();

   public GameObject ChunkMeshPrefab;
   public GameObject roadMeshPrefab;
   public GameObject indicator; // for visualization
   public GameObject cube;

   public GameObject player;

   // Start is called before the first frame update
   void Start() {
      InvokeRepeating("LoadLocal", 0, 0.75f);
      InvokeRepeating("DestroyDistant", 0.75f, 0.75f);
   }

   public void DestroyDistant() {
      Vector2Int pos = W2C(player.transform.position);
      ArrayList meshesToRemove = new ArrayList();
      foreach (KeyValuePair<Vector2Int, GameObject> kv in chunkMeshes) {
         Vector2Int idx = kv.Key;
         if (Vector2Int.Distance(pos, idx) > loadRadius * 4) {
            GameObject go = kv.Value;
            Destroy(go);
            meshesToRemove.Add(idx);

         }
      }
      foreach (Vector2Int idx in meshesToRemove) {
         chunkMeshes.Remove(idx);
      }

      
   }

   void LoadLocal() {
      StartCoroutine("GenLocalCoroutine");
   }
   // Preload/Loads at player position
   IEnumerator GenLocalCoroutine() {
      Vector3 pos3 = player.transform.position;
      Vector2Int idx = W2C(pos3);

      // Snapshot Region and build highways
      //yield return StartCoroutine(GenRegionPatch(idx));
      GenRegionPatch(idx);

      // Build list of chunk patch indices
      ArrayList idxToRender = new ArrayList();
      foreach (Vector2Int v in GetPatchIdx(idx)) {
         idxToRender.Add(v);
      }
      // Build larger chunk patch for locales
      ArrayList idxToConsider = new ArrayList();
      foreach (Vector2Int v in GetLargePatchChunks(idx, 5)) {
         idxToConsider.Add(v);
      }

      // Preload Chunks
      foreach (Vector2Int v in idxToRender) {
         // Preload Chunk
         for (int i = -1; i <= 1; i++) {
            for (int j = -1; j <= 1; j++) {
               PreloadChunk(v + new Vector2Int(i, j));
            }
         }
      }


      // Find Areas, Gen Arterial Paths, Gen Local Roads
      GenChunkPatchAreas(idxToRender);
      // Gen Arterial Paths in patch
      GenChunkPatchArterialPaths(idxToRender, idxToConsider);

      // Gen Chunks
      int c = 0;
      foreach (Vector2Int v in idxToRender) {
         //Debug.Log("LLL Chunk loading: " + i);
         // Preloads patch & Loads center
         GenChunkAtIdx(v);
         c++;
         if (c % 8 == 0) {
            yield return null;
         }
      }
   }

   // Adds road to road graph
   public static void AddToRoadGraph(Vector2 v1, Vector2 v2) {
      //Debug.Log("AddToRoadGraph " + v1 + " " + v2);
      
      if (!roadGraph.ContainsKey(v1)) {
         roadGraph[v1] = new HashSet<Vector2>();
      }
      if (!roadGraph.ContainsKey(v2)) {
         roadGraph[v2] = new HashSet<Vector2>();
      }

      if (!roadGraph[v1].Contains(v2)) {
         roadGraph[v1].Add(v2);
      }
      if (!roadGraph[v2].Contains(v1)) {
         roadGraph[v2].Add(v1);
      }

   }
   // Adds edge to hash set
   public static void AddToArterialEdgeSet(Vector2 v1, Vector2 v2) {
      (Vector2, Vector2) tup1 = (v1, v2);
      (Vector2, Vector2) tup2 = (v2, v1);

      arterialEdgeSet.Add(tup1);
      arterialEdgeSet.Add(tup2);
   }

   // Preloads 3x3 patch, Loads center
   //IEnumerator GenChunkAtIdx(Vector2Int idx) {
   private void GenChunkAtIdx(Vector2Int idx) {
      /*// Preload Chunk
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            PreloadChunk(idx + new Vector2Int(i, j));
         }
      }*/
      LoadChunk(idx);
      RenderChunk(idx);
      //yield return null;
   }

   // Snapshots /////////////////////////////////////////////////////////////////////
   //    peeks into each chunk and calculates center coordinate instead of entire chunk
   public void GenRegionPatch(Vector2Int chunkIdx) {
      Vector2 world = C2W(chunkIdx);
      Vector2Int regionIdx = W2R(new Vector2Int((int)world.x, (int)world.y));
      ArrayList patchDensityCenters = new ArrayList();
      Dictionary<Vector2Int, float> patchDensitySnapshotsMap = new Dictionary<Vector2Int, float>();
      for (int i = -3; i <= 3; i++) {
         for (int j = -3; j <= 3; j++) {
            Region region;
            Vector2Int thisIdx = regionIdx + new Vector2Int(i, j);
            if (regions.ContainsKey(thisIdx)) {
               region = regions[thisIdx];
            }
            else {
               (float[,], Dictionary<Vector2Int, float>) snapshotPair = SnapshotRegion(thisIdx);
               region = new Region(thisIdx, snapshotPair.Item1, snapshotPair.Item2);

               regions[thisIdx] = region;
            }

            region.CalcDensityCenters();

            foreach ((Vector2, float) cd in region.densityCenters) {
               patchDensityCenters.Add(cd);
            }
            foreach (KeyValuePair<Vector2Int, float> pair in region.densitySnapshotsMap) {
               patchDensitySnapshotsMap.Add(pair.Key, pair.Value);
            }

            if (!region.generated) {
               foreach ((Vector2, float) cd in region.densityCenters) {
                  Vector2 center = cd.Item1;
                  Instantiate(indicator, new Vector3(center.x, 10, center.y), Quaternion.identity);
               }
               region.generated = true;
            }
         }
      }

      // Gen highway
      if (!regions[regionIdx].highwayPatchExecuted) {
         regions[regionIdx].highwayPatchExecuted = true;
         // Gen highway and wait till done
         GenBuildHighway(patchDensityCenters, regionIdx);
         //yield return StartCoroutine(GenBuildHighway(patchDensityCenters, regionIdx));

         // Prep arterial
         if (!regions[regionIdx].arterialLayoutGenerated) {
            regions[regionIdx].arterialLayoutGenerated = true;
            StartCoroutine(GenBuildArterial(patchDensitySnapshotsMap, regionIdx));
         }
      }
   }

   /*IEnumerator GenBuildHighway(ArrayList patchDensityCenters, Vector2Int regionIdx) {
      Region region = regions[regionIdx];
      region.hwg = new HighwayGenerator(patchDensityCenters, regionIdx);
      //HighwayGenerator highwayGen = new HighwayGenerator(patchDensityCenters, regionIdx);
      IEnumerator genHighwayCoroutine = region.hwg.GenHighway();
      yield return StartCoroutine(genHighwayCoroutine);
      wb.BuildHighway(region.hwg, regionIdx);
      yield return null;
   }*/
   private void GenBuildHighway(ArrayList patchDensityCenters, Vector2Int regionIdx) {
      Region region = regions[regionIdx];
      region.hwg = new HighwayGenerator(patchDensityCenters, regionIdx);
      region.hwg.GenHighwayCoroutine();
      wb.BuildHighway(region.hwg, regionIdx);
   }

   // DOES NOT BUILD ART PATHS, ONLY GEN LAYOUT
   IEnumerator GenBuildArterial(Dictionary<Vector2Int, float> patchDensitySnapshotsMap, Vector2Int regionIdx) {
      Region region = regions[regionIdx];
      region.atg = new ArterialGenerator(patchDensitySnapshotsMap, region.bounds, regionIdx);
      //IEnumerator genArterialCoroutine = region.atg.GenArterialLayout();
      region.atg.GenArterialLayout();
      //yield return StartCoroutine(genHighwayCoroutine);
      wb.BuildArterialLayout(region.atg, regionIdx);
      yield return null;
   }

   // Find current areas, gen arterial paths of those areas, gen local roads for each area
   public void GenChunkPatchAreas(ArrayList chunkPatch) {
      List<Bounds> boundsList = new List<Bounds>();
      foreach (Vector2Int v in chunkPatch) { //patch indices
         // Gather all bounds in chunk patch
         boundsList.Add(chunks[v].bounds);
      }
      Bounds patchBound = Bounds.Merge(boundsList);
      //Debug.Log("patchBound: " + patchBound.dim + " " + patchBound.xMin + " " + patchBound.zMin);
      Vector2Int regionIdx = Util.W2R(patchBound.GetCenter());
      HashSet<Vector2> sourceVerts = new HashSet<Vector2>();
      List<(Vector2, Vector2)> edges = new List<(Vector2, Vector2)>(); // edges to build
      HashSet<(Vector2, Vector2)> edgesBidirectionalSet = new HashSet<(Vector2, Vector2)>();

      Vector2Int thisRegionIdx = regionIdx;
      if (regions.ContainsKey(thisRegionIdx)) {
         ArterialGenerator atg = regions[thisRegionIdx].atg;
         //Debug.Log(thisRegionIdx + " " + regions[thisRegionIdx].atg);
         foreach ((Vector2, Vector2) e in atg.arterialEdges) {
            if (!edgesBidirectionalSet.Contains(e) && (patchBound.InBounds(e.Item1) || patchBound.InBounds(e.Item2))) {
               if (patchBound.InBounds(e.Item1)) {
                  sourceVerts.Add(e.Item1);
               }
               if (patchBound.InBounds(e.Item2)) {
                  sourceVerts.Add(e.Item2);
               }
               //edges.Add(e);

               // register with bidirectional set
               edgesBidirectionalSet.Add(e);
               edgesBidirectionalSet.Add((e.Item2, e.Item1));
            }
         }
      }

      // Find all areas using vertices as seed
      List<Vector2> sourceVertList = new List<Vector2>();
      foreach (Vector2 v in sourceVerts) {
         sourceVertList.Add(v);
      }
      List<Area> currAreas = AreaFinder.FindAreas(sourceVertList);
      HashSet<string> currAreasSet = new HashSet<string>();
      foreach (Area a in currAreas) {
         currAreasSet.Add(a.ToString());
      }
      wb.DestroyDistantAreas(currAreas);
      foreach (Area a in currAreas) {
         //Debug.Log("Name: " + a.ToString());
         string areaName = a.ToString();
         currAreasSet.Add(areaName);
         if (!areas.ContainsKey(areaName)) {
            a.GenArea();
            areas[areaName] = a;
         }
         Area actualArea = areas[areaName];
         if (!WorldBuilder.builtAreas.ContainsKey(areaName)) { 
            Debug.Log("Gening Area " + areaName);
            foreach (KeyValuePair<(Vector2, Vector2), ArrayList> e in actualArea.arterialSegments) {
               wb.BuildArterial(e.Key, e.Value);
            }
            wb.BuildAreaSeeds(actualArea.seeds);
            wb.BuildAreaLocal(actualArea);
         }
      }
      
      
      //Debug.Log("AreasFound: " + areas.Count);
   }


   // Generates final arterial road paths from existing edges that lie in patch
   public void GenChunkPatchArterialPaths(ArrayList chunkPatch, ArrayList largeChunkPatch) {
      List<Bounds> boundsList = new List<Bounds>();
      foreach (Vector2Int v in chunkPatch) { //patch indices
         // Gather all bounds in chunk patch
         boundsList.Add(chunks[v].bounds);
      }
      Bounds patchBound = Bounds.Merge(boundsList);
      //Debug.Log("patchBound: " + patchBound.dim + " " + patchBound.xMin + " " + patchBound.zMin);
      Vector2Int regionIdx = Util.W2R(patchBound.GetCenter());
      List<(Vector2, Vector2)> edges = new List<(Vector2, Vector2)>(); // edges to build
      HashSet<(Vector2, Vector2)> edgesBidirectionalSet = new HashSet<(Vector2, Vector2)>();


      Vector2Int thisRegionIdx = regionIdx;
      if (regions.ContainsKey(thisRegionIdx)) {
         ArterialGenerator atg = regions[thisRegionIdx].atg;
         //Debug.Log(thisRegionIdx + " " + regions[thisRegionIdx].atg);
         foreach ((Vector2, Vector2) e in atg.arterialEdges) {
            if (!edgesBidirectionalSet.Contains(e) && (patchBound.InBounds(e.Item1) || patchBound.InBounds(e.Item2))) {
               edges.Add(e);
               // register with directional set
               edgesBidirectionalSet.Add(e);
               edgesBidirectionalSet.Add((e.Item2, e.Item1));
            }
         }
      }

      // Path find each edge
      ArterialPathfinding pathfinding = new ArterialPathfinding();
      /*foreach ((Vector2, Vector2) e in edges) {
         // A* pathfind from v0 to v1
         ArrayList segments = pathfinding.FindPath(e.Item1, e.Item2);
         //Debug.Log("pathfinding " + e + " " + segments.Count);
         //wb.BuildArterial(e, segments);
      }*/
   }

   public (float[,], Dictionary<Vector2Int, float>) SnapshotRegion(Vector2Int regionIdx) {
      float[,] densitySnapshots = new float[regionDim, regionDim];
      Dictionary<Vector2Int, float> densitySnapshotMap = new Dictionary<Vector2Int, float>();
      Vector2Int regionCenterChunkIdx = RegionIdx2ChunkCenter(regionIdx);
      int regionOffsetMag = (regionDim - 1) / 2;
      for (int locali = 0, i = -regionOffsetMag; i <= regionOffsetMag; locali++, i++) {
         for (int localj = 0, j = -regionOffsetMag; j <= regionOffsetMag; localj++, j++) {
            Vector2Int currIdx = regionCenterChunkIdx + new Vector2Int(i, j);
            float density = SnapshotChunk(currIdx);
            densitySnapshots[locali, localj] = density;
            densitySnapshotMap.Add(currIdx, density);
         }
      }
      return (densitySnapshots, densitySnapshotMap);
   }
   public float SnapshotChunk(Vector2Int idx) {
      Chunk chunk;
      if (chunks.ContainsKey(idx)) {
         chunk = chunks[idx];
      }
      else {
         Bounds bounds = new Bounds(chunkSize, idx.x * chunkSize, idx.y * chunkSize);
         chunk = new Chunk(bounds);
         chunks[idx] = chunk;
      }

      if (chunk.state == ChunkState.UNLOADED) {
         chunk.Snapshot();
      }
      return chunk.densitySnapshot;
   }

   // Preloading /////////////////////////////////////////////////////////////////////
   public void PreloadChunk(Vector2Int idx) {
      Chunk chunk;
      if (chunks.ContainsKey(idx)) {
         chunk = chunks[idx];
      }
      else {
         Bounds bounds = new Bounds(chunkSize, idx.x * chunkSize, idx.y * chunkSize);
         chunk = new Chunk(bounds);
         chunks[idx] = chunk;
      }

      if (chunk.state == ChunkState.UNLOADED || chunk.state == ChunkState.SNAPSHOTTED) {
         chunk.Preload(this);
      }
   }

   // Loading /////////////////////////////////////////////////////////////////////
   public void LoadChunk(Vector2Int idx) {
      //Debug.Log("Loading Chunk:" + idx);
      if (chunks.ContainsKey(idx)) {
         Chunk chunk = chunks[idx];
         if (chunk.state == ChunkState.PRELOADED) {
            chunk.Load(GetPatchChunks(idx));
            //chunk.Load(GetLargePatchChunks(idx));
         }
      }
      else {
         Debug.Log("ERROR: Loading Nonexistent Chunk:" + idx);
      }
   }

   public void RenderChunk(Vector2Int idx) {
      //Debug.Log("Rendering: " + idx + " " + chunkMeshes.ContainsKey(idx));
      if (!chunkMeshes.ContainsKey(idx)) {
         GameObject chunkMesh = Instantiate(ChunkMeshPrefab, new Vector3(0, 0, 0), Quaternion.identity);
         chunkMesh.transform.parent = chunkMeshesParent.transform;
         ChunkMeshRenderer renderer = chunkMesh.GetComponent<ChunkMeshRenderer>();
         renderer.BuildMesh(chunks[idx]);
         chunkMeshes[idx] = chunkMesh;
      }
   }

   public ArrayList GetPatchChunks(Vector2Int idx) {
      ArrayList patchChunks = new ArrayList();
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            patchChunks.Add(chunks[idx + new Vector2Int(i, j)]);

         }
      }
      return patchChunks;
   }
   public ArrayList GetLargePatchChunks(Vector2Int idx, int size) {
      ArrayList patchChunks = new ArrayList();
      for (int i = -loadRadius - size; i <= loadRadius + size; i++) {
         for (int j = -loadRadius - size; j <= loadRadius + size; j++) {
            patchChunks.Add(idx + new Vector2Int(i, j));

         }
      }
      return patchChunks;
   }

   public ArrayList GetPatchIdx(Vector2Int idx) {
      ArrayList patchChunks = new ArrayList();
      for (int i = -loadRadius; i <= loadRadius; i++) {
         for (int j = -loadRadius; j <= loadRadius; j++) {
            patchChunks.Add(idx + new Vector2Int(i, j));

         }
      }
      return patchChunks;
   }

   // World coordinate to Region index
   public static Vector2Int W2R(Vector2 worldCoord) {
      int regionSize = regionDim * chunkSize;

      if (worldCoord.x < 0) {
         worldCoord.x -= regionSize;
      }
      if (worldCoord.y < 0) {
         worldCoord.y -= regionSize;
      }
      return new Vector2Int((int)(worldCoord.x / regionSize), (int)(worldCoord.y / regionSize));
   }

   public static Vector2Int RegionIdx2ChunkCenter(Vector2Int regionIdx) {
      int regionOffsetMag = (regionDim - 1) / 2;

      return new Vector2Int((regionIdx.x * regionDim) + regionOffsetMag, (regionIdx.y * regionDim) + regionOffsetMag);
   }

   // World coordinate to Chunk index
   public static Vector2Int W2C(Vector2 worldCoord) {

      if (worldCoord.x < 0) {
         worldCoord.x -= chunkSize;
      }
      if (worldCoord.y < 0) {
         worldCoord.y -= chunkSize;
      }
      return new Vector2Int((int)(worldCoord.x / chunkSize), (int)(worldCoord.y / chunkSize));
   }
   public static Vector2Int W2C(Vector3 worldCoord) {
      return W2C(new Vector2(worldCoord.x, worldCoord.z));
   }

   // Chunk index to World coordinate of chunk center
   public static Vector2 C2W(Vector2Int worldCoord) {
      return new Vector2Int((int)(worldCoord.x * chunkSize) + chunkSize / 2, (int)(worldCoord.y * chunkSize) + chunkSize / 2);
   }


}
