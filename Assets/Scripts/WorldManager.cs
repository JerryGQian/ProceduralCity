using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;



public class WorldManager : MonoBehaviour {

   public static int dim = 500;
   public static int dimArea = dim * dim;
   public static float[,] landValueMap = new float[dim, dim];
   public static float[,] terrainHeightMap = new float[dim, dim];
   public static float[,] waterDistanceMap = new float[dim, dim];
   public static float[] waterDistanceArr = new float[dimArea];

   //public static Random randomSequence = new Random(12345);
   //int randomNumber1 = randomSequence.Next();

   public static float waterDistanceLimit = 15;

   public static int loadRadius = 5;
   public static int chunkSize = 10;
   private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
   private Dictionary<Vector2Int, GameObject> chunkMeshes = new Dictionary<Vector2Int, GameObject>();

   public static int regionDim = 11; // num of chunks in length, must be odd
   private Dictionary<Vector2Int, Region> regions = new Dictionary<Vector2Int, Region>();

   public GameObject ChunkMeshPrefab;
   public GameObject indicator; // for visualization
   public GameObject cube;

   public GameObject player;

   // Start is called before the first frame update
   void Start() {
      InvokeRepeating("LoadLocalChunks", 0, 0.5f);
      //InvokeRepeating("DestroyDistantChunks", 0.5f, 0.5f);
   }

   public void DestroyDistantChunks() {
      Vector2Int pos = W2C(player.transform.position);
      ArrayList meshesToRemove = new ArrayList();
      foreach (KeyValuePair<Vector2Int, GameObject> kv in chunkMeshes) {
         Vector2Int idx = kv.Key;
         if (Vector2Int.Distance(pos, idx) > 3f) {
            GameObject go = kv.Value;
            Destroy(go);
            meshesToRemove.Add(idx);

         }
      }
      foreach (Vector2Int idx in meshesToRemove) {
         chunkMeshes.Remove(idx);
      }
   }

   void LoadLocalChunks() {
      StartCoroutine("LoadLocalChunksCoroutine");
   }

   // Preload/Loads at player position
   IEnumerator LoadLocalChunksCoroutine() {
      Vector3 pos3 = player.transform.position;
      Vector2Int idx = W2C(pos3);
      //Debug.Log("Loading local: " + idx);
      int i = 0;
      foreach (Vector2Int v in GetPatchIdx(idx)) {
         //LoadChunkAtPos(v);
         StartCoroutine("LoadChunkAtPos", v);
         if (i % 4 == 0) {
            yield return null;
         }
      }
   }

