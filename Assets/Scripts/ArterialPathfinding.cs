using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ArterialPathfinding {

   private const float MOVE_STRAIGHT_COST = 1f;
   private const float MOVE_DIAGONAL_COST = 1.414f;

   public static ArterialPathfinding Instance { get; private set; }

   private Grid<PathNode> grid;
   private List<PathNode> openList;
   private List<PathNode> closedList;
   private Dictionary<Vector2, PathNode> nodeDict;
   private int increment = 3;
   private int searchExtention = 50; // amount to search beyond min/max
   private float waterCost = 10f;
   private Vector2Int min;
   private Vector2Int max;

   public ArterialPathfinding() {
      Instance = this;
      nodeDict = new Dictionary<Vector2, PathNode>();
   }

   public ArterialPathfinding(Grid<PathNode> grid) {
      Instance = this;
      this.grid = grid;

   }

   public Grid<PathNode> GetGrid() {
      return grid;
   }

   public ArrayList FindPath(Vector2 startWorldPosition, Vector2 endWorldPosition) {
      nodeDict.Clear();
      min = new Vector2Int(Mathf.Min((int)startWorldPosition.x, (int)endWorldPosition.x), Mathf.Min((int)startWorldPosition.y, (int)endWorldPosition.y));
      max = new Vector2Int(Mathf.Max((int)startWorldPosition.x, (int)endWorldPosition.x), Mathf.Max((int)startWorldPosition.y, (int)endWorldPosition.y));
      min -= new Vector2Int(searchExtention, searchExtention);
      max += new Vector2Int(searchExtention, searchExtention);

      List<PathNode> path = FindPath((int)startWorldPosition.x, (int)startWorldPosition.y, (int)endWorldPosition.x, (int)endWorldPosition.y);
      if (path == null) {
         return null;
      }
      else {
         ArrayList vectorPath = new ArrayList();
         foreach (PathNode pathNode in path) {
            vectorPath.Add(pathNode.Vector2Int());
         }
         vectorPath.Add(endWorldPosition);

         return vectorPath;
      }
   }

   public List<PathNode> FindPath(int startX, int startY, int endX, int endY) {
      //Debug.Log("FindPath: start" + startX + "," + startY + " end" + endX + "," + endY);
      PathNode startNode = GetNode(startX, startY);
      PathNode endNode = GetNode(endX, endY);


      if (startNode == null || endNode == null) {
         // Invalid Path
         Debug.LogWarning("Invalid Path!");
         return null;
      }

      openList = new List<PathNode> { startNode };
      closedList = new List<PathNode>();

      startNode.gCost = 0;
      startNode.hCost = CalculateDistanceCost(startNode, endNode);
      startNode.CalculateFCost();

      // Search
      while (openList.Count > 0) {
         PathNode currentNode = GetLowestFCostNode(openList);

         if ((currentNode.Vector2Int() - endNode.Vector2Int()).magnitude < 15) {
            // Reached final node
            return CalculatePath(currentNode); //not endnode because not exact hit
         }

         openList.Remove(currentNode);
         closedList.Add(currentNode);


         foreach (PathNode neighbourNode in GetNeighbourList(currentNode)) {
            if (closedList.Contains(neighbourNode)) {
               continue;
            }
            /*if (!neighbourNode.isWalkable) {
               closedList.Add(neighbourNode);
               continue;
            }*/

            float tentativeGCost = currentNode.gCost + CalculateDistanceCost(currentNode, neighbourNode) + InclineCost(currentNode, neighbourNode) + WaterCost(neighbourNode);
            if (tentativeGCost < neighbourNode.gCost) {
               neighbourNode.cameFromNode = currentNode;
               neighbourNode.gCost = tentativeGCost;
               neighbourNode.hCost = CalculateDistanceCost(neighbourNode, endNode);
               neighbourNode.CalculateFCost();

               if (!openList.Contains(neighbourNode)) {
                  openList.Add(neighbourNode);
               }
            }
         }
      }

      // Out of nodes on the openList
      Debug.Log("Failed to find path to " + endNode);
      return null;
   }

   private float InclineCost(PathNode a, PathNode b) {
      return 5 * Mathf.Abs(a.height - b.height);
   }

   private float WaterCost(PathNode a) {
      if (a.isWater) {
         return waterCost;
      }
      return 0;
   }

   private List<PathNode> GetNeighbourList(PathNode currentNode) {
      List<PathNode> neighbourList = new List<PathNode>();
      //Tuple<int, int> currXY = currentNode.GetGlobalXY();
      int x = currentNode.x;
      int y = currentNode.y;

      if (InSearchBounds(x - increment, y)) neighbourList.Add(GetNode(x - increment, y));
      if (InSearchBounds(x - increment, y - increment)) neighbourList.Add(GetNode(x - increment, y - increment));
      if (InSearchBounds(x - increment, y + increment)) neighbourList.Add(GetNode(x - increment, y + increment));
      if (InSearchBounds(x + increment, y)) neighbourList.Add(GetNode(x + increment, y));
      if (InSearchBounds(x + increment, y - increment)) neighbourList.Add(GetNode(x + increment, y - increment));
      if (InSearchBounds(x + increment, y + increment)) neighbourList.Add(GetNode(x + increment, y + increment));
      if (InSearchBounds(x, y - increment)) neighbourList.Add(GetNode(x, y - increment));
      if (InSearchBounds(x, y + increment)) neighbourList.Add(GetNode(x, y + increment));

      return neighbourList;
   }

   private bool InSearchBounds(int x, int y) {
      return !(x < min.x - searchExtention || x > max.x + searchExtention || y < min.y - searchExtention || y > max.y + searchExtention);
   }

   public PathNode GetNode(int x, int y) {
      PathNode node;
      if (nodeDict.ContainsKey(new Vector2(x, y))) {
         node = nodeDict[new Vector2(x, y)];
      }
      else {
         node = new PathNode(x, y);
         node.gCost = 99999999;
         node.CalculateFCost();
         node.cameFromNode = null;
         nodeDict[new Vector2(x, y)] = node;
      }
      return node;
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


