/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Pathfinding {

   private const float MOVE_STRAIGHT_COST = 1f;
   private const float MOVE_DIAGONAL_COST = 1.414f;

   public static Pathfinding Instance { get; private set; }

   private Grid<PathNode> grid;
   private List<PathNode> openList;
   private List<PathNode> closedList;
   float cellSize = 1f;

   public Pathfinding() {
      Instance = this;
   }

   public Pathfinding(Grid<PathNode> grid) {
      Instance = this;
      this.grid = grid;
      
   }

   public Pathfinding(Vector3 startWorld, Vector3 endWorld) {
      Instance = this;
      //this.grid = grid;
      //Vector2Int startChunk = Util.wm.WorldToChunk(startWorld);
      //Vector2Int endChunk = Util.wm.WorldToChunk(endWorld);
      Vector2Int startChunk = WorldManager.W2C(startWorld);
      Vector2Int endChunk = WorldManager.W2C(endWorld);
      //Vector2Int diff = endChunk - startChunk;
      
   }

   public Grid<PathNode> GetGrid() {
      return grid;
   }

   public List<Vector2> FindPath(Vector2 startWorldPosition, Vector2 endWorldPosition) {
      //Vector3Int startCell = Util.wm.WorldToGlobalCell(startWorldPosition);
      //Vector3Int endCell = Util.wm.WorldToGlobalCell(endWorldPosition);
      //Debug.Log("startCell:" + startCell + " endCell:" + endCell);
      ////Debug.Log(Mathf.FloorToInt((endWorldPosition).x / 10f));
      List<PathNode> path = FindPath((int)startWorldPosition.x, (int)startWorldPosition.y, (int)endWorldPosition.x, (int)endWorldPosition.y);
      if (path == null) {
         return null;
      }
      else {
         List<Vector2> vectorPath = new List<Vector2>();
         foreach (PathNode pathNode in path) {
            vectorPath.Add(new Vector2(pathNode.x, pathNode.y));// * cellSize + Vector3.one * cellSize * .5f);
         }
         return vectorPath;
      }
   }

   public List<PathNode> FindPath(int startX, int startY, int endX, int endY) {
      //TileData startNode = grid.GetGridObject(startX, startY);
      //TileData endNode = grid.GetGridObject(endX, endY);
      Debug.Log("FindPath: start" + startX + "," + startY + " end" + endX + "," + endY);
      PathNode startNode = GetNode(startX, startY);//Util.wm.GetTileData(startX, startY);
      PathNode endNode = GetNode(endX, endY);//Util.wm.GetTileData(endX, endY);

      if (startNode == null || endNode == null) {
         // Invalid Path
         Debug.LogWarning("Invalid Path!");
         return null;
      }

      openList = new List<PathNode> { startNode };
      closedList = new List<PathNode>();

      /*for (int x = 0; x < Util.wm.chunkSize; x++) {
         for (int y = 0; y < Util.wm.chunkSize; y++) {
            PathNode pathNode = Util.wm.GetTileData(x, y);
            pathNode.gCost = 99999999;
            pathNode.CalculateFCost();
            pathNode.cameFromNode = null;
         }
      }*/

      startNode.gCost = 0;
      startNode.hCost = CalculateDistanceCost(startNode, endNode);
      startNode.CalculateFCost();

      // Search
      while (openList.Count > 0) {
         PathNode currentNode = GetLowestFCostNode(openList);
         Debug.Log("Looping! " + currentNode);
         if (currentNode == endNode) {
            // Reached final node
            return CalculatePath(endNode);
         }

         openList.Remove(currentNode);
         closedList.Add(currentNode);

         
         foreach (PathNode neighbourNode in GetNeighbourList(currentNode)) {
            
            if (closedList.Contains(neighbourNode)) continue;
            if (!neighbourNode.isWalkable) {
               closedList.Add(neighbourNode);
               continue;
            }

            float tentativeGCost = currentNode.gCost + CalculateDistanceCost(currentNode, neighbourNode);
            Debug.Log(neighbourNode + " " + tentativeGCost + " " + neighbourNode.gCost);
            if (tentativeGCost < neighbourNode.gCost) {
               neighbourNode.cameFromNode = currentNode;
               neighbourNode.gCost = tentativeGCost;
               neighbourNode.hCost = CalculateDistanceCost(neighbourNode, endNode);
               neighbourNode.CalculateFCost();

               if (!openList.Contains(neighbourNode)) {
                  openList.Add(neighbourNode);
               }
            }
            //PathfindingDebugStepVisual.Instance.TakeSnapshot(grid, currentNode, openList, closedList);
         }
      }

      // Out of nodes on the openList
      return null;
   }

   private List<PathNode> GetNeighbourList(PathNode currentNode) {
      List<PathNode> neighbourList = new List<PathNode>();
      //Tuple<int, int> currXY = currentNode.GetGlobalXY();
      int x = currentNode.x;
      int y = currentNode.y;
      //Debug.Log("GetNeighborList " + x + "," + y);
      //if (x - 1 >= 0) {
      // Left
      neighbourList.Add(GetNode(x - 1, y));
         // Left Down //REVISIT THIS, ALLOW NEGATIVES
         //if (y - 1 >= 0) 
         if (GetNode(x - 1, y - 1) != null) neighbourList.Add(GetNode(x - 1, y - 1));
         // Left Up
         //if (y + 1 < grid.GetHeight()) 
         if (GetNode(x - 1, y + 1) != null) neighbourList.Add(GetNode(x - 1, y + 1));
      //}
      //if (x + 1 < grid.GetWidth()) {
         // Right
         neighbourList.Add(GetNode(x + 1, y));
         // Right Down
         //if (y - 1 >= 0) 
         if (GetNode(x + 1, y - 1) != null) neighbourList.Add(GetNode(x + 1, y - 1));
         // Right Up
         //if (y + 1 < grid.GetHeight())
         if (GetNode(x + 1, y + 1) != null) neighbourList.Add(GetNode(x + 1, y + 1));
      //}
      // Down
      //if (y - 1 >= 0) 
      if (GetNode(x, y - 1) != null) neighbourList.Add(GetNode(x, y - 1));
      // Up
      //if (y + 1 < grid.GetHeight()) 
      if (GetNode(x, y + 1) != null) neighbourList.Add(GetNode(x, y + 1));

      return neighbourList;
   }

   public PathNode GetNode(int x, int y) {
      return new PathNode(x, y);
      //return Util.wm.GetTileData(x, y);
   }

   private List<PathNode> CalculatePath(PathNode endNode) {
      List<PathNode> path = new List<PathNode>();
      path.Add(endNode);
      PathNode currentNode = endNode;
      while (currentNode.cameFromNode != null) {
         path.Add(currentNode.cameFromNode);
         currentNode = currentNode.cameFromNode;
      }
      path.Reverse();
      return path;
   }

   private float CalculateDistanceCost(PathNode a, PathNode b) {
      int xDistance = Mathf.Abs(a.x - b.x);
      int yDistance = Mathf.Abs(a.y - b.y);
      int remaining = Mathf.Abs(xDistance - yDistance);
      return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, yDistance) + MOVE_STRAIGHT_COST * remaining;
   }

   private PathNode GetLowestFCostNode(List<PathNode> pathNodeList) {
      PathNode lowestFCostNode = pathNodeList[0];
      for (int i = 1; i < pathNodeList.Count; i++) {
         if (pathNodeList[i].fCost < lowestFCostNode.fCost) {
            lowestFCostNode = pathNodeList[i];
         }
      }
      return lowestFCostNode;
   }

}

