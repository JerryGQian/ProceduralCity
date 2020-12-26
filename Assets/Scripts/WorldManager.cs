﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour {

   public static int dim = 500;
   public static int dimArea = dim * dim;
   public static float[,] landValueMap = new float[dim, dim];
   public static float[,] terrainHeightMap = new float[dim, dim];
   public static float[,] waterDistanceMap = new float[dim, dim];
   public static float[] waterDistanceArr = new float[dimArea];

   public static float waterDistanceLimit = 60.5f;

   public static int chunkSize = 100;
   private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
   private Dictionary<Vector2Int, GameObject> chunkMeshes = new Dictionary<Vector2Int, GameObject>();

   public GameObject ChunkMeshPrefab;

   public GameObject player;

   // Start is called before the first frame update
   void Start() {
      InvokeRepeating("LoadLocalChunks", 0, 0.5f);
      InvokeRepeating("DestroyDistantChunks", 0.5f, 0.5f);
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
      foreach (Vector2Int v in GetPatchIdx(idx)) {
         //LoadChunkAtPos(v);
         StartCoroutine("LoadChunkAtPos", v);
         yield return null;
      }
   }

   // Preloads 3x3 patch, Loads center
   /*IEnumerator LoadChunkAtPos(Vector2 pos) {
      Vector2Int idx = W2C(pos);
      //Debug.Log("Pos:" + pos + " idx:" + idx);
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            PreloadChunk(idx + new Vector2Int(i, j));
            yield return null;
         }
      }
      LoadChunk(idx);
      RenderChunk(idx);
   }*/
   IEnumerator LoadChunkAtPos(Vector2Int idx) {
      //Debug.Log("Pos:" + pos + " idx:" + idx);
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            PreloadChunk(idx + new Vector2Int(i, j));
            yield return null;
         }
      }
      LoadChunk(idx);
      RenderChunk(idx);
   }

   public void PreloadChunk(Vector2Int idx) {
      Chunk c;
      if (chunks.ContainsKey(idx)) {
         Chunk chunk = chunks[idx];
         if (chunk.state == ChunkState.UNLOADED) {
            chunk.Preload(this);
         }
         c = chunk;
      }
      else {
         Bounds bounds = new Bounds(chunkSize, idx.x * chunkSize, idx.y * chunkSize);
         Chunk chunk = new Chunk(bounds);
         chunk.Preload(this);
         chunks[idx] = chunk;
         c = chunk;
      }
      //Debug.Log("Preloading Chunk:" + idx);
      /*if (idx == new Vector2Int(-1,2) || idx == new Vector2Int(0, 2)) {
         Debug.Log("data:" + c.terrainHeightMap[0,0] + " " + c.terrainHeightMap[99, 0] + " " + c.bounds.xMin + " " + c.bounds.xMax + " " + c.bounds.zMin + " " + c.bounds.zMax);
      }*/
   }

   IEnumerator PrepareWaterList(Chunk chunk) {
      for (int i = 0, x = (int)chunk.bounds.xMin; x < chunk.bounds.xMax; x += 2) {
         for (int z = (int)chunk.bounds.zMin; z < chunk.bounds.zMax; z += 2) {
            Vector2Int localPoint = chunk.GetLocalCoord(new Vector2Int(x, z));
            if (chunk.IsShore(localPoint)) {
               chunk.waterList.Add(new Vector2Int(x, z));
            }
            i++;
            if (i > 30) {
               i = 0;
               yield return null;
            }
         }
      }
      //Debug.Log("water:" + chunk.waterList.Count);
      chunk.state = ChunkState.PRELOADED;
   }

   public void LoadChunk(Vector2Int idx) {
      //Debug.Log("Loading Chunk:" + idx);
      if (chunks.ContainsKey(idx)) {
         Chunk chunk = chunks[idx];
         if (chunk.state == ChunkState.PRELOADED) {
            chunk.Load(GetPatchChunks(idx));
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

   public ArrayList GetPatchIdx(Vector2Int idx) {
      ArrayList patchChunks = new ArrayList();
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            patchChunks.Add(idx + new Vector2Int(i, j));

         }
      }
      return patchChunks;
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


}
