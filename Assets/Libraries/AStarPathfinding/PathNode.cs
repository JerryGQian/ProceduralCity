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

public class PathNode {
   //private Grid<PathNode> grid;
   public int x;
   public int y;
   public float height;

   public float gCost;
   public float hCost;
   public float fCost;

   public bool isWalkable;
   public bool isWater;
   public PathNode cameFromNode;

   public PathNode(int x, int y) { //Grid<PathNode> grid, 
      //this.grid = grid;
      this.x = x;
      this.y = y;
      height = TerrainGen.GenerateTerrainAt(x, y);
      isWalkable = height > 0;
      isWater = height <= 0;
   }

   public void CalculateFCost() {
      fCost = gCost + hCost;
   }

   public void SetIsWalkable(bool isWalkable) {
      this.isWalkable = isWalkable;
   }

   public Vector2 Vector2Int() {
      return new Vector2Int(x,y);
   }

   public override string ToString() {
      return x + "," + y;
   }

}