/*

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pathfinding {

   private const int MOVE_STRAIGHT_COST = 10;
   private const int MOVE_DIAGONAL_COST = 14;

   public static Pathfinding Instance { get; private set; }

   private Grid<PathNode> grid;
   private List<PathNode> openList;
   private List<PathNode> closedList;

   public Pathfinding(int width, int height) {
      Instance = this;
      grid = new Grid<PathNode>(width, height, 10f, Vector3.zero, (Grid<PathNode> g, int x, int y) => new PathNode(g, x, y));
   }

   public Pathfinding(Grid<PathNode> grid, int width, int height) {
      Instance = this;
      this.grid = grid;
   }

   public Grid<PathNode> GetGrid() {
      return grid;
   }

   public List<Vector3> FindPath(Vector3 startWorldPosition, Vector3 endWorldPosition) {
      grid.GetXY(startWorldPosition, out int startX, out int startY);
      grid.GetXY(endWorldPosition, out int endX, out int endY);
      //Debug.Log("start:" + startWorldPosition + " end:" + endWorldPosition);
      //Debug.Log(Mathf.FloorToInt((endWorldPosition).x / 10f));
      List<PathNode> path = FindPath(startX, startY, endX, endY);
      if (path == null) {
         return null;
      }
      else {
         List<Vector3> vectorPath = new List<Vector3>();
         foreach (PathNode pathNode in path) {
            vectorPath.Add(new Vector3(pathNode.x, pathNode.y) * grid.GetCellSize() + Vector3.one * grid.GetCellSize() * .5f);
         }
         return vectorPath;
      }
   }

   public List<PathNode> FindPath(int startX, int startY, int endX, int endY) {
      PathNode startNode = grid.GetGridObject(startX, startY);
      PathNode endNode = grid.GetGridObject(endX, endY);

      if (startNode == null || endNode == null) {
         // Invalid Path
         return null;
      }

      openList = new List<PathNode> { startNode };
      closedList = new List<PathNode>();

      for (int x = 0; x < grid.GetWidth(); x++) {
         for (int y = 0; y < grid.GetHeight(); y++) {
            PathNode pathNode = grid.GetGridObject(x, y);
            pathNode.gCost = 99999999;
            pathNode.CalculateFCost();
            pathNode.cameFromNode = null;
         }
      }

      startNode.gCost = 0;
      startNode.hCost = CalculateDistanceCost(startNode, endNode);
      startNode.CalculateFCost();

      //PathfindingDebugStepVisual.Instance.ClearSnapshots();
      //PathfindingDebugStepVisual.Instance.TakeSnapshot(grid, startNode, openList, closedList);

      while (openList.Count > 0) {
         PathNode currentNode = GetLowestFCostNode(openList);
         if (currentNode == endNode) {
            // Reached final node
            //PathfindingDebugStepVisual.Instance.TakeSnapshot(grid, currentNode, openList, closedList);
            //PathfindingDebugStepVisual.Instance.TakeSnapshotFinalPath(grid, CalculatePath(endNode));
            return CalculatePath(endNode);
         }

         openList.Remove(currentNode);
         closedList.Add(currentNode);

         foreach (PathNode neighbourNode in GetNeighbourList(currentNode)) {
            if (closedList.Contains(neighbourNode)) continue;
            if (!neighbourNode.isWalkable) {
               closedList.Add(neighbourNode);
               continue;
            }

            int tentativeGCost = currentNode.gCost + CalculateDistanceCost(currentNode, neighbourNode);
            if (tentativeGCost < neighbourNode.gCost) {
               neighbourNode.cameFromNode = currentNode;
               neighbourNode.gCost = tentativeGCost;
               neighbourNode.hCost = CalculateDistanceCost(neighbourNode, endNode);
               neighbourNode.CalculateFCost();

               if (!openList.Contains(neighbourNode)) {
                  openList.Add(neighbourNode);
               }
            }
            //PathfindingDebugStepVisual.Instance.TakeSnapshot(grid, currentNode, openList, closedList);
         }
      }

      // Out of nodes on the openList
      return null;
   }

   private List<PathNode> GetNeighbourList(PathNode currentNode) {
      List<PathNode> neighbourList = new List<PathNode>();

      if (currentNode.x - 1 >= 0) {
         // Left
         neighbourList.Add(GetNode(currentNode.x - 1, currentNode.y));
         // Left Down
         if (currentNode.y - 1 >= 0) neighbourList.Add(GetNode(currentNode.x - 1, currentNode.y - 1));
         // Left Up
         if (currentNode.y + 1 < grid.GetHeight()) neighbourList.Add(GetNode(currentNode.x - 1, currentNode.y + 1));
      }
      if (currentNode.x + 1 < grid.GetWidth()) {
         // Right
         neighbourList.Add(GetNode(currentNode.x + 1, currentNode.y));
         // Right Down
         if (currentNode.y - 1 >= 0) neighbourList.Add(GetNode(currentNode.x + 1, currentNode.y - 1));
         // Right Up
         if (currentNode.y + 1 < grid.GetHeight()) neighbourList.Add(GetNode(currentNode.x + 1, currentNode.y + 1));
      }
      // Down
      if (currentNode.y - 1 >= 0) neighbourList.Add(GetNode(currentNode.x, currentNode.y - 1));
      // Up
      if (currentNode.y + 1 < grid.GetHeight()) neighbourList.Add(GetNode(currentNode.x, currentNode.y + 1));

      return neighbourList;
   }

   public PathNode GetNode(int x, int y) {
      return grid.GetGridObject(x, y);
   }

   private List<PathNode> CalculatePath(PathNode endNode) {
      List<PathNode> path = new List<PathNode>();
      path.Add(endNode);
      PathNode currentNode = endNode;
      while (currentNode.cameFromNode != null) {
         path.Add(currentNode.cameFromNode);
         currentNode = currentNode.cameFromNode;
      }
      path.Reverse();
      return path;
   }

   private int CalculateDistanceCost(PathNode a, PathNode b) {
      int xDistance = Mathf.Abs(a.x - b.x);
      int yDistance = Mathf.Abs(a.y - b.y);
      int remaining = Mathf.Abs(xDistance - yDistance);
      return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, yDistance) + MOVE_STRAIGHT_COST * remaining;
   }

   private PathNode GetLowestFCostNode(List<PathNode> pathNodeList) {
      PathNode lowestFCostNode = pathNodeList[0];
      for (int i = 1; i < pathNodeList.Count; i++) {
         if (pathNodeList[i].fCost < lowestFCostNode.fCost) {
            lowestFCostNode = pathNodeList[i];
         }
      }
      return lowestFCostNode;
   }

}
*/