   // Preloads 3x3 patch, Loads center
   IEnumerator LoadChunkAtPos(Vector2Int idx) {
      // Snapshot Region
      GenRegionPatch(idx);


      // Preload Chunk
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            PreloadChunk(idx + new Vector2Int(i, j));
            yield return null;
         }
      }
      LoadChunk(idx);
      RenderChunk(idx);
   }

   // Snapshots /////////////////////////////////////////////////////////////////////
   //    peeks into each chunk and calculates center coordinate instead of entire chunk
   public void GenRegionPatch(Vector2Int chunkIdx) {
      Vector2 world = C2W(chunkIdx);
      Vector2Int regionIdx = W2R(new Vector2Int((int)world.x, (int)world.y));
      //Debug.Log(world + " " + regionIdx);
      ArrayList patchDensityCenters = new ArrayList();
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            Region region;
            Vector2Int thisIdx = regionIdx + new Vector2Int(i, j);
            if (regions.ContainsKey(thisIdx)) {
               region = regions[thisIdx];
            }
            else {
               //Debug.Log("New region:" + thisIdx);
               float[,] densitySnapshots = SnapshotRegion(thisIdx);
               region = new Region(thisIdx, densitySnapshots);

               regions[thisIdx] = region;
            }

            if (region.state == RegionState.UNEXECUTED) {
               region.CalcDensityCenters();
            }

            foreach (Vector2 c in region.densityCenters) {
               patchDensityCenters.Add(c);
            }

            if (!region.generated) {
               foreach (Vector2 center in region.densityCenters) {
                  //Debug.Log("center: " + center + " " + C2W(center) + " " + thisIdx * regionDim * chunkSize);
                  //Vector2 gc = center;//C2W(center) + thisIdx * regionDim * chunkSize;
                  Instantiate(indicator, new Vector3(center.x, 10, center.y), Quaternion.identity);
               }
               region.generated = true;
            }
         }
      }

      // Gen highway
      if (regions[regionIdx].state != RegionState.PATCH_EXECUTED) {
         HighwayGenerator highwayGen = new HighwayGenerator(patchDensityCenters);
         highwayGen.GenHighway();
         BuildHighway(highwayGen.mesh, regionIdx);
         regions[regionIdx].state = RegionState.PATCH_EXECUTED;
      }
   }

   private void BuildHighway(IMesh mesh, Vector2Int regionIdx) {
      ArrayList vert = new ArrayList();
      foreach (var v in mesh.Vertices) {
         vert.Add(v);
      }

      foreach (var e in mesh.Edges) {
         Vector2 P0 = new Vector2((float)((Vertex)vert[e.P0]).X, (float)((Vertex)vert[e.P0]).Y);
         Vector2 P1 = new Vector2((float)((Vertex)vert[e.P1]).X, (float)((Vertex)vert[e.P1]).Y);

         if (regions[regionIdx].bounds.InBounds(P0) || regions[regionIdx].bounds.InBounds(P1)) {
            float dist = (P0 - P1).magnitude;


            Debug.Log(P0 + " -> " + P1 + " " + dist);

            float x = (float)(P0.x + P1.x) / 2;
            float y = (float)(P0.y + P1.y) / 2;

            Vector2 vec = new Vector2(P1.x - P0.x, P1.y - P0.y);

            float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

            GameObject segment = Instantiate(cube, new Vector3(x, 10, y), Quaternion.AngleAxis(angle, Vector3.up));
            Transform trans = segment.GetComponent<Transform>();
            //trans.position = new Vector3(x, 0, y);
            //trans.rotation = Quaternion.AngleAxis(angle, Vector3.up);
            trans.localScale = new Vector3(vec.magnitude, 1, 2);
         }
      }
   }

   public float[,] SnapshotRegion(Vector2Int regionIdx) {
      float[,] densitySnapshots = new float[regionDim, regionDim];
      Vector2Int regionCenterChunkIdx = RegionIdx2ChunkCenter(regionIdx);
      int regionOffsetMag = (regionDim - 1) / 2;
      for (int locali = 0, i = -regionOffsetMag; i <= regionOffsetMag; locali++, i++) {
         for (int localj = 0, j = -regionOffsetMag; j <= regionOffsetMag; localj++, j++) {
            densitySnapshots[locali, localj] = SnapshotChunk(regionCenterChunkIdx + new Vector2Int(i, j));
         }
      }
      return densitySnapshots;
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
         ChunkMeshRenderer renderer = chunkMesh.GetComponent<ChunkMeshRenderer>();
         renderer.BuildMesh(chunks[idx]);
         chunkMeshes[idx] = chunkMesh;
         //Debug.Log("Rendered: " + idx + " " + chunkMeshes.Count);
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
   public ArrayList GetLargePatchChunks(Vector2Int idx) {
      ArrayList patchChunks = new ArrayList();
      for (int i = -2; i <= 2; i++) {
         for (int j = -2; j <= 2; j++) {
            patchChunks.Add(chunks[idx + new Vector2Int(i, j)]);

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
   public Vector2Int W2R(Vector2 worldCoord) {
      int regionSize = regionDim * chunkSize;

      if (worldCoord.x < 0) {
         worldCoord.x -= regionSize;
      }
      if (worldCoord.y < 0) {
         worldCoord.y -= regionSize;
      }
      return new Vector2Int((int)(worldCoord.x / regionSize), (int)(worldCoord.y / regionSize));
   }

   public Vector2Int RegionIdx2ChunkCenter(Vector2Int regionIdx) {
      int regionOffsetMag = (regionDim - 1) / 2;

      return new Vector2Int((regionIdx.x * regionDim) + regionOffsetMag, (regionIdx.y * regionDim) + regionOffsetMag);
   }

   // World coordinate to Chunk index
   public Vector2Int W2C(Vector2 worldCoord) {

      if (worldCoord.x < 0) {
         worldCoord.x -= chunkSize;
      }
      if (worldCoord.y < 0) {
         worldCoord.y -= chunkSize;
      }
      return new Vector2Int((int)(worldCoord.x / chunkSize), (int)(worldCoord.y / chunkSize));
   }
   public Vector2Int W2C(Vector3 worldCoord) {
      return W2C(new Vector2(worldCoord.x, worldCoord.z));
   }

   // Chunk index to World coordinate of chunk center
   public Vector2 C2W(Vector2Int worldCoord) {
      return new Vector2Int((int)(worldCoord.x * chunkSize) + chunkSize / 2, (int)(worldCoord.y * chunkSize) + chunkSize / 2);
   }


}
